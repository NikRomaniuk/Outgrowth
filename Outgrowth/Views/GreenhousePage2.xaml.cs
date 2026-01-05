using Outgrowth.ViewModels;
using Outgrowth.Models;
using Outgrowth.Services;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Controls.Shapes;
using PlantsSaveData = Outgrowth.Services.PlantsSaveService.PlantSaveData;
#if WINDOWS
using Outgrowth.Platforms.Windows;
#endif

namespace Outgrowth.Views;

public partial class GreenhousePage2 : ContentPage
{
    // Static reference to current instance for saving from App lifecycle events
    private static GreenhousePage2? _currentInstance;
    
    // Current centered pot index (0=Pot1 rightmost, increases left)
    // Navigation limited to indices 1-3 (Pot2, Pot3, Pot4), starts at Pot2
    private int _currentItemIndex = 1;
    
    // Selected tool/item for planting/watering
    private LiquidData? _selectedLiquid;
    private SeedData? _selectedSeed;
    private bool _isHarvesterSelected = false;
    
    // Animation lock to prevent opening other panels during animation
    private bool _isAnimating = false;
    
    // Debounce timer for OnPageSizeChanged
    private System.Threading.Timer? _sizeChangedDebounceTimer;
    
    // Cache for panel items to avoid full recreation when only selection changes
    private List<StyledPanel>? _liquidPanelItems;
    private List<StyledPanel>? _seedPanelItems;
    // 9-Slice panel containers for liquids/seeds and their inner scroll views
    private Microsoft.Maui.Controls.Grid? _liquidsNineSlicePanel;
    private Microsoft.Maui.Controls.ScrollView? _liquidsPanelContentScroll;
    private Microsoft.Maui.Controls.Grid? _seedsNineSlicePanel;
    private Microsoft.Maui.Controls.ScrollView? _seedsPanelContentScroll;
    // Dynamically created SelectedLiquidPanel and SelectedSeedPanel (Android only)
    private VisualElement? _selectedLiquidPanel;
    private VisualElement? _selectedSeedPanel;

    // Dynamically created Bottom Panels (replacement for removed XAML BottomPanelsContainer)
    private Microsoft.Maui.Controls.Grid? _bottomNineSlicePanel;
    // Grid that holds the 9-slice panel and any overlayed controls (buttons)
    private Microsoft.Maui.Controls.Grid? _bottomPanelContentGrid;
    private Border? _bottomPanelWrapper;
    // Container that holds bottom panel and the move panel side-by-side
    private Microsoft.Maui.Controls.Grid? _bottomRowContainer;
    // Bottom panel button references (dynamically created)
    private StyledPanel? _harvesterButtonBorder;
    private StyledPanel? _liquidsButtonBorder;
    private StyledPanel? _seedsButtonBorder;

    // Dynamically created Move Panel (Android only, replacement for removed XAML MovePanel)
    private Microsoft.Maui.Controls.Grid? _moveNineSlicePanel;
    private Microsoft.Maui.Controls.Grid? _movePanelContentGrid;
    private Border? _movePanelWrapper;
    // Move panel button references (dynamically created)
    private Border? _leftArrowButtonBorder;
    private Border? _rightArrowButtonBorder;

    // Backward-compatible aliases for existing code that referenced the old names
    private VisualElement? SelectedLiquidPanel => _selectedLiquidPanel;
    private VisualElement? SelectedSeedPanel => _selectedSeedPanel;
    
#if WINDOWS
    private WindowsInput? _windowsInput;
#endif
    
    private readonly List<PotObject> _pots =
    [
        new PotObject(1, 9400, -150, "pot_object_s001.png"),   // Pot 1
        new PotObject(2, 9000, -150, "pot_object_s001.png"),   // Pot 2
        new PotObject(3, 8600, -150, "pot_object_s001.png"),   // Pot 3
        new PotObject(4, 8200, -150, "pot_object_s001.png"),   // Pot 4
        new PotObject(5, 7800, -150, "pot_object_s001.png")    // Pot 5
    ];
    
    private int[] BaseItemPositions => _pots.Select(p => p.X).ToArray();
    
    public GreenhousePage2()
    {
        InitializeComponent();
        BindingContext = new GreenhouseViewModel();
        
        // Register this instance for global access
        _currentInstance = this;
        
        foreach (var pot in _pots)
        {
            pot.Clicked += (sender, e) => OnPotClicked(pot);
            pot.InteractAction = () => HandlePotInteraction(pot.PotNumber);
        }
        
#if ANDROID
        if (_movePanelWrapper != null)
        {
            _movePanelWrapper.IsVisible = true;
        }
#endif
        
        this.SizeChanged += OnPageSizeChanged;
        this.Disappearing += OnPageDisappearing;
        this.Loaded += OnPageLoaded;
    }
    
    private void OnPageLoaded(object? sender, EventArgs e)
    {
        // Data libraries are pre-loaded by GameDataManager at app startup
        
        CreatePotElements();
        UpdatePotPositions();
        UpdateContentPosition(); // Center Pot2 on load
        
        // Initialize font sizes BEFORE updating panels (resources must be initialized)
        // This ensures ResourcePanelIconSize, ResourcePanelQtySize, etc. are available when CreateLiquidItem/CreateSeedItem are called
        var screenProps = ScreenProperties.Instance;
        if (this.Width > 0 && this.Height > 0)
        {
            screenProps.UpdateScreenProperties(this.Width, this.Height);
            screenProps.UpdateFontSizes(screenProps.AdaptiveScale);
        }
        else
        {
            // If page size is not yet available, use default scale (1.0 for Windows 1920px)
            screenProps.UpdateFontSizes(1.0);
        }
        
        // Create dynamic panels from libraries (after font sizes are initialized)
        UpdateLiquidsPanel();
        UpdateSeedsPanel();

        // Create dynamic BottomPanelsContainer replacement (empty for now)
        UpdateBottomPanel();
        
        // Create dynamic MovePanel (Android only)
#if ANDROID
        UpdateMovePanel();
#endif
        
        // Initialize panel scales for animation (set to 0 if not visible, 1 if visible)
        if (LiquidsPanel != null)
        {
            LiquidsPanel.AnchorX = 0.5;
            LiquidsPanel.AnchorY = 0.5;
            if (LiquidsPanel.Handler != null)
            {
                try { LiquidsPanel.Scale = LiquidsPanel.IsVisible ? 1 : 0; }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Failed setting LiquidsPanel.Scale: {ex.Message}"); }
            }
        }
        if (SeedsPanel != null)
        {
            SeedsPanel.AnchorX = 0.5;
            SeedsPanel.AnchorY = 0.5;
            if (SeedsPanel.Handler != null)
            {
                try { SeedsPanel.Scale = SeedsPanel.IsVisible ? 1 : 0; }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Failed setting SeedsPanel.Scale: {ex.Message}"); }
            }
        }
        
        // Load saved plants or create test plant
        LoadSavedPlants();
        
        // Update all plants growth on page load (to catch up on time passed while away)
        // This will process any cycles that passed while the game was closed
        System.Diagnostics.Debug.WriteLine("[GreenhousePage] Calling PlantsManager.UpdateAllPlants() to catch up on missed cycles");
        PlantsManager.Instance.UpdateAllPlants();
        System.Diagnostics.Debug.WriteLine("[GreenhousePage] PlantsManager.UpdateAllPlants() completed");
        
        // Save after updating (to save any stage changes from missed cycles)
        SavePlants();
        
#if WINDOWS
        _windowsInput = new WindowsInput(
            onLeftArrow: OnLeftArrowPressed,
            onRightArrow: OnRightArrowPressed,
            onEscape: () => _ = CloseAllPanels() // Fire and forget async method
        );
        _windowsInput.Attach();
#endif
    }

    // Overload: parameterless call resolves adaptive scale from ScreenProperties
    private void UpdateBottomPanel()
    {
        var scale = ScreenProperties.Instance?.AdaptiveScale ?? 1.0;
        UpdateBottomPanel(scale);
    }
    
    /// <summary>
    /// Loads saved plants from file and places them in their pots
    /// </summary>
    private void LoadSavedPlants()
    {
        var savedPlants = PlantsSaveService.LoadPlants();
        
        if (savedPlants.Count == 0)
        {
            // No saved plants found; do not auto-plant any test plants
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] No saved plants found; skipping auto-planting");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Loading {savedPlants.Count} saved plants");
        
        foreach (var (plantData, cyclesPassed) in savedPlants)
        {
            // Find the pot for this plant
            var potIndex = plantData.PotNumber > 0 && plantData.PotNumber <= _pots.Count 
                ? plantData.PotNumber - 1 
                : -1;
            
            if (potIndex < 0 || potIndex >= _pots.Count)
            {
                System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Invalid pot number {plantData.PotNumber}, skipping");
                continue;
            }
            
            // Create plant from saved data
            var plant = PlantObject.FromSaveData(
                plantData.PlantId,
                plantData.PotNumber,
                0, 0, // Coordinates are relative to pot center
                plantData.PlantedAtCycle,
                plantData.CyclesLived + cyclesPassed // Add cycles that passed while the game was closed
            );
            
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Loaded plant {plant.Id} into pot {plantData.PotNumber}, cycles passed: {cyclesPassed}, CyclesLived: {plant.CyclesLived}");
            
            // Place plant in pot
            _pots[potIndex].PlantSlot = plant;
            UpdatePotVisualElement(_pots[potIndex]);
            
            // Subscribe to stage changes for auto-saving
            plant.StageChanged += (sender, e) => SavePlants();
            
            // Subscribe to plant click events
            plant.Clicked += OnPlantClicked;
        }
        
        // Save after loading (to update LastSavedCycle)
        SavePlants();
    }
    
    /// <summary>
    /// Adds a plant to a pot and saves the state
    /// </summary>
    public void AddPlantToPot(int potIndex, PlantObject plant)
    {
        if (potIndex < 0 || potIndex >= _pots.Count)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Invalid pot index {potIndex}");
            return;
        }
        
        _pots[potIndex].PlantSlot = plant;
        UpdatePotVisualElement(_pots[potIndex]);
        
        // Subscribe to stage changes for auto-saving
        plant.StageChanged += (sender, e) => SavePlants();
        
        // Subscribe to plant click events
        plant.Clicked += OnPlantClicked;
        
        SavePlants();
    }
    
    /// <summary>
    /// Saves all plants to file
    /// </summary>
    private void SavePlants()
    {
        // Build mapping of pot number to plant
        var potNumberToPlant = new Dictionary<int, PlantObject>();
        for (int i = 0; i < _pots.Count; i++)
        {
            if (_pots[i].PlantSlot != null)
            {
                potNumberToPlant[_pots[i].PotNumber] = _pots[i].PlantSlot!;
            }
        }
        
        PlantsSaveService.SavePlantsWithPotMapping(potNumberToPlant);
    }
    
    /// <summary>
    /// Static method to save plants state. Called from App.OnSleep()
    /// Saves plants if GreenhousePage instance is currently loaded
    /// </summary>
    public static void SavePlantsIfLoaded()
    {
        if (_currentInstance != null)
        {
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] Saving plants from App.OnSleep()");
            _currentInstance.SavePlants();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] No active instance to save plants");
        }
    }
    
    private void OnPageDisappearing(object? sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        // Unsubscribe from timer events
        // Timer.Tick -= OnTimerTick;
        
        // Save plants before leaving the page
        SavePlants();
        
        // Reset animation flag to allow closing (page is disappearing)
        _isAnimating = false;
        
        // Clear all selections
        _isHarvesterSelected = false;
        _selectedLiquid = null;
        _selectedSeed = null;
        
        // Close panels immediately without animation
        if (LiquidsPanel != null)
        {
            LiquidsPanel.Scale = 0;
            LiquidsPanel.IsVisible = false;
            LiquidsPanel.InputTransparent = true;
        }
        if (SeedsPanel != null)
        {
            SeedsPanel.Scale = 0;
            SeedsPanel.IsVisible = false;
            SeedsPanel.InputTransparent = true;
        }
        
        // Hide dynamic selected panels on Android
#if ANDROID
        if (_selectedLiquidPanel != null)
        {
            _selectedLiquidPanel.IsVisible = false;
            _selectedLiquidPanel.Scale = 0;
        }
        if (_selectedSeedPanel != null)
        {
            _selectedSeedPanel.IsVisible = false;
            _selectedSeedPanel.Scale = 0;
        }
#endif
        
        // Update button highlighting to clear all highlights
                
        
        CleanupPotElements();
        
        // Unregister instance
        if (_currentInstance == this)
        {
            _currentInstance = null;
        }
        
        // Dispose debounce timer
        _sizeChangedDebounceTimer?.Dispose();
        _sizeChangedDebounceTimer = null;
        
#if WINDOWS
        _windowsInput?.Detach();
        _windowsInput = null;
#endif
#endif
    }
    
    private void CleanupPotElements()
    {
#if ANDROID || WINDOWS
        if (ContentContainer == null)
            return;
        
        foreach (var pot in _pots)
        {
            if (pot.VisualElement != null && ContentContainer.Children.Contains(pot.VisualElement))
            {
                // Clear gesture recognizers to prevent memory leaks
                if (pot.VisualElement is Grid grid)
                {
                    foreach (var child in grid.Children)
                    {
                        if (child is Border border && border.GestureRecognizers.Count > 0)
                        {
                            border.GestureRecognizers.Clear();
                        }
                    }
                }
                
                ContentContainer.Children.Remove(pot.VisualElement);
            }
        }
#endif
    }

#if WINDOWS
    private void OnLeftArrowPressed()
    {
        System.Diagnostics.Debug.WriteLine($"[Windows] Left Arrow/A pressed, current index: {_currentItemIndex}");
        
        // Move left (increase index, max = pots.Count - 2)
        if (_currentItemIndex < _pots.Count - 2)
        {
            _currentItemIndex++;
            System.Diagnostics.Debug.WriteLine($"[Windows] Moving left, new index: {_currentItemIndex}");
            UpdateContentPosition();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[Windows] Cannot move left, already at max index");
        }
    }
    
    private void OnRightArrowPressed()
    {
        System.Diagnostics.Debug.WriteLine($"[Windows] Right Arrow/D pressed, current index: {_currentItemIndex}");
        
        // Move right (decrease index, min = 1)
        if (_currentItemIndex > 1)
        {
            _currentItemIndex--;
            System.Diagnostics.Debug.WriteLine($"[Windows] Moving right, new index: {_currentItemIndex}");
            UpdateContentPosition();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[Windows] Cannot move right, already at min index");
        }
    }
#endif

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        // Debounce: cancel previous timer and start new one
        _sizeChangedDebounceTimer?.Dispose();
        _sizeChangedDebounceTimer = new System.Threading.Timer(_ =>
        {
            MainThread.BeginInvokeOnMainThread(() => PerformPageSizeUpdate());
        }, null, 50, Timeout.Infinite); // 50ms debounce delay
#endif
    }
    
    private void PerformPageSizeUpdate()
    {
#if ANDROID || WINDOWS
        if (EnvironmentWrapper != null && EnvironmentContainer != null && ContentContainer != null && HubButton != null 
            && LiquidsPanel != null && SeedsPanel != null && _bottomPanelWrapper != null 
            && LeftGutterPlaceholder != null && LiquidsPanelWrapper != null && SeedsPanelWrapper != null)
        {
            var pageHeight = this.Height;
            var pageWidth = this.Width;
            
            if (pageHeight > 0 && pageWidth > 0)
            {
                var screenProps = ScreenProperties.Instance;
                screenProps.UpdateScreenProperties(pageWidth, pageHeight);
                
                EnvironmentContainer.WidthRequest = ScreenProperties.ReferenceWidth;
                EnvironmentContainer.HeightRequest = ScreenProperties.ReferenceHeight;
                
                EnvironmentWrapper.AnchorX = 0.5;
                EnvironmentWrapper.AnchorY = 0.5;
                if (EnvironmentWrapper.Handler != null)
                {
                    try { EnvironmentWrapper.Scale = screenProps.Scale; }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Failed setting EnvironmentWrapper.Scale: {ex.Message}"); }
                }
                EnvironmentWrapper.WidthRequest = ScreenProperties.ReferenceWidth;
                EnvironmentWrapper.HeightRequest = ScreenProperties.ReferenceHeight;
                
                EnvironmentContainer.InputTransparent = false;
                EnvironmentContainer.BackgroundColor = Colors.Transparent;
                
                EnvironmentWrapper.TranslationX = screenProps.OffsetX;
                EnvironmentWrapper.TranslationY = screenProps.OffsetY;
                
                HubButton.AnchorX = 1;
                HubButton.AnchorY = 1;
                if (HubButton.Handler != null)
                {
                    try { HubButton.Scale = screenProps.Scale; }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Failed setting HubButton.Scale: {ex.Message}"); }
                }
                
                LeftGutterPlaceholder.AnchorX = 0;
                LeftGutterPlaceholder.AnchorY = 0.5;
                if (LeftGutterPlaceholder.Handler != null)
                {
                    try { LeftGutterPlaceholder.Scale = screenProps.Scale; }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Failed setting LeftGutterPlaceholder.Scale: {ex.Message}"); }
                }
                
                var adaptive = screenProps.AdaptiveScale;
                screenProps.UpdateFontSizes(adaptive);
                UpdatePanelSizes(adaptive);
                UpdateBottomPanel(adaptive);
#if ANDROID
                UpdateMovePanel(adaptive);
#endif
                
                // Update panels to refresh font sizes ONLY if panels are visible
                // This avoids expensive UI updates when panels are hidden
                if (LiquidsPanel.IsVisible)
                    UpdateLiquidsPanel();
                if (SeedsPanel.IsVisible)
                    UpdateSeedsPanel();
                
                UpdatePotPositions();
                UpdateContentPosition();
            }
        }
#endif
    }
    
    private void UpdatePanelSizes(double adaptiveScale)
    {
        // Use UserInterfaceCreator to calculate platform-agnostic sizes
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

        if (LiquidsPanel != null)
        {
            LiquidsPanel.WidthRequest = sizes.Width;
            LiquidsPanel.HeightRequest = sizes.Height;
            LiquidsPanel.Margin = new Thickness(sizes.Margin, 0, 0, 0);
        }

        if (SeedsPanel != null)
        {
            SeedsPanel.WidthRequest = sizes.Width;
            SeedsPanel.HeightRequest = sizes.Height;
            SeedsPanel.Margin = new Thickness(sizes.Margin, 0, 0, 0);
        }

        if (LiquidsPanelWrapper != null && LiquidsPanel != null)
        {
            AbsoluteLayout.SetLayoutBounds(LiquidsPanel, new Rect(0, sizes.PanelYPosition, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
            AbsoluteLayout.SetLayoutFlags(LiquidsPanel, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.YProportional);
        }

        if (SeedsPanelWrapper != null && SeedsPanel != null)
        {
            AbsoluteLayout.SetLayoutBounds(SeedsPanel, new Rect(0, sizes.PanelYPosition, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
            AbsoluteLayout.SetLayoutFlags(SeedsPanel, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.YProportional);
        }
    }
    
    // UpdateToolsPanelSize removed — bottom panel is now dynamic and sized via UpdateBottomPanel
    
    /// <summary>
    /// Updates the visual highlighting of tools panel buttons based on selection state
    /// </summary>
    // UpdateToolsPanelButtons removed — bottom panel buttons are managed by UpdateBottomPanelButtons
    
    // Overload: parameterless call resolves adaptive scale from ScreenProperties
    private void UpdateMovePanel()
    {
        var scale = ScreenProperties.Instance?.AdaptiveScale ?? 1.0;
        UpdateMovePanel(scale);
    }
    
    /// <summary>
    /// Creates an empty move panel container (9-slice panel + wrapper) dynamically (Android only).
    /// Positioned to the right of BottomPanel with two arrow buttons for navigation.
    /// </summary>
    private void UpdateMovePanel(double adaptiveScale)
    {
#if ANDROID
        try
        {
            double adaptive = adaptiveScale;

            const double baseWidth = 300.0;
            const double baseHeight = 150.0;
            const double baseMargin = 20.0;

            var sizes = UserInterfaceCreator.GetPanelSizes(adaptive, baseWidth, baseHeight, baseMargin, baseWidth, baseHeight, isAndroid: true);

            double panelWidth = baseWidth * adaptive;
            double panelHeight = baseHeight * adaptive;
            double cornerSize = 40 * adaptive;

            var movePanel = new StyledPanel(
                width: panelWidth,
                height: panelHeight,
                cornerSize: cornerSize,
                backgroundColor: Color.FromArgb("#1A1A1A"),
                borderColor: Color.FromArgb("#FFD700"),
                content: null,
                cornerImage: "ui__panel_corner.png",
                horizontalEdgeImage: "ui__panel_edge_horizontal.png",
                verticalEdgeImage: "ui__panel_edge_vertical.png",
                centerImage: "ui__panel_center.png"
            );

            _moveNineSlicePanel = movePanel.Panel;

            // Create a content grid: 5 columns (Star, Auto, Star, Auto, Star) for two buttons
            var contentGrid = new Grid
            {
                HeightRequest = panelHeight,
                WidthRequest = panelWidth,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.End,
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star }
                }
            };

            // Add the 9-slice panel spanning all columns as background
            contentGrid.Children.Add(_moveNineSlicePanel);
            Grid.SetColumn(_moveNineSlicePanel, 0);
            Grid.SetColumnSpan(_moveNineSlicePanel, 5);

            _movePanelContentGrid = contentGrid;

            // Create buttons inside the wrapper content grid
            UpdateMovePanelButtons(adaptive);

            var wrapper = new Border
            {
                Content = _movePanelContentGrid,
                Stroke = null,
                StrokeThickness = 0
            };

            wrapper.HorizontalOptions = LayoutOptions.End;
            wrapper.WidthRequest = panelWidth;
            wrapper.VerticalOptions = LayoutOptions.End;
            wrapper.Margin = new Thickness(0, 0, sizes.Margin, 20);
            wrapper.AnchorX = 1.0;
            wrapper.AnchorY = 1.0;
            wrapper.Scale = 1;
            wrapper.IsVisible = true;
            wrapper.InputTransparent = false;
            wrapper.ZIndex = 1500;

            _movePanelWrapper = wrapper;

            if (_bottomRowContainer != null)
            {
                // Place move panel into the right auto column (index 2) of the bottom-row container
                if (!_bottomRowContainer.Children.Contains(_movePanelWrapper))
                {
                    _bottomRowContainer.Children.Add(_movePanelWrapper);
                    Grid.SetColumn(_movePanelWrapper, 2);
                }
            }
            else if (this.Content is Grid rootGrid)
            {
                // Fallback: add to right gutter if bottom-row container not created
                Grid.SetColumn(_movePanelWrapper, 2);
                rootGrid.Children.Add(_movePanelWrapper);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Failed to create move panel: {ex.Message}");
        }
#endif
    }

    /// <summary>
    /// Creates and populates the move panel with two arrow buttons (Left, Right).
    /// Buttons are created using CreateNineSlicePanel and arranged in a Grid with equal spacing.
    /// </summary>
    private void UpdateMovePanelButtons(double adaptiveScale)
    {
#if ANDROID
        if (_movePanelContentGrid == null)
            return;

        try
        {
            const double baseHeight = 150.0;
            double panelHeight = baseHeight * adaptiveScale;
            double buttonSize = panelHeight * 0.7; // 70% of panel height
            double cornerSize = 40 * adaptiveScale;

            // Helper to create a button with 9-slice panel and label icon
            Border CreateArrowButton(string labelText, EventHandler tapHandler)
            {
                var icon = new Label
                {
                    Text = labelText,
                    FontSize = buttonSize * 0.5,
                    TextColor = Colors.White,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };

                var buttonPanel = new StyledPanel(
                    width: buttonSize,
                    height: buttonSize,
                    cornerSize: cornerSize,
                    backgroundColor: Color.FromArgb("#1F1F0F"),
                    borderColor: Color.FromArgb("#4CAF50"),
                    content: icon,
                    cornerImage: "ui__panel_item_corner.png",
                    horizontalEdgeImage: "ui__panel_item_edge_horizontal.png",
                    verticalEdgeImage: "ui__panel_item_edge_vertical.png",
                    centerImage: "ui__panel_item_center.png"
                );

                var border = new Border
                {
                    Content = buttonPanel.Panel,
                    Stroke = null,
                    StrokeThickness = 0,
                    WidthRequest = buttonSize,
                    HeightRequest = buttonSize,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };

                var tap = new TapGestureRecognizer();
                tap.Tapped += (s, e) => tapHandler?.Invoke(s, EventArgs.Empty);
                border.GestureRecognizers.Add(tap);

                return border;
            }

            // Create buttons and place them into content grid auto columns (1,3)
            _leftArrowButtonBorder = CreateArrowButton("◄", OnLeftArrowButtonClicked);
            _rightArrowButtonBorder = CreateArrowButton("►", OnRightArrowButtonClicked);

            _movePanelContentGrid.Children.Add(_leftArrowButtonBorder);
            Grid.SetColumn(_leftArrowButtonBorder, 1);

            _movePanelContentGrid.Children.Add(_rightArrowButtonBorder);
            Grid.SetColumn(_rightArrowButtonBorder, 3);

            System.Diagnostics.Debug.WriteLine("[GreenhousePage] Created move panel buttons dynamically");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Failed to create move panel buttons: {ex.Message}");
        }
#endif
    }
    
    // Parameterless overload for call sites that don't have adaptiveScale available
    private void UpdateMovePanelButtons()
    {
        var scale = ScreenProperties.Instance?.AdaptiveScale ?? 1.0;
        UpdateMovePanelButtons(scale);
    }
    
    /// <summary>
    /// Updates the LiquidsPanel with all available liquids from LiquidLibrary.
    /// Optimized: Only updates selection highlighting if items already exist, otherwise recreates panel.
    /// </summary>
    private void UpdateLiquidsPanel()
    {
#if ANDROID || WINDOWS
        // Optimization: Skip update if panel is not visible (saves CPU cycles)
        if (LiquidsPanel != null && !LiquidsPanel.IsVisible)
        {
            // Panel is hidden, no need to update (will be updated when opened)
            return;
        }
        
        if (LiquidsList == null)
        {
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] UpdateLiquidsPanel: LiquidsList is null");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] UpdateLiquidsPanel: Current selection: {_selectedLiquid?.Name ?? "null"}");
        
        try
        {
            var liquids = LiquidLibrary.GetAllLiquids().ToList();
            
            // Check if we can update selection only (if cache exists and item count matches)
            if (_liquidPanelItems != null && _liquidPanelItems.Count == liquids.Count)
            {
                // Update selection highlighting only (optimized path)
                bool allFound = true;
                foreach (var liquid in liquids)
                {
                    var panel = _liquidPanelItems.FirstOrDefault(p => p.ClassId == liquid.Id);
                    if (panel != null)
                    {
                        bool isSelected = _selectedLiquid?.Id == liquid.Id;
                        panel.SetPanelSelected(isSelected, animate: true);
                    }
                    else
                    {
                        // Item not in cache, need full recreation
                        _liquidPanelItems = null;
                        allFound = false;
                        break;
                    }
                }

                // If cache update was successful, we're done
                if (allFound && _liquidPanelItems != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Updated LiquidsPanel selection highlighting (optimized), selected: {_selectedLiquid?.Name ?? "null"}");
                    return;
                }
            }
            
            // Full recreation path: ensure 9-slice panel exists and populate its inner stack
            // Create 9-slice panel once and reuse it; inner ScrollView contains a VerticalStackLayout
            if (_liquidsNineSlicePanel == null)
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
                double cornerSize = 40 * adaptive;

                var liquidsStyledPanel = new StyledPanel(
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

                _liquidsNineSlicePanel = liquidsStyledPanel.Panel;
                _liquidsPanelContentScroll = liquidsStyledPanel.ScrollContainer;

                // Replace content of LiquidsPanel with a 9-slice panel
                if (LiquidsPanel != null)
                {
                    LiquidsPanel.Children.Clear();
                    LiquidsPanel.Children.Add(_liquidsNineSlicePanel);
                    _liquidsNineSlicePanel.AnchorX = 0.5;
                    _liquidsNineSlicePanel.AnchorY = 0.5;
                }
            }
            
            // Create SelectedLiquidPanel dynamically on Android (only once)
#if ANDROID
            try
            {
                if (_selectedLiquidPanel == null && LiquidsPanelWrapper != null)
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
                    
                    // Create label bound to ViewModel.SelectedLiquidName
                    var selectedLabel = new Label
                    {
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        TextColor = Colors.White
                    };
                    if (BindingContext != null)
                    {
                        selectedLabel.BindingContext = BindingContext;
                        selectedLabel.SetBinding(Label.TextProperty, new Binding("SelectedLiquidName"));
                    }
                    
                    try
                    {
                        selectedLabel.FontFamily = "SilkscreenBold";
                    }
                    catch { }
                    
                    var selectedStyledPanel = new StyledPanel(
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

                    // Wrap the StyledPanel.Panel in a Border so it has a visible stroke/background
                    var wrapperBorder = new Border
                    {
                        Content = selectedStyledPanel.Panel,
                        Stroke = null,
                        StrokeThickness = 0
                    };
                    
                    // Place in left gutter wrapper at proportional position x=0, y=0.15
                    AbsoluteLayout.SetLayoutBounds(wrapperBorder, new Rect(0, 0.15, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
                    AbsoluteLayout.SetLayoutFlags(wrapperBorder, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.XProportional | Microsoft.Maui.Layouts.AbsoluteLayoutFlags.YProportional);
                    
                    // Initial animation/visibility state
                    wrapperBorder.AnchorX = 0.5;
                    wrapperBorder.AnchorY = 0.5;
                    wrapperBorder.Scale = 0;
                    wrapperBorder.IsVisible = false;
                    wrapperBorder.InputTransparent = true;
                    
                    _selectedLiquidPanel = wrapperBorder;
                    // ensure it's on top of LiquidsPanel
                    _selectedLiquidPanel.ZIndex = 1001;
                    LiquidsPanelWrapper.Children.Add(_selectedLiquidPanel);
                    System.Diagnostics.Debug.WriteLine("[GreenhousePage] Created dynamic SelectedLiquidPanel (Android)");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Error creating SelectedLiquidPanel: {ex.Message}");
            }
#endif

            // Populate the inner stack inside the scroll view (fall back to LiquidsList if needed)
            var contentStack = _liquidsPanelContentScroll?.Content as VerticalStackLayout ?? LiquidsList;
            if (contentStack == null)
            {
                System.Diagnostics.Debug.WriteLine("[GreenhousePage] UpdateLiquidsPanel: no content stack available");
                return;
            }

            // Initialize cache list if needed
            if (_liquidPanelItems == null)
                _liquidPanelItems = new List<StyledPanel>();

            // Check if cache matches current library (recreate if mismatch)
            var currentLiquidIds = liquids.Select(l => l.Id).ToHashSet();
            var cachedIds = _liquidPanelItems.Select(p => p.ClassId).ToHashSet();
            bool needsRecreation = !currentLiquidIds.SetEquals(cachedIds) || contentStack.Children.Count == 0;

            if (needsRecreation)
            {
                // Recreate all items
                contentStack.Children.Clear();
                _liquidPanelItems.Clear();
                foreach (var liquid in liquids)
                {
                    var liquidItem = CreateLiquidItem(liquid);
                    // store id on panel for lookup
                    liquidItem.ClassId = liquid.Id;
                    _liquidPanelItems.Add(liquidItem);
                    contentStack.Children.Add(liquidItem);
                }
                System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Recreated all LiquidsPanel items, selected: {_selectedLiquid?.Name ?? "null"}");
            }
            else
            {
                // All items exist already — update selection highlighting only
                foreach (var liquid in liquids)
                {
                    var panel = _liquidPanelItems.FirstOrDefault(p => p.ClassId == liquid.Id);
                    if (panel != null)
                    {
                        bool isSelected = _selectedLiquid?.Id == liquid.Id;
                        panel.SetPanelSelected(isSelected, animate: true);
                    }
                }
                System.Diagnostics.Debug.WriteLine($"[GreenhousePage] LiquidsPanel items already exist, updated selection highlighting: {_selectedLiquid?.Name ?? "null"}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Error updating LiquidsPanel: {ex.Message}");
            _liquidPanelItems = null; // Clear cache on error
        }
#endif
    }
    
    /// <summary>
    /// Updates the SeedsPanel with all available seeds from SeedLibrary.
    /// Optimized: Only updates selection highlighting if items already exist, otherwise recreates panel.
    /// </summary>
    private void UpdateSeedsPanel()
    {
#if ANDROID || WINDOWS
        // Optimization: Skip update if panel is not visible (saves CPU cycles)
        if (SeedsPanel != null && !SeedsPanel.IsVisible)
        {
            // Panel is hidden, no need to update (will be updated when opened)
            return;
        }
        
        if (SeedsList == null)
        {
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] UpdateSeedsPanel: SeedsList is null");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] UpdateSeedsPanel: Current selection: {_selectedSeed?.Name ?? "null"}");
        
        try
        {
            var seeds = SeedLibrary.GetAllSeeds().ToList();
            
                                // Update button highlighting (GreenhousePage specific)
                                UpdateBottomPanelButtons();
            if (_seedPanelItems != null && _seedPanelItems.Count == seeds.Count)
            {
                // Update selection highlighting only (optimized path)
                bool allFound = true;
                foreach (var seed in seeds)
                {
                    var panel = _seedPanelItems.FirstOrDefault(p => p.ClassId == seed.Id);
                    if (panel != null)
                    {
                        bool isSelected = _selectedSeed?.Id == seed.Id;
                        panel.SetPanelSelected(isSelected, animate: true);
                    }
                    else
                    {
                        // Item not in cache, need full recreation
                        _seedPanelItems = null;
                        allFound = false;
                        break;
                    }
                }

                // If cache update was successful, we're done
                if (allFound && _seedPanelItems != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Updated SeedsPanel selection highlighting (optimized), selected: {_selectedSeed?.Name ?? "null"}");
                    return;
                }
            }
            
            // Full recreation path: ensure 9-slice panel exists and populate its inner stack
            if (_seedsNineSlicePanel == null)
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
                double cornerSize = 40 * adaptive;

                var seedsStyledPanel = new StyledPanel(
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

                _seedsNineSlicePanel = seedsStyledPanel.Panel;
                _seedsPanelContentScroll = seedsStyledPanel.ScrollContainer;

                if (SeedsPanel != null)
                {
                    SeedsPanel.Children.Clear();
                    SeedsPanel.Children.Add(_seedsNineSlicePanel);
                    _seedsNineSlicePanel.AnchorX = 0.5;
                    _seedsNineSlicePanel.AnchorY = 0.5;
                }
            }
            
            // Create SelectedSeedPanel dynamically on Android (only once)
#if ANDROID
            try
            {
                if (_selectedSeedPanel == null && SeedsPanelWrapper != null)
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
                    
                    // Create label bound to ViewModel.SelectedSeedName
                    var selectedLabel = new Label
                    {
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        TextColor = Colors.White
                    };
                    if (BindingContext != null)
                    {
                        selectedLabel.BindingContext = BindingContext;
                        selectedLabel.SetBinding(Label.TextProperty, new Binding("SelectedSeedName"));
                    }
                    
                    try
                    {
                        selectedLabel.FontFamily = "SilkscreenBold";
                    }
                    catch { }
                    
                    var selectedPanel = new StyledPanel(
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
                        Content = selectedPanel.Panel,
                        Stroke = null,
                        StrokeThickness = 0
                    };
                    
                    // Place in left gutter wrapper at proportional position x=0, y=0.15
                    // Match SeedsPanel left margin so panel aligns with list
                    wrapperBorder.Margin = new Thickness(sizes.Margin, 0, 0, 0);
                    AbsoluteLayout.SetLayoutBounds(wrapperBorder, new Rect(0, 0.15, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
                    AbsoluteLayout.SetLayoutFlags(wrapperBorder, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.XProportional | Microsoft.Maui.Layouts.AbsoluteLayoutFlags.YProportional);
                    
                    // Initial animation/visibility state
                    wrapperBorder.AnchorX = 0.5;
                    wrapperBorder.AnchorY = 0.5;
                    wrapperBorder.Scale = 0;
                    wrapperBorder.IsVisible = false;
                    wrapperBorder.InputTransparent = true;
                    
                    _selectedSeedPanel = wrapperBorder;
                    // ensure it's on top of SeedsPanel
                    _selectedSeedPanel.ZIndex = 1001;
                    SeedsPanelWrapper.Children.Add(_selectedSeedPanel);
                    System.Diagnostics.Debug.WriteLine("[GreenhousePage] Created dynamic SelectedSeedPanel (Android)");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Error creating SelectedSeedPanel: {ex.Message}");
            }
#endif

            var contentStack = _seedsPanelContentScroll?.Content as VerticalStackLayout ?? SeedsList;
            if (contentStack == null)
            {
                System.Diagnostics.Debug.WriteLine("[GreenhousePage] UpdateSeedsPanel: no content stack available");
                return;
            }

            contentStack = _seedsPanelContentScroll?.Content as VerticalStackLayout ?? SeedsList;
            if (contentStack == null)
            {
                System.Diagnostics.Debug.WriteLine("[GreenhousePage] UpdateSeedsPanel: no content stack available");
                return;
            }

            if (_seedPanelItems == null)
                _seedPanelItems = new List<StyledPanel>();

            // Check if cache matches current library (recreate if mismatch)
            var currentSeedIds = seeds.Select(s => s.Id).ToHashSet();
            var cachedSeedIds = _seedPanelItems.Select(p => p.ClassId).ToHashSet();
            bool needsRecreation = !currentSeedIds.SetEquals(cachedSeedIds) || contentStack.Children.Count == 0;

            if (needsRecreation)
            {
                // Recreate all items
                contentStack.Children.Clear();
                _seedPanelItems.Clear();
                foreach (var seed in seeds)
                {
                    var seedItem = CreateSeedItem(seed);
                    seedItem.ClassId = seed.Id;
                    _seedPanelItems.Add(seedItem);
                    contentStack.Children.Add(seedItem);
                }
                System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Recreated all SeedsPanel items, selected: {_selectedSeed?.Name ?? "null"}");
            }
            else
            {
                // All seeds present: update selection highlights only
                foreach (var seed in seeds)
                {
                    var panel = _seedPanelItems.FirstOrDefault(p => p.ClassId == seed.Id);
                    if (panel != null)
                    {
                        bool isSelected = _selectedSeed?.Id == seed.Id;
                        panel.SetPanelSelected(isSelected, animate: true);
                    }
                }
                System.Diagnostics.Debug.WriteLine($"[GreenhousePage] SeedsPanel items already exist, updated selection highlighting: {_selectedSeed?.Name ?? "null"}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Error updating SeedsPanel: {ex.Message}");
            _seedPanelItems = null; // Clear cache on error
        }
#endif
    }

    /// <summary>
    /// Creates an empty bottom panels container (9-slice panel + wrapper) dynamically.
    /// Mirrors the pattern used by UpdateLiquidsPanel/UpdateSeedsPanel but creates
    /// a placeholder BottomPanelsContainer for future dynamic content.
    /// </summary>
    private void UpdateBottomPanel(double adaptiveScale)
    {
#if ANDROID || WINDOWS
        try
        {
            // Use the provided adaptiveScale to size the bottom panel
            double adaptive = adaptiveScale;

            const double baseWidth = 450.0;
            const double baseHeight = 150.0;
            const double baseMargin = 20.0;

            bool isAndroid = DeviceInfo.Platform == DevicePlatform.Android;
            var sizes = UserInterfaceCreator.GetPanelSizes(adaptive, baseWidth, baseHeight, baseMargin, baseWidth, baseHeight, isAndroid);

            double panelWidth = baseWidth * adaptive;
            double panelHeight = baseHeight * adaptive;
            double cornerSize = 40 * adaptive;

            var bottomPanel = new StyledPanel(
                width: panelWidth,
                height: panelHeight,
                cornerSize: cornerSize,
                backgroundColor: Color.FromArgb("#1A1A1A"),
                borderColor: Color.FromArgb("#FFD700"),
                content: null,
                cornerImage: "ui__panel_corner.png",
                horizontalEdgeImage: "ui__panel_edge_horizontal.png",
                verticalEdgeImage: "ui__panel_edge_vertical.png",
                centerImage: "ui__panel_center.png"
            );

            _bottomNineSlicePanel = bottomPanel.Panel;

            // Create a content grid inside the wrapper so we can overlay buttons on top
            // Create a 7-column grid: Star, Auto, Star, Auto, Star, Auto, Star
            // Buttons will be placed in Auto columns (1,3,5) so outer and inner star columns equalize spacing
            var contentGrid = new Grid
            {
                HeightRequest = panelHeight,
                // Constrain the grid to the panel width and center it so it doesn't fill the whole screen
                WidthRequest = panelWidth,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.End,
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star }
                }
            };

            // Add the 9-slice panel spanning all columns so it forms the background
            contentGrid.Children.Add(_bottomNineSlicePanel);
            Grid.SetColumn(_bottomNineSlicePanel, 0);
            Grid.SetColumnSpan(_bottomNineSlicePanel, 7);

            _bottomPanelContentGrid = contentGrid;

            // Create buttons inside the wrapper content grid (overlay)
            UpdateBottomPanelButtons(adaptive);

            var wrapper = new Border
            {
                Content = _bottomPanelContentGrid,
                Stroke = null,
                StrokeThickness = 0
            };

            // Center the wrapper and constrain its width to the panel so the inner grid doesn't stretch full-width
            wrapper.HorizontalOptions = LayoutOptions.Center;
            wrapper.WidthRequest = panelWidth;
            wrapper.VerticalOptions = LayoutOptions.End;
            // Use zero horizontal margin so the centered column truly centers the panel
            wrapper.Margin = new Thickness(0, 0, 0, 20);
            wrapper.AnchorX = 0.5;
            wrapper.AnchorY = 1.0;
            // Show bottom panel by default so it's visible after creation
            wrapper.Scale = 1;
            wrapper.IsVisible = true;
            wrapper.InputTransparent = false;
            wrapper.ZIndex = 1500;

            _bottomPanelWrapper = wrapper;

            // Create or reuse a bottom-row container that spans the full page width.
            // This container has columns: Star, Auto (bottom panel), Auto (move panel), Star
            if (this.Content is Grid rootGrid)
            {
                if (_bottomRowContainer == null)
                {
                    _bottomRowContainer = new Grid
                    {
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.End,
                        ColumnDefinitions = new ColumnDefinitionCollection
                        {
                            new ColumnDefinition { Width = GridLength.Star },
                            new ColumnDefinition { Width = GridLength.Auto },
                            new ColumnDefinition { Width = GridLength.Auto },
                            new ColumnDefinition { Width = GridLength.Star }
                        }
                    };

                    // Give container a height matching the panel to reserve layout space
                    _bottomRowContainer.HeightRequest = panelHeight + 40;

                    // Ensure the container and its children receive input and sit above main content
                    _bottomRowContainer.InputTransparent = false;
                    _bottomRowContainer.ZIndex = 1500;

                    // Add the container to the root grid spanning all columns
                    Grid.SetColumnSpan(_bottomRowContainer, 3);
                    rootGrid.Children.Add(_bottomRowContainer);
                }

                // Ensure container height matches panel height for this update
                _bottomRowContainer.HeightRequest = panelHeight + 40;

                // Place the bottom panel wrapper in the center auto column (index 1)
                if (!_bottomRowContainer.Children.Contains(_bottomPanelWrapper))
                {
                    _bottomRowContainer.Children.Add(_bottomPanelWrapper);
                    Grid.SetColumn(_bottomPanelWrapper, 1);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Failed to create bottom panels placeholder: {ex.Message}");
        }
#endif
    }

    /// <summary>
    /// Creates and populates the bottom panel with three buttons (Harvester, Liquids, Seeds).
    /// Buttons are created using CreateNineSlicePanel and arranged in a Grid with equal spacing.
    /// Button size: height = 70% of panel height, width = height (square buttons).
    /// </summary>
    private void UpdateBottomPanelButtons(double adaptiveScale)
    {
#if ANDROID || WINDOWS
        if (_bottomPanelContentGrid == null)
            return;

        try
        {
            const double baseHeight = 150.0;
            double panelHeight = baseHeight * adaptiveScale;
            double buttonSize = panelHeight * 0.7; // 70% of panel height
            double cornerSize = 40 * adaptiveScale;

            StyledPanel CreateButton(string iconSource, EventHandler tapHandler)
            {
                var icon = new Image
                {
                    Source = iconSource,
                    Aspect = Aspect.AspectFit,
                    // Fill most of the button (80%) with a small inset
                    WidthRequest = buttonSize * 0.8,
                    HeightRequest = buttonSize * 0.8,
                    Margin = new Thickness(buttonSize * 0.1),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };

                // Use StyledPanel selection constructor
                var outerBorder = new StyledPanel(
                    type: "selection",
                    width: buttonSize,
                    height: buttonSize,
                    isSelected: false,
                    cornerSize: cornerSize,
                    cornerImage: "ui__panel_item_corner.png",
                    horizontalEdgeImage: "ui__panel_item_edge_horizontal.png",
                    verticalEdgeImage: "ui__panel_item_edge_vertical.png",
                    centerImage: "ui__panel_item_center.png"
                );

                // Place the icon into the content grid
                if (outerBorder.ContentGrid != null)
                {
                    outerBorder.ContentGrid.Children.Add(icon);
                    // Center the icon inside the grid cell
                    Grid.SetColumn(icon, 0);
                    Grid.SetRow(icon, 0);
                }

                // Ensure sizing and alignment on the outer border
                outerBorder.WidthRequest = buttonSize;
                outerBorder.HeightRequest = buttonSize;
                outerBorder.HorizontalOptions = LayoutOptions.Center;
                outerBorder.VerticalOptions = LayoutOptions.Center;

                return outerBorder;
            }

            // Ensure buttons exist and are attached to the current content grid
            if (_harvesterButtonBorder == null)
            {
                _harvesterButtonBorder = CreateButton("harvester_icon.png", OnHarvesterButtonClicked);
                var tapSel = new TapGestureRecognizer();
                tapSel.Tapped += (s, e) =>
                {
                    if (_harvesterButtonBorder is StyledPanel hPanel)
                        hPanel.SetPanelSelected(true, animate: true);
                    if (_liquidsButtonBorder is StyledPanel lPanel)
                        lPanel.SetPanelSelected(false, animate: true);
                    if (_seedsButtonBorder is StyledPanel sPanel)
                        sPanel.SetPanelSelected(false, animate: true);
                    OnHarvesterButtonClicked(s, e);
                };
                _harvesterButtonBorder.GestureRecognizers.Add(tapSel);
            }
            // Add or re-add to current grid
            if (_harvesterButtonBorder != null && !_bottomPanelContentGrid.Children.Contains(_harvesterButtonBorder))
            {
                _bottomPanelContentGrid.Children.Add(_harvesterButtonBorder);
                Grid.SetColumn(_harvesterButtonBorder, 1);
            }

            if (_liquidsButtonBorder == null)
            {
                _liquidsButtonBorder = CreateButton("liquid__water.png", OnLiquidsButtonClicked);
                var tapSel = new TapGestureRecognizer();
                tapSel.Tapped += (s, e) =>
                {
                    if (_harvesterButtonBorder is StyledPanel hPanel)
                        hPanel.SetPanelSelected(false, animate: true);
                    if (_liquidsButtonBorder is StyledPanel lPanel)
                        lPanel.SetPanelSelected(true, animate: true);
                    if (_seedsButtonBorder is StyledPanel sPanel)
                        sPanel.SetPanelSelected(false, animate: true);
                    OnLiquidsButtonClicked(s, e);
                };
                _liquidsButtonBorder.GestureRecognizers.Add(tapSel);
            }
            if (_liquidsButtonBorder != null && !_bottomPanelContentGrid.Children.Contains(_liquidsButtonBorder))
            {
                _bottomPanelContentGrid.Children.Add(_liquidsButtonBorder);
                Grid.SetColumn(_liquidsButtonBorder, 3);
            }

            if (_seedsButtonBorder == null)
            {
                _seedsButtonBorder = CreateButton("seeds__lumivial.png", OnSeedsButtonClicked);
                var tapSel = new TapGestureRecognizer();
                tapSel.Tapped += (s, e) =>
                {
                    if (_harvesterButtonBorder is StyledPanel hPanel)
                        hPanel.SetPanelSelected(false, animate: true);
                    if (_liquidsButtonBorder is StyledPanel lPanel)
                        lPanel.SetPanelSelected(false, animate: true);
                    if (_seedsButtonBorder is StyledPanel sPanel)
                        sPanel.SetPanelSelected(true, animate: true);
                    OnSeedsButtonClicked(s, e);
                };
                _seedsButtonBorder.GestureRecognizers.Add(tapSel);
            }
            if (_seedsButtonBorder != null && !_bottomPanelContentGrid.Children.Contains(_seedsButtonBorder))
            {
                _bottomPanelContentGrid.Children.Add(_seedsButtonBorder);
                Grid.SetColumn(_seedsButtonBorder, 5);
            }

            System.Diagnostics.Debug.WriteLine("[GreenhousePage] Created bottom panel buttons dynamically");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Failed to create bottom panel buttons: {ex.Message}");
        }
#endif
    }
    
    // Parameterless overload for call sites that don't have adaptiveScale available
    private void UpdateBottomPanelButtons()
    {
        var scale = ScreenProperties.Instance?.AdaptiveScale ?? 1.0;
        UpdateBottomPanelButtons(scale);
    }
    
    /// <summary>
    /// Creates a UI element for a liquid item
    /// </summary>
    private StyledPanel CreateLiquidItem(LiquidData liquid)
    {
        var isSelected = _selectedLiquid?.Id == liquid.Id;
#if ANDROID
        bool isAndroid = true;
#else
        bool isAndroid = false;
#endif

        // Prefer application-level resources updated by ScreenProperties; fall back to page resources
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

        var panel = new StyledPanel(
            type: "panelItem",
            id: liquid.Id,
            name: liquid.Name,
            sprite: liquid.Sprite,
            isSelected: isSelected,
            panelItemHeight: panelItemHeight,
            qtyFontSize: qtySize,
            bodyFontSize: bodySize,
            isAndroid: isAndroid,
            onTapped: () => OnLiquidSelected(liquid),
            isEnabled: liquid.Quantity > 0,
            bindingContext: liquid);
        panel.ClassId = liquid.Id;
        return panel;
    }
    
    /// <summary>
    /// Creates a UI element for a seed item
    /// </summary>
    private StyledPanel CreateSeedItem(SeedData seed)
    {
        // Delegate creation to the centralized UserInterfaceCreator to keep layout consistent
        bool isSelected = _selectedSeed?.Id == seed.Id;
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] CreateSeedItem: {seed.Name}, isSelected={isSelected}, _selectedSeed={_selectedSeed?.Name ?? "null"}");

#if ANDROID
        bool isAndroid = true;
#else
        bool isAndroid = false;
#endif

        // Prefer application-level resources updated by ScreenProperties; fall back to page resources
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

        var panel = new StyledPanel(
            type: "panelItem",
            id: seed.Id,
            name: seed.Name,
            sprite: seed.Sprite,
            isSelected: isSelected,
            panelItemHeight: panelItemHeight,
            qtyFontSize: qtySize,
            bodyFontSize: bodySize,
            isAndroid: isAndroid,
            onTapped: () => OnSeedSelected(seed),
            isEnabled: seed.Quantity > 0,
            bindingContext: seed);
        panel.ClassId = seed.Id;
        return panel;
    }

    private async void OnHubClicked(object sender, EventArgs e)
    {
        await NavigationService.NavigateWithFadeAsync("//HubPage");
    }

    private void HandlePotInteraction(int potNumber)
    {
        // TODO: Add pot interaction logic
        System.Diagnostics.Debug.WriteLine($"Pot {potNumber} interacted via IInteractable.OnInteract()");
    }

    private void OnPotClicked(PotObject pot)
    {
        System.Diagnostics.Debug.WriteLine($"Pot {pot.PotNumber} (ID: {pot.Id}) clicked");
        
        // If a seed is selected and pot is empty, plant it
        if (_selectedSeed != null && pot.PlantSlot == null)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Planting seed {_selectedSeed.Name} (ID: {_selectedSeed.Id}) in pot {pot.PotNumber}");
            
            // Create plant from seed
            string plantId = $"plant_{_selectedSeed.PlantId}_{pot.PotNumber}";
            var plant = new PlantObject(plantId, _selectedSeed.PlantId, 0, 0);
            
            // Place plant in pot
            pot.PlantSlot = plant;
            UpdatePotVisualElement(pot);
            
            // Subscribe to plant events
            plant.StageChanged += (sender, e) => SavePlants();
            plant.Clicked += OnPlantClicked;
            
            // Keep seed selection active (don't clear it) so user can plant multiple seeds
            
            // Save state after planting
            SavePlants();
            
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Plant {plant.Id} planted in pot {pot.PotNumber}");
        }
        else if (_selectedSeed != null && pot.PlantSlot != null)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Cannot plant seed: pot {pot.PotNumber} is already occupied");
        }
        // TODO: Add other pot-specific click logic (watering, etc.)
    }
    
    /// <summary>
    /// Handles click on a plant
    /// </summary>
    private void OnPlantClicked(object? sender, TappedEventArgs e)
    {
        if (sender is not PlantObject plant)
        {
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] OnPlantClicked: sender is not PlantObject");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Plant {plant.Id} (PlantId: {plant.PlantId}) clicked");
        
        // If harvester is selected, harvest the plant
        if (_isHarvesterSelected)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Harvesting plant {plant.Id}");
            
            // Find the pot containing this plant (try several fallbacks to handle deserialization/instance mismatches)
            var pot = _pots.FirstOrDefault(p => p.PlantSlot == plant);
            if (pot == null)
            {
                // Fallback: match by plant Id
                pot = _pots.FirstOrDefault(p => p.PlantSlot != null && p.PlantSlot.Id == plant.Id);
                if (pot != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Fixup: matched plant by Id for {plant.Id} and assigned pot {pot.PotNumber}");
                }
            }
            if (pot == null)
            {
                // Fallback: match by PlantId (type) if unique
                var candidates = _pots.Where(p => p.PlantSlot != null && p.PlantSlot.PlantId == plant.PlantId).ToList();
                if (candidates.Count == 1)
                {
                    pot = candidates.First();
                    System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Fixup: matched plant by PlantId for {plant.PlantId} to pot {pot.PotNumber}");
                }
                else if (candidates.Count > 1)
                {
                    System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Warning: multiple candidate pots found for PlantId {plant.PlantId}, cannot disambiguate");
                }
            }
            if (pot != null)
            {
                plant.Harvest();
                
                // Remove plant from pot
                pot.PlantSlot = null;
                UpdatePotVisualElement(pot);
                
                // Save state after harvesting
                SavePlants();
                
                System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Plant {plant.Id} harvested and removed from pot {pot.PotNumber}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Warning: Plant {plant.Id} not found in any pot");
            }
        }
        // TODO: Add other plant-specific click logic
    }

    private async void OnLiquidsButtonClicked(object? sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        try
        {
            if (_isAnimating)
                return;

            System.Diagnostics.Debug.WriteLine("[GreenhousePage] OnLiquidsButtonClicked called");
            // Don't clear selections - only toggle panel visibility

            if (LiquidsPanel != null && SeedsPanel != null)
            {
                // Toggle liquids panel (close if already open, open if closed)
                bool wasOpen = LiquidsPanel.IsVisible;

                // Close seeds panel with animation (if open)
                await CloseSeedsPanelWithAnimation();

                // Toggle liquids panel
                if (wasOpen)
                {
                    // Close liquids panel with animation
                    await CloseLiquidsPanelWithAnimation();
                }
                else
                {
                    // Open liquids panel with animation
                    await OpenLiquidsPanelWithAnimation();
                }

                System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Liquids panel toggled");
            }
            
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Exception in OnLiquidsButtonClicked: {ex.Message}\n{ex.StackTrace}");
        }
#endif
    }

    private async void OnSeedsButtonClicked(object? sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        if (_isAnimating)
            return;
        
        System.Diagnostics.Debug.WriteLine("[GreenhousePage] OnSeedsButtonClicked called");
        // Don't clear selections - only toggle panel visibility
        
        if (LiquidsPanel != null && SeedsPanel != null)
        {
            // Toggle seeds panel (close if already open, open if closed)
            bool wasOpen = SeedsPanel.IsVisible;
            
            // Close liquids panel with animation (if open)
            await CloseLiquidsPanelWithAnimation();
            
            // Toggle seeds panel
            if (wasOpen)
            {
                // Close seeds panel with animation
                await CloseSeedsPanelWithAnimation();
            }
            else
            {
                // Open seeds panel with animation
                await OpenSeedsPanelWithAnimation();
            }
            
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Seeds panel toggled");
        }
        
#endif
    }

    private async void OnHarvesterButtonClicked(object? sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        System.Diagnostics.Debug.WriteLine("[GreenhousePage] OnHarvesterButtonClicked called");
        
        // Toggle harvester selection
        _isHarvesterSelected = !_isHarvesterSelected;
        
        // Clear liquid and seed selections when harvester is selected
        if (_isHarvesterSelected)
        {
            _selectedLiquid = null;
            _selectedSeed = null;
            UpdateLiquidsPanel();
            UpdateSeedsPanel();
            
            // Close panels with animation
            await CloseAllPanels();
        }
        
        // Update button highlighting
        UpdateBottomPanelButtons();
        
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Harvester selected: {_isHarvesterSelected}");
#endif
    }

    private void OnLeftArrowButtonClicked(object? sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        // Move left (max = pots.Count - 2)
        if (_currentItemIndex < _pots.Count - 2)
        {
            _currentItemIndex++;
            UpdateContentPosition();
        }
#endif
    }

    private void OnRightArrowButtonClicked(object? sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        // Move right (min = 1, Pot2)
        if (_currentItemIndex > 1)
        {
            _currentItemIndex--;
            UpdateContentPosition();
        }
#endif
    }
    
    private void CreatePotElements()
    {
#if ANDROID || WINDOWS
        if (ContentContainer == null)
            return;
        
        foreach (var pot in _pots)
        {
            var visualElement = pot.CreateVisualElement();
            visualElement.ZIndex = pot.ZIndex;
            ContentContainer.Children.Add(visualElement);
        }
#endif
    }
    
    /// <summary>
    /// Updates the visual element for a specific pot. Use this after modifying PlantSlot.
    /// Optimized to update only the plant slot element instead of recreating the entire pot.
    /// </summary>
    private void UpdatePotVisualElement(PotObject pot)
    {
#if ANDROID || WINDOWS
        if (ContentContainer == null || pot.VisualElement == null)
            return;
        
        // Optimized: Update only the plant slot element instead of recreating entire pot
        if (pot.VisualElement is Grid mainGrid && mainGrid.Children.Count > 0)
        {
            // Find the plant slot border (second child, index 1) if plant exists, otherwise it doesn't exist
            Border? plantSlotBorder = null;
            
            // Check if plant slot exists (it's the second child if plant exists)
            if (mainGrid.Children.Count > 1 && mainGrid.Children[1] is Border border)
            {
                plantSlotBorder = border;
            }
            
            // Determine if we need to add or remove plant slot
            bool shouldHavePlant = pot.PlantSlot != null;
            bool hasPlantSlot = plantSlotBorder != null;
            
            if (shouldHavePlant && !hasPlantSlot)
            {
                // Add plant slot
                var slotBorder = new Border
                {
                    BackgroundColor = Colors.Transparent,
                    StrokeThickness = 0,
                    HeightRequest = pot.Height,
                    WidthRequest = pot.Width,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Start,
                    Margin = new Thickness(0, -pot.Height / 2, 0, 0)
                };
                
                // Create visual element for the plant
                pot.PlantSlot!.X = 0;
                pot.PlantSlot!.Y = 0;
                var plantView = pot.PlantSlot.CreateVisualElement();
                slotBorder.Content = plantView;
                
                mainGrid.Children.Add(slotBorder);
            }
            else if (!shouldHavePlant && hasPlantSlot)
            {
                // Remove plant slot
                mainGrid.Children.Remove(plantSlotBorder!);
            }
            else if (shouldHavePlant && hasPlantSlot && pot.PlantSlot != null)
            {
                // Plant exists and slot exists - no update needed
                // Plant sprite updates are handled by PlantObject.UpdatePlantSprite() automatically
                // when stage changes (via ChangeGrowthStage -> UpdatePlantSprite)
                // This branch should not be reached during normal operation
                return;
            }
            
            // No need to update positions - pot position hasn't changed, only plant slot content
            return;
        }
        
        // Fallback: If structure is unexpected, recreate entire pot (shouldn't happen in normal flow)
        if (ContentContainer.Children.Contains(pot.VisualElement))
        {
            ContentContainer.Children.Remove(pot.VisualElement);
        }
        
        var newVisualElement = pot.CreateVisualElement();
        newVisualElement.ZIndex = pot.ZIndex;
        ContentContainer.Children.Add(newVisualElement);
        UpdatePotPositions();
#endif
    }
    
    // Updates pot positions (container center: X=9600, Y=540, 1:1 ratio)
    private void UpdatePotPositions()
    {
#if ANDROID || WINDOWS
        if (ContentContainer == null)
            return;
        
        const double containerCenterX = 9600.0;
        const double containerCenterY = 540.0;
        
        foreach (var pot in _pots)
        {
            pot.UpdatePosition(containerCenterX, containerCenterY);
        }
#endif
    }
    
    // Centers selected pot: translationOffset = screenCenter - itemCenterX
    private void UpdateContentPosition()
    {
#if ANDROID || WINDOWS
        if (ContentContainer == null)
            return;
        
        int currentItemLogicalX = BaseItemPositions[_currentItemIndex];
        const double containerCenter = 9600.0;
        const double screenCenter = 960.0;
        
        double itemCenterX = containerCenter + currentItemLogicalX; // 1:1 ratio
        double translationOffset = screenCenter - itemCenterX;
        
        ContentContainer.TranslationX = translationOffset;
#endif
    }
    
    // ============================================================================
    // Item Selection Handlers
    // ============================================================================
    
    private void OnLiquidSelected(LiquidData liquid)
    {
        // If already selected, clear selection (toggle off)
        if (_selectedLiquid?.Id == liquid.Id)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Deselecting liquid {liquid.Name}");
            
            // Animate deselection on the panel item
            if (_liquidPanelItems != null)
            {
                var panel = _liquidPanelItems.FirstOrDefault(p => p.ClassId == liquid.Id);
                if (panel != null)
                    panel.SetPanelSelected(false, animate: true);
            }
            
            _selectedLiquid = null;
            
            // Update selected name via ViewModel binding
            var gvm = BindingContext as GreenhouseViewModel;
            if (gvm != null)
                gvm.SelectedLiquidName = string.Empty;
            
            // Update dynamic selected panel visibility (Android)
            _ = UpdateSelectedLiquidPanelVisibility();
            return;
        }
        
        // Deselect previously selected liquid panel (if any)
        if (_selectedLiquid != null && _liquidPanelItems != null)
        {
            var oldPanel = _liquidPanelItems.FirstOrDefault(p => p.ClassId == _selectedLiquid.Id);
            if (oldPanel != null)
                oldPanel.SetPanelSelected(false, animate: true);
        }
        
        // Clear other selections (seed and harvester)
        _selectedSeed = null;
        _isHarvesterSelected = false;
        
        // Set new selection
        _selectedLiquid = liquid;
        
        // Animate selection on the new panel item
        if (_liquidPanelItems != null)
        {
            var newPanel = _liquidPanelItems.FirstOrDefault(p => p.ClassId == liquid.Id);
            if (newPanel != null)
                newPanel.SetPanelSelected(true, animate: true);
        }
        
        // Update selected name via ViewModel binding
        var gvm2 = BindingContext as GreenhouseViewModel;
        if (gvm2 != null)
            gvm2.SelectedLiquidName = liquid.Name;
        
        // Update dynamic selected panel visibility (Android)
        _ = UpdateSelectedLiquidPanelVisibility();
    }
    
    private void OnSeedSelected(SeedData seed)
    {
        // If already selected, clear selection (toggle off)
        if (_selectedSeed?.Id == seed.Id)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Deselecting seed {seed.Name}");
            
            // Animate deselection on the panel item
            if (_seedPanelItems != null)
            {
                var panel = _seedPanelItems.FirstOrDefault(p => p.ClassId == seed.Id);
                if (panel != null)
                    panel.SetPanelSelected(false, animate: true);
            }
            
            _selectedSeed = null;
            
            // Update selected name via ViewModel binding
            var gvm = BindingContext as GreenhouseViewModel;
            if (gvm != null)
                gvm.SelectedSeedName = string.Empty;
            
            // Update dynamic selected panel visibility (Android)
            _ = UpdateSelectedSeedPanelVisibility();
            return;
        }
        
        // Deselect previously selected seed panel (if any)
        if (_selectedSeed != null && _seedPanelItems != null)
        {
            var oldPanel = _seedPanelItems.FirstOrDefault(p => p.ClassId == _selectedSeed.Id);
            if (oldPanel != null)
                oldPanel.SetPanelSelected(false, animate: true);
        }
        
        // Clear other selections (liquid and harvester)
        _selectedLiquid = null;
        _isHarvesterSelected = false;
        
        // Set new selection
        _selectedSeed = seed;
        
        // Animate selection on the new panel item
        if (_seedPanelItems != null)
        {
            var newPanel = _seedPanelItems.FirstOrDefault(p => p.ClassId == seed.Id);
            if (newPanel != null)
                newPanel.SetPanelSelected(true, animate: true);
        }
        
        // Update selected name via ViewModel binding
        var gvm2 = BindingContext as GreenhouseViewModel;
        if (gvm2 != null)
            gvm2.SelectedSeedName = seed.Name;
        
        // Update dynamic selected panel visibility (Android)
        _ = UpdateSelectedSeedPanelVisibility();
    }

    // ============================================================================
    // Panel Animation Methods
    // ============================================================================
    
    private async Task OpenLiquidsPanelWithAnimation()
    {
#if ANDROID || WINDOWS
        if (LiquidsPanel == null || _isAnimating)
            return;
        
        _isAnimating = true;
        
        // Prepare main panel for animation
        LiquidsPanel.AnchorX = 0.5;
        LiquidsPanel.AnchorY = 0.5;
        LiquidsPanel.Scale = 0;
        LiquidsPanel.IsVisible = true;
        LiquidsPanel.InputTransparent = false;
        
        // Update panel to ensure items are populated (especially important on first open)
        UpdateLiquidsPanel();
        
        // Update button highlighting (GreenhousePage specific)
        UpdateBottomPanelButtons();
        
        // Animate panels opening in parallel (main + selected panel)
        var mainPanelTask = LiquidsPanel.ScaleTo(1, 200, Easing.SpringOut);
    #if ANDROID
        var selectedTask = UpdateSelectedLiquidPanelVisibility(forceHide: false);
        await Task.WhenAll(mainPanelTask, selectedTask);
    #else
        await mainPanelTask;
    #endif
        
        _isAnimating = false;
#else
        await Task.CompletedTask;
#endif
    }
    
    private async Task OpenSeedsPanelWithAnimation()
    {
#if ANDROID || WINDOWS
        if (SeedsPanel == null || _isAnimating)
            return;
        
        _isAnimating = true;
        
        // Prepare main panel for animation
        SeedsPanel.AnchorX = 0.5;
        SeedsPanel.AnchorY = 0.5;
        SeedsPanel.Scale = 0;
        SeedsPanel.IsVisible = true;
        SeedsPanel.InputTransparent = false;
        
        // Update panel to ensure items are populated (especially important on first open)
        UpdateSeedsPanel();
        
        // Update button highlighting (GreenhousePage specific)
        UpdateBottomPanelButtons();
        
        // Animate panels opening in parallel (main + selected panel)
        var mainPanelTask = SeedsPanel.ScaleTo(1, 200, Easing.SpringOut);
    #if ANDROID
        var selectedTask = UpdateSelectedSeedPanelVisibility(forceHide: false);
        await Task.WhenAll(mainPanelTask, selectedTask);
    #else
        await mainPanelTask;
    #endif
        
        _isAnimating = false;
#else
        await Task.CompletedTask;
#endif
    }
    
    private async Task CloseLiquidsPanelWithAnimation()
    {
#if ANDROID || WINDOWS
        if (LiquidsPanel == null || !LiquidsPanel.IsVisible || _isAnimating)
            return;
        
        _isAnimating = true;
        
        // Don't clear selection - selection persists when panel is closed
        // Update button highlighting immediately (GreenhousePage specific)
        UpdateBottomPanelButtons();
        
        // Animate main panel closing and selected panel hiding in parallel
    #if ANDROID
        var mainPanelTask = LiquidsPanel.ScaleTo(0, 200, Easing.SpringIn);
        var selectedTask = UpdateSelectedLiquidPanelVisibility(forceHide: true);
        await Task.WhenAll(mainPanelTask, selectedTask);
        // Clean up after animation
        LiquidsPanel.IsVisible = false;
        LiquidsPanel.InputTransparent = true;
    #else
        // Animate main panel closing
        await LiquidsPanel.ScaleTo(0, 200, Easing.SpringIn);
        LiquidsPanel.IsVisible = false;
        LiquidsPanel.InputTransparent = true;
    #endif
        
        _isAnimating = false;
#else
        await Task.CompletedTask;
#endif
    }
    
    private async Task CloseSeedsPanelWithAnimation()
    {
#if ANDROID || WINDOWS
        if (SeedsPanel == null || !SeedsPanel.IsVisible || _isAnimating)
            return;
        
        _isAnimating = true;
        
        // Don't clear selection - selection persists when panel is closed
        // Update button highlighting immediately (GreenhousePage specific)
        UpdateBottomPanelButtons();
        
        // Animate main panel closing and selected panel hiding in parallel
    #if ANDROID
        var mainPanelTask = SeedsPanel.ScaleTo(0, 200, Easing.SpringIn);
        var selectedTask = UpdateSelectedSeedPanelVisibility(forceHide: true);
        await Task.WhenAll(mainPanelTask, selectedTask);
        // Clean up after animation
        SeedsPanel.IsVisible = false;
        SeedsPanel.InputTransparent = true;
    #else
        // Animate main panel closing
        await SeedsPanel.ScaleTo(0, 200, Easing.SpringIn);
        SeedsPanel.IsVisible = false;
        SeedsPanel.InputTransparent = true;
    #endif
        
        _isAnimating = false;
#else
        await Task.CompletedTask;
#endif
    }
    
    private async Task CloseAllPanels()
    {
#if ANDROID || WINDOWS
        // Don't clear selections - only close panels
        // Selections are cleared only by re-clicking the same item or selecting a different item
        
        // Close panels with animation
        await CloseLiquidsPanelWithAnimation();
        await CloseSeedsPanelWithAnimation();
#else
        await Task.CompletedTask;
#endif
    }
    
    // ============================================================================
    // Selected Panel Visibility Updates
    // ============================================================================
    
    // SelectedLiquidPanel visibility/animation (dynamic Android panel)
    private async Task UpdateSelectedLiquidPanelVisibility(bool forceHide = false)
    {
#if ANDROID
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] UpdateSelectedLiquidPanelVisibility called. selectedLiquid={(_selectedLiquid?.Name ?? "null")}, forceHide={forceHide}");
        
        if (_selectedLiquidPanel == null)
        {
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] _selectedLiquidPanel is null");
            return;
        }
        
        bool shouldShow = !forceHide && _selectedLiquid != null && LiquidsPanel != null && LiquidsPanel.IsVisible;
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] SelectedLiquidPanel shouldShow={shouldShow}, currentIsVisible={(_selectedLiquidPanel?.IsVisible ?? false)}");
        
        if (shouldShow)
        {
            // Ensure layout bounds are applied
            AbsoluteLayout.SetLayoutBounds(_selectedLiquidPanel, new Rect(0, 0.15, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
            AbsoluteLayout.SetLayoutFlags(_selectedLiquidPanel, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.XProportional | Microsoft.Maui.Layouts.AbsoluteLayoutFlags.YProportional);
            
            // Bring to front by re-adding as last child
            try
            {
                if (LiquidsPanelWrapper != null)
                {
                    if (LiquidsPanelWrapper.Children.Contains(_selectedLiquidPanel))
                    {
                        LiquidsPanelWrapper.Children.Remove(_selectedLiquidPanel);
                    }
                    LiquidsPanelWrapper.Children.Add(_selectedLiquidPanel);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Error re-adding SelectedLiquidPanel: {ex.Message}");
            }
            
            if (_selectedLiquidPanel != null)
            {
                _selectedLiquidPanel.InputTransparent = false;
                _selectedLiquidPanel.IsVisible = true;
                _selectedLiquidPanel.AnchorX = 0.5;
                _selectedLiquidPanel.AnchorY = 0.5;
                _selectedLiquidPanel.Scale = 0;
            }
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] Showing SelectedLiquidPanel and animating scale in");
            try
            {
                if (_selectedLiquidPanel != null)
                    await _selectedLiquidPanel.ScaleTo(1, 200, Easing.SpringOut);
            }
            catch { }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] Hiding SelectedLiquidPanel (if present)");
            if (_selectedLiquidPanel != null)
            {
                _selectedLiquidPanel.InputTransparent = true;
                try { await _selectedLiquidPanel.ScaleTo(0, 150, Easing.SpringIn); } catch { }
                _selectedLiquidPanel.IsVisible = false;
            }
        }
#else
        await Task.CompletedTask;
#endif
    }
    
    // SelectedSeedPanel visibility/animation (dynamic Android panel)
    private async Task UpdateSelectedSeedPanelVisibility(bool forceHide = false)
    {
#if ANDROID
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] UpdateSelectedSeedPanelVisibility called. selectedSeed={(_selectedSeed?.Name ?? "null")}, forceHide={forceHide}");
        
        if (_selectedSeedPanel == null)
        {
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] _selectedSeedPanel is null");
            return;
        }
        
        bool shouldShow = !forceHide && _selectedSeed != null && SeedsPanel != null && SeedsPanel.IsVisible;
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] SelectedSeedPanel shouldShow={shouldShow}, currentIsVisible={(_selectedSeedPanel?.IsVisible ?? false)}");
        
        if (shouldShow)
        {
            // Ensure layout bounds are applied
            AbsoluteLayout.SetLayoutBounds(_selectedSeedPanel, new Rect(0, 0.15, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
            AbsoluteLayout.SetLayoutFlags(_selectedSeedPanel, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.XProportional | Microsoft.Maui.Layouts.AbsoluteLayoutFlags.YProportional);
            
            // Bring to front by re-adding as last child
            try
            {
                if (SeedsPanelWrapper != null)
                {
                    if (SeedsPanelWrapper.Children.Contains(_selectedSeedPanel))
                    {
                        SeedsPanelWrapper.Children.Remove(_selectedSeedPanel);
                    }
                    SeedsPanelWrapper.Children.Add(_selectedSeedPanel);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Error re-adding SelectedSeedPanel: {ex.Message}");
            }
            
            if (_selectedSeedPanel != null)
            {
                _selectedSeedPanel.InputTransparent = false;
                _selectedSeedPanel.IsVisible = true;
                _selectedSeedPanel.AnchorX = 0.5;
                _selectedSeedPanel.AnchorY = 0.5;
                _selectedSeedPanel.Scale = 0;
            }
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] Showing SelectedSeedPanel and animating scale in");
            try
            {
                if (_selectedSeedPanel != null)
                    await _selectedSeedPanel.ScaleTo(1, 200, Easing.SpringOut);
            }
            catch { }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] Hiding SelectedSeedPanel (if present)");
            if (_selectedSeedPanel != null)
            {
                _selectedSeedPanel.InputTransparent = true;
                try { await _selectedSeedPanel.ScaleTo(0, 150, Easing.SpringIn); } catch { }
                _selectedSeedPanel.IsVisible = false;
            }
        }
#else
        await Task.CompletedTask;
#endif
    }
}
