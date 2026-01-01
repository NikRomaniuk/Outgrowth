using Outgrowth.ViewModels;
using Outgrowth.Models;
using Outgrowth.Services;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using PlantsSaveData = Outgrowth.Services.PlantsSaveService.PlantSaveData;
#if WINDOWS
using Outgrowth.Platforms.Windows;
#endif

namespace Outgrowth.Views;

public partial class GreenhousePage : ContentPage
{
    // Static reference to current instance for saving from App lifecycle events
    private static GreenhousePage? _currentInstance;
    
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
    private Dictionary<string, Border>? _liquidItemCache;
    private Dictionary<string, Border>? _seedItemCache;
    
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
    
    public GreenhousePage()
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
        if (MovePanel != null)
        {
            MovePanel.IsVisible = true;
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
    
    /// <summary>
    /// Loads saved plants from file and places them in their pots
    /// </summary>
    private void LoadSavedPlants()
    {
        var savedPlants = PlantsSaveService.LoadPlants();
        
        if (savedPlants.Count == 0)
        {
            // No saved plants, create test plant
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] No saved plants found, creating test plant");
            AddPlantToPot(2, new PlantObject("plant_grass_1", "grass", 0, 0));
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
        if (SelectedLiquidPanel != null)
        {
            SelectedLiquidPanel.IsVisible = false;
            SelectedLiquidPanel.Scale = 0;
        }
        if (SelectedSeedPanel != null)
        {
            SelectedSeedPanel.IsVisible = false;
            SelectedSeedPanel.Scale = 0;
        }
        
        // Update button highlighting to clear all highlights
        UpdateToolsPanelButtons();
        
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
            && LiquidsPanel != null && SeedsPanel != null && ToolsPanel != null 
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
                UpdateToolsPanelSize(adaptive);
                UpdateMovePanelSize(adaptive);
                
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

        if (SelectedLiquidPanel != null)
        {
            SelectedLiquidPanel.WidthRequest = sizes.SelectedWidth;
            SelectedLiquidPanel.HeightRequest = sizes.SelectedHeight;
            SelectedLiquidPanel.Margin = new Thickness(sizes.SelectedMarginLeft, 0, 0, 0);
        }

        if (SelectedSeedPanel != null)
        {
            SelectedSeedPanel.WidthRequest = sizes.SelectedWidth;
            SelectedSeedPanel.HeightRequest = sizes.SelectedHeight;
            SelectedSeedPanel.Margin = new Thickness(sizes.SelectedMarginLeft, 0, 0, 0);
        }
    }
    
    private void UpdateToolsPanelSize(double adaptiveScale)
    {
        const double baseWidth = 600.0;
        const double baseHeight = 150.0;
        const double basePanelPadding = 15.0;
        const double baseSpacing = 20.0;
        
        if (ToolsPanel.Children.Count > 0 && ToolsPanel.Children[0] is Border toolsBorder)
        {
            var panelHeight = baseHeight * adaptiveScale;
            var panelPadding = basePanelPadding * adaptiveScale;
            
            toolsBorder.WidthRequest = baseWidth * adaptiveScale;
            toolsBorder.HeightRequest = panelHeight;
            toolsBorder.Padding = panelPadding;
            
            // Button size = panel height - (2 * padding)
            var buttonSize = panelHeight - (2 * panelPadding);
            
            if (ToolsButtonsLayout != null)
            {
                ToolsButtonsLayout.Spacing = baseSpacing * adaptiveScale;
            }
            
            if (LiquidsButton != null && SeedsButton != null && HarvesterButton != null && CancelButton != null)
            {
                var buttonPadding = buttonSize * 0.15; // 15% of button size
                var iconSize = buttonSize * 0.5; // 50% of button size
                
                LiquidsButton.WidthRequest = buttonSize;
                LiquidsButton.HeightRequest = buttonSize;
                LiquidsButton.Padding = buttonPadding;
                
                SeedsButton.WidthRequest = buttonSize;
                SeedsButton.HeightRequest = buttonSize;
                SeedsButton.Padding = buttonPadding;
                
                HarvesterButton.WidthRequest = buttonSize;
                HarvesterButton.HeightRequest = buttonSize;
                HarvesterButton.Padding = buttonPadding;
                
                CancelButton.WidthRequest = buttonSize;
                CancelButton.HeightRequest = buttonSize;
                CancelButton.Padding = buttonPadding;
                
                // Scale icon font sizes
                if (LiquidsIcon != null && SeedsIcon != null && HarvesterIcon != null && CancelIcon != null)
                {
                    LiquidsIcon.FontSize = iconSize;
                    SeedsIcon.FontSize = iconSize;
                    HarvesterIcon.FontSize = iconSize;
                    CancelIcon.FontSize = iconSize;
                }
            }
        }
        
        // Update button highlighting
        UpdateToolsPanelButtons();
    }
    
    /// <summary>
    /// Updates the visual highlighting of tools panel buttons based on selection state
    /// </summary>
    private void UpdateToolsPanelButtons()
    {
#if ANDROID || WINDOWS
        if (LiquidsButton == null || SeedsButton == null || HarvesterButton == null)
            return;
        
        // Reset all buttons to default state
        LiquidsButton.Stroke = Color.FromArgb("#4CAF50");
        LiquidsButton.StrokeThickness = 2;
        LiquidsButton.BackgroundColor = Color.FromArgb("#0F1F2F");
        
        SeedsButton.Stroke = Color.FromArgb("#4CAF50");
        SeedsButton.StrokeThickness = 2;
        SeedsButton.BackgroundColor = Color.FromArgb("#1F1F0F");
        
        HarvesterButton.Stroke = Color.FromArgb("#4CAF50");
        HarvesterButton.StrokeThickness = 2;
        HarvesterButton.BackgroundColor = Color.FromArgb("#1F1F0F");
        
        // Highlight buttons based on state
        // Highlight panel buttons if their panels are open and visible (Scale > 0.1)
        if (LiquidsPanel != null && LiquidsPanel.IsVisible && LiquidsPanel.Scale > 0.1)
        {
            LiquidsButton.Stroke = Color.FromArgb("#FFD700");
            LiquidsButton.StrokeThickness = 3;
        }
        
        if (SeedsPanel != null && SeedsPanel.IsVisible && SeedsPanel.Scale > 0.1)
        {
            SeedsButton.Stroke = Color.FromArgb("#FFD700");
            SeedsButton.StrokeThickness = 3;
        }
        
        // Highlight harvester if selected
        if (_isHarvesterSelected)
        {
            HarvesterButton.Stroke = Color.FromArgb("#FFD700");
            HarvesterButton.StrokeThickness = 3;
        }
#endif
    }
    
    private void UpdateMovePanelSize(double adaptiveScale)
    {
#if ANDROID
        const double baseWidth = 300.0;
        const double baseHeight = 150.0;
        const double basePanelPadding = 15.0;
        const double baseSpacing = 20.0;
        
        if (MovePanel.Children.Count > 0 && MovePanel.Children[0] is Border moveBorder)
        {
            var panelHeight = baseHeight * adaptiveScale;
            var panelPadding = basePanelPadding * adaptiveScale;
            
            moveBorder.WidthRequest = baseWidth * adaptiveScale;
            moveBorder.HeightRequest = panelHeight;
            moveBorder.Padding = panelPadding;
            
            // Button size = panel height - (2 * padding)
            var buttonSize = panelHeight - (2 * panelPadding);
            
                if (MoveButtonsLayout != null)
                {
                    MoveButtonsLayout.Spacing = baseSpacing * adaptiveScale;
                }
            
            if (LeftArrowButton != null && RightArrowButton != null)
            {
                var buttonPadding = buttonSize * 0.15; // 15% of button size
                var iconSize = buttonSize * 0.5; // 50% of button size
                
                LeftArrowButton.WidthRequest = buttonSize;
                LeftArrowButton.HeightRequest = buttonSize;
                LeftArrowButton.Padding = buttonPadding;
                
                RightArrowButton.WidthRequest = buttonSize;
                RightArrowButton.HeightRequest = buttonSize;
                RightArrowButton.Padding = buttonPadding;
                
                if (LeftArrowIcon != null && RightArrowIcon != null)
                {
                    LeftArrowIcon.FontSize = iconSize;
                    RightArrowIcon.FontSize = iconSize;
                }
            }
        }
#endif
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
            if (_liquidItemCache != null && _liquidItemCache.Count == liquids.Count)
            {
                // Update selection highlighting only (optimized path)
                foreach (var liquid in liquids)
                {
                    if (_liquidItemCache.TryGetValue(liquid.Id, out var border))
                    {
                        bool isSelected = _selectedLiquid?.Id == liquid.Id;
                        border.Stroke = isSelected ? Color.FromArgb("#FFD700") : Color.FromArgb("#4CAF50");
                        border.StrokeThickness = isSelected ? 2 : 1;
                    }
                    else
                    {
                        // Item not in cache, need full recreation
                        _liquidItemCache = null;
                        break;
                    }
                }
                
                // If cache update was successful, we're done
                if (_liquidItemCache != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Updated LiquidsPanel selection highlighting (optimized), selected: {_selectedLiquid?.Name ?? "null"}");
                    return;
                }
            }
            
            // Full recreation path: clear and rebuild
            LiquidsList.Children.Clear();
            _liquidItemCache = new Dictionary<string, Border>();
            
            foreach (var liquid in liquids)
            {
                var liquidItem = CreateLiquidItem(liquid);
                _liquidItemCache[liquid.Id] = liquidItem;
                LiquidsList.Children.Add(liquidItem);
            }
            
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Updated LiquidsPanel with {liquids.Count} liquids (full recreation), selected: {_selectedLiquid?.Name ?? "null"}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Error updating LiquidsPanel: {ex.Message}");
            _liquidItemCache = null; // Clear cache on error
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
            
            // Check if we can update selection only (if cache exists and item count matches)
            if (_seedItemCache != null && _seedItemCache.Count == seeds.Count)
            {
                // Update selection highlighting only (optimized path)
                foreach (var seed in seeds)
                {
                    if (_seedItemCache.TryGetValue(seed.Id, out var border))
                    {
                        bool isSelected = _selectedSeed?.Id == seed.Id;
                        border.Stroke = isSelected ? Color.FromArgb("#FFD700") : Color.FromArgb("#4CAF50");
                        border.StrokeThickness = isSelected ? 2 : 1;
                    }
                    else
                    {
                        // Item not in cache, need full recreation
                        _seedItemCache = null;
                        break;
                    }
                }
                
                // If cache update was successful, we're done
                if (_seedItemCache != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Updated SeedsPanel selection highlighting (optimized), selected: {_selectedSeed?.Name ?? "null"}");
                    return;
                }
            }
            
            // Full recreation path: clear and rebuild
            SeedsList.Children.Clear();
            _seedItemCache = new Dictionary<string, Border>();
            
            foreach (var seed in seeds)
            {
                var seedItem = CreateSeedItem(seed);
                _seedItemCache[seed.Id] = seedItem;
                SeedsList.Children.Add(seedItem);
            }
            
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Updated SeedsPanel with {seeds.Count} seeds (full recreation), selected: {_selectedSeed?.Name ?? "null"}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Error updating SeedsPanel: {ex.Message}");
            _seedItemCache = null; // Clear cache on error
        }
#endif
    }
    
    /// <summary>
    /// Creates a UI element for a liquid item
    /// </summary>
    private Border CreateLiquidItem(LiquidData liquid)
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

        return UserInterfaceCreator.CreatePanelItem(liquid.Id, liquid.Name, liquid.Sprite, isSelected,
            panelItemHeight, qtySize, bodySize, isAndroid, () => OnLiquidSelected(liquid), isEnabled: liquid.Quantity > 0, bindingContext: liquid);
    }
    
    /// <summary>
    /// Creates a UI element for a seed item
    /// </summary>
    private Border CreateSeedItem(SeedData seed)
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

        return UserInterfaceCreator.CreatePanelItem(seed.Id, seed.Name, seed.Sprite, isSelected,
            panelItemHeight, qtySize, bodySize, isAndroid, () => OnSeedSelected(seed), isEnabled: seed.Quantity > 0, bindingContext: seed);
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

    private async void OnLiquidsButtonClicked(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        if (_isAnimating)
            return;
        
        System.Diagnostics.Debug.WriteLine("[GreenhousePage] OnLiquidsButtonClicked called");
        
        // Clear harvester selection when opening panel
        _isHarvesterSelected = false;
        
        if (LiquidsPanel != null && SeedsPanel != null && SelectedLiquidPanel != null && SelectedSeedPanel != null)
        {
            // Toggle liquids panel (close if already open, open if closed)
            bool wasOpen = LiquidsPanel.IsVisible;
            
            // Clear seed selection when opening liquids panel
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Before opening liquids: _selectedSeed={_selectedSeed?.Name ?? "null"}");
            _selectedSeed = null;
            SelectedSeedPanel.IsVisible = false;
            
            // Close seeds panel with animation (if open)
            await CloseSeedsPanelWithAnimation();
            
            // Note: CloseSeedsPanelWithAnimation already calls UpdateSeedsPanel(), so no need to call it again
            
            // Toggle liquids panel
            if (wasOpen)
            {
                // Close liquids panel with animation (will update button highlighting inside)
                await CloseLiquidsPanelWithAnimation();
            }
            else
            {
                // Open liquids panel with animation (will update button highlighting inside)
                await OpenLiquidsPanelWithAnimation();
            }
            
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Liquids panel toggled, _selectedSeed cleared");
        }
        
        // Final update of button highlighting after all operations
        UpdateToolsPanelButtons();
#endif
    }

    private async void OnSeedsButtonClicked(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        if (_isAnimating)
            return;
        
        System.Diagnostics.Debug.WriteLine("[GreenhousePage] OnSeedsButtonClicked called");
        
        // Clear harvester selection when opening panel
        _isHarvesterSelected = false;
        
        if (LiquidsPanel != null && SeedsPanel != null && SelectedLiquidPanel != null && SelectedSeedPanel != null)
        {
            // Toggle seeds panel (close if already open, open if closed)
            bool wasOpen = SeedsPanel.IsVisible;
            
            // Clear liquid selection when opening seeds panel
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Before opening seeds: _selectedLiquid={_selectedLiquid?.Name ?? "null"}");
            _selectedLiquid = null;
            SelectedLiquidPanel.IsVisible = false;
            
            // Close liquids panel with animation (if open)
            await CloseLiquidsPanelWithAnimation();
            
            // Note: CloseLiquidsPanelWithAnimation already calls UpdateLiquidsPanel(), so no need to call it again
            
            // Toggle seeds panel
            if (wasOpen)
            {
                // Close seeds panel with animation (will update button highlighting inside)
                await CloseSeedsPanelWithAnimation();
            }
            else
            {
                // Open seeds panel with animation (will update button highlighting inside)
                await OpenSeedsPanelWithAnimation();
            }
            
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Seeds panel toggled, _selectedLiquid cleared");
        }
        
        // Final update of button highlighting after all operations
        UpdateToolsPanelButtons();
#endif
    }

    private async void OnHarvesterButtonClicked(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        System.Diagnostics.Debug.WriteLine("[GreenhousePage] OnHarvesterButtonClicked called");
        
        // Toggle harvester selection
        _isHarvesterSelected = !_isHarvesterSelected;
        
        // Clear other selections when harvester is selected
        if (_isHarvesterSelected)
        {
            _selectedLiquid = null;
            _selectedSeed = null;
            
            // Close panels with animation
            await CloseAllPanels();
        }
        
        // Update button highlighting
        UpdateToolsPanelButtons();
        
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Harvester selected: {_isHarvesterSelected}");
#endif
    }

    private async void OnCancelButtonClicked(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        // Clear harvester selection when closing panels (before closing)
        _isHarvesterSelected = false;
        
        await CloseAllPanels();
        
        // Update button highlighting
        UpdateToolsPanelButtons();
#endif
    }

    private void OnLeftArrowButtonClicked(object sender, EventArgs e)
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

    private void OnRightArrowButtonClicked(object sender, EventArgs e)
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


    private void OnLiquidsPanelTapped(object sender, EventArgs e)
    {
    }

    private void OnSeedsPanelTapped(object sender, EventArgs e)
    {
    }
    
    // ============================================================================
    // Item Selection Handlers
    // ============================================================================
    
    private void OnLiquidSelected(LiquidData liquid)
    {
        // Ignore if already selected
        if (_selectedLiquid?.Id == liquid.Id)
            return;
        
        // Clear other selections
        _selectedSeed = null;
        _isHarvesterSelected = false;
        if (SelectedSeedPanel != null)
            SelectedSeedPanel.IsVisible = false;
        UpdateSeedsPanel();
        
        // Check if selected panel was already visible
        bool wasAlreadyVisible = SelectedLiquidPanel != null && SelectedLiquidPanel.IsVisible && SelectedLiquidPanel.Scale > 0.1;
        
        // Set new selection
        _selectedLiquid = liquid;
        UpdateSelectedLiquidPanelVisibility();
        
        // Update selected name via ViewModel binding
        if (BindingContext is GreenhouseViewModel gvm)
            gvm.SelectedLiquidName = liquid.Name;
        
        // Animate selected panel appearance (Android only, only if wasn't already visible)
#if ANDROID
        if (SelectedLiquidPanel != null && SelectedLiquidPanel.IsVisible && !wasAlreadyVisible &&
            LiquidsPanel != null && LiquidsPanel.IsVisible && LiquidsPanel.Scale > 0.1)
        {
            SelectedLiquidPanel.AnchorX = 0.5;
            SelectedLiquidPanel.AnchorY = 0.5;
            SelectedLiquidPanel.Scale = 0;
            _ = SelectedLiquidPanel.ScaleTo(1, 200, Easing.SpringOut);
        }
#endif
        
        // Update UI
        UpdateLiquidsPanel();
        UpdateToolsPanelButtons();
    }
    
    private void OnSeedSelected(SeedData seed)
    {
        // Ignore if already selected
        if (_selectedSeed?.Id == seed.Id)
            return;
        
        // Clear other selections
        _selectedLiquid = null;
        _isHarvesterSelected = false;
        if (SelectedLiquidPanel != null)
            SelectedLiquidPanel.IsVisible = false;
        UpdateLiquidsPanel();
        
        // Check if selected panel was already visible
        bool wasAlreadyVisible = SelectedSeedPanel != null && SelectedSeedPanel.IsVisible && SelectedSeedPanel.Scale > 0.1;
        
        // Set new selection
        _selectedSeed = seed;
        UpdateSelectedSeedPanelVisibility();
        
        // Update selected name via ViewModel binding
        if (BindingContext is GreenhouseViewModel gvm2)
            gvm2.SelectedSeedName = seed.Name;
        
        // Animate selected panel appearance (Android only, only if wasn't already visible)
#if ANDROID
        if (SelectedSeedPanel != null && SelectedSeedPanel.IsVisible && !wasAlreadyVisible &&
            SeedsPanel != null && SeedsPanel.IsVisible && SeedsPanel.Scale > 0.1)
        {
            SelectedSeedPanel.AnchorX = 0.5;
            SelectedSeedPanel.AnchorY = 0.5;
            SelectedSeedPanel.Scale = 0;
            _ = SelectedSeedPanel.ScaleTo(1, 200, Easing.SpringOut);
        }
#endif
        
        // Update UI
        UpdateSeedsPanel();
        UpdateToolsPanelButtons();
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
        
        // Prepare selected panel for animation (Android only)
        Task? selectedPanelTask = null;
#if ANDROID
        UpdateSelectedLiquidPanelVisibility();
        if (SelectedLiquidPanel != null && SelectedLiquidPanel.IsVisible)
        {
            SelectedLiquidPanel.AnchorX = 0.5;
            SelectedLiquidPanel.AnchorY = 0.5;
            SelectedLiquidPanel.Scale = 0;
        }
#elif WINDOWS
        if (SelectedLiquidPanel != null)
            SelectedLiquidPanel.IsVisible = false;
#endif
        
        // Update button highlighting (GreenhousePage specific)
        UpdateToolsPanelButtons();
        
        // Animate panels opening in parallel
        var mainPanelTask = LiquidsPanel.ScaleTo(1, 200, Easing.SpringOut);
#if ANDROID
        if (SelectedLiquidPanel != null && SelectedLiquidPanel.IsVisible)
            selectedPanelTask = SelectedLiquidPanel.ScaleTo(1, 200, Easing.SpringOut);
#endif
        
        await mainPanelTask;
        if (selectedPanelTask != null)
            await selectedPanelTask;
        
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
        
        // Prepare selected panel for animation (Android only)
        Task? selectedPanelTask = null;
#if ANDROID
        UpdateSelectedSeedPanelVisibility();
        if (SelectedSeedPanel != null && SelectedSeedPanel.IsVisible)
        {
            SelectedSeedPanel.AnchorX = 0.5;
            SelectedSeedPanel.AnchorY = 0.5;
            SelectedSeedPanel.Scale = 0;
        }
#elif WINDOWS
        if (SelectedSeedPanel != null)
            SelectedSeedPanel.IsVisible = false;
#endif
        
        // Update button highlighting (GreenhousePage specific)
        UpdateToolsPanelButtons();
        
        // Animate panels opening in parallel
        var mainPanelTask = SeedsPanel.ScaleTo(1, 200, Easing.SpringOut);
#if ANDROID
        if (SelectedSeedPanel != null && SelectedSeedPanel.IsVisible)
            selectedPanelTask = SelectedSeedPanel.ScaleTo(1, 200, Easing.SpringOut);
#endif
        
        await mainPanelTask;
        if (selectedPanelTask != null)
            await selectedPanelTask;
        
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
        
        // Clear selection and update panel to clear visual highlighting BEFORE animation starts
        // (while panel is still visible, so UpdateLiquidsPanel will execute)
        _selectedLiquid = null;
        UpdateLiquidsPanel();
        
        // Update button highlighting immediately (GreenhousePage specific)
        UpdateToolsPanelButtons();
        
        // Prepare selected panel for closing animation (Android only)
        Task? selectedPanelTask = null;
#if ANDROID
        if (SelectedLiquidPanel != null && SelectedLiquidPanel.IsVisible)
        {
            SelectedLiquidPanel.AnchorX = 0.5;
            SelectedLiquidPanel.AnchorY = 0.5;
            selectedPanelTask = SelectedLiquidPanel.ScaleTo(0, 200, Easing.SpringIn);
        }
#endif
        
        // Animate main panel closing
        await LiquidsPanel.ScaleTo(0, 200, Easing.SpringIn);
        
        // Wait for selected panel animation to complete
        if (selectedPanelTask != null)
            await selectedPanelTask;
        
        LiquidsPanel.IsVisible = false;
        LiquidsPanel.InputTransparent = true;
        
#if ANDROID
        if (SelectedLiquidPanel != null)
        {
            SelectedLiquidPanel.IsVisible = false;
            SelectedLiquidPanel.Scale = 0;
        }
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
        
        // Clear selection and update panel to clear visual highlighting BEFORE animation starts
        // (while panel is still visible, so UpdateSeedsPanel will execute)
        _selectedSeed = null;
        UpdateSeedsPanel();
        
        // Update button highlighting immediately (GreenhousePage specific)
        UpdateToolsPanelButtons();
        
        // Prepare selected panel for closing animation (Android only)
        Task? selectedPanelTask = null;
#if ANDROID
        if (SelectedSeedPanel != null && SelectedSeedPanel.IsVisible)
        {
            SelectedSeedPanel.AnchorX = 0.5;
            SelectedSeedPanel.AnchorY = 0.5;
            selectedPanelTask = SelectedSeedPanel.ScaleTo(0, 200, Easing.SpringIn);
        }
#endif
        
        // Animate main panel closing
        await SeedsPanel.ScaleTo(0, 200, Easing.SpringIn);
        
        // Wait for selected panel animation to complete
        if (selectedPanelTask != null)
            await selectedPanelTask;
        
        SeedsPanel.IsVisible = false;
        SeedsPanel.InputTransparent = true;
        
#if ANDROID
        if (SelectedSeedPanel != null)
        {
            SelectedSeedPanel.IsVisible = false;
            SelectedSeedPanel.Scale = 0;
        }
#endif
        
        _isAnimating = false;
#else
        await Task.CompletedTask;
#endif
    }
    
    private async Task CloseAllPanels()
    {
#if ANDROID || WINDOWS
        // Clear selections
        _selectedLiquid = null;
        _selectedSeed = null;
        
        // Update button highlighting immediately
        UpdateToolsPanelButtons();
        
        // Close panels with animation
        await CloseLiquidsPanelWithAnimation();
        await CloseSeedsPanelWithAnimation();
        
        // Ensure buttons are unhighlighted
        UpdateToolsPanelButtons();
#else
        await Task.CompletedTask;
#endif
    }
    
    // ============================================================================
    // Selected Panel Visibility Updates
    // ============================================================================
    
    private void UpdateSelectedLiquidPanelVisibility()
    {
#if ANDROID
        if (SelectedLiquidPanel == null)
            return;
        
        SelectedLiquidPanel.IsVisible = _selectedLiquid != null && LiquidsPanel != null && LiquidsPanel.IsVisible;
#elif WINDOWS
        if (SelectedLiquidPanel != null)
            SelectedLiquidPanel.IsVisible = false;
#endif
    }
    
    private void UpdateSelectedSeedPanelVisibility()
    {
#if ANDROID
        if (SelectedSeedPanel == null)
            return;
        
        SelectedSeedPanel.IsVisible = _selectedSeed != null && SeedsPanel != null && SeedsPanel.IsVisible;
#elif WINDOWS
        if (SelectedSeedPanel != null)
            SelectedSeedPanel.IsVisible = false;
#endif
    }
}
