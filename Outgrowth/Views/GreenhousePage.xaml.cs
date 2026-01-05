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

public partial class GreenhousePage : ContentPage
{
    // Static reference to current instance for saving from App lifecycle events
    private static GreenhousePage? _currentInstance;

    private ScreenProperties screenProps = ScreenProperties.Instance;
    
    // Current centered pot index (0=Pot1 rightmost, increases left)
    // Navigation limited to indices 1-3 (Pot2, Pot3, Pot4), starts at Pot2
    private int _currentItemIndex = 1;
    
    // Selected tool/item for planting/watering
    private LiquidData? _selectedLiquid = null;
    private SeedData? _selectedSeed = null;
    private bool _isHarvesterSelected = false;
    private bool _isLiquidsButtonSelected = false;
    private bool _isSeedsButtonSelected = false;
    
    // Per-panel animation tracking to allow multiple panels to animate concurrently
    private readonly System.Collections.Generic.HashSet<StyledPanel> _animatingPanels = new();
    
    // Debounce timer for OnPageSizeChanged
    private System.Threading.Timer? _sizeChangedDebounceTimer;
    
    // Track whether dynamic panels have been created to avoid duplicate creation
    private bool _panelsCreated = false;
    
    // Cache for panel items to avoid full recreation when only selection changes
    private List<StyledPanel>? _liquidPanelItems;
    private List<StyledPanel>? _seedPanelItems;
    // Panel containers for liquids/seeds

    // --- Dynamically created Panels ---
    private StyledPanel? _liquidsPanel;
    private AbsoluteLayout? _liquidsPanelWrapper;
    private StyledPanel? _seedsPanel;
    private AbsoluteLayout? _seedsPanelWrapper;
    // SelectedLiquidPanel and SelectedSeedPanel (Android only)
#if ANDROID
    private StyledPanel? _selectedLiquidPanel;
    private Border? _selectedLiquidPanelWrapper;
    private StyledPanel? _selectedSeedPanel;
    private Border? _selectedSeedPanelWrapper;
#endif
    private StyledPanel? _toolsPanel;
    private Microsoft.Maui.Controls.Grid? _toolsPanelContentGrid;
    private Border? _toolsPanelWrapper;
#if ANDROID
    private StyledPanel? _movePanel;
    private Border? _movePanelWrapper;
    private Microsoft.Maui.Controls.Grid? _movePanelContentGrid;
#endif
    // Container that holds bottom panel and the move panel side-by-side
    private Microsoft.Maui.Controls.Grid? _bottomRowContainer;
    // Bottom panel button references (dynamically created)
    private StyledPanel? _harvesterButton;
    private StyledPanel? _liquidsButton;
    private StyledPanel? _seedsButton;

    // Dynamically created Move Panel (Android only, replacement for removed XAML MovePanel)
#if ANDROID
    private StyledPanel? _leftArrowButton;
    private StyledPanel? _rightArrowButton;
#endif
    
    // Navigation buttons (right gutter)
    private StyledPanel? _hubButton;
    private AbsoluteLayout? _hubButtonWrapper;
    
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
        
        this.Disappearing += OnPageDisappearing;
        this.Loaded += OnPageLoaded;
        this.SizeChanged += OnPageSizeChanged;
    }
    
    private void OnPageLoaded(object? sender, EventArgs e)
    {
        // Data libraries are pre-loaded by GameDataManager at app startup
        
        CreatePotElements();
        
        // Initialize screen properties and font sizes
        var screenProps = ScreenProperties.Instance;
        if (this.Width > 0 && this.Height > 0)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] OnPageLoaded: Page size available ({this.Width}x{this.Height}), creating panels immediately");
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
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Failed setting EnvironmentWrapper.Scale: {ex.Message}"); }
                }
                EnvironmentWrapper.WidthRequest = ScreenProperties.ReferenceWidth;
                EnvironmentWrapper.HeightRequest = ScreenProperties.ReferenceHeight;
                
                EnvironmentContainer.InputTransparent = false;
                EnvironmentContainer.BackgroundColor = Colors.Transparent;
                
                EnvironmentWrapper.TranslationX = screenProps.OffsetX;
                EnvironmentWrapper.TranslationY = screenProps.OffsetY;
            }
            
            UpdatePotPositions();
            UpdateContentPosition(); // Center Pot2 on load
            
            // Create panels now that screen size is valid
            CreateDynamicPanels();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] OnPageLoaded: Page size not yet available, deferring panel creation to OnPageSizeChanged");
            // Don't call UpdateFontSizes with default 1.0 - wait for valid size in OnPageSizeChanged
            UpdatePotPositions();
            UpdateContentPosition();
        }
        
        // Load saved plants
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
            onEscape: FireAndForgetCloseAllPanels,
            onE: () => OnHubClicked(this, EventArgs.Empty)
        );
        _windowsInput.Attach();
#endif
    }
    
    /// <summary>
    /// Creates all dynamic UI panels (liquids, seeds, tools, navigation, move).
    /// Called after valid screen dimensions are available.
    /// </summary>
    private void CreateDynamicPanels()
    {
        if (_panelsCreated)
        {
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] CreateDynamicPanels: Panels already created, skipping");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine("[GreenhousePage] CreateDynamicPanels: Creating all panels");
        
        CreateLiquidsPanel();
        CreateSeedsPanel();
        CreateToolsPanel();
        CreateNavigationButtons();
        
#if ANDROID
        CreateMovePanel();
#endif
        
        _panelsCreated = true;
        System.Diagnostics.Debug.WriteLine("[GreenhousePage] CreateDynamicPanels: All panels created successfully");
    }

    // -=-=- POTS -=-=-

    private void CreatePotElements()
    {
        if (ContentContainer == null)
            return;
        
        foreach (var pot in _pots)
        {
            var visualElement = pot.CreateVisualElement();
            visualElement.ZIndex = pot.ZIndex;
            ContentContainer.Children.Add(visualElement);
        }
    }

    // Updates pot positions (container center: X=9600, Y=540, 1:1 ratio)
    private void UpdatePotPositions()
    {
        if (ContentContainer == null)
            return;
        
        const double containerCenterX = 9600.0;
        const double containerCenterY = 540.0;
        
        foreach (var pot in _pots)
        {
            pot.UpdatePosition(containerCenterX, containerCenterY);
        }
    }

    // Centers selected pot: translationOffset = screenCenter - itemCenterX
    private void UpdateContentPosition()
    {
        if (ContentContainer == null)
            return;
        
        int currentItemLogicalX = BaseItemPositions[_currentItemIndex];
        const double containerCenter = 9600.0;
        const double screenCenter = 960.0;
        
        double itemCenterX = containerCenter + currentItemLogicalX;
        double translationOffset = screenCenter - itemCenterX;
        
        ContentContainer.TranslationX = translationOffset;
    }
    
    // -=-=- PANELS -=-=-
    private void CreateLiquidsPanel()
    {
        try
        {
            var liquids = LiquidLibrary.GetAllLiquids().ToList();
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

            _liquidsPanelWrapper = new AbsoluteLayout
            {
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Fill,
                Margin = new Thickness(20, 0, 0, 0),
                ZIndex = 1000
            };
            MainGrid.SetColumn(_liquidsPanelWrapper, 0);
            MainGrid.Children.Add(_liquidsPanelWrapper);

            _liquidsPanel = new StyledPanel(
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

            if (_liquidsPanelWrapper != null)
            {
                _liquidsPanelWrapper.Children.Clear();
                _liquidsPanelWrapper.Children.Add(_liquidsPanel.Panel);
                AbsoluteLayout.SetLayoutBounds(_liquidsPanel.Panel, new Rect(0, 0.5, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
                AbsoluteLayout.SetLayoutFlags(_liquidsPanel.Panel, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.YProportional);
                _liquidsPanel.Panel.AnchorX = 0.5;
                _liquidsPanel.Panel.AnchorY = 0.5;
                _liquidsPanel.Panel.Scale = 0;
                _liquidsPanel.Panel.IsVisible = false;
                _liquidsPanel.Panel.InputTransparent = true;
            }

#if ANDROID
            // Create SelectedLiquidPanel
            try
            {
                double selectedPanelWidth = baseWidth * adaptive;
                double selectedPanelHeight = 160.0 * adaptive;

                // Create Label for selected liquid name
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
                try { selectedLabel.FontFamily = "SilkscreenBold"; } catch { }

                _selectedLiquidPanel = new StyledPanel(
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
                _selectedLiquidPanelWrapper = new Border
                {
                    Content = _selectedLiquidPanel.Panel,
                    Stroke = null,
                    StrokeThickness = 0
                };

                AbsoluteLayout.SetLayoutBounds(_selectedLiquidPanelWrapper, new Rect(0, 0.15, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
                AbsoluteLayout.SetLayoutFlags(_selectedLiquidPanelWrapper, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.XProportional | Microsoft.Maui.Layouts.AbsoluteLayoutFlags.YProportional);
                _selectedLiquidPanelWrapper.AnchorX = 0.5;
                _selectedLiquidPanelWrapper.AnchorY = 0.5;
                _selectedLiquidPanelWrapper.Scale = 0;
                _selectedLiquidPanelWrapper.IsVisible = false;
                _selectedLiquidPanelWrapper.InputTransparent = true;
                _selectedLiquidPanelWrapper.ZIndex = 1005;
                _liquidsPanelWrapper?.Children.Add(_selectedLiquidPanelWrapper);
                System.Diagnostics.Debug.WriteLine("[GreenhousePage] Created SelectedLiquidPanel");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Error creating SelectedLiquidPanel: {ex.Message}");
            }
#endif

            var contentStack = _liquidsPanel.ScrollContainer?.Content as VerticalStackLayout;
            if (contentStack == null)
            {
                System.Diagnostics.Debug.WriteLine("[GreenhousePage] CreateLiquidsPanel: No content stack available");
                return;
            }
            
            // Create all Liquid Panel Items
            contentStack.Children.Clear();

            _liquidPanelItems = new List<StyledPanel>();
            _liquidPanelItems.Clear();
            foreach (var liquid in liquids)
            {
                var liquidItem = CreateLiquidItem(liquid);
                liquidItem.ClassId = liquid.Id;
                
                // Manually add TapGestureRecognizer since onTapped parameter doesn't work for panelItems
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (s, e) => await OnLiquidSelected(liquid);
                liquidItem.Panel.GestureRecognizers.Add(tap);
                
                _liquidPanelItems.Add(liquidItem);
                contentStack.Children.Add(liquidItem.Panel);
            }
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Created {_liquidPanelItems.Count} LiquidsPanel items");
            
            // Update panel items to set initial enabled/disabled state
            UpdateLiquidsPanel();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Error creating LiquidsPanel: {ex.Message}");
            _liquidPanelItems = null;
        }
    }

    private void CreateSeedsPanel()
    {
        try
        {
            var seeds = SeedLibrary.GetAllSeeds().ToList();
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

            _seedsPanelWrapper = new AbsoluteLayout
            {
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Fill,
                Margin = new Thickness(20, 0, 0, 0),
                ZIndex = 1000
            };
            MainGrid.SetColumn(_seedsPanelWrapper, 0);
            if (!MainGrid.Children.Contains(_seedsPanelWrapper))
                MainGrid.Children.Add(_seedsPanelWrapper);

            _seedsPanel = new StyledPanel(
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

            if (_seedsPanelWrapper != null)
            {
                _seedsPanelWrapper.Children.Clear();
                _seedsPanelWrapper.Children.Add(_seedsPanel.Panel);
                AbsoluteLayout.SetLayoutBounds(_seedsPanel.Panel, new Rect(0, 0.5, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
                AbsoluteLayout.SetLayoutFlags(_seedsPanel.Panel, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.YProportional);
                _seedsPanel.Panel.AnchorX = 0.5;
                _seedsPanel.Panel.AnchorY = 0.5;
                _seedsPanel.Panel.Scale = 0;
                _seedsPanel.Panel.IsVisible = false;
                _seedsPanel.Panel.InputTransparent = true;
            }

#if ANDROID
            // Create SelectedSeedPanel
            try
            {
                double selectedPanelWidth = baseWidth * adaptive;
                double selectedPanelHeight = 160.0 * adaptive;

                // Create Label for selected seed name
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
                try { selectedLabel.FontFamily = "SilkscreenBold"; } catch { }

                _selectedSeedPanel = new StyledPanel(
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
                _selectedSeedPanelWrapper = new Border
                {
                    Content = _selectedSeedPanel.Panel,
                    Stroke = null,
                    StrokeThickness = 0
                };

                AbsoluteLayout.SetLayoutBounds(_selectedSeedPanelWrapper, new Rect(0, 0.15, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
                AbsoluteLayout.SetLayoutFlags(_selectedSeedPanelWrapper, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.XProportional | Microsoft.Maui.Layouts.AbsoluteLayoutFlags.YProportional);
                _selectedSeedPanelWrapper.AnchorX = 0.5;
                _selectedSeedPanelWrapper.AnchorY = 0.5;
                _selectedSeedPanelWrapper.Scale = 0;
                _selectedSeedPanelWrapper.IsVisible = false;
                _selectedSeedPanelWrapper.InputTransparent = true;
                _selectedSeedPanelWrapper.ZIndex = 1005;
                _seedsPanelWrapper?.Children.Add(_selectedSeedPanelWrapper);
                System.Diagnostics.Debug.WriteLine("[GreenhousePage] Created SelectedSeedPanel");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Error creating SelectedSeedPanel: {ex.Message}");
            }
#endif

            var contentStack = _seedsPanel.ScrollContainer?.Content as VerticalStackLayout;
            if (contentStack == null)
            {
                System.Diagnostics.Debug.WriteLine("[GreenhousePage] CreateSeedsPanel: No content stack available");
                return;
            }
            
            // Create all Seed Panel Items
            contentStack.Children.Clear();

            _seedPanelItems = new List<StyledPanel>();
            _seedPanelItems.Clear();
            foreach (var seed in seeds)
            {
                var seedItem = CreateSeedItem(seed);
                seedItem.ClassId = seed.Id;
                
                // Manually add TapGestureRecognizer since onTapped parameter doesn't work for panelItems
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (s, e) => await OnSeedSelected(seed);
                seedItem.Panel.GestureRecognizers.Add(tap);
                
                _seedPanelItems.Add(seedItem);
                contentStack.Children.Add(seedItem.Panel);
            }
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Created {_seedPanelItems.Count} SeedsPanel items");
            
            // Update panel items to set initial enabled/disabled state
            UpdateSeedsPanel();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Error creating SeedsPanel: {ex.Message}");
            _seedPanelItems = null;
        }
    }

    private void CreateToolsPanel()
    {
        try
        {
            var adaptive = screenProps.AdaptiveScale;

            const double baseWidth = 450.0;
            const double baseHeight = 150.0;

            double panelWidth = baseWidth * adaptive;
            double panelHeight = baseHeight * adaptive;
            double cornerSize = 40 * adaptive;

            _toolsPanel = new StyledPanel(
                width: panelWidth,
                height: panelHeight,
                cornerSize: cornerSize,
                backgroundColor: Color.FromArgb("#0f0c29"),
                borderColor: Color.FromArgb("#302b63"),
                content: null,
                cornerImage: "ui__panel_corner.png",
                horizontalEdgeImage: "ui__panel_edge_horizontal.png",
                verticalEdgeImage: "ui__panel_edge_vertical.png",
                centerImage: "ui__panel_center.png"
            );

            _toolsPanelContentGrid = new Grid
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
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star }
                }
            };

            _toolsPanelContentGrid.Children.Add(_toolsPanel.Panel);
            Grid.SetColumn(_toolsPanel.Panel, 0);
            Grid.SetColumnSpan(_toolsPanel.Panel, 7);

            CreateToolsPanelButtons();

            _toolsPanelWrapper = new Border
            {
                Content = _toolsPanelContentGrid,
                Stroke = null,
                StrokeThickness = 0
            };

            _toolsPanelWrapper.HorizontalOptions = LayoutOptions.Center;
            _toolsPanelWrapper.WidthRequest = panelWidth;
            _toolsPanelWrapper.VerticalOptions = LayoutOptions.End;
            _toolsPanelWrapper.Margin = new Thickness(0, 0, 0, 20);
            _toolsPanelWrapper.AnchorX = 0.5;
            _toolsPanelWrapper.AnchorY = 1.0;
            _toolsPanelWrapper.Scale = 1;
            _toolsPanelWrapper.IsVisible = true;
            _toolsPanelWrapper.InputTransparent = false;
            _toolsPanelWrapper.ZIndex = 1100;

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

                _bottomRowContainer.HeightRequest = panelHeight + 40;
                _bottomRowContainer.InputTransparent = false;
                _bottomRowContainer.ZIndex = 1100;
                Grid.SetColumnSpan(_bottomRowContainer, 3);
                MainGrid.Children.Add(_bottomRowContainer);
            }

            _bottomRowContainer.HeightRequest = panelHeight + 40;

            if (!_bottomRowContainer.Children.Contains(_toolsPanelWrapper))
            {
                _bottomRowContainer.Children.Add(_toolsPanelWrapper);
                Grid.SetColumn(_toolsPanelWrapper, 1);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Failed to create bottom panels placeholder: {ex.Message}");
        }
    }

    private void CreateToolsPanelButtons()
    {
        if (_toolsPanelContentGrid == null)
            return;

        try
        {
            // Clear existing buttons to prevent duplicates on page re-entry
            if (_harvesterButton != null && _toolsPanelContentGrid.Children.Contains(_harvesterButton))
                _toolsPanelContentGrid.Children.Remove(_harvesterButton);
            if (_liquidsButton != null && _toolsPanelContentGrid.Children.Contains(_liquidsButton))
                _toolsPanelContentGrid.Children.Remove(_liquidsButton);
            if (_seedsButton != null && _toolsPanelContentGrid.Children.Contains(_seedsButton))
                _toolsPanelContentGrid.Children.Remove(_seedsButton);
            
            _harvesterButton = null;
            _liquidsButton = null;
            _seedsButton = null;
            
            var adaptive = screenProps.AdaptiveScale;

            const double baseHeight = 150.0;
            double panelHeight = baseHeight * adaptive;
            double buttonSize = panelHeight * 0.7; // 70% of panel height
            double cornerSize = 40 * adaptive;

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
                var button = new StyledPanel(
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
                if (button.ContentGrid != null)
                {
                    button.ContentGrid.Children.Add(icon);
                    // Center the icon inside the grid cell
                    Grid.SetColumn(icon, 0);
                    Grid.SetRow(icon, 0);
                }

                // Ensure sizing and alignment on the outer border
                button.WidthRequest = buttonSize;
                button.HeightRequest = buttonSize;
                button.HorizontalOptions = LayoutOptions.Center;
                button.VerticalOptions = LayoutOptions.Center;

                // Attach the provided tap handler to the button
                if (tapHandler != null)
                {
                    var tap = new TapGestureRecognizer();
                    tap.Tapped += (s, e) => tapHandler?.Invoke(s, e);
                    button.GestureRecognizers.Add(tap);
                }

                return button;
            }

            if (_harvesterButton == null)
            {
                _harvesterButton = CreateButton("harvester_icon.png", OnHarvesterButtonClicked);
                _toolsPanelContentGrid.Children.Add(_harvesterButton);
                Grid.SetColumn(_harvesterButton, 1);
            }

            if (_liquidsButton == null)
            {
                _liquidsButton = CreateButton("liquid__water.png", OnLiquidsButtonClicked);
                _toolsPanelContentGrid.Children.Add(_liquidsButton);
                Grid.SetColumn(_liquidsButton, 3);
            }

            if (_seedsButton == null)
            {
                _seedsButton = CreateButton("seeds__lumivial.png", OnSeedsButtonClicked);
                _toolsPanelContentGrid.Children.Add(_seedsButton);
                Grid.SetColumn(_seedsButton, 5);
            }

            System.Diagnostics.Debug.WriteLine("[GreenhousePage] Created tools panel buttons dynamically");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Failed to create tools panel buttons: {ex.Message}");
        }
    }

#if ANDROID
    private void CreateMovePanel()
    {
        try
        {
            var adaptive = screenProps.AdaptiveScale;

            const double baseWidth = 300.0;
            const double baseHeight = 150.0;

            double panelWidth = baseWidth * adaptive;
            double panelHeight = baseHeight * adaptive;
            double cornerSize = 40 * adaptive;

            _movePanel = new StyledPanel(
                width: panelWidth,
                height: panelHeight,
                cornerSize: cornerSize,
                backgroundColor: Color.FromArgb("#0f0c29"),
                borderColor: Color.FromArgb("#302b63"),
                content: null,
                cornerImage: "ui__panel_corner.png",
                horizontalEdgeImage: "ui__panel_edge_horizontal.png",
                verticalEdgeImage: "ui__panel_edge_vertical.png",
                centerImage: "ui__panel_center.png"
            );

            _movePanelContentGrid = new Grid
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

            _movePanelContentGrid.Children.Add(_movePanel.Panel);
            Grid.SetColumn(_movePanel.Panel, 0);
            Grid.SetColumnSpan(_movePanel.Panel, 5);
            CreateMovePanelButtons();

            _movePanelWrapper = new Border
            {
                Content = _movePanelContentGrid,
                Stroke = null,
                StrokeThickness = 0
            };

            _movePanelWrapper.HorizontalOptions = LayoutOptions.End;
            _movePanelWrapper.WidthRequest = panelWidth;
            _movePanelWrapper.VerticalOptions = LayoutOptions.End;
            _movePanelWrapper.Margin = new Thickness(0, 0, 20, 20);
            _movePanelWrapper.AnchorX = 1.0;
            _movePanelWrapper.AnchorY = 1.0;
            _movePanelWrapper.Scale = 1;
            _movePanelWrapper.IsVisible = true;
            _movePanelWrapper.InputTransparent = false;
            _movePanelWrapper.ZIndex = 1500;

            if (_bottomRowContainer != null)
            {
                if (!_bottomRowContainer.Children.Contains(_movePanelWrapper))
                {
                    _bottomRowContainer.Children.Add(_movePanelWrapper);
                    Grid.SetColumn(_movePanelWrapper, 2);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Failed to create move panel: {ex.Message}");
        }
    }

    private void CreateMovePanelButtons()
    {
        if (_movePanelContentGrid == null)
            return;

        try
        {
            // Clear existing buttons to prevent duplicates on page re-entry
            if (_leftArrowButton != null && _movePanelContentGrid.Children.Contains(_leftArrowButton))
                _movePanelContentGrid.Children.Remove(_leftArrowButton);
            if (_rightArrowButton != null && _movePanelContentGrid.Children.Contains(_rightArrowButton))
                _movePanelContentGrid.Children.Remove(_rightArrowButton);
            
            _leftArrowButton = null;
            _rightArrowButton = null;
            
            var adaptive = screenProps.AdaptiveScale;

            const double baseHeight = 150.0;
            double panelHeight = baseHeight * adaptive;
            double buttonSize = panelHeight * 0.7; // 70% of panel height
            double cornerSize = 40 * adaptive;

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
                var button = new StyledPanel(
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
                if (button.ContentGrid != null)
                {
                    button.ContentGrid.Children.Add(icon);
                    // Center the icon inside the grid cell
                    Grid.SetColumn(icon, 0);
                    Grid.SetRow(icon, 0);
                }

                // Ensure sizing and alignment on the outer border
                button.WidthRequest = buttonSize;
                button.HeightRequest = buttonSize;
                button.HorizontalOptions = LayoutOptions.Center;
                button.VerticalOptions = LayoutOptions.Center;

                return button;
            }

            if (_leftArrowButton == null)
            {
                _leftArrowButton = CreateButton("◄", OnLeftArrowButtonClicked);
                var tapSel = new TapGestureRecognizer();
                tapSel.Tapped += OnLeftArrowButtonClicked;
                _leftArrowButton.GestureRecognizers.Add(tapSel);
                _movePanelContentGrid.Children.Add(_leftArrowButton);
                Grid.SetColumn(_leftArrowButton, 1);
            }

            if (_rightArrowButton == null)
            {
                _rightArrowButton = CreateButton("►", OnRightArrowButtonClicked);
                var tapSel = new TapGestureRecognizer();
                tapSel.Tapped += OnRightArrowButtonClicked;
                _rightArrowButton.GestureRecognizers.Add(tapSel);
                _movePanelContentGrid.Children.Add(_rightArrowButton);
                Grid.SetColumn(_rightArrowButton, 3);
            }

            System.Diagnostics.Debug.WriteLine("[GreenhousePage] Created move panel buttons dynamically");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Failed to create move panel buttons: {ex.Message}");
        }
    }
#endif

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
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Fill,
                Margin = new Thickness(0, 0, 20, 0),
                ZIndex = 1000
            };


            MainGrid.SetColumn(_hubButtonWrapper, 2);
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
                Source = "hub_icon.png", // Replace with actual hub icon if available
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
                    Text = "E",
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
                tooltipLabel.FontSize = 60 * adaptive;
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

            System.Diagnostics.Debug.WriteLine("[GreenhousePage] Created navigation buttons dynamically");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Failed to create navigation buttons: {ex.Message}");
        }
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
        // Save plants before leaving the page
        SavePlants();
        
        // Clear all flags and selections
        _isHarvesterSelected = false;
        _isLiquidsButtonSelected = false;
        _isSeedsButtonSelected = false;
        _selectedLiquid = null;
        _selectedSeed = null;
        
        CleanupPotElements();
        CleanupDynamicPanels();
        
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
    }
    
    private void CleanupDynamicPanels()
    {
        try
        {
            // Remove liquids panel wrapper from MainGrid
            if (_liquidsPanelWrapper != null && MainGrid.Children.Contains(_liquidsPanelWrapper))
            {
                MainGrid.Children.Remove(_liquidsPanelWrapper);
            }
            
            // Remove seeds panel wrapper from MainGrid
            if (_seedsPanelWrapper != null && MainGrid.Children.Contains(_seedsPanelWrapper))
            {
                MainGrid.Children.Remove(_seedsPanelWrapper);
            }
            
            // Remove bottom row container from MainGrid
            if (_bottomRowContainer != null && MainGrid.Children.Contains(_bottomRowContainer))
            {
                MainGrid.Children.Remove(_bottomRowContainer);
            }
            
            // Clear panel item caches
            _liquidPanelItems?.Clear();
            _seedPanelItems?.Clear();
            
            // Reset panel creation flag
            _panelsCreated = false;
            
            // Nullify all panel references
            _liquidsPanel = null;
            _liquidsPanelWrapper = null;
            _seedsPanel = null;
            _seedsPanelWrapper = null;
            _toolsPanel = null;
            _toolsPanelContentGrid = null;
            _toolsPanelWrapper = null;
            _bottomRowContainer = null;
            _harvesterButton = null;
            _liquidsButton = null;
            _seedsButton = null;
            _liquidPanelItems = null;
            _seedPanelItems = null;
            _hubButton = null;
            _hubButtonWrapper = null;
            
#if ANDROID
            _selectedLiquidPanel = null;
            _selectedLiquidPanelWrapper = null;
            _selectedSeedPanel = null;
            _selectedSeedPanelWrapper = null;
            _movePanel = null;
            _movePanelWrapper = null;
            _movePanelContentGrid = null;
            _leftArrowButton = null;
            _rightArrowButton = null;
#endif
            
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] Cleaned up all dynamic panels");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Error cleaning up dynamic panels: {ex.Message}");
        }
    }
    
    private void CleanupPotElements()
    {
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
    
    private void UpdateLiquidsPanel()
    {
        // Ensure each panel item reflects current selection state and enabled state (no animation)
        if (_liquidPanelItems == null)
        {
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] UpdateLiquidsPanel: _liquidPanelItems is null");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] UpdateLiquidsPanel: Updating {_liquidPanelItems.Count} items");
        
        foreach (var panel in _liquidPanelItems)
        {
            try
            {
                bool isSelected = _selectedLiquid != null && panel.ClassId == _selectedLiquid.Id;
                panel.SetPanelSelected(isSelected, animate: false);
                
                // Check if material quantity is > 0 and enable/disable panel item accordingly
                if (panel.BindingContext is LiquidData liquid)
                {
                    System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Liquid {liquid.Name} (ClassId: {panel.ClassId}): Quantity={liquid.Quantity}, setting enabled={liquid.Quantity > 0}");
                    panel.SetPanelItemEnabled(liquid.Quantity > 0);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Panel ClassId={panel.ClassId} has no LiquidData in BindingContext");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GreenhousePage] UpdateLiquidsPanel: failed updating panel {panel.ClassId}: {ex.Message}");
            }
        }
    }
    
    private void UpdateSeedsPanel()
    {
        // Ensure each panel item reflects current selection state and enabled state (no animation)
        if (_seedPanelItems == null)
        {
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] UpdateSeedsPanel: _seedPanelItems is null");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] UpdateSeedsPanel: Updating {_seedPanelItems.Count} items");
        
        foreach (var panel in _seedPanelItems)
        {
            try
            {
                bool isSelected = _selectedSeed != null && panel.ClassId == _selectedSeed.Id;
                panel.SetPanelSelected(isSelected, animate: false);
                
                // Check if material quantity is > 0 and enable/disable panel item accordingly
                if (panel.BindingContext is SeedData seed)
                {
                    System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Seed {seed.Name} (ClassId: {panel.ClassId}): Quantity={seed.Quantity}, setting enabled={seed.Quantity > 0}");
                    panel.SetPanelItemEnabled(seed.Quantity > 0);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Panel ClassId={panel.ClassId} has no SeedData in BindingContext");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GreenhousePage] UpdateSeedsPanel: failed updating panel {panel.ClassId}: {ex.Message}");
            }
        }
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        // Debounce: cancel previous timer and start a new one
        _sizeChangedDebounceTimer?.Dispose();
        _sizeChangedDebounceTimer = new System.Threading.Timer(_ =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                PerformPageSizeUpdate();
            });
        }, null, 300, Timeout.Infinite);
    }

    private void PerformPageSizeUpdate()
    {
        if (EnvironmentWrapper != null && EnvironmentContainer != null && ContentContainer != null)
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
                
                var adaptive = screenProps.AdaptiveScale;
                screenProps.UpdateFontSizes(adaptive);
                
                UpdatePotPositions();
                UpdateContentPosition();
                
                // Create panels if they haven't been created yet (deferred from OnPageLoaded)
                if (!_panelsCreated)
                {
                    System.Diagnostics.Debug.WriteLine($"[GreenhousePage] PerformPageSizeUpdate: Creating deferred panels with valid screen size ({pageWidth}x{pageHeight})");
                    CreateDynamicPanels();
                }
            }
        }
    }

    private void UpdateBottomPanel(double adaptiveScale)
    {
        
    }
    
    /// <summary>
    /// Creates a UI element for a liquid item
    /// </summary>
    private StyledPanel CreateLiquidItem(LiquidData liquid)
    {
        bool isSelected = _selectedLiquid?.Id == liquid.Id;
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] CreateLiquidItem: {liquid.Name}, isSelected={isSelected}, _selectedLiquid={_selectedLiquid?.Name ?? "null"}");
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
            id: liquid.Id,
            name: liquid.Name,
            sprite: liquid.Sprite,
            isSelected: isSelected,
            panelItemHeight: panelItemHeight,
            qtyFontSize: qtySize,
            bodyFontSize: bodySize,
            isAndroid: isAndroid,
            bindingContext: liquid);
        panelItem.ClassId = liquid.Id;
        panelItem.BindingContext = liquid; // Set BindingContext on the panel itself for access in Update methods
        return panelItem;
    }
    
    /// <summary>
    /// Creates a UI element for a seed item
    /// </summary>
    private StyledPanel CreateSeedItem(SeedData seed)
    {
        bool isSelected = _selectedSeed?.Id == seed.Id;
        System.Diagnostics.Debug.WriteLine($"[GreenhousePage] CreateSeedItem: {seed.Name}, isSelected={isSelected}, _selectedSeed={_selectedSeed?.Name ?? "null"}");
#if ANDROID
        bool isAndroid = true;
#else
        bool isAndroid = false;
#endif
        var adaptive = screenProps.AdaptiveScale;
        var appRes = Application.Current?.Resources;
        // Use resource values directly — they are already scaled by UpdateFontSizes
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
            id: seed.Id,
            name: seed.Name,
            sprite: seed.Sprite,
            isSelected: isSelected,
            panelItemHeight: panelItemHeight,
            qtyFontSize: qtySize,
            bodyFontSize: bodySize,
            isAndroid: isAndroid,
            bindingContext: seed);
        panelItem.ClassId = seed.Id;
        panelItem.BindingContext = seed; // Set BindingContext on the panel itself for access in Update methods
        return panelItem;
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
        try
        {
            if (_animatingPanels.Count > 0)
                System.Diagnostics.Debug.WriteLine("[GreenhousePage] OnLiquidsButtonClicked: other panel animations in progress");

            System.Diagnostics.Debug.WriteLine("[GreenhousePage] OnLiquidsButtonClicked called");

            // Toggle liquids panel using dedicated flag
            _isLiquidsButtonSelected = !_isLiquidsButtonSelected;
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Liquids button selected state: {_isLiquidsButtonSelected}");
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Button states - harvester:{_harvesterButton != null}, liquids:{_liquidsButton != null}, seeds:{_seedsButton != null}");

            // Update button selection states FIRST, before any async operations
            if (_isLiquidsButtonSelected)
            {
                // Opening liquids panel
                System.Diagnostics.Debug.WriteLine("[GreenhousePage] Opening liquids panel - setting buttons");
                _harvesterButton?.SetPanelSelected(false, animate: true);
                _liquidsButton?.SetPanelSelected(true, animate: true);
                _seedsButton?.SetPanelSelected(false, animate: true);
                _isSeedsButtonSelected = false;
                _isHarvesterSelected = false;
            }
            else
            {
                // Closing liquids panel
                System.Diagnostics.Debug.WriteLine("[GreenhousePage] Closing liquids panel - deselecting button");
                _liquidsButton?.SetPanelSelected(false, animate: true);
            }

            // Close seeds panel with animation (if open)
#if ANDROID
            await Task.WhenAll(
            ClosePanel(true, _seedsPanel),
            ClosePanel(true, _selectedSeedPanel)
            );
#else
            await ClosePanel(true, _seedsPanel);
#endif

            // Toggle liquids panel
            if (_isLiquidsButtonSelected)
            {
                // Open liquids panel with animation
#if ANDROID
                await Task.WhenAll(
                OpenPanel(true, _liquidsPanel),
                OpenPanel(true, _selectedLiquidPanel)
                );
#else
                await OpenPanel(true, _liquidsPanel);
#endif
            }
            else
            {
                // Close liquids panel with animation
#if ANDROID
                await Task.WhenAll(
                ClosePanel(true, _liquidsPanel),
                ClosePanel(true, _selectedLiquidPanel)
                );
#else
                await ClosePanel(true, _liquidsPanel);
#endif
            }

            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Liquids panel toggled");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Exception in OnLiquidsButtonClicked: {ex.Message}");
        }
    }

    private async void OnSeedsButtonClicked(object? sender, EventArgs e)
    {
        try
        {
            if (_animatingPanels.Count > 0)
                System.Diagnostics.Debug.WriteLine("[GreenhousePage] OnSeedsButtonClicked: other panel animations in progress");
            
            System.Diagnostics.Debug.WriteLine("[GreenhousePage] OnSeedsButtonClicked called");

            // Toggle seeds panel using dedicated flag
            _isSeedsButtonSelected = !_isSeedsButtonSelected;
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Seeds button selected state: {_isSeedsButtonSelected}");
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Button states - harvester:{_harvesterButton != null}, liquids:{_liquidsButton != null}, seeds:{_seedsButton != null}");
            
            // Update button selection states FIRST, before any async operations
            if (_isSeedsButtonSelected)
            {
                // Opening seeds panel
                System.Diagnostics.Debug.WriteLine("[GreenhousePage] Opening seeds panel - setting buttons");
                _harvesterButton?.SetPanelSelected(false, animate: true);
                _liquidsButton?.SetPanelSelected(false, animate: true);
                _seedsButton?.SetPanelSelected(true, animate: true);
                _isLiquidsButtonSelected = false;
                _isHarvesterSelected = false;
            }
            else
            {
                // Closing seeds panel
                System.Diagnostics.Debug.WriteLine("[GreenhousePage] Closing seeds panel - deselecting button");
                _seedsButton?.SetPanelSelected(false, animate: true);
            }
            
            // Close liquids panel with animation (if open)
#if ANDROID
            await Task.WhenAll(
            ClosePanel(true, _liquidsPanel),
            ClosePanel(true, _selectedLiquidPanel)
            );
#else
            await ClosePanel(true, _liquidsPanel);
#endif
            
            // Toggle seeds panel
            if (_isSeedsButtonSelected)
            {
                // Open seeds panel with animation
#if ANDROID
                await Task.WhenAll(
                OpenPanel(true, _seedsPanel),
                OpenPanel(true, _selectedSeedPanel)
                );
#else
                await OpenPanel(true, _seedsPanel);
#endif
            }
            else
            {
                // Close seeds panel with animation
#if ANDROID
                await Task.WhenAll(
                ClosePanel(true, _seedsPanel),
                ClosePanel(true, _selectedSeedPanel)
                );
#else
                await ClosePanel(true, _seedsPanel);
#endif
            }
            
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Seeds panel toggled");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Exception in OnSeedsButtonClicked: {ex.Message}");
        }
    }

    private async void OnHarvesterButtonClicked(object? sender, EventArgs e)
    {
        try
        {
            if (_animatingPanels.Count > 0)
                System.Diagnostics.Debug.WriteLine("[GreenhousePage] OnHarvesterButtonClicked: other panel animations in progress");

            System.Diagnostics.Debug.WriteLine("[GreenhousePage] OnHarvesterButtonClicked called");
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Button states - harvester:{_harvesterButton != null}, liquids:{_liquidsButton != null}, seeds:{_seedsButton != null}");
            
            // Toggle harvester selection
            _isHarvesterSelected = !_isHarvesterSelected;
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Harvester selected: {_isHarvesterSelected}");
            
            // Clear liquid and seed selections when harvester is selected
            if (_isHarvesterSelected)
            {
                _selectedLiquid = null;
                _selectedSeed = null;
                _isLiquidsButtonSelected = false;
                _isSeedsButtonSelected = false;
                System.Diagnostics.Debug.WriteLine("[GreenhousePage] Calling SetPanelSelected on buttons (harvester active)");
                _harvesterButton?.SetPanelSelected(true, animate: true);
                System.Diagnostics.Debug.WriteLine("[GreenhousePage] harvesterButton SetPanelSelected(true) done");
                _liquidsButton?.SetPanelSelected(false, animate: true);
                System.Diagnostics.Debug.WriteLine("[GreenhousePage] liquidsButton SetPanelSelected(false) done");
                _seedsButton?.SetPanelSelected(false, animate: true);
                System.Diagnostics.Debug.WriteLine("[GreenhousePage] seedsButton SetPanelSelected(false) done");

                // Close panels with animation
                await CloseAllPanels();
            }
            else
            {
                _harvesterButton?.SetPanelSelected(false, animate: true);
            }
            
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Harvester selected: {_isHarvesterSelected}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GreenhousePage] Exception in OnHarvesterButtonClicked: {ex.Message}");
        }
    }

    private void OnLeftArrowButtonClicked(object? sender, EventArgs e)
    {
        // Move left (max = pots.Count - 2)
        if (_currentItemIndex < _pots.Count - 2)
        {
            _currentItemIndex++;
            UpdateContentPosition();
        }
    }

    private void OnRightArrowButtonClicked(object? sender, EventArgs e)
    {
        // Move right (min = 1, Pot2)
        if (_currentItemIndex > 1)
        {
            _currentItemIndex--;
            UpdateContentPosition();
        }
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
    
    // ============================================================================
    // Item Selection Handlers
    // ============================================================================
    
    private async Task OnLiquidSelected(LiquidData liquid)
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
#if ANDROID
            await ClosePanel(true, _selectedLiquidPanel);
#endif
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
#if ANDROID
        await OpenPanel(true, _selectedLiquidPanel);
#endif
    }
    
    private async Task OnSeedSelected(SeedData seed)
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
#if ANDROID
            await ClosePanel(true, _selectedSeedPanel);
#endif
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
#if ANDROID
        await OpenPanel(true, _selectedSeedPanel);
#endif
    }

    // ============================================================================
    // Panel Animation Methods
    // ============================================================================
    
    private async Task OpenPanel(bool animate = true, StyledPanel? panel = null)
    {
        if (panel == null)
            return;
        if (_animatingPanels.Contains(panel))
            return;

#if ANDROID
        // For selected panels on Android, animate the wrapper instead
        View? targetView = null;
        if (panel == _selectedLiquidPanel && _selectedLiquidPanelWrapper != null)
            targetView = _selectedLiquidPanelWrapper;
        else if (panel == _selectedSeedPanel && _selectedSeedPanelWrapper != null)
            targetView = _selectedSeedPanelWrapper;
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
            if(panel == _liquidsPanel)
                UpdateLiquidsPanel();
            if(panel == _seedsPanel)
                UpdateSeedsPanel();

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
            if(panel == _liquidsPanel)
                UpdateLiquidsPanel();
            if(panel == _seedsPanel)
                UpdateSeedsPanel();
        }
    }

    private async Task ClosePanel(bool animate = true, StyledPanel? panel = null)
    {
        if (panel == null)
            return;
        if (_animatingPanels.Contains(panel))
            return;

#if ANDROID
        // For selected panels on Android, animate the wrapper instead
        View? targetView = null;
        if (panel == _selectedLiquidPanel && _selectedLiquidPanelWrapper != null)
            targetView = _selectedLiquidPanelWrapper;
        else if (panel == _selectedSeedPanel && _selectedSeedPanelWrapper != null)
            targetView = _selectedSeedPanelWrapper;
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
        _isLiquidsButtonSelected = false;
        _isSeedsButtonSelected = false;
        _liquidsButton?.SetPanelSelected(false, animate: true);
        _seedsButton?.SetPanelSelected(false, animate: true);
#if ANDROID
        await Task.WhenAll(
            ClosePanel(true, _liquidsPanel),
            ClosePanel(true, _seedsPanel),
            ClosePanel(true, _selectedLiquidPanel),
            ClosePanel(true, _selectedSeedPanel)
        );
#else
        await Task.WhenAll(
            ClosePanel(true, _liquidsPanel),
            ClosePanel(true, _seedsPanel)
        );
#endif
    }

    // Helper to call CloseAllPanels from a synchronous Action without CS4014 warnings
    private void FireAndForgetCloseAllPanels()
    {
#pragma warning disable CS4014
        CloseAllPanels();
#pragma warning restore CS4014
    }
}
