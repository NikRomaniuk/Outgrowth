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
    private ScreenProperties screenProps = ScreenProperties.Instance;
    
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
    
    // Per-panel animation tracking to allow multiple panels to animate concurrently
    private readonly System.Collections.Generic.HashSet<StyledPanel> _animatingPanels = new();
    
    // Lock for machine display animation
    private bool _isDisplayAnimating = false;
    // Indicates the translate (slide) phase of the display animation is running
    private bool _isDisplaySliding = false;
    // Cancellation token for display animation delay to allow interruption
    private CancellationTokenSource? _displayAnimationCts;
    
    // Track whether dynamic panels have been created to avoid duplicate creation
    private bool _panelsCreated = false;
    
    // Cache for resource panel items to avoid full recreation when only selection changes
    private List<StyledPanel>? _resourcePanelItems;
    
    // --- Dynamically created Panels ---
    private StyledPanel? _resourcePanel;
    private AbsoluteLayout? _resourcePanelWrapper;
    
    // SelectedResourcePanel (Android only)
#if ANDROID
    private StyledPanel? _selectedResourcePanel;
    private Border? _selectedResourcePanelWrapper;
#endif
    
    // Navigation buttons (left gutter)
    private StyledPanel? _hubButton;
    private AbsoluteLayout? _hubButtonWrapper;
    
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
        
        // Initialize screen properties and font sizes
        var screenProps = ScreenProperties.Instance;
        if (this.Width > 0 && this.Height > 0)
        {
            System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] OnPageLoaded: Page size available ({this.Width}x{this.Height}), creating panels immediately");
            screenProps.UpdateScreenProperties(this.Width, this.Height);
            screenProps.UpdateFontSizes(screenProps.AdaptiveScale);
            
            // Scale and position EnvironmentWrapper
            if (EnvironmentWrapper != null && EnvironmentContainer != null)
            {
                EnvironmentContainer.WidthRequest = ScreenProperties.ReferenceWidth;
                EnvironmentContainer.HeightRequest = ScreenProperties.ReferenceHeight;
                
                EnvironmentWrapper.AnchorX = 0.5;
                EnvironmentWrapper.AnchorY = 0.5;
                if (EnvironmentWrapper.Handler != null)
                {
                    try { EnvironmentWrapper.Scale = screenProps.Scale; }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Scale exception: {ex.Message}"); }
                }
                EnvironmentWrapper.WidthRequest = ScreenProperties.ReferenceWidth;
                EnvironmentWrapper.HeightRequest = ScreenProperties.ReferenceHeight;
                
                EnvironmentContainer.InputTransparent = false;
                EnvironmentContainer.BackgroundColor = Colors.Transparent;
                
                EnvironmentWrapper.TranslationX = screenProps.OffsetX;
                EnvironmentWrapper.TranslationY = screenProps.OffsetY;
            }
            
            CreateDynamicPanels();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[LaboratoryPage] OnPageLoaded: Page size not available, deferring panel creation to SizeChanged");
        }
        
#if WINDOWS
        // Attach Windows keyboard input handler (Esc for closing panels, E for hub navigation)
        _windowsInput = new WindowsInput(
            onLeftArrow: () => { }, // No action
            onRightArrow: () => { }, // No action
            onEscape: FireAndForgetCloseAllPanels,  // Close panels with Esc key
            onQ: () => OnHubClicked(this, EventArgs.Empty)
        );
        _windowsInput.Attach();
#endif
    }
    
    /// <summary>
    /// Creates all dynamic UI panels (resource panel, navigation buttons).
    /// Called after valid screen dimensions are available.
    /// </summary>
    private void CreateDynamicPanels()
    {
        if (_panelsCreated)
        {
            System.Diagnostics.Debug.WriteLine("[LaboratoryPage] CreateDynamicPanels: Panels already created, skipping");
            return;
        }
        System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] CreateDynamicPanels: Creating panels with scale {screenProps.AdaptiveScale}");
        
        CreateResourcePanel();
        CreateNavigationButtons();
        _panelsCreated = true;
    }

    // -=-=- PANELS -=-=-
    
    private void CreateResourcePanel()
    {
        try
        {
            var resources = ResourceLibrary.GetAllResources().ToList();
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
            double cornerSize = 40 * adaptive;

            _resourcePanelWrapper = new AbsoluteLayout
            {
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Fill,
                Margin = new Thickness(0, 0, 20, 0),
                ZIndex = 1000
            };
            MainGrid.SetColumn(_resourcePanelWrapper, 2);
            if (!MainGrid.Children.Contains(_resourcePanelWrapper))
                MainGrid.Children.Add(_resourcePanelWrapper);

            _resourcePanel = new StyledPanel(
                type: "scroll",
                width: panelWidth,
                height: panelHeight,
                cornerSize: cornerSize,
                backgroundColor: Color.FromArgb("#0f0c29"),
                borderColor: Color.FromArgb("#302b63"),
                cornerImage: "ui__panel_corner.png",
                horizontalEdgeImage: "ui__panel_edge_horizontal.png",
                verticalEdgeImage: "ui__panel_edge_vertical.png",
                centerImage: "ui__panel_center.png"
            );

            if (_resourcePanelWrapper != null)
            {
                _resourcePanelWrapper.Children.Clear();
                _resourcePanelWrapper.Children.Add(_resourcePanel.Panel);
#if ANDROID
                AbsoluteLayout.SetLayoutBounds(_resourcePanel.Panel, new Rect(0, 0.75, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
#else
                AbsoluteLayout.SetLayoutBounds(_resourcePanel.Panel, new Rect(0, 0.5, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
#endif
                AbsoluteLayout.SetLayoutFlags(_resourcePanel.Panel, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.YProportional);
                _resourcePanel.Panel.AnchorX = 0.5;
                _resourcePanel.Panel.AnchorY = 0.5;
                _resourcePanel.Panel.Scale = 0;
                _resourcePanel.Panel.IsVisible = false;
                _resourcePanel.Panel.InputTransparent = true;
            }

#if ANDROID
            // Create SelectedResourcePanel
            try
            {
                double selectedPanelWidth = baseWidth * adaptive;
                double selectedPanelHeight = 160.0 * adaptive;

                // Create Label for selected resource name
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
                try { selectedLabel.FontFamily = "SilkscreenBold"; } catch { }

                _selectedResourcePanel = new StyledPanel(
                    width: selectedPanelWidth,
                    height: selectedPanelHeight,
                    cornerSize: cornerSize,
                    backgroundColor: Color.FromArgb("#2874a7"),
                    borderColor: Color.FromArgb("#00d2ff"),
                    content: selectedLabel,
                    cornerImage: "ui__panel_highlighted_corner.png",
                    horizontalEdgeImage: "ui__panel_highlighted_edge_horizontal.png",
                    verticalEdgeImage: "ui__panel_highlighted_edge_vertical.png",
                    centerImage: "ui__panel_highlighted_center.png"
                );
                _selectedResourcePanelWrapper = new Border
                {
                    Content = _selectedResourcePanel.Panel,
                    Stroke = null,
                    StrokeThickness = 0
                };

                AbsoluteLayout.SetLayoutBounds(_selectedResourcePanelWrapper, new Rect(0, 0.15, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
                AbsoluteLayout.SetLayoutFlags(_selectedResourcePanelWrapper, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.XProportional | Microsoft.Maui.Layouts.AbsoluteLayoutFlags.YProportional);
                _selectedResourcePanelWrapper.AnchorX = 0.5;
                _selectedResourcePanelWrapper.AnchorY = 0.5;
                _selectedResourcePanelWrapper.Scale = 0;
                _selectedResourcePanelWrapper.IsVisible = false;
                _selectedResourcePanelWrapper.InputTransparent = true;
                _selectedResourcePanelWrapper.ZIndex = 1005;
                _resourcePanelWrapper?.Children.Add(_selectedResourcePanelWrapper);
                System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Created SelectedResourcePanel");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Error creating SelectedResourcePanel: {ex.Message}");
            }
#endif

            var contentStack = _resourcePanel.ScrollContainer?.Content as VerticalStackLayout;
            if (contentStack == null)
            {
                System.Diagnostics.Debug.WriteLine("[LaboratoryPage] CreateResourcePanel: No content stack available");
                return;
            }
            
            // Create all Resource Panel Items
            contentStack.Children.Clear();

            _resourcePanelItems = new List<StyledPanel>();
            _resourcePanelItems.Clear();
            foreach (var resource in resources)
            {
                var resourceItem = CreateResourceItem(resource);
                resourceItem.ClassId = resource.Id;
                
                // Manually add TapGestureRecognizer since onTapped parameter doesn't work for panelItems
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (s, e) => await OnResourceSelected(resource);
                resourceItem.Panel.GestureRecognizers.Add(tap);
                
                _resourcePanelItems.Add(resourceItem);
                contentStack.Children.Add(resourceItem.Panel);
            }
            System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Created {_resourcePanelItems.Count} ResourcePanel items");
            
            // Update panel items to set initial enabled/disabled state
            UpdateResourcePanel();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Error creating ResourcePanel: {ex.Message}");
            _resourcePanelItems = null;
        }
    }
    private void CreateNavigationButtons()
    {
        try
        {
            var adaptive = screenProps.AdaptiveScale;

#if ANDROID
            const double baseWidth = 200.0;
            const double baseHeight = 200.0;
#else
            const double baseWidth = 200.0;
            const double baseHeight = 200.0;
#endif
            double buttonWidth = baseWidth * adaptive;
            double buttonHeight = baseHeight * adaptive;
            double cornerSize = 40 * adaptive;

            _hubButtonWrapper = new AbsoluteLayout
            {
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Fill,
                Margin = new Thickness(20, 0, 0, 0),
                ZIndex = 1000
            };


            MainGrid.SetColumn(_hubButtonWrapper, 0);
            if (!MainGrid.Children.Contains(_hubButtonWrapper))
                MainGrid.Children.Add(_hubButtonWrapper);

            // Create Hub button using StyledPanel selection constructor
            _hubButton = new StyledPanel(
                type: "selection",
                width: buttonWidth,
                height: buttonHeight,
                isSelected: false,
                cornerSize: cornerSize,
                cornerImage: "ui__panel_item_corner.png",
                horizontalEdgeImage: "ui__panel_item_edge_horizontal.png",
                verticalEdgeImage: "ui__panel_item_edge_vertical.png",
                centerImage: "ui__panel_item_center.png"
            );

            // Create icon for Hub button
            var hubIcon = new Image
            {
                Source = "ui__icon_hub.png", // Replace with actual hub icon if available
                Aspect = Aspect.AspectFit,
                WidthRequest = buttonHeight * 0.7,
                HeightRequest = buttonHeight * 0.7,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            if (_hubButton.ContentGrid != null)
            {
#if WINDOWS
                // Configure ContentGrid to be 2x2 on Windows
                var cg = _hubButton.ContentGrid;
                cg.Children.Clear();
                cg.ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star }
                };
                cg.RowDefinitions = new RowDefinitionCollection
                {
                    new RowDefinition { Height = GridLength.Star },
                    new RowDefinition { Height = GridLength.Star }
                };

                // Place hubIcon to span both rows and columns
                Grid.SetRow(hubIcon, 0);
                Grid.SetColumn(hubIcon, 0);
                Grid.SetRowSpan(hubIcon, 2);
                Grid.SetColumnSpan(hubIcon, 2);
                hubIcon.ZIndex = 0;
                cg.Children.Add(hubIcon);

                // Tooltip label at row=2,col=2 (1,1 zero-based) with higher ZIndex
                var tooltipLabel = new Label
                {
                    Text = "Q",
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };
                try
                {
                    var appRes = Application.Current?.Resources;
                    if (appRes != null && appRes.ContainsKey("ResourcePanelBodyFont"))
                        tooltipLabel.FontFamily = (string)appRes["ResourcePanelBodyFont"];
                    else if (Resources != null && Resources.ContainsKey("ResourcePanelBodyFont"))
                        tooltipLabel.FontFamily = (string)Resources["ResourcePanelBodyFont"];
                }
                catch { }
                tooltipLabel.FontSize = 50 * adaptive;
                Grid.SetRow(tooltipLabel, 1);
                Grid.SetColumn(tooltipLabel, 1);
                tooltipLabel.ZIndex = 1;
                cg.Children.Add(tooltipLabel);
#else
                _hubButton.ContentGrid.Children.Add(hubIcon);
#endif
            }

            // Position button
            _hubButtonWrapper.Children.Add(_hubButton.Panel);
#if WINDOWS
            AbsoluteLayout.SetLayoutBounds(_hubButton.Panel, new Rect(0, 0.97, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
#else
            AbsoluteLayout.SetLayoutBounds(_hubButton.Panel, new Rect(0, 0.925, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
#endif
            AbsoluteLayout.SetLayoutFlags(_hubButton.Panel, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.YProportional);
            _hubButton.Panel.AnchorX = 0.5;
            _hubButton.Panel.AnchorY = 0.5;
            _hubButton.Panel.IsVisible = true;
            _hubButton.Panel.InputTransparent = false;
            
            // Ensure wrapper is interactive
            _hubButtonWrapper.InputTransparent = false;
            _hubButtonWrapper.IsVisible = true;

            // Attach tap gesture to Panel (skip on Windows — handled by keyboard 'E')
#if !WINDOWS
            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) => OnHubClicked(s ?? this, EventArgs.Empty);
            _hubButton.Panel.GestureRecognizers.Add(tap);
#endif

            System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Created navigation buttons dynamically");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Failed to create navigation buttons: {ex.Message}");
        }
    }
    
    private void OnPageDisappearing(object? sender, EventArgs e)
    {
        // Cancel any ongoing display animation
        _displayAnimationCts?.Cancel();
        _displayAnimationCts?.Dispose();
        _displayAnimationCts = null;
        
        // Close all panels immediately without animation
        _animatingPanels.Clear();
        if (_resourcePanel != null && _resourcePanel.Panel.IsVisible)
        {
            _resourcePanel.Panel.IsVisible = false;
            _resourcePanel.Panel.Scale = 0;
            _resourcePanel.Panel.InputTransparent = true;
        }
#if ANDROID
        if (_selectedResourcePanel != null && _selectedResourcePanel.Panel.IsVisible)
        {
            _selectedResourcePanel.Panel.IsVisible = false;
        }
#endif

        // Leaving the page is allowed to clear selection regardless of slide state
        _selectedResource = null;
        UpdateMachineContentVisual();
        
        // Unsubscribe from display animation events
        DisplayAnimationStarted -= OnDisplayAnimationStarted;
        DisplayAnimationEnded -= OnDisplayAnimationEnded;
        
        // Clean up dynamically created elements to prevent memory leaks
        CleanupStationObjects();
        CleanupFurnitureObjects();
        CleanupDynamicPanels();
        
#if WINDOWS
        // Detach Windows keyboard input handler
        _windowsInput?.Detach();
        _windowsInput = null;
#endif
    }
    
    
    private void CleanupDynamicPanels()
    {
        try
        {
            // Remove resource panel wrapper from MainGrid
            if (_resourcePanelWrapper != null && MainGrid.Children.Contains(_resourcePanelWrapper))
            {
                MainGrid.Children.Remove(_resourcePanelWrapper);
            }
            
            // Remove hub button wrapper from MainGrid
            if (_hubButtonWrapper != null && MainGrid.Children.Contains(_hubButtonWrapper))
            {
                MainGrid.Children.Remove(_hubButtonWrapper);
            }
            
            // Clear panel item caches
            _resourcePanelItems?.Clear();
            
            // Reset panel creation flag
            _panelsCreated = false;
            
            // Nullify all panel references
            _resourcePanel = null;
            _resourcePanelWrapper = null;
            _resourcePanelItems = null;
            _hubButton = null;
            _hubButtonWrapper = null;
            
#if ANDROID
            _selectedResourcePanel = null;
            _selectedResourcePanelWrapper = null;
#endif
            
            System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Cleaned up all dynamic panels");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Error cleaning up dynamic panels: {ex.Message}");
        }
    }

    // -=-=- STATION OBJECTS -=-=-
    
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

    // -=-=- FURNITURE OBJECTS -=-=-
    
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
        if (EnvironmentWrapper != null && EnvironmentContainer != null)
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
                
                // Update station object positions from absolute coordinates
                UpdateStationPositions();
                UpdateFurniturePositions();
                
                var adaptive = screenProps.AdaptiveScale;
                screenProps.UpdateFontSizes(adaptive);
                
                // Create panels if not already created
                if (!_panelsCreated)
                {
                    System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] OnPageSizeChanged: Creating panels with dimensions {pageWidth}x{pageHeight}");
                    CreateDynamicPanels();
                }
            }
        }
#endif
    }
    
    private void UpdatePanelSize(double adaptiveScale)
    {
        
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
    
    private async void OnHubClicked(object? sender, EventArgs e)
    {
        await NavigationService.NavigateWithFadeAsync("//HubPage");
    }
    
    private async void OnResourceSlotClicked(object sender, EventArgs e)
    {
        if (_resourcePanel == null)
            return;

        try
        {
            if (_animatingPanels.Count > 0)
                return; // Ignore clicks during animations
            
            System.Diagnostics.Debug.WriteLine("[LaboratoryPage] OnResourceSlotClicked called");
            
            // Toggle seeds panel
            if (!_resourcePanel.Panel.IsVisible)
            {
                // Open resource panel
#if ANDROID
                if (_selectedResource == null)
                {
                    await OpenPanel(true, _resourcePanel);
                }
                else
                {
                    await Task.WhenAll(
                    OpenPanel(true, _resourcePanel),
                    OpenPanel(true, _selectedResourcePanel)
                    );
                }
#else
                await OpenPanel(true, _resourcePanel);
#endif
            }
            else
            {
                // Close resource panel with animation
#if ANDROID
                await Task.WhenAll(
                ClosePanel(true, _resourcePanel),
                ClosePanel(true, _selectedResourcePanel)
                );
#else
                await ClosePanel(true, _resourcePanel);
#endif
            }
            
            System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Resource panel toggled");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Exception in OnResourceSlotClicked: {ex.Message}");
        }
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

            // Check whether we still have enough for another extraction
            bool hasEnough = _selectedResource.Quantity >= required;

            // If not enough for another extraction, slide the MachineDisplayButton back and set light to red
            if (!hasEnough)
            {
                if (_selectedResource.Quantity <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Deselecting resource {_selectedResource.Name}");
                    
                    // Animate deselection on the panel item
                    if (_resourcePanelItems != null)
                    {
                        var panel = _resourcePanelItems.FirstOrDefault(p => p.ClassId == _selectedResource.Id);
                        if (panel != null)
                            panel.SetPanelSelected(false, animate: true);
                    }

                    _selectedResource = null;
            
                    // Update selected name via ViewModel binding
                    var gvm = BindingContext as LaboratoryViewModel;
                    if (gvm != null)
                        gvm.SelectedResourceName = string.Empty;

                    UpdateResourcePanel();
                    UpdateMachineContentVisual();
                    _ = AnimateMachineDisplayForSelection();
                    
                    // Update dynamic selected panel visibility (Android)
#if ANDROID
                    await ClosePanel(true, _selectedResourcePanel);
#endif
                    return;
                }

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

    private async Task OpenPanel(bool animate = true, StyledPanel? panel = null)
    {
        if (panel == null)
            return;

#if ANDROID
        // For selected panels on Android, animate the wrapper instead
        View? targetView = null;
        if (panel == _selectedResourcePanel && _selectedResourcePanelWrapper != null)
            targetView = _selectedResourcePanelWrapper;
        else
            targetView = panel.Panel;
#else
        View targetView = panel.Panel;
#endif

        if (animate)
        {
            _animatingPanels.Add(panel);

            // Prepare panel for animation
            targetView.Scale = 0;
            targetView.IsVisible = true;
            targetView.InputTransparent = false;
            
            // Update panel to ensure items are populated
            if(panel == _resourcePanel)
                UpdateResourcePanel();

            // Animate panel
            await targetView.ScaleTo(1, 200, Easing.SpringOut);

            _animatingPanels.Remove(panel);
        }
        else
        {
            // Non-animated open
            targetView.Scale = 1;
            targetView.IsVisible = true;
            targetView.InputTransparent = false;

            // Update panel to ensure items are populated
            if(panel == _resourcePanel)
                UpdateResourcePanel();
        }
    }

    private async Task ClosePanel(bool animate = true, StyledPanel? panel = null)
    {
        if (panel == null)
            return;

#if ANDROID
        // For selected panels on Android, animate the wrapper instead
        View? targetView = null;
        if (panel == _selectedResourcePanel && _selectedResourcePanelWrapper != null)
            targetView = _selectedResourcePanelWrapper;
        else
            targetView = panel.Panel;
#else
        View targetView = panel.Panel;
#endif

        if (animate)
        {
            _animatingPanels.Add(panel);

            // Animate panel
            await targetView.ScaleTo(0, 200, Easing.SpringIn);
            targetView.IsVisible = false;
            targetView.InputTransparent = true;

            _animatingPanels.Remove(panel);
        }
        else
        {
            // Non-animated open
            targetView.AnchorX = 0.5;
            targetView.AnchorY = 0.5;
            targetView.Scale = 0;
            targetView.IsVisible = false;
            targetView.InputTransparent = true;

        }
    }
    
    private async Task CloseAllPanels()
    {
        var tasks = new List<Task>();
        
#if ANDROID
        if (_resourcePanel != null && _resourcePanel.Panel.IsVisible)
            tasks.Add(ClosePanel(true, _resourcePanel));
        if (_selectedResourcePanel != null && _selectedResourcePanel.Panel.IsVisible)
            tasks.Add(ClosePanel(true, _selectedResourcePanel));
#else
        if (_resourcePanel != null && _resourcePanel.Panel.IsVisible)
            tasks.Add(ClosePanel(true, _resourcePanel));
#endif

        await Task.WhenAll(tasks);
    }

    // Helper to call CloseAllPanels from a synchronous Action without CS4014 warnings
    private void FireAndForgetCloseAllPanels()
    {
#pragma warning disable CS4014
        CloseAllPanels();
#pragma warning restore CS4014
    }

    private StyledPanel CreateResourceItem(ResourceData resource)
    {
        bool isSelected = _selectedResource?.Id == resource.Id;
        System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] CreateResourceItem: {resource.Name}, isSelected={isSelected}, _selectedResource={_selectedResource?.Name ?? "null"}");
#if ANDROID
        bool isAndroid = true;
#else
        bool isAndroid = false;
#endif
        var adaptive = screenProps.AdaptiveScale;
        var appRes = Application.Current?.Resources;
        // Resource values are already set by ScreenProperties.UpdateFontSizes(adaptiveScale)
        // so use them directly without applying `adaptive` again to avoid double-scaling.
        double panelItemHeight = (appRes != null && appRes.ContainsKey("ResourcePanelIconSize")
            ? (double)appRes["ResourcePanelIconSize"]
            : (double)Resources["ResourcePanelIconSize"]);
        double qtySize = (appRes != null && appRes.ContainsKey("ResourcePanelQtySize")
            ? (double)appRes["ResourcePanelQtySize"]
            : (double)Resources["ResourcePanelQtySize"]);
        double bodySize = (appRes != null && appRes.ContainsKey("ResourcePanelBodySize")
            ? (double)appRes["ResourcePanelBodySize"]
            : (double)Resources["ResourcePanelBodySize"]);

        var panelItem = new StyledPanel(
            type: "panelItem",
            id: resource.Id,
            name: resource.Name,
            sprite: resource.Sprite,
            isSelected: isSelected,
            panelItemHeight: panelItemHeight,
            qtyFontSize: qtySize,
            bodyFontSize: bodySize,
            isAndroid: isAndroid,
            bindingContext: resource);
        panelItem.ClassId = resource.Id;
        panelItem.BindingContext = resource; // Set BindingContext on the panel itself for access in Update methods
        return panelItem;
    }
    
    // ============================================================================
    // Item Selection Handlers
    // ============================================================================

    private async Task OnResourceSelected(ResourceData resource)
    {
        // Block selection while display is sliding
        if (_isDisplaySliding)
            return;
        
        if (_resourcePanelItems != null)
        {
            var panel = _resourcePanelItems.FirstOrDefault(p => p.ClassId == resource.Id);
            if (!panel!.ItemIsEnabled)
            {
                System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Tried to select disabled resource {resource.Name}");
                return;
            }
        }

        // If already selected, clear selection (toggle off)
        if (_selectedResource?.Id == resource.Id)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Deselecting resource {resource.Name}");
            
            // Animate deselection on the panel item
            if (_resourcePanelItems != null)
            {
                var panel = _resourcePanelItems.FirstOrDefault(p => p.ClassId == resource.Id);
                if (panel != null)
                    panel.SetPanelSelected(false, animate: true);
            }
            
            _selectedResource = null;
            
            // Update selected name via ViewModel binding
            var gvm = BindingContext as LaboratoryViewModel;
            if (gvm != null)
                gvm.SelectedResourceName = string.Empty;

            UpdateResourcePanel();
            UpdateMachineContentVisual();
            _ = AnimateMachineDisplayForSelection();
            
            // Update dynamic selected panel visibility (Android)
#if ANDROID
            await ClosePanel(true, _selectedResourcePanel);
#endif
            return;
        }
        
        // Deselect previously selected liquid panel (if any)
        if (_selectedResource != null && _resourcePanelItems != null)
        {
            var oldPanel = _resourcePanelItems.FirstOrDefault(p => p.ClassId == _selectedResource.Id);
            if (oldPanel != null)
                oldPanel.SetPanelSelected(false, animate: true);
        }
        
        // Set new selection
        _selectedResource = resource;
        
        // Animate selection on the new panel item
        if (_resourcePanelItems != null)
        {
            var newPanel = _resourcePanelItems.FirstOrDefault(p => p.ClassId == resource.Id);
            if (newPanel != null)
                newPanel.SetPanelSelected(true, animate: true);
        }
        
        // Update selected name via ViewModel binding
        var gvm2 = BindingContext as LaboratoryViewModel;
        if (gvm2 != null)
            gvm2.SelectedResourceName = resource.Name;

        UpdateResourcePanel();
        UpdateMachineContentVisual();
        _ = AnimateMachineDisplayForSelection();
        
        // Update dynamic selected panel visibility (Android)
#if ANDROID
        await OpenPanel(true, _selectedResourcePanel);
#endif
    }

    private void UpdateResourcePanel()
    {
        if (_resourcePanelItems == null)
            return;

        foreach (var panel in _resourcePanelItems)
        {
            // Find the corresponding resource from panel's binding context
            if (panel.BindingContext is ResourceData resource)
            {
                // Determine whether this item is selected
                bool isSelected = _selectedResource != null && _selectedResource.Id == resource.Id;
                // Update selection state
                panel.SetPanelSelected(isSelected, animate: false);
                panel.SetPanelItemEnabled(resource.Quantity > 0);
            }
        }
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
        System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] UpdateSelectedResourcePanelVisibility called. selectedResource={(_selectedResource?.Name ?? "null")}, forceHide={forceHide}");

        if (_selectedResourcePanel == null)
        {
            System.Diagnostics.Debug.WriteLine("[LaboratoryPage] _selectedResourcePanel is null");
            return;
        }

        bool shouldShow = !forceHide && _selectedResource != null && _resourcePanel != null && _resourcePanel.Panel.IsVisible;
        System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] SelectedResourcePanel shouldShow={shouldShow}, currentIsVisible={_selectedResourcePanel.Panel.IsVisible}");

        if (shouldShow)
        {
            await OpenPanel(true, _selectedResourcePanel);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[LaboratoryPage] Hiding SelectedResourcePanel (if present)");
            await ClosePanel(true, _selectedResourcePanel);
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
        System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Display animation started, disabling {_resourcePanelItems?.Count ?? 0} resource items");
        if (_resourcePanelItems == null)
            return;

        foreach (var panel in _resourcePanelItems)
        {
            // Disable all items during animation
            panel.SetPanelItemEnabled(false);
        }
    }
    
    /// <summary>
    /// Handles display animation end: re-enable items that should be enabled (quantity > 0)
    /// </summary>
    private void OnDisplayAnimationEnded(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Display animation ended, re-enabling resource items with qty > 0");
        if (_resourcePanelItems == null)
            return;

        foreach (var panel in _resourcePanelItems)
        {
            // Find the corresponding resource from panel's binding context
            if (panel.BindingContext is ResourceData resource)
            {
                // Determine whether this item is selected
                bool isSelected = _selectedResource != null && _selectedResource.Id == resource.Id;
                // Update selection state
                panel.SetPanelSelected(isSelected, animate: false);
                panel.SetPanelItemEnabled(resource.Quantity > 0);
            }
        }
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

