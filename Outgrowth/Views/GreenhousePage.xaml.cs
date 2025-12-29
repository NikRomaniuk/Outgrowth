using Outgrowth.ViewModels;
using Outgrowth.Models;
using Outgrowth.Services;
using System.Linq;
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
    
    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        // Ensure libraries are initialized before creating plant objects
        // InitializeAsync() is idempotent and thread-safe, so safe to call multiple times
        await PlantLibrary.InitializeAsync();
        await SeedLibrary.InitializeAsync();
        await LiquidLibrary.InitializeAsync();
        
        CreatePotElements();
        UpdatePotPositions();
        UpdateContentPosition(); // Center Pot2 on load
        
        // Create dynamic panels from libraries
        UpdateLiquidsPanel();
        UpdateSeedsPanel();
        
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
            onEscape: CloseAllPanels
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
                potNumberToPlant[_pots[i].PotNumber] = _pots[i].PlantSlot;
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
        // Save plants before leaving the page
        SavePlants();
        
        CloseAllPanels();
        CleanupPotElements();
        
        // Unregister instance
        if (_currentInstance == this)
        {
            _currentInstance = null;
        }
        
#if WINDOWS
        _windowsInput?.Detach();
        _windowsInput = null;
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
                EnvironmentWrapper.Scale = screenProps.Scale;
                EnvironmentWrapper.WidthRequest = ScreenProperties.ReferenceWidth;
                EnvironmentWrapper.HeightRequest = ScreenProperties.ReferenceHeight;
                
                EnvironmentContainer.InputTransparent = false;
                EnvironmentContainer.BackgroundColor = Colors.Transparent;
                
                EnvironmentWrapper.TranslationX = screenProps.OffsetX;
                EnvironmentWrapper.TranslationY = screenProps.OffsetY;
                
                HubButton.AnchorX = 1;
                HubButton.AnchorY = 1;
                HubButton.Scale = screenProps.Scale;
                
                LeftGutterPlaceholder.AnchorX = 0;
                LeftGutterPlaceholder.AnchorY = 0.5;
                LeftGutterPlaceholder.Scale = screenProps.Scale;
                
                UpdateFontSizes(screenProps.FontScale);
                UpdatePanelSizes(screenProps.FontScale);
                UpdateToolsPanelSize(screenProps.FontScale);
                UpdateMovePanelSize(screenProps.FontScale);
                
                // Update panels to refresh font sizes for dynamically created items
                UpdateLiquidsPanel();
                UpdateSeedsPanel();
                
                UpdatePotPositions();
                UpdateContentPosition();
            }
        }
#endif
    }
    
    private void UpdatePanelSizes(double fontScale)
    {
        // Panel width - different for Android and Windows
#if ANDROID
        const double baseWidth = 250.0;
#else
        const double baseWidth = 300.0;
#endif
        const double baseHeight = 500.0;
        const double baseMargin = 20.0;
        
        LiquidsPanel.WidthRequest = baseWidth * fontScale;
        LiquidsPanel.HeightRequest = baseHeight * fontScale;
        LiquidsPanel.Margin = new Thickness(baseMargin * fontScale, 0, 0, 0);
        
        SeedsPanel.WidthRequest = baseWidth * fontScale;
        SeedsPanel.HeightRequest = baseHeight * fontScale;
        SeedsPanel.Margin = new Thickness(baseMargin * fontScale, 0, 0, 0);
        
        // Update panel Y position - lower on Android
#if ANDROID
        const double panelYPosition = 0.7; // Lower position on Android
#else
        const double panelYPosition = 0.5; // Center position on Windows
#endif
        
        if (LiquidsPanelWrapper != null && LiquidsPanel != null)
        {
            AbsoluteLayout.SetLayoutBounds(LiquidsPanel, new Rect(0, panelYPosition, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
            AbsoluteLayout.SetLayoutFlags(LiquidsPanel, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.YProportional);
        }
        
        if (SeedsPanelWrapper != null && SeedsPanel != null)
        {
            AbsoluteLayout.SetLayoutBounds(SeedsPanel, new Rect(0, panelYPosition, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
            AbsoluteLayout.SetLayoutFlags(SeedsPanel, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.YProportional);
        }
        
        // Update selected info panels (same width as main panels)
#if ANDROID
        const double selectedPanelWidth = 250.0;
#else
        const double selectedPanelWidth = 300.0;
#endif
        const double selectedPanelHeight = 160.0;
        
        if (SelectedLiquidPanel != null)
        {
            SelectedLiquidPanel.WidthRequest = selectedPanelWidth * fontScale;
            SelectedLiquidPanel.HeightRequest = selectedPanelHeight * fontScale;
            SelectedLiquidPanel.Margin = new Thickness(baseMargin * fontScale, 0, 0, 0);
        }
        
        if (SelectedSeedPanel != null)
        {
            SelectedSeedPanel.WidthRequest = selectedPanelWidth * fontScale;
            SelectedSeedPanel.HeightRequest = selectedPanelHeight * fontScale;
            SelectedSeedPanel.Margin = new Thickness(baseMargin * fontScale, 0, 0, 0);
        }
    }
    
    private void UpdateToolsPanelSize(double fontScale)
    {
        const double baseWidth = 600.0;
        const double baseHeight = 150.0;
        const double basePanelPadding = 15.0;
        const double baseSpacing = 20.0;
        
        if (ToolsPanel.Children.Count > 0 && ToolsPanel.Children[0] is Border toolsBorder)
        {
            var panelHeight = baseHeight * fontScale;
            var panelPadding = basePanelPadding * fontScale;
            
            toolsBorder.WidthRequest = baseWidth * fontScale;
            toolsBorder.HeightRequest = panelHeight;
            toolsBorder.Padding = panelPadding;
            
            // Button size = panel height - (2 * padding)
            var buttonSize = panelHeight - (2 * panelPadding);
            
            if (ToolsButtonsLayout != null)
            {
                ToolsButtonsLayout.Spacing = baseSpacing * fontScale;
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
        // Highlight panel buttons if their panels are open
        if (LiquidsPanel != null && LiquidsPanel.IsVisible)
        {
            LiquidsButton.Stroke = Color.FromArgb("#FFD700");
            LiquidsButton.StrokeThickness = 3;
        }
        
        if (SeedsPanel != null && SeedsPanel.IsVisible)
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
    
    private void UpdateMovePanelSize(double fontScale)
    {
#if ANDROID
        const double baseWidth = 300.0;
        const double baseHeight = 150.0;
        const double basePanelPadding = 15.0;
        const double baseSpacing = 20.0;
        
        if (MovePanel.Children.Count > 0 && MovePanel.Children[0] is Border moveBorder)
        {
            var panelHeight = baseHeight * fontScale;
            var panelPadding = basePanelPadding * fontScale;
            
            moveBorder.WidthRequest = baseWidth * fontScale;
            moveBorder.HeightRequest = panelHeight;
            moveBorder.Padding = panelPadding;
            
            // Button size = panel height - (2 * padding)
            var buttonSize = panelHeight - (2 * panelPadding);
            
            if (MoveButtonsLayout != null)
            {
                MoveButtonsLayout.Spacing = baseSpacing * fontScale;
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
    
    private void UpdateFontSizes(double fontScale)
    {
        const double baseTitleSize = 40.0;
        const double baseBodySize = 30.0;
        const double baseQtySize = 24.0;
        const double baseIconSize = 40.0;
        
        Resources["ResourcePanelTitleSize"] = baseTitleSize * fontScale;
        Resources["ResourcePanelBodySize"] = baseBodySize * fontScale;
        Resources["ResourcePanelQtySize"] = baseQtySize * fontScale;
        Resources["ResourcePanelIconSize"] = baseIconSize * fontScale;
    }
    
    /// <summary>
    /// Updates the LiquidsPanel with all available liquids from LiquidLibrary
    /// </summary>
    private void UpdateLiquidsPanel()
    {
#if ANDROID || WINDOWS
        if (LiquidsList == null)
        {
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] UpdateLiquidsPanel: LiquidsList is null");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] UpdateLiquidsPanel: Current selection: {_selectedLiquid?.Name ?? "null"}");
        
        // Clear existing items
        LiquidsList.Children.Clear();
        
        try
        {
            var liquids = LiquidLibrary.GetAllLiquids();
            foreach (var liquid in liquids)
            {
                var liquidItem = CreateLiquidItem(liquid);
                LiquidsList.Children.Add(liquidItem);
            }
            
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Updated LiquidsPanel with {liquids.Count()} liquids, selected: {_selectedLiquid?.Name ?? "null"}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Error updating LiquidsPanel: {ex.Message}");
        }
#endif
    }
    
    /// <summary>
    /// Updates the SeedsPanel with all available seeds from SeedLibrary
    /// </summary>
    private void UpdateSeedsPanel()
    {
#if ANDROID || WINDOWS
        if (SeedsList == null)
        {
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] UpdateSeedsPanel: SeedsList is null");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] UpdateSeedsPanel: Current selection: {_selectedSeed?.Name ?? "null"}");
        
        // Clear existing items
        SeedsList.Children.Clear();
        
        try
        {
            var seeds = SeedLibrary.GetAllSeeds();
            foreach (var seed in seeds)
            {
                var seedItem = CreateSeedItem(seed);
                SeedsList.Children.Add(seedItem);
            }
            
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Updated SeedsPanel with {seeds.Count()} seeds, selected: {_selectedSeed?.Name ?? "null"}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Error updating SeedsPanel: {ex.Message}");
        }
#endif
    }
    
    /// <summary>
    /// Creates a UI element for a liquid item
    /// </summary>
    private Border CreateLiquidItem(LiquidData liquid)
    {
        // Check if this liquid is selected
        bool isSelected = _selectedLiquid?.Id == liquid.Id;
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] CreateLiquidItem: {liquid.Name}, isSelected={isSelected}, _selectedLiquid={_selectedLiquid?.Name ?? "null"}");
        
        var border = new Border
        {
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#4CAF50"),
            BackgroundColor = Color.FromArgb("#0F1F2F"),
            Padding = new Thickness(10)
        };
        
        border.StrokeShape = new RoundRectangle { CornerRadius = 8 };
        
        if (isSelected)
        {
            border.Stroke = Color.FromArgb("#FFD700");
            border.StrokeThickness = 2;
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] CreateLiquidItem: {liquid.Name} marked as selected (gold border)");
        }
        
#if ANDROID
        // On Android, use Grid to position icon on left and quantity on right
        var contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };
        
        var iconLabel = new Label
        {
            Text = liquid.Sprite,
            FontSize = (double)Resources["ResourcePanelIconSize"],
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Start
        };
        Grid.SetColumn(iconLabel, 0);
        contentGrid.Children.Add(iconLabel);
        
        var qtyLabel = new Label
        {
            Text = "0", // TODO: Get actual quantity from inventory
            FontSize = (double)Resources["ResourcePanelQtySize"],
            TextColor = Colors.White,
            Opacity = 0.7,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        };
        Grid.SetColumn(qtyLabel, 1);
        contentGrid.Children.Add(qtyLabel);
        
        border.Content = contentGrid;
#else
        // On Windows, show icon, name, and quantity
        var horizontalStack = new HorizontalStackLayout
        {
            Spacing = 10
        };
        
        var iconLabel = new Label
        {
            Text = liquid.Sprite,
            FontSize = (double)Resources["ResourcePanelIconSize"],
            VerticalOptions = LayoutOptions.Center
        };
        var verticalStack = new VerticalStackLayout
        {
            Spacing = 3
        };
        
        var nameLabel = new Label
        {
            Text = liquid.Name,
            FontSize = (double)Resources["ResourcePanelBodySize"],
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        };
        
        var qtyLabel = new Label
        {
            Text = "Qty: 0", // TODO: Get actual quantity from inventory
            FontSize = (double)Resources["ResourcePanelQtySize"],
            TextColor = Colors.White,
            Opacity = 0.7
        };
        
        verticalStack.Children.Add(nameLabel);
        verticalStack.Children.Add(qtyLabel);
        
        horizontalStack.Children.Add(iconLabel);
        horizontalStack.Children.Add(verticalStack);
        
        border.Content = horizontalStack;
#endif
        
        // Add tap gesture to select this liquid
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (s, e) => OnLiquidSelected(liquid);
        border.GestureRecognizers.Add(tapGesture);
        
        return border;
    }
    
    /// <summary>
    /// Creates a UI element for a seed item
    /// </summary>
    private Border CreateSeedItem(SeedData seed)
    {
        // Check if this seed is selected
        bool isSelected = _selectedSeed?.Id == seed.Id;
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] CreateSeedItem: {seed.Name}, isSelected={isSelected}, _selectedSeed={_selectedSeed?.Name ?? "null"}");
        
        var border = new Border
        {
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#4CAF50"),
            BackgroundColor = Color.FromArgb("#0F1F0F"),
            Padding = new Thickness(10)
        };
        
        border.StrokeShape = new RoundRectangle { CornerRadius = 8 };
        
        if (isSelected)
        {
            border.Stroke = Color.FromArgb("#FFD700");
            border.StrokeThickness = 2;
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] CreateSeedItem: {seed.Name} marked as selected (gold border)");
        }
        
#if ANDROID
        // On Android, use Grid to position icon on left and quantity on right
        var contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };
        
        var iconLabel = new Label
        {
            Text = seed.Sprite,
            FontSize = (double)Resources["ResourcePanelIconSize"],
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Start
        };
        Grid.SetColumn(iconLabel, 0);
        contentGrid.Children.Add(iconLabel);
        
        var qtyLabel = new Label
        {
            Text = "0", // TODO: Get actual quantity from inventory
            FontSize = (double)Resources["ResourcePanelQtySize"],
            TextColor = Colors.White,
            Opacity = 0.7,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        };
        Grid.SetColumn(qtyLabel, 1);
        contentGrid.Children.Add(qtyLabel);
        
        border.Content = contentGrid;
#else
        // On Windows, show icon, name, and quantity
        var horizontalStack = new HorizontalStackLayout
        {
            Spacing = 10
        };
        
        var iconLabel = new Label
        {
            Text = seed.Sprite,
            FontSize = (double)Resources["ResourcePanelIconSize"],
            VerticalOptions = LayoutOptions.Center
        };
        var verticalStack = new VerticalStackLayout
        {
            Spacing = 3
        };
        
        var nameLabel = new Label
        {
            Text = seed.Name,
            FontSize = (double)Resources["ResourcePanelBodySize"],
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        };
        
        var qtyLabel = new Label
        {
            Text = "Qty: 0", // TODO: Get actual quantity from inventory
            FontSize = (double)Resources["ResourcePanelQtySize"],
            TextColor = Colors.White,
            Opacity = 0.7
        };
        
        verticalStack.Children.Add(nameLabel);
        verticalStack.Children.Add(qtyLabel);
        
        horizontalStack.Children.Add(iconLabel);
        horizontalStack.Children.Add(verticalStack);
        
        border.Content = horizontalStack;
#endif
        
        // Add tap gesture to select this seed
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (s, e) => OnSeedSelected(seed);
        border.GestureRecognizers.Add(tapGesture);
        
        return border;
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
            
            // Find the pot containing this plant
            var pot = _pots.FirstOrDefault(p => p.PlantSlot == plant);
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

    private void OnLiquidsButtonClicked(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
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
            
            // Close seeds panel
            SeedsPanel.IsVisible = false;
            SeedsPanel.InputTransparent = true;
            
            // Update seeds panel to remove highlighting
            UpdateSeedsPanel();
            
            // Toggle liquids panel
            if (wasOpen)
            {
                // Close liquids panel
                LiquidsPanel.IsVisible = false;
                LiquidsPanel.InputTransparent = true;
                SelectedLiquidPanel.IsVisible = false;
                _selectedLiquid = null;
                UpdateLiquidsPanel();
            }
            else
            {
                // Open liquids panel
                LiquidsPanel.IsVisible = true;
                LiquidsPanel.InputTransparent = false;
#if ANDROID
                UpdateSelectedLiquidPanelVisibility();
#elif WINDOWS
                // On Windows, always hide selected panel (not needed)
                if (SelectedLiquidPanel != null)
                {
                    SelectedLiquidPanel.IsVisible = false;
                }
#endif
            }
            
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Liquids panel toggled, _selectedSeed cleared");
        }
        
        // Update button highlighting
        UpdateToolsPanelButtons();
#endif
    }

    private void OnSeedsButtonClicked(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
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
            
            // Close liquids panel
            LiquidsPanel.IsVisible = false;
            LiquidsPanel.InputTransparent = true;
            
            // Update liquids panel to remove highlighting
            UpdateLiquidsPanel();
            
            // Toggle seeds panel
            if (wasOpen)
            {
                // Close seeds panel
                SeedsPanel.IsVisible = false;
                SeedsPanel.InputTransparent = true;
                SelectedSeedPanel.IsVisible = false;
                _selectedSeed = null;
                UpdateSeedsPanel();
            }
            else
            {
                // Open seeds panel
                SeedsPanel.IsVisible = true;
                SeedsPanel.InputTransparent = false;
#if ANDROID
                UpdateSelectedSeedPanelVisibility();
#elif WINDOWS
                // On Windows, always hide selected panel (not needed)
                if (SelectedSeedPanel != null)
                {
                    SelectedSeedPanel.IsVisible = false;
                }
#endif
            }
            
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Seeds panel toggled, _selectedLiquid cleared");
        }
        
        // Update button highlighting
        UpdateToolsPanelButtons();
#endif
    }

    private void OnHarvesterButtonClicked(object sender, EventArgs e)
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
            
            // Close panels
            CloseAllPanels();
        }
        
        // Update button highlighting
        UpdateToolsPanelButtons();
        
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Harvester selected: {_isHarvesterSelected}");
#endif
    }

    private void OnCancelButtonClicked(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        // Clear harvester selection when closing panels (before closing)
        _isHarvesterSelected = false;
        
        CloseAllPanels();
        
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
            ContentContainer.Children.Add(visualElement);
        }
#endif
    }
    
    /// <summary>
    /// Updates the visual element for a specific pot. Use this after modifying PlantSlot.
    /// </summary>
    private void UpdatePotVisualElement(PotObject pot)
    {
#if ANDROID || WINDOWS
        if (ContentContainer == null || pot.VisualElement == null)
            return;
        
        // Remove old visual element
        if (ContentContainer.Children.Contains(pot.VisualElement))
        {
            ContentContainer.Children.Remove(pot.VisualElement);
        }
        
        // Create new visual element with updated plant slot
        var newVisualElement = pot.CreateVisualElement();
        ContentContainer.Children.Add(newVisualElement);
        
        // Update position after recreating
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
    
    /// <summary>
    /// Handles selection of a liquid item
    /// </summary>
    private void OnLiquidSelected(LiquidData liquid)
    {
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] OnLiquidSelected: {liquid.Name}, current _selectedSeed={_selectedSeed?.Name ?? "null"}");
        
        // Clear other selections (only one thing can be selected at a time)
        _selectedSeed = null;
        _isHarvesterSelected = false;
        if (SelectedSeedPanel != null)
        {
            SelectedSeedPanel.IsVisible = false;
        }
        UpdateSeedsPanel(); // Update to remove seed highlighting
        
        _selectedLiquid = liquid;
        UpdateSelectedLiquidPanelVisibility();
        
        if (SelectedLiquidName != null)
        {
            SelectedLiquidName.Text = liquid.Name;
        }
        
        // Refresh the liquids panel to update selection highlighting
        UpdateLiquidsPanel();
        
        // Update button highlighting
        UpdateToolsPanelButtons();
        
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Selected liquid: {liquid.Name} (ID: {liquid.Id}), _selectedSeed cleared");
    }
    
    /// <summary>
    /// Handles selection of a seed item
    /// </summary>
    private void OnSeedSelected(SeedData seed)
    {
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] OnSeedSelected: {seed.Name}, current _selectedLiquid={_selectedLiquid?.Name ?? "null"}");
        
        // Clear other selections (only one thing can be selected at a time)
        _selectedLiquid = null;
        _isHarvesterSelected = false;
        if (SelectedLiquidPanel != null)
        {
            SelectedLiquidPanel.IsVisible = false;
        }
        UpdateLiquidsPanel(); // Update to remove liquid highlighting
        
        _selectedSeed = seed;
        UpdateSelectedSeedPanelVisibility();
        
        if (SelectedSeedName != null)
        {
            SelectedSeedName.Text = seed.Name;
        }
        
        // Refresh the seeds panel to update selection highlighting
        UpdateSeedsPanel();
        
        // Update button highlighting
        UpdateToolsPanelButtons();
        
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Selected seed: {seed.Name} (ID: {seed.Id}), _selectedLiquid cleared");
    }

    private void CloseAllPanels()
    {
#if ANDROID || WINDOWS
        System.Diagnostics.Debug.WriteLine("[GreenhousePage] CloseAllPanels called");
        
        if (LiquidsPanel != null && SeedsPanel != null && SelectedLiquidPanel != null && SelectedSeedPanel != null)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Before close: _selectedLiquid={_selectedLiquid?.Name ?? "null"}, _selectedSeed={_selectedSeed?.Name ?? "null"}");
            
            LiquidsPanel.IsVisible = false;
            LiquidsPanel.InputTransparent = true;
            SelectedLiquidPanel.IsVisible = false;
            
            SeedsPanel.IsVisible = false;
            SeedsPanel.InputTransparent = true;
            SelectedSeedPanel.IsVisible = false;
            
            // Clear selections when closing panels
            _selectedLiquid = null;
            _selectedSeed = null;
            
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] After clear: _selectedLiquid={_selectedLiquid?.Name ?? "null"}, _selectedSeed={_selectedSeed?.Name ?? "null"}");
            
            // Refresh panels to remove visual highlighting
            UpdateLiquidsPanel();
            UpdateSeedsPanel();
            
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] Panels refreshed after clearing selections");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] CloseAllPanels: Some panel references are null");
        }
#endif
    }
    
    /// <summary>
    /// Updates visibility of selected liquid panel based on selection state
    /// </summary>
    private void UpdateSelectedLiquidPanelVisibility()
    {
#if ANDROID
        if (SelectedLiquidPanel == null)
        {
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] UpdateSelectedLiquidPanelVisibility: SelectedLiquidPanel is null");
            return;
        }
        
        bool shouldBeVisible = _selectedLiquid != null && LiquidsPanel != null && LiquidsPanel.IsVisible;
        SelectedLiquidPanel.IsVisible = shouldBeVisible;
        
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] UpdateSelectedLiquidPanelVisibility: shouldBeVisible={shouldBeVisible}, _selectedLiquid={_selectedLiquid?.Name ?? "null"}, LiquidsPanel.IsVisible={LiquidsPanel?.IsVisible ?? false}, SelectedLiquidPanel.IsVisible={SelectedLiquidPanel.IsVisible}, SelectedLiquidPanel.Width={SelectedLiquidPanel.Width}, SelectedLiquidPanel.Height={SelectedLiquidPanel.Height}");
#elif WINDOWS
        // On Windows, always hide the selected panel (not needed, names are shown in main panel)
        if (SelectedLiquidPanel != null)
        {
            SelectedLiquidPanel.IsVisible = false;
        }
#endif
    }
    
    /// <summary>
    /// Updates visibility of selected seed panel based on selection state
    /// </summary>
    private void UpdateSelectedSeedPanelVisibility()
    {
#if ANDROID
        if (SelectedSeedPanel == null)
        {
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] UpdateSelectedSeedPanelVisibility: SelectedSeedPanel is null");
            return;
        }
        
        bool shouldBeVisible = _selectedSeed != null && SeedsPanel != null && SeedsPanel.IsVisible;
        SelectedSeedPanel.IsVisible = shouldBeVisible;
        
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] UpdateSelectedSeedPanelVisibility: shouldBeVisible={shouldBeVisible}, _selectedSeed={_selectedSeed?.Name ?? "null"}, SeedsPanel.IsVisible={SeedsPanel?.IsVisible ?? false}, SelectedSeedPanel.IsVisible={SelectedSeedPanel.IsVisible}, SelectedSeedPanel.Width={SelectedSeedPanel.Width}, SelectedSeedPanel.Height={SelectedSeedPanel.Height}");
#elif WINDOWS
        // On Windows, always hide the selected panel (not needed, names are shown in main panel)
        if (SelectedSeedPanel != null)
        {
            SelectedSeedPanel.IsVisible = false;
        }
#endif
    }
}
