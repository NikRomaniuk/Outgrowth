using Outgrowth.ViewModels;
using Outgrowth.Models;
using Outgrowth.Services;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
#if WINDOWS
using Outgrowth.Platforms.Windows;
#endif

namespace Outgrowth.Views;

public partial class LaboratoryPage : ContentPage
{
    // Station objects - created dynamically on page load
    private readonly List<StationObject> _stationObjects = new()
    {
        new StationObject("ResourceSlot", 0, (0 + 80), 160, 160),
        new StationObject("ExtractButton", 480, (0 - 240), 160, 160)
    };
    
    // Furniture objects - created dynamically on page load
    private readonly List<FurnitureObject> _furnitureObjects = new()
    {
        new FurnitureObject("MachineBase", 0, 0, 480, 960, "lab__machine_base.png"),
        new FurnitureObject("MachineGlass", 0, (0 + 80), 160, 160, "lab__machine_glass.png", 110),
        new FurnitureObject("MachineLight", 0, (0 - 240), 160, 160, "lab__machine_light_red.png", 110),
        new FurnitureObject("MachineDisplay", 480, (0), 480, 320, "lab__machine_display_off.png", 90),
        new FurnitureObject("MachineDisplayOverlay", 520, (0), 240, 240, "", 100),
        new FurnitureObject("MachineDisplayButton", 480, (0), 160, 160, "lab__machine_ready_button.png", 85),
        new FurnitureObject("MachineContent", 0, (0 + 80), 160, 160, "", 105)
    };
    
    // Selected resource for interaction
    private ResourceData? _selectedResource;
    
    // Animation lock to prevent opening other panels during animation
    private bool _isAnimating = false;
    // Lock for machine display animation
    private bool _isDisplayAnimating = false;
    // Indicates the translate (slide) phase of the display animation is running
    private bool _isDisplaySliding = false;
    // Cancellation token for display animation delay to allow interruption
    private CancellationTokenSource? _displayAnimationCts;
    // Cache for resource panel items to enable/disable during animation
    private Dictionary<string, Border> _resourceItemCache = new();
    // 9-Slice panel container and its content scroll view
    private Grid? _nineSlicePanelContainer;
    private ScrollView? _panelContentScroll;
    // Dynamically created SelectedResourcePanel (Android only)
    private VisualElement? _selectedResourcePanel;
    
    // Events for display animation lifecycle
    public event EventHandler? DisplayAnimationStarted;
    public event EventHandler? DisplayAnimationEnded;
    // References to layered display images to avoid Source swaps (preloaded, toggled via Opacity)
    private Microsoft.Maui.Controls.Image? _displayOffImage;
    private Microsoft.Maui.Controls.Image? _displayOnImage;
    // Grid inside MachineDisplayOverlay to show extra content when display is on
    private Microsoft.Maui.Controls.Grid? _displayContentGrid;
    private Microsoft.Maui.Controls.Image? _displayContentImage;
    private Microsoft.Maui.Controls.Label? _displayContentLabel;
    // Machine light preloaded images (red/green) to cross-fade without reloading
    private Microsoft.Maui.Controls.Image? _machineLightRedImage;
    private Microsoft.Maui.Controls.Image? _machineLightGreenImage;
    // Machine display button image cache
    private Microsoft.Maui.Controls.Image? _machineButtonImage;
    
#if WINDOWS
    private WindowsInput? _windowsInput;
#endif
    
    public LaboratoryPage()
    {
        InitializeComponent();
        BindingContext = new LaboratoryViewModel();
        
        // Set up station object click handlers (only invoke when station.CanInteract)
        _stationObjects[0].Clicked += (s, e) =>
        {
            var st = _stationObjects[0];
            if (st.CanInteract)
                OnResourceSlotClicked(s ?? this, e);
        };
        _stationObjects[0].InteractAction = () => System.Diagnostics.Debug.WriteLine("Resource Slot interacted");

        _stationObjects[1].Clicked += (s, e) =>
        {
            var st = _stationObjects[1];
            if (st.CanInteract)
                OnExtractClicked(s ?? this, e);
        };
        _stationObjects[1].InteractAction = () => System.Diagnostics.Debug.WriteLine("Extract interacted");
        
        // Handle page size changes for scaling
        this.SizeChanged += OnPageSizeChanged;
        
        // Close panel when navigating away
        this.Disappearing += OnPageDisappearing;
        
        // Update element positions on load
        this.Loaded += OnPageLoaded;
    }
    
    private void OnPageLoaded(object? sender, EventArgs e)
    {
        // Data libraries are pre-loaded by GameDataManager at app startup
        
        // Create station objects and add to environment
        CreateStationObjects();
        // Initialize element positions from absolute coordinates
        UpdateStationPositions();
        
        // Create furniture objects and add to environment
        CreateFurnitureObjects();
        // Initialize furniture positions from absolute coordinates
        UpdateFurniturePositions();

        // Set MachineDisplay default position (appear at 0,0) by offsetting translation
        var display = _furnitureObjects.FirstOrDefault(f => f.Id == "MachineDisplay");
        if (display != null && display.VisualElement != null)
        {
            // Start hidden at center (0) by translating left by 480 (layout X=480 + TranslationX = 0)
            display.VisualElement.TranslationX = -480;
            // Ensure the image shows the "off" sprite
            SetupMachineDisplayImages(display);
        }
        // Prepare machine light preloaded images
        var light = _furnitureObjects.FirstOrDefault(f => f.Id == "MachineLight");
        if (light != null && light.VisualElement != null)
        {
            SetupMachineLightImages(light);
        }
        // Prepare overlay content grid (hidden by default)
        var overlay = _furnitureObjects.FirstOrDefault(f => f.Id == "MachineDisplayOverlay");
        if (overlay != null && overlay.VisualElement != null)
        {
            SetupMachineDisplayOverlay(overlay);
        }
        // Ensure MachineDisplayButton is hidden initially
        var buttonObj = _furnitureObjects.FirstOrDefault(f => f.Id == "MachineDisplayButton");
        if (buttonObj != null && buttonObj.VisualElement != null)
        {
            buttonObj.VisualElement.IsVisible = false;
            buttonObj.VisualElement.TranslationY = 0;
        }
        // Ensure ExtractButton is not interactable until the MachineDisplayButton is shown
        var extractStation = _stationObjects.FirstOrDefault(s => s.Id == "ExtractButton");
        if (extractStation != null)
        {
            extractStation.CanInteract = false;
            if (extractStation.VisualElement is View ev)
                ev.InputTransparent = true;
        }
        UpdateMachineContentVisual();
        System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] OnPageLoaded: selectedResource={_selectedResource?.Name ?? "null"}, displayTranslation={display?.VisualElement?.TranslationX}");
        
        // Subscribe to display animation events to control resource panel items
        DisplayAnimationStarted += OnDisplayAnimationStarted;
        DisplayAnimationEnded += OnDisplayAnimationEnded;
        
        // Create dynamic resource panel from library
        UpdateResourcePanel();
        
        // Initialize panel scale for animation (set to 0 if not visible, 1 if visible)
        if (ResourceListContainer != null)
        {
            ResourceListContainer.AnchorX = 0.5;
            ResourceListContainer.AnchorY = 0.5;
            ResourceListContainer.Scale = ResourceListContainer.IsVisible ? 1 : 0;
        }
        
        // SelectedResourcePanel removed from XAML; will be created dynamically on Android inside UpdateResourcePanel
        
#if WINDOWS
        // Attach Windows keyboard input handler (only Esc for closing panels)
        _windowsInput = new WindowsInput(
            onLeftArrow: () => { }, // No action
            onRightArrow: () => { }, // No action
            onEscape: () => _ = CloseResourcePanelWithAnimation()  // Close panel with Esc key (with animation)
        );
        _windowsInput.Attach();
#endif
    }
    
    private void OnPageDisappearing(object? sender, EventArgs e)
    {
        // Unsubscribe from timer events
        // Timer.Tick -= OnTimerTick;
        
        // Reset animation flag to allow closing (page is disappearing)
        _isAnimating = false;
        // Close panel immediately without animation
        CloseResourcePanel();

        // Leaving the page is allowed to clear selection regardless of slide state
        _selectedResource = null;
        UpdateMachineContentVisual();
        
        // Unsubscribe from display animation events
        DisplayAnimationStarted -= OnDisplayAnimationStarted;
        DisplayAnimationEnded -= OnDisplayAnimationEnded;
        
        // Clean up dynamically created elements to prevent memory leaks
        CleanupStationObjects();
        CleanupFurnitureObjects();
        
#if WINDOWS
        // Detach Windows keyboard input handler
        _windowsInput?.Detach();
        _windowsInput = null;
#endif
    }
    
    /// <summary>
    /// Cleans up station objects and their visual elements to prevent memory leaks
    /// </summary>
    private void CleanupStationObjects()
    {
#if ANDROID || WINDOWS
        if (EnvironmentContainer == null)
            return;
        
        // Remove all dynamically created elements from container
        foreach (var station in _stationObjects)
        {
            if (station.VisualElement != null && EnvironmentContainer.Children.Contains(station.VisualElement))
            {
                // Clear gesture recognizers to break circular references
                if (station.VisualElement is Border border && border.GestureRecognizers.Count > 0)
                {
                    border.GestureRecognizers.Clear();
                }
                
                EnvironmentContainer.Children.Remove(station.VisualElement);
            }
        }
#endif
    }
    
    /// <summary>
    /// Creates furniture object UI elements and adds them to EnvironmentContainer
    /// </summary>
    private void CreateFurnitureObjects()
    {
#if ANDROID || WINDOWS
        if (EnvironmentContainer == null)
            return;
        
        foreach (var furniture in _furnitureObjects)
        {
            var visualElement = furniture.CreateVisualElement();
            visualElement.ZIndex = furniture.ZIndex;
            EnvironmentContainer.Children.Add(visualElement);
        }
#endif
    }
    
    /// <summary>
    /// Updates furniture positions using 1:1 coordinate system (container center: X=960, Y=540)
    /// </summary>
    private void UpdateFurniturePositions()
    {
#if ANDROID || WINDOWS
        if (EnvironmentContainer == null)
            return;
        
        const double containerCenterX = 960.0;
        const double containerCenterY = 540.0;
        
        foreach (var furniture in _furnitureObjects)
        {
            furniture.UpdatePosition(containerCenterX, containerCenterY);
        }
#endif
    }
    
    /// <summary>
    /// Cleans up furniture objects and their visual elements to prevent memory leaks
    /// </summary>
    private void CleanupFurnitureObjects()
    {
#if ANDROID || WINDOWS
        if (EnvironmentContainer == null)
            return;
        
        // Remove all dynamically created elements from container
        foreach (var furniture in _furnitureObjects)
        {
            if (furniture.VisualElement != null && EnvironmentContainer.Children.Contains(furniture.VisualElement))
            {
                EnvironmentContainer.Children.Remove(furniture.VisualElement);
            }
        }

        // Clear cached display references so they are recreated on next load
        _displayOffImage = null;
        _displayOnImage = null;
        _displayContentGrid = null;
        _displayContentImage = null;
        _displayContentLabel = null;
        _machineLightRedImage = null;
        _machineLightGreenImage = null;
        _machineButtonImage = null;
#endif
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        if (EnvironmentWrapper != null && EnvironmentContainer != null && HubButton != null 
            && ResourceListContainer != null && ResourceListPlaceholder != null && ResourceListWrapper != null)
        {
            var pageHeight = this.Height;
            var pageWidth = this.Width;
            
            if (pageHeight > 0 && pageWidth > 0)
            {
                // Update screen properties
                var screenProps = ScreenProperties.Instance;
                screenProps.UpdateScreenProperties(pageWidth, pageHeight);
                
                EnvironmentContainer.WidthRequest = ScreenProperties.ReferenceWidth;
                EnvironmentContainer.HeightRequest = ScreenProperties.ReferenceHeight;
                
                // Scale environment wrapper from center to maintain centering
                EnvironmentWrapper.AnchorX = 0.5;
                EnvironmentWrapper.AnchorY = 0.5;
                EnvironmentWrapper.Scale = screenProps.Scale;
                EnvironmentWrapper.WidthRequest = ScreenProperties.ReferenceWidth;
                EnvironmentWrapper.HeightRequest = ScreenProperties.ReferenceHeight;
                
                EnvironmentContainer.InputTransparent = false;
                EnvironmentContainer.BackgroundColor = Colors.Transparent;
                
                // Center offset: account for scale transform
                EnvironmentWrapper.TranslationX = screenProps.OffsetX;
                EnvironmentWrapper.TranslationY = screenProps.OffsetY;
                
                // Scale HubButton from left edge (matches environment scale)
                HubButton.AnchorX = 0;
                HubButton.AnchorY = 1;
                HubButton.Scale = screenProps.Scale;
                
                // Scale placeholder from right edge (maintains column width, prevents offset)
                ResourceListPlaceholder.AnchorX = 1;
                ResourceListPlaceholder.AnchorY = 0.5;
                ResourceListPlaceholder.Scale = screenProps.Scale;
                
                // Update station object positions from absolute coordinates
                UpdateStationPositions();
                UpdateFurniturePositions();
                
                var adaptive = screenProps.AdaptiveScale;
                screenProps.UpdateFontSizes(adaptive);
                UpdatePanelSize(adaptive);
                
                // Update resource panel to refresh font sizes for dynamically created items
                UpdateResourcePanel();
            }
        }
#endif
    }
    
    private void UpdatePanelSize(double adaptiveScale)
    {
        // Use UserInterfaceCreator for generic panel sizing
        const double baseHeight = 500.0;
        const double baseMargin = 20.0;
        const double selectedPanelHeight = 160.0;

    #if ANDROID
        const double baseWidth = 250.0;
        const double selectedPanelWidth = 250.0;
        bool isAndroid = true;
    #else
        const double baseWidth = 300.0;
        const double selectedPanelWidth = 300.0;
        bool isAndroid = false;
    #endif

        var sizes = UserInterfaceCreator.GetPanelSizes(adaptiveScale, baseWidth, baseHeight, baseMargin, selectedPanelWidth, selectedPanelHeight, isAndroid);

        if (ResourceListContainer != null)
        {
            ResourceListContainer.WidthRequest = sizes.Width;
            ResourceListContainer.HeightRequest = sizes.Height;
            ResourceListContainer.Margin = new Thickness(0, 0, sizes.Margin, 0);
        }

        if (ResourceListWrapper != null && ResourceListContainer != null)
        {
            AbsoluteLayout.SetLayoutBounds(ResourceListContainer, new Rect(1, sizes.PanelYPosition, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
            AbsoluteLayout.SetLayoutFlags(ResourceListContainer, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.XProportional | Microsoft.Maui.Layouts.AbsoluteLayoutFlags.YProportional);
        }

        // Placeholder scales with buttonScale (layout sizing, maintains column width)
        ResourceListPlaceholder.WidthRequest = 300.0;
        ResourceListPlaceholder.HeightRequest = baseHeight;
        ResourceListPlaceholder.Margin = new Thickness(0, 0, baseMargin, 0);

        // SelectedResourcePanel removed; selected-panel sizing handled elsewhere when recreated
    }

    /// <summary>
    /// Creates station object UI elements and adds them to EnvironmentContainer
    /// </summary>
    private void CreateStationObjects()
    {
#if ANDROID || WINDOWS
        if (EnvironmentContainer == null)
            return;
        
        foreach (var station in _stationObjects)
        {
            var visualElement = station.CreateVisualElement();
            visualElement.ZIndex = station.ZIndex;
            EnvironmentContainer.Children.Add(visualElement);
        }
#endif
    }
    
    /// <summary>
    /// Updates station positions using 1:1 coordinate system (container center: X=960, Y=540)
    /// </summary>
    private void UpdateStationPositions()
    {
#if ANDROID || WINDOWS
        if (EnvironmentContainer == null)
            return;
        
        const double containerCenterX = 960.0;
        const double containerCenterY = 540.0;
        
        foreach (var station in _stationObjects)
        {
            station.UpdatePosition(containerCenterX, containerCenterY);
        }
#endif
    }
    
    // ============================================================================
    // Navigation & Panel Controls
    // ============================================================================
    
    private async void OnHubClicked(object sender, EventArgs e)
    {
        await NavigationService.NavigateWithFadeAsync("//HubPage");
    }
    
    private async void OnResourceSlotClicked(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        if (ResourceListContainer == null || _isAnimating)
            return;
        
#if ANDROID
        // On Android, toggle panel visibility (close if open, open if closed)
        if (ResourceListContainer.IsVisible)
        {
            await CloseResourcePanelWithAnimation();
        }
        else
        {
            await OpenResourcePanelWithAnimation();
        }
#else
        // On Windows, always open panel (close with Esc key)
        if (!ResourceListContainer.IsVisible)
        {
            await OpenResourcePanelWithAnimation();
        }
#endif
#endif
    }

    private async void OnExtractClicked(object? sender, EventArgs e)
    {
        // Perform extraction: consume required amount from selected resource
        // and add extractionResult items to player liquids, then persist state.
#if ANDROID || WINDOWS
        System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Extract button clicked");

        if (_selectedResource == null)
        {
            System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Extract: no resource selected");
            return;
        }

        int required = _selectedResource.AmountForExtraction ?? 0;
        if (required <= 0)
        {
            System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Extract: resource '{_selectedResource.Name}' has no AmountForExtraction configured");
            return;
        }

        if (_selectedResource.Quantity < required)
        {
            System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Extract: not enough '{_selectedResource.Name}' (have {_selectedResource.Quantity}, need {required})");
            return;
        }

        try
        {
            // Consume resources
            _selectedResource.Quantity -= required;
            System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Extracted {required} of {_selectedResource.Name}. Remaining: {_selectedResource.Quantity}");

            // Add extraction results to liquids (or other categories if needed)
            if (_selectedResource.ExtractionResult != null)
            {
                foreach (var kv in _selectedResource.ExtractionResult)
                {
                    var liquidId = kv.Key;
                    var qty = kv.Value;
                    if (string.IsNullOrEmpty(liquidId) || qty <= 0)
                        continue;

                    var liquid = GameDataManager.Liquids.Get(liquidId);
                    if (liquid != null)
                    {
                        liquid.Quantity += qty;
                        System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Added {qty}x {liquidId} (new qty={liquid.Quantity})");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Extract: unknown liquid id '{liquidId}' in extraction result");
                    }
                }
            }

            // Persist current material quantities
            GameDataManager.SaveMaterialsState();

            // Do not disable the resource panel item (avoid recreating UI);
            // instead check whether we still have enough for another extraction.
            bool hasEnough = _selectedResource.Quantity >= required;

            // If not enough for another extraction, slide the MachineDisplayButton back and set light to red
            if (!hasEnough)
            {
                var buttonFurniture = _furnitureObjects.FirstOrDefault(f => f.Id == "MachineDisplayButton");
                var buttonView = buttonFurniture?.VisualElement;
                try
                {
                    if (buttonView != null && buttonView.IsVisible && buttonView.TranslationY > 10)
                    {
                        await buttonView.TranslateTo(0, 0, 300, Easing.CubicIn);
                    }
                    if (buttonView != null)
                    {
                        buttonView.IsVisible = false;
                        buttonView.TranslationY = 0;
                    }
                }
                catch { }

                // Force light to red
                try
                {
                    if (_machineLightGreenImage != null && _machineLightRedImage != null)
                    {
                        _ = _machineLightGreenImage.FadeTo(0, 120, Easing.Linear);
                        _ = _machineLightRedImage.FadeTo(1, 120, Easing.Linear);
                    }
                }
                catch { }

                // Disable extract station (no further extracts possible)
                var extractStation = _stationObjects.FirstOrDefault(s => s.Id == "ExtractButton");
                if (extractStation != null)
                {
                    extractStation.CanInteract = false;
                    if (extractStation.VisualElement is View evd)
                        evd.InputTransparent = true;
                }
            }
            else
            {
                // Still enough: ensure ExtractButton remains interactable
                var extractStation = _stationObjects.FirstOrDefault(s => s.Id == "ExtractButton");
                if (extractStation != null)
                {
                    extractStation.CanInteract = true;
                    if (extractStation.VisualElement is View ev)
                        ev.InputTransparent = false;
                }
            }

            // Refresh lightweight visuals (selection stroke) only
            UpdateResourceItemSelectionVisuals();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Error during extraction: {ex.Message}");
        }
#else
        System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Extract clicked (unsupported platform)");
#endif
    }

    // ============================================================================
    // Panel Animation Methods
    // ============================================================================
    
    private async Task OpenResourcePanelWithAnimation()
    {
#if ANDROID || WINDOWS
        if (ResourceListContainer == null || _isAnimating)
            return;
        
        _isAnimating = true;
        
        // Prepare main panel for animation
        ResourceListContainer.AnchorX = 0.5;
        ResourceListContainer.AnchorY = 0.5;
        ResourceListContainer.Scale = 0;
        ResourceListContainer.IsVisible = true;
        ResourceListContainer.InputTransparent = false;
        
        // No separate selected panel animation here (SelectedResourcePanel removed)
        
        // Animate panels opening in parallel (main + selected panel)
        var mainPanelTask = ResourceListContainer.ScaleTo(1, 200, Easing.SpringOut);
    #if ANDROID
        var selectedTask = UpdateSelectedResourcePanelVisibility(forceHide: false);
        await Task.WhenAll(mainPanelTask, selectedTask);
    #else
        await mainPanelTask;
    #endif

        _isAnimating = false;
#else
        await Task.CompletedTask;
#endif
    }
    
    private async Task CloseResourcePanelWithAnimation()
    {
#if ANDROID || WINDOWS
        if (ResourceListContainer == null || !ResourceListContainer.IsVisible || _isAnimating)
            return;
        
        _isAnimating = true;
        
        // No selected panel closing animation (SelectedResourcePanel removed)
        
        // Animate main panel closing and selected panel hiding in parallel
    #if ANDROID
        var mainPanelTask = ResourceListContainer.ScaleTo(0, 200, Easing.SpringIn);
        var selectedTask = UpdateSelectedResourcePanelVisibility(forceHide: true);
        await Task.WhenAll(mainPanelTask, selectedTask);
        // Clean up after animation: hide panels but keep current selection and machine state
        ResourceListContainer.IsVisible = false;
        ResourceListContainer.InputTransparent = true;
    #else
        // Animate main panel closing
        await ResourceListContainer.ScaleTo(0, 200, Easing.SpringIn);
        ResourceListContainer.IsVisible = false;
        ResourceListContainer.InputTransparent = true;
    #endif

        // IMPORTANT: do NOT clear `_selectedResource` nor trigger `AnimateMachineDisplayForSelection()` here.
        // Leaving selection intact prevents the ResourceSlot sprite from flickering when the panel closes.
        
        _isAnimating = false;
#else
        await Task.CompletedTask;
#endif
    }
    
    /// <summary>
    /// Closes panel immediately without animation (used for page disappearing)
    /// </summary>
    private void CloseResourcePanel()
    {
#if ANDROID || WINDOWS
        if (ResourceListContainer == null || _isAnimating)
            return;
        
        ResourceListContainer.Scale = 0;
        ResourceListContainer.IsVisible = false;
        ResourceListContainer.InputTransparent = true;
        
        // Hide dynamic SelectedResourcePanel on Android when closing
    #if ANDROID
        if (_selectedResourcePanel != null)
        {
            _selectedResourcePanel.IsVisible = false;
            _selectedResourcePanel.Scale = 0;
        }
    #endif
#endif
    }
    
    /// <summary>
    /// Updates the ResourcePanel with all available resources from ResourceLibrary
    /// Creates a 9-Slice panel with pixel-art style if not already created
    /// </summary>
    private void UpdateResourcePanel()
    {
#if ANDROID || WINDOWS
        if (ResourcesList == null)
        {
            System.Diagnostics.Debug.WriteLine("[LaboratoryPage] UpdateResourcePanel: ResourcesList is null");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine("[LaboratoryPage] UpdateResourcePanel: Updating resource panel");
        
        // Create 9-slice panel if not already created
        if (_nineSlicePanelContainer == null)
        {
            var screenProps = ScreenProperties.Instance;
            var adaptive = screenProps.AdaptiveScale;
            
#if ANDROID
            const double baseWidth = 250.0;
            const double baseHeight = 500.0;
#else
            const double baseWidth = 300.0;
            const double baseHeight = 500.0;
#endif
            
            double panelWidth = baseWidth * adaptive;
            double panelHeight = baseHeight * adaptive;
            double cornerSize = 40 * adaptive; // Scale corner size with screen
            
            var (panel, scroll) = UserInterfaceCreator.CreateNineSlicePanelWithScroll(
                panelWidth,
                panelHeight,
                cornerSize,
                Color.FromArgb("#0f0c29"),
                Color.FromArgb("#302b63"),
                cornerImage: "ui__panel_corner.png",
                horizontalEdgeImage: "ui__panel_edge_horizontal.png",
                verticalEdgeImage: "ui__panel_edge_vertical.png",
                centerImage: "ui__panel_center.png"
            );
            
            _nineSlicePanelContainer = panel;
            _panelContentScroll = scroll;
            try { _nineSlicePanelContainer.AutomationId = "ResourceNineSlicePanel"; } catch { }
            try { _panelContentScroll.AutomationId = "ResourceScroll"; } catch { }
            
            // Replace ResourceListContainer content with 9-slice panel
            if (ResourceListContainer != null)
            {
                ResourceListContainer.Children.Clear();
                ResourceListContainer.Children.Add(_nineSlicePanelContainer);
                
                // Set up animation properties
                _nineSlicePanelContainer.AnchorX = 0.5;
                _nineSlicePanelContainer.AnchorY = 0.5;
            }
            
            System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Created 9-slice panel: {panelWidth}x{panelHeight}, corner: {cornerSize}");
        }

        // Create SelectedResourcePanel dynamically on Android (only once)
#if ANDROID
        try
        {
            // Create selected panel only if missing
            var missingSelected = UserInterfaceCreator.CheckUiElementsExist(ResourceListWrapper, new[] { "SelectedResourcePanel" });
            if ((missingSelected is System.Collections.Generic.List<string> miss && miss.Contains("SelectedResourcePanel")) || (_selectedResourcePanel == null && ResourceListWrapper != null))
            {
                var screenProps = ScreenProperties.Instance;
                var adaptive = screenProps.AdaptiveScale;

                const double baseHeight = 500.0;
                const double baseMargin = 20.0;
                const double selectedPanelHeight = 160.0;
                const double baseWidth = 250.0;
                const double selectedPanelWidth = 250.0;

                var sizes = UserInterfaceCreator.GetPanelSizes(adaptive, baseWidth, baseHeight, baseMargin, selectedPanelWidth, selectedPanelHeight, isAndroid: true);

                double cornerSize = 40 * adaptive;

                // Create label bound to ViewModel.SelectedResourceName
                var selectedLabel = new Label
                {
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    TextColor = Colors.White
                };
                if (BindingContext != null)
                {
                    selectedLabel.BindingContext = BindingContext;
                    selectedLabel.SetBinding(Label.TextProperty, new Binding("SelectedResourceName"));
                }
                
                try
                {
                    selectedLabel.FontFamily = "SilkscreenBold";
                }
                catch { }

                var selectedPanel = UserInterfaceCreator.CreateNineSlicePanel(
                    width: sizes.SelectedWidth,
                    height: sizes.SelectedHeight,
                    cornerSize: cornerSize,
                    backgroundColor: Color.FromArgb("#1A1A1A"),
                    borderColor: Color.FromArgb("#FFD700"),
                    content: selectedLabel,
                    cornerImage: "ui__panel_highlighted_corner.png",
                    horizontalEdgeImage: "ui__panel_highlighted_edge_horizontal.png",
                    verticalEdgeImage: "ui__panel_highlighted_edge_vertical.png",
                    centerImage: "ui__panel_highlighted_center.png"
                );

                // Wrap the 9-slice panel in a Border so it has a visible stroke/background
                var wrapperBorder = new Border
                {
                    Content = selectedPanel,
                    Stroke = null,
                    StrokeThickness = 0
                };

                // Place in right gutter wrapper at proportional position x=1, y=0.15
                // Match ResourceListContainer right margin so panel aligns with list
                wrapperBorder.Margin = new Thickness(0, 0, sizes.Margin, 0);
                AbsoluteLayout.SetLayoutBounds(wrapperBorder, new Rect(1, 0.15, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
                AbsoluteLayout.SetLayoutFlags(wrapperBorder, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.XProportional | Microsoft.Maui.Layouts.AbsoluteLayoutFlags.YProportional);

                // Initial animation/visibility state
                wrapperBorder.AnchorX = 0.5;
                wrapperBorder.AnchorY = 0.5;
                wrapperBorder.Scale = 0;
                wrapperBorder.IsVisible = false;
                wrapperBorder.InputTransparent = true;

                    _selectedResourcePanel = wrapperBorder;
                    try { wrapperBorder.AutomationId = "SelectedResourcePanel"; } catch { }
                // ensure it's on top of ResourceListContainer
                _selectedResourcePanel.ZIndex = 1001;
                ResourceListWrapper.Children.Add(_selectedResourcePanel);
                System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Created dynamic SelectedResourcePanel (Android)");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Error creating SelectedResourcePanel: {ex.Message}");
        }
#endif
        
        // Get the content stack layout from scroll view
        if (_panelContentScroll?.Content is not VerticalStackLayout contentStack)
        {
            System.Diagnostics.Debug.WriteLine("[LaboratoryPage] UpdateResourcePanel: Content stack layout not found");
            return;
        }

        // Use CheckUiElementsExist to avoid recreating existing items
        try
        {
            var resources = ResourceLibrary.GetAllResources().ToList();
            var requiredIds = resources.Select(r => $"ResourceItem_{r.Id}").ToList();

            var missingObj = UserInterfaceCreator.CheckUiElementsExist(_panelContentScroll as VisualElement ?? (VisualElement?)contentStack, requiredIds);

            if (_resourceItemCache == null)
                _resourceItemCache = new Dictionary<string, Border>();

            if (missingObj is bool allPresent && allPresent == false)
            {
                // All present: update selection visuals only
                UpdateResourceItemSelectionVisuals();
                System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Resource items already exist, updated selection visuals");
            }
            else if (missingObj is System.Collections.Generic.List<string> missing)
            {
                // Create only missing resource items
                foreach (var missingId in missing)
                {
                    var idSuffix = missingId.StartsWith("ResourceItem_") ? missingId.Substring("ResourceItem_".Length) : missingId;
                    var resource = resources.FirstOrDefault(r => r.Id == idSuffix);
                    if (resource == null) continue;

                    var resourceItem = CreateResourceItem(resource);
                    try { resourceItem.AutomationId = $"ResourceItem_{resource.Id}"; } catch { }
                    _resourceItemCache[resource.Id] = resourceItem;
                    contentStack.Children.Add(resourceItem);
                }

                // Fallback if still missing
                var stillMissing = UserInterfaceCreator.CheckUiElementsExist(_panelContentScroll as VisualElement ?? (VisualElement?)contentStack, requiredIds);
                if (!(stillMissing is bool sb && sb == false))
                {
                    // Recreate everything as a fallback
                    contentStack.Children.Clear();
                    _resourceItemCache.Clear();
                    foreach (var resource in resources)
                    {
                        var resourceItem = CreateResourceItem(resource);
                        try { resourceItem.AutomationId = $"ResourceItem_{resource.Id}"; } catch { }
                        _resourceItemCache[resource.Id] = resourceItem;
                        contentStack.Children.Add(resourceItem);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Created {missing.Count} missing resource items");
            }
            else
            {
                // Unknown result — fall back to full recreation
                contentStack.Children.Clear();
                _resourceItemCache.Clear();
                foreach (var resource in resources)
                {
                    var resourceItem = CreateResourceItem(resource);
                    try { resourceItem.AutomationId = $"ResourceItem_{resource.Id}"; } catch { }
                    _resourceItemCache[resource.Id] = resourceItem;
                    contentStack.Children.Add(resourceItem);
                }
                System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Fallback: recreated all resource items");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Error updating ResourcePanel: {ex.Message}");
        }
#endif
    }
    
    /// <summary>
    /// Creates a UI element for a resource item
    /// Layout matches SeedsPanel/LiquidsPanel exactly
    /// </summary>
    private Border CreateResourceItem(ResourceData resource)
    {
        // Delegate to centralized creator for consistency with other panels
        bool isSelected = _selectedResource?.Id == resource.Id;
        System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] CreateResourceItem: {resource.Name}, isSelected={isSelected}, _selectedResource={_selectedResource?.Name ?? "null"}");

#if ANDROID
        bool isAndroid = true;
#else
        bool isAndroid = false;
#endif

        var appRes = Application.Current?.Resources;
        double panelItemHeight = appRes != null && appRes.ContainsKey("ResourcePanelIconSize")
            ? (double)appRes["ResourcePanelIconSize"]
            : (double)Resources["ResourcePanelIconSize"];
        double qtySize = appRes != null && appRes.ContainsKey("ResourcePanelQtySize")
            ? (double)appRes["ResourcePanelQtySize"]
            : (double)Resources["ResourcePanelQtySize"];
        double bodySize = appRes != null && appRes.ContainsKey("ResourcePanelBodySize")
            ? (double)appRes["ResourcePanelBodySize"]
            : (double)Resources["ResourcePanelBodySize"];

        return UserInterfaceCreator.CreatePanelItem(resource.Id, resource.Name, resource.Sprite, isSelected,
            panelItemHeight, qtySize, bodySize, isAndroid, () => OnResourceSelected(resource), isEnabled: resource.Quantity > 0, bindingContext: resource);
    }
    
    // ============================================================================
    // Item Selection Handlers
    // ============================================================================
    
    private void OnResourceSelected(ResourceData resource)
    {
        // Block selection while display is sliding
        if (_isDisplaySliding)
        {
            System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Resource selection ignored during display slide animation");
            return;
        }
        
        // Toggle selection if clicking already-selected resource (only if enabled: quantity > 0 and not sliding)
        if (_selectedResource?.Id == resource.Id)
        {
            if (resource.Quantity > 0 && !_isDisplaySliding)
                {
                    System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Deselecting resource '{resource.Name}'");
                    _selectedResource = null;
                    // Animate/hide the selected resource panel when deselecting
                    _ = UpdateSelectedResourcePanelVisibility();
                    UpdateResourceItemSelectionVisuals();
                    UpdateMachineContentVisual();
                    _ = AnimateMachineDisplayForSelection();
                }
            return;
        }
        
            // Set new selection
            _selectedResource = resource;

            // Update selected name via ViewModel binding
            if (BindingContext is LaboratoryViewModel vm)
                vm.SelectedResourceName = resource.Name;

            // Update dynamic selected panel visibility (Android)
            _ = UpdateSelectedResourcePanelVisibility();
        
        // Selected panel animation removed (SelectedResourcePanel recreated elsewhere)
        
        // Update UI
        // Update selection visuals without rebuilding the whole panel to avoid image flicker
        UpdateResourceItemSelectionVisuals();
        UpdateMachineContentVisual();
        _ = AnimateMachineDisplayForSelection();
    }

    /// <summary>
    /// Update selection/highlight visuals for resource items using the cached Border elements.
    /// Uses fade animation to switch between normal and highlighted 9-slice backgrounds.
    /// </summary>
    private void UpdateResourceItemSelectionVisuals()
    {
#if ANDROID || WINDOWS
        try
        {
            foreach (var kv in _resourceItemCache)
            {
                var id = kv.Key;
                var border = kv.Value;

                if (border == null)
                    continue;

                // Determine whether this item is selected
                bool isSelected = _selectedResource != null && _selectedResource.Id == id;

                // Use UserInterfaceCreator helper to animate selection state
                UserInterfaceCreator.SetPanelItemSelected(border, isSelected, animate: true);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] UpdateResourceItemSelectionVisuals error: {ex.Message}");
        }
#endif
    }

    /// <summary>
    /// Replace the MachineLight visual with two preloaded Images (red/green) stacked
    /// so we can cross-fade the light color without reloading images.
    /// </summary>
    private void SetupMachineLightImages(FurnitureObject light)
    {
#if ANDROID || WINDOWS
        if (light == null || light.VisualElement == null)
            return;

        // If already created, ensure it shows red by default
        if (_machineLightRedImage != null && _machineLightGreenImage != null)
        {
            _machineLightRedImage.Opacity = 1;
            _machineLightGreenImage.Opacity = 0;
            return;
        }

        System.Diagnostics.Debug.WriteLine("[LaboratoryPage] SetupMachineLightImages called");
        if (light.VisualElement is Grid grid)
        {
            grid.Children.Clear();

            _machineLightRedImage = new Image
            {
                Source = "lab__machine_light_red.png",
                Aspect = Aspect.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Opacity = 1
            };

            _machineLightGreenImage = new Image
            {
                Source = "lab__machine_light_green.png",
                Aspect = Aspect.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Opacity = 0
            };

            grid.Children.Add(_machineLightRedImage);
            grid.Children.Add(_machineLightGreenImage);

            light.VisualElement = grid;
            System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Preloaded MachineLight red/green images added to grid");
        }
#endif
    }

    /// <summary>
    /// Replace the MachineDisplay visual with two preloaded Images (off/on) stacked and
    /// keep references so we toggle Opacity instead of swapping Source to avoid flicker.
    /// </summary>
    private void SetupMachineDisplayImages(FurnitureObject display)
    {
#if ANDROID || WINDOWS
        if (display == null || display.VisualElement == null)
            return;

        // If we've already set up the images, adjust opacity according to selection
        if (_displayOffImage != null && _displayOnImage != null)
        {
            SetDisplayOn(_selectedResource != null, animate: false);
            return;
        }

        System.Diagnostics.Debug.WriteLine("[LaboratoryPage] SetupMachineDisplayImages called");
        // Expect VisualElement to be a Grid created by FurnitureObject
        if (display.VisualElement is Grid grid)
        {
            // Remove any existing children (the default single Image)
            grid.Children.Clear();

            // Off image (visible by default)
            _displayOffImage = new Image
            {
                Source = "lab__machine_display_off.png",
                Aspect = Aspect.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Opacity = 1
            };

            // On image (hidden by default)
            _displayOnImage = new Image
            {
                Source = "lab__machine_display_on.png",
                Aspect = Aspect.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Opacity = 0
            };

            grid.Children.Add(_displayOffImage);
            grid.Children.Add(_displayOnImage);

            // Ensure VisualElement references remain correct
            display.VisualElement = grid;
            System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Preloaded MachineDisplay on/off images added to grid");
        }
#endif
    }

    /// <summary>
    /// Create a `MachineDisplayContent` Grid inside the MachineDisplayOverlay furniture object
    /// The grid is hidden by default and toggled in SetDisplayOn
    /// </summary>
    private void SetupMachineDisplayOverlay(FurnitureObject overlay)
    {
#if ANDROID || WINDOWS
        if (overlay == null || overlay.VisualElement == null)
            return;
        System.Diagnostics.Debug.WriteLine("[LaboratoryPage] SetupMachineDisplayOverlay called");

        if (_displayContentGrid != null)
        {
            // already created; adjust visibility according to current state
            _displayContentGrid.IsVisible = _selectedResource != null;
            _displayContentGrid.Opacity = _selectedResource != null ? 1 : 0;
            _displayContentGrid.InputTransparent = _selectedResource == null;
            System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] MachineDisplayContent already exists. Visible={_displayContentGrid.IsVisible}");
            return;
        }

        if (overlay.VisualElement is Grid grid)
        {
            // Create a two-column content grid: left image, right label
            _displayContentGrid = new Grid
            {
                WidthRequest = overlay.Width,
                HeightRequest = overlay.Height,
                IsVisible = false,
                Opacity = 0,
                InputTransparent = true,
                AutomationId = "MachineDisplayContent",
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) }
                },
                RowDefinitions = new RowDefinitionCollection
                {
                    new RowDefinition { Height = GridLength.Star }
                }
            };

            // Left: image (preserve aspect, expand)
            _displayContentImage = new Image
            {
                Aspect = Aspect.AspectFit,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };
            Grid.SetColumn(_displayContentImage, 0);

            // Right: label showing extraction amount
            double titleSize = 14;
            try
            {
                var appRes = Application.Current?.Resources;
                if (appRes != null && appRes.ContainsKey("ResourcePanelTitleSize"))
                    titleSize = (double)appRes["ResourcePanelTitleSize"] * 2;
                else if (Resources.ContainsKey("ResourcePanelTitleSize"))
                    titleSize = (double)Resources["ResourcePanelTitleSize"] * 2;
            }
            catch { }

            _displayContentLabel = new Label
            {
                Text = "x0",
                VerticalTextAlignment = TextAlignment.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                FontSize = titleSize,
                Margin = new Thickness(8, 0, 8, 0)
            };
            Grid.SetColumn(_displayContentLabel, 1);

            _displayContentGrid.Children.Add(_displayContentImage);
            _displayContentGrid.Children.Add(_displayContentLabel);

            grid.Children.Add(_displayContentGrid);
            overlay.VisualElement = grid;

            System.Diagnostics.Debug.WriteLine("[LaboratoryPage] MachineDisplayContent grid created with image+label");
        }
#endif
    }

    /// <summary>
    /// Toggle the MachineDisplay between on/off by fading the preloaded images.
    /// </summary>
    private void SetDisplayOn(bool on, bool animate = true)
    {
#if ANDROID || WINDOWS
                        // Toggle preloaded on/off images and show/hide overlay content grid
        try
        {
            System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] SetDisplayOn called. on={on}, animate={animate}");
            if (_displayOffImage == null || _displayOnImage == null)
                return;

            if (animate)
            {
                // Cross-fade quickly to avoid visible reloads
                if (on)
                {
                    _ = _displayOnImage.FadeTo(1, 120, Easing.Linear);
                    _ = _displayOffImage.FadeTo(0, 120, Easing.Linear);
                }
                else
                {
                    _ = _displayOnImage.FadeTo(0, 120, Easing.Linear);
                    _ = _displayOffImage.FadeTo(1, 120, Easing.Linear);
                }
            }
            else
            {
                _displayOnImage.Opacity = on ? 1 : 0;
                _displayOffImage.Opacity = on ? 0 : 1;
            }
            
            // Also update the MachineLight color (green when enough resources, red otherwise)
            try
            {
                if (_machineLightRedImage != null && _machineLightGreenImage != null)
                {
                    if (on && _selectedResource != null)
                    {
                        int required = _selectedResource.AmountForExtraction ?? 0;
                        bool hasEnough = _selectedResource.Quantity >= required;
                        if (animate)
                        {
                            if (hasEnough)
                            {
                                _ = _machineLightGreenImage.FadeTo(1, 120, Easing.Linear);
                                _ = _machineLightRedImage.FadeTo(0, 120, Easing.Linear);
                            }
                            else
                            {
                                _ = _machineLightGreenImage.FadeTo(0, 120, Easing.Linear);
                                _ = _machineLightRedImage.FadeTo(1, 120, Easing.Linear);
                            }
                        }
                        else
                        {
                            _machineLightGreenImage.Opacity = hasEnough ? 1 : 0;
                            _machineLightRedImage.Opacity = hasEnough ? 0 : 1;
                        }
                    }
                    else
                    {
                        // Always red when display is off or no selection
                        if (animate)
                        {
                            _ = _machineLightGreenImage.FadeTo(0, 120, Easing.Linear);
                            _ = _machineLightRedImage.FadeTo(1, 120, Easing.Linear);
                        }
                        else
                        {
                            _machineLightGreenImage.Opacity = 0;
                            _machineLightRedImage.Opacity = 1;
                        }
                    }
                }
            }
            catch { }
        }
        catch { }

        try
        {
            if (_displayContentGrid != null)
            {
                if (on)
                {
                    _displayContentGrid.IsVisible = true;
                    _displayContentGrid.InputTransparent = false;
                    if (animate)
                        _ = _displayContentGrid.FadeTo(1, 120, Easing.Linear);
                    else
                        _displayContentGrid.Opacity = 1;
                }
                else
                {
                    _displayContentGrid.InputTransparent = true;
                    if (animate)
                        _ = _displayContentGrid.FadeTo(0, 120, Easing.Linear);
                    else
                        _displayContentGrid.Opacity = 0;
                }
            }
        }
        catch { }
#endif
    }
    
    // SelectedResourcePanel visibility/animation (dynamic Android panel)
    private async Task UpdateSelectedResourcePanelVisibility(bool forceHide = false)
    {
#if ANDROID
        System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] UpdateSelectedResourcePanelVisibility called. selectedResource={( _selectedResource?.Name ?? "null")}, forceHide={forceHide}");

        if (_selectedResourcePanel == null)
        {
            System.Diagnostics.Debug.WriteLine("[LaboratoryPage] _selectedResourcePanel is null");
            return;
        }

        bool shouldShow = !forceHide && _selectedResource != null && ResourceListContainer != null && ResourceListContainer.IsVisible;
        System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] SelectedResourcePanel shouldShow={shouldShow}, currentIsVisible={_selectedResourcePanel.IsVisible}");

        if (shouldShow)
        {
            // Ensure layout bounds are applied
            AbsoluteLayout.SetLayoutBounds(_selectedResourcePanel, new Rect(1, 0.15, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
            AbsoluteLayout.SetLayoutFlags(_selectedResourcePanel, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.XProportional | Microsoft.Maui.Layouts.AbsoluteLayoutFlags.YProportional);

            // Bring to front by re-adding as last child
            try
            {
                if (ResourceListWrapper != null)
                {
                    if (ResourceListWrapper.Children.Contains(_selectedResourcePanel))
                    {
                        ResourceListWrapper.Children.Remove(_selectedResourcePanel);
                    }
                    ResourceListWrapper.Children.Add(_selectedResourcePanel);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Error re-adding SelectedResourcePanel: {ex.Message}");
            }

            _selectedResourcePanel.InputTransparent = false;
            _selectedResourcePanel.IsVisible = true;
            _selectedResourcePanel.AnchorX = 0.5;
            _selectedResourcePanel.AnchorY = 0.5;
            _selectedResourcePanel.Scale = 0;
            System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Showing SelectedResourcePanel and animating scale in");
            try
            {
                await _selectedResourcePanel.ScaleTo(1, 200, Easing.SpringOut);
            }
            catch { }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Hiding SelectedResourcePanel (if present)");
            _selectedResourcePanel.InputTransparent = true;
            try { await _selectedResourcePanel.ScaleTo(0, 150, Easing.SpringIn); } catch { }
            _selectedResourcePanel.IsVisible = false;
        }
#else
        await Task.CompletedTask;
#endif
    }
    
    // ============================================================================
    // Display Animation Event Handlers
    // ============================================================================
    
    /// <summary>
    /// Handles display animation start: disable all resource panel items
    /// </summary>
    private void OnDisplayAnimationStarted(object? sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        foreach (var item in _resourceItemCache.Values)
        {
            UserInterfaceCreator.SetPanelItemEnabled(item, false);
        }
#endif
    }
    
    /// <summary>
    /// Handles display animation end: re-enable items that should be enabled (quantity > 0)
    /// </summary>
    private void OnDisplayAnimationEnded(object? sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        var resources = ResourceLibrary.GetAllResources();
        foreach (var resource in resources)
        {
            if (_resourceItemCache.TryGetValue(resource.Id, out var item))
            {
                // Enable item if quantity > 0, disable otherwise
                bool shouldEnable = resource.Quantity > 0;
                UserInterfaceCreator.SetPanelItemEnabled(item, shouldEnable);
            }
        }
#endif
    }

    /// <summary>
    /// Sets the MachineContent furniture image to the selected resource sprite,
    /// or hides the MachineContent visual when no resource is selected.
    /// </summary>
    private void UpdateMachineContentVisual()
    {
#if ANDROID || WINDOWS
        var machine = _furnitureObjects.FirstOrDefault(f => f.Id == "MachineContent");
        if (machine == null || machine.VisualElement == null)
            return;
        System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] UpdateMachineContentVisual: selectedResource={_selectedResource?.Name ?? "null"}");

        // If a resource is selected, set the image source and show the element
        if (_selectedResource != null)
        {
            // Machine VisualElement is a Grid with an Image as its first child
            if (machine.VisualElement is Grid grid && grid.Children.Count > 0 && grid.Children[0] is Image img)
            {
                img.Source = _selectedResource.Sprite ?? string.Empty;
                System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Set MachineContent image to {_selectedResource.Sprite}");
            }
            machine.VisualElement.IsVisible = true;

            // Update overlay content image and label if present
            if (_displayContentImage != null)
            {
                _displayContentImage.Source = _selectedResource.Sprite ?? string.Empty;
                System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Set overlay image to {_selectedResource.Sprite}");
            }
            if (_displayContentLabel != null)
            {
                _displayContentLabel.Text = "x" + (_selectedResource.AmountForExtraction?.ToString() ?? "0");
                try
                {
                    var appRes = Application.Current?.Resources;
                    if (appRes != null && appRes.ContainsKey("ResourcePanelTitleFont"))
                        _displayContentLabel.FontFamily = (string)appRes["ResourcePanelTitleFont"];
                }
                catch { }
                System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Set overlay label to {_displayContentLabel.Text}");
            }
        }
        else
        {
            // Hide machine content when no resource selected
            machine.VisualElement.IsVisible = false;

            if (_displayContentImage != null)
            {
                _displayContentImage.Source = string.Empty;
                System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Cleared overlay image");
            }
            if (_displayContentLabel != null)
            {
                _displayContentLabel.Text = "x0";
                System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Cleared overlay label");
            }
            SetDisplayOn(false, animate: false);
        }
#endif
    }

    private async Task AnimateMachineDisplayForSelection()
    {
#if ANDROID || WINDOWS
        System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] AnimateMachineDisplayForSelection start. selectedResource={_selectedResource?.Name ?? "null"}");
        // cancel any previous pending delay so new actions take precedence
        try { _displayAnimationCts?.Cancel(); } catch { }
        _displayAnimationCts?.Dispose();
        _displayAnimationCts = new CancellationTokenSource();
        var token = _displayAnimationCts.Token;

        var display = _furnitureObjects.FirstOrDefault(f => f.Id == "MachineDisplay");
        var button = _furnitureObjects.FirstOrDefault(f => f.Id == "MachineDisplayButton");
        if (display == null || display.VisualElement == null)
            return;

        if (_isDisplayAnimating)
            return;

        _isDisplayAnimating = true;
        
        // Notify listeners animation started (disable all items)
        DisplayAnimationStarted?.Invoke(this, EventArgs.Empty);

        // Ensure ExtractButton is not interactable at animation start (will be enabled only after full slide)
        var extractStationInit = _stationObjects.FirstOrDefault(s => s.Id == "ExtractButton");
        if (extractStationInit != null)
        {
            extractStationInit.CanInteract = false;
            if (extractStationInit.VisualElement is View evInit)
                evInit.InputTransparent = true;
        }
        
        try
        {
            if (_selectedResource != null)
            {
                // Step 1: Slide display out to reveal at X=480 (translation -> 0)
                // Button is invisible at X=480 Y=0
                if (button?.VisualElement != null)
                {
                    button.VisualElement.IsVisible = false;
                    button.VisualElement.TranslationX = 0;
                    button.VisualElement.TranslationY = 0;
                }
                
                _isDisplaySliding = true;
                System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Sliding display out to visible (TranslateTo 0)");
                await display.VisualElement.TranslateTo(0, 0, 600, Easing.CubicInOut);
                _isDisplaySliding = false;

                // Step 2: Display has slid out. Begin delay before SetDisplayOn
                // Step 3: During delay, make button visible (but don't move yet)
                if (button?.VisualElement != null)
                {
                    button.VisualElement.IsVisible = true;
                    System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Button made visible during delay");
                    // Do NOT enable ExtractButton yet — enable only after the button finishes sliding down.
                }

                // Re-enable items briefly during delay (step 3 allows selection)
                DisplayAnimationEnded?.Invoke(this, EventArgs.Empty);

                // Wait up to 1s but allow cancellation when selection changes
                try
                {
                    await Task.Delay(1000, token);
                }
                catch (TaskCanceledException)
                {
                    // If cancelled and selection cleared, immediately slide back in
                    if (_selectedResource == null)
                    {
                        // Hide button before reversing
                        if (button?.VisualElement != null)
                        {
                            button.VisualElement.IsVisible = false;
                            button.VisualElement.TranslationY = 0;
                        }
                        
                        // Immediately set display image to off when sliding back starts
                        SetDisplayOn(false, animate: false);

                        _isDisplaySliding = true;
                        System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Sliding display back to hidden (TranslateTo -480)");
                        await display.VisualElement.TranslateTo(-480, 0, 400, Easing.CubicInOut);
                        _isDisplaySliding = false;

                        // After sliding back, notify listeners as well
                        DisplayAnimationEnded?.Invoke(this, EventArgs.Empty);
                    }
                    return;
                }

                // Step 4: After delay, if selection still present, switch sprite on
                if (_selectedResource != null)
                {
                    // Disable items again before SetDisplayOn animation
                    DisplayAnimationStarted?.Invoke(this, EventArgs.Empty);
                    
                    // Switch to "on" visual using preloaded images
                    SetDisplayOn(true, animate: true);
                    
                    // Wait for SetDisplayOn fade to complete (120ms)
                    await Task.Delay(150);
                    
                    // Step 5: Check if we have enough resources
                    int required = _selectedResource.AmountForExtraction ?? 0;
                    bool hasEnough = _selectedResource.Quantity >= required;
                    
                        if (hasEnough && button?.VisualElement != null)
                    {
                        // Step 6: Slide button down to Y=240 (positive moves downward in this layout)
                        System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Sliding button down (hasEnough=true)");
                        await button.VisualElement.TranslateTo(0, 240, 300, Easing.CubicOut);
                        System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Button animation complete");

                        // Now that the button is fully extended, make ExtractButton interactable
                        var extractEnable = _stationObjects.FirstOrDefault(s => s.Id == "ExtractButton");
                        if (extractEnable != null)
                        {
                            extractEnable.CanInteract = true;
                            if (extractEnable.VisualElement is View ev2)
                                ev2.InputTransparent = false;
                        }
                    }
                    else
                    {
                        // Not enough resources: hide button, animation ends here
                        if (button?.VisualElement != null)
                        {
                            button.VisualElement.IsVisible = false;
                            button.VisualElement.TranslationY = 0;
                        }
                        // Disable ExtractButton when button hidden or not enough resources
                        var extractDisable = _stationObjects.FirstOrDefault(s => s.Id == "ExtractButton");
                        if (extractDisable != null)
                        {
                            extractDisable.CanInteract = false;
                            if (extractDisable.VisualElement is View evd)
                                evd.InputTransparent = true;
                        }
                        System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Not enough resources, re-enabling items");
                    }
                    
                    // Re-enable items after button animation completes (or immediately if not shown)
                    DisplayAnimationEnded?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    // Otherwise, set image off immediately and slide back; notify end after slide completes
                    if (button?.VisualElement != null)
                    {
                        button.VisualElement.IsVisible = false;
                        button.VisualElement.TranslationY = 0;
                    }
                    var extractHide = _stationObjects.FirstOrDefault(s => s.Id == "ExtractButton");
                    if (extractHide != null)
                    {
                        extractHide.CanInteract = false;
                        if (extractHide.VisualElement is View evh)
                            evh.InputTransparent = true;
                    }
                    
                    SetDisplayOn(false, animate: false);

                    _isDisplaySliding = true;
                    System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Sliding display back to hidden (TranslateTo -480)");
                    await display.VisualElement.TranslateTo(-480, 0, 400, Easing.CubicInOut);
                    _isDisplaySliding = false;

                    DisplayAnimationEnded?.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                // Reverse animation: deselection
                // Step 1 (reverse): If button is shown and slid down, slide it back up first
                if (button?.VisualElement != null && button.VisualElement.IsVisible && button.VisualElement.TranslationY > 10)
                {
                    System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Sliding button back up before hiding display");
                    await button.VisualElement.TranslateTo(0, 0, 300, Easing.CubicIn);
                    button.VisualElement.IsVisible = false;
                }
                else if (button?.VisualElement != null)
                {
                    // Button not shown or not slid down, just hide it
                    button.VisualElement.IsVisible = false;
                    button.VisualElement.TranslationY = 0;
                }
                
                // Step 2 (reverse): SetDisplayOn(false)
                SetDisplayOn(false, animate: true);
                
                // Wait for SetDisplayOn fade to complete
                await Task.Delay(150);
                
                // Step 3 (reverse): Slide display back to hidden position (translation -> -480)
                _isDisplaySliding = true;
                System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Sliding display back to hidden (TranslateTo -480)");
                await display.VisualElement.TranslateTo(-480, 0, 400, Easing.CubicInOut);
                _isDisplaySliding = false;

                DisplayAnimationEnded?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            _isDisplayAnimating = false;
            try { _displayAnimationCts?.Dispose(); } catch { }
            _displayAnimationCts = null;
        }
#endif
    }
}

