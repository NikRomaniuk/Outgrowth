using Outgrowth.ViewModels;
using Outgrowth.Models;
using Outgrowth.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Layouts;
#if WINDOWS
using Outgrowth.Platforms.Windows;
#endif

namespace Outgrowth.Views;

public partial class HubPage : ContentPage
{
    private ScreenProperties screenProps = ScreenProperties.Instance;
    
    // Station objects - created dynamically on page load
    private readonly List<StationObject> _stationObjects = new()
    {
        new StationObject("hub__holo_market.png", -480, 0, 320, 320),
        new StationObject("hub__holo_quests.png", 0, 160, 320, 320),
        new StationObject("hub__holo_stats.png", 480, 0, 320, 320)
    };
    
    // Furniture objects - created dynamically on page load
    private readonly List<FurnitureObject> _furnitureObjects = new()
    {
        new FurnitureObject("HoloSpotlight1", -480, (0 - 160), 320, 320, "hub__holo_spotlight.png"),
        new FurnitureObject("HoloSpotlight2", 0, (160 - 160), 320, 320, "hub__holo_spotlight.png"),
        new FurnitureObject("HoloSpotlight3", 480, (0 - 160), 320, 320, "hub__holo_spotlight.png")
    };
    
    // Per-panel animation tracking to allow multiple panels to animate concurrently
    private readonly System.Collections.Generic.HashSet<StyledPanel> _animatingPanels = new();
    
    // Track whether dynamic panels have been created to avoid duplicate creation
    private bool _panelsCreated = false;
    
    // --- Dynamically created Panels ---
    private StyledPanel? _marketPanel;
    private AbsoluteLayout? _marketPanelWrapper;
    
    private StyledPanel? _questPanel;
    private AbsoluteLayout? _questPanelWrapper;
    
    private StyledPanel? _statsPanel;
    private AbsoluteLayout? _statsPanelWrapper;
    
    // Navigation buttons (left and right gutters)
    private StyledPanel? _greenhouseButton;
    private AbsoluteLayout? _greenhouseButtonWrapper;
    private StyledPanel? _laboratoryButton;
    private AbsoluteLayout? _laboratoryButtonWrapper;
    
#if WINDOWS
    private WindowsInput? _windowsInput;
#endif
    
    public HubPage()
    {
        InitializeComponent();
        BindingContext = new HubViewModel();
        
        // Set up station object click handlers
        _stationObjects[0].Clicked += (s, e) => OnMarketClicked(s ?? this, e);
        _stationObjects[0].InteractAction = () => System.Diagnostics.Debug.WriteLine("Market interacted");
        
        _stationObjects[1].Clicked += (s, e) => OnQuestClicked(s ?? this, e);
        _stationObjects[1].InteractAction = () => System.Diagnostics.Debug.WriteLine("Quest Console interacted");
        
        _stationObjects[2].Clicked += (s, e) => OnStatsClicked(s ?? this, e);
        _stationObjects[2].InteractAction = () => System.Diagnostics.Debug.WriteLine("Statistics interacted");
        
        // Handle page size changes for scaling
        this.SizeChanged += OnPageSizeChanged;
        
        // Close panels when navigating away
        this.Disappearing += OnPageDisappearing;
        
        // Update element positions on load
        this.Loaded += OnPageLoaded;
    }
    
    private void OnPageLoaded(object? sender, EventArgs e)
    {
        // Create station objects and add to environment
        CreateStationObjects();
        // Initialize element positions from absolute coordinates
        UpdateStationPositions();
        
        // Create furniture objects (future feature)
        CreateFurnitureObjects();
        UpdateFurniturePositions();
        
        // Initialize screen properties
        var screenProps = ScreenProperties.Instance;
        screenProps.UpdateScreenProperties(this.Width, this.Height);
        
        // Create dynamic panels after screen properties are set
        if (this.Width > 0 && this.Height > 0)
        {
            // Update screen properties and scaling
            screenProps.UpdateScreenProperties(this.Width, this.Height);
            
            if (EnvironmentWrapper != null && EnvironmentContainer != null)
            {
                // Apply scaling to environment
                double scale = screenProps.Scale;
                EnvironmentWrapper.Scale = scale;
                
                if (EnvironmentWrapper.Handler != null)
                {
                    // Force layout update after scaling
                    EnvironmentWrapper.InvalidateMeasure();
                }
                
                // Update positions with scaling applied
                UpdateStationPositions();
                UpdateFurniturePositions();
            }
            
            // Create all dynamic panels (market, quest, stats)
            CreateDynamicPanels();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("HubPage: Invalid dimensions on load, panels not created yet");
        }
        
#if WINDOWS
        // Attach Windows keyboard input handler (only Esc for closing panels)
        _windowsInput = new WindowsInput(
            onLeftArrow: () => { }, // No action
            onRightArrow: () => { }, // No action
            onEscape: CloseAllPanels, // Close panels with Esc key
            onE: () => OnLaboratoryClicked(this, EventArgs.Empty),
            onQ: () => OnGreenhouseClicked(this, EventArgs.Empty)
        );
        _windowsInput.Attach();
#endif
    }
    
    /// <summary>
    /// Creates all dynamic UI panels (market, quest, stats).
    /// Called after valid screen dimensions are available.
    /// </summary>
    private void CreateDynamicPanels()
    {
        if (_panelsCreated)
        {
            System.Diagnostics.Debug.WriteLine("HubPage: Panels already created, skipping.");
            return;
        }

        CreateMarketPanel();
        CreateQuestPanel();
        CreateStatsPanel();
        CreateNavigationButtons();
        
        _panelsCreated = true;
        System.Diagnostics.Debug.WriteLine("HubPage: Dynamic panels created successfully");
    }
    
    private void OnPageDisappearing(object? sender, EventArgs e)
    {
        // Close all panels before leaving page
        if (_marketPanel != null && _marketPanel.Panel.IsVisible)
        {
            _marketPanel.Panel.IsVisible = false;
        }
        if (_questPanel != null && _questPanel.Panel.IsVisible)
        {
            _questPanel.Panel.IsVisible = false;
        }
        if (_statsPanel != null && _statsPanel.Panel.IsVisible)
        {
            _statsPanel.Panel.IsVisible = false;
        }
        
        // Cleanup dynamic panels and objects
        CleanupDynamicPanels();
        CleanupNavigationButtons();
        CleanupStationObjects();
        CleanupFurnitureObjects();
        
#if WINDOWS
        // Detach Windows keyboard input handler
        _windowsInput?.Detach();
        _windowsInput = null;
#endif
    }
    
    
    private void CleanupDynamicPanels()
    {
        // Remove market panel from grid
        if (_marketPanel != null && _marketPanelWrapper != null)
        {
            if (this.Content is Grid mainGrid && mainGrid.Children.Contains(_marketPanelWrapper))
            {
                mainGrid.Children.Remove(_marketPanelWrapper);
            }
            _marketPanel = null;
            _marketPanelWrapper = null;
        }
        
        // Remove quest panel from grid
        if (_questPanel != null && _questPanelWrapper != null)
        {
            if (this.Content is Grid mainGrid && mainGrid.Children.Contains(_questPanelWrapper))
            {
                mainGrid.Children.Remove(_questPanelWrapper);
            }
            _questPanel = null;
            _questPanelWrapper = null;
        }
        
        // Remove stats panel from grid
        if (_statsPanel != null && _statsPanelWrapper != null)
        {
            if (this.Content is Grid mainGrid && mainGrid.Children.Contains(_statsPanelWrapper))
            {
                mainGrid.Children.Remove(_statsPanelWrapper);
            }
            _statsPanel = null;
            _statsPanelWrapper = null;
        }
        
        _panelsCreated = false;
        System.Diagnostics.Debug.WriteLine("HubPage: Dynamic panels cleaned up");
    }
    
    /// <summary>
    /// Cleans up navigation buttons to prevent memory leaks
    /// </summary>
    private void CleanupNavigationButtons()
    {
        // Remove greenhouse button from main grid
        if (_greenhouseButton != null && _greenhouseButtonWrapper != null)
        {
            if (this.Content is Grid mainGrid && mainGrid.Children.Contains(_greenhouseButtonWrapper))
            {
                mainGrid.Children.Remove(_greenhouseButtonWrapper);
            }
            _greenhouseButton = null;
            _greenhouseButtonWrapper = null;
        }
        
        // Remove laboratory button from main grid
        if (_laboratoryButton != null && _laboratoryButtonWrapper != null)
        {
            if (this.Content is Grid mainGrid && mainGrid.Children.Contains(_laboratoryButtonWrapper))
            {
                mainGrid.Children.Remove(_laboratoryButtonWrapper);
            }
            _laboratoryButton = null;
            _laboratoryButtonWrapper = null;
        }
        
        System.Diagnostics.Debug.WriteLine("HubPage: Navigation buttons cleaned up");
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

    // -=-=- FURNITURE OBJECTS -=-=-
    
    /// <summary>
    /// Creates furniture object UI elements and adds them to EnvironmentContainer (future feature)
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
        
        double containerCenterX = 960;
        double containerCenterY = 540;
        
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
        
        // Remove all furniture elements from container
        foreach (var furniture in _furnitureObjects)
        {
            if (furniture.VisualElement != null && EnvironmentContainer.Children.Contains(furniture.VisualElement))
            {
                EnvironmentContainer.Children.Remove(furniture.VisualElement);
            }
        }
#endif
    }

    // -=-=- PANELS -=-=-
    
    private void CreateMarketPanel()
    {  
        var adaptive = screenProps.AdaptiveScale;
        double panelWidth = 1200 * adaptive;
        double panelHeight = 800 * adaptive;
        double cornerSize = 40 * adaptive;

        screenProps?.UpdateFontSizes(adaptive);
        
        // Create scroll-type panel with 9-slice styling
        _marketPanel = new StyledPanel(
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
        
        // Create wrapper for positioning and animations
        _marketPanelWrapper = new AbsoluteLayout
        {
            BackgroundColor = Colors.Transparent,
            IsVisible = false,
            ZIndex = 500,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        
        // Add tap gesture to wrapper background (closes panel)
        var wrapperTapGesture = new TapGestureRecognizer();
        wrapperTapGesture.Tapped += OnMarketPanelTapped;
        _marketPanelWrapper.GestureRecognizers.Add(wrapperTapGesture);
        
        // Configure panel for animation
        _marketPanel.AnchorX = 0.5;
        _marketPanel.AnchorY = 0.5;
        _marketPanel.Scale = 0;
        
        // Position panel at center of screen
        AbsoluteLayout.SetLayoutBounds(_marketPanel, new Rect(0.5, 0.5, panelWidth, panelHeight));
        AbsoluteLayout.SetLayoutFlags(_marketPanel, AbsoluteLayoutFlags.PositionProportional);
        
        // Add panel border tap gesture (stops propagation)
        var borderTapGesture = new TapGestureRecognizer();
        borderTapGesture.Tapped += OnMarketBorderTapped;
        _marketPanel.GestureRecognizers.Add(borderTapGesture);
        
        // Add title and placeholder content to scroll container
        if (_marketPanel.ScrollContainer?.Content is VerticalStackLayout scrollContent)
        {
            scrollContent.Children.Clear();
            
            var title = new Label
            {
                Text = "📦 Market Panel",
                FontSize = 50 * adaptive,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 0, 0, 20)
            };
            
            var placeholder = new Label
            {
                Text = "Trading and market features coming soon...",
                FontSize = 30 * adaptive,
                TextColor = Colors.White,
                Opacity = 0.7,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 40, 0, 0)
            };
            
            try
            {
                var appRes = Application.Current?.Resources;
                if (appRes != null && appRes.ContainsKey("ResourcePanelTitleFont"))
                    title.FontFamily = (string)appRes["ResourcePanelTitleFont"];
                else if (Resources != null && Resources.ContainsKey("ResourcePanelTitleFont"))
                    title.FontFamily = (string)Resources["ResourcePanelTitleFont"];
                if (appRes != null && appRes.ContainsKey("ResourcePanelBodyFont"))
                    placeholder.FontFamily = (string)appRes["ResourcePanelBodyFont"];
                else if (Resources != null && Resources.ContainsKey("ResourcePanelBodyFont"))
                    placeholder.FontFamily = (string)Resources["ResourcePanelBodyFont"];
            }
            catch { }
            // Fallback to known bundled font if resources are not available
            if (string.IsNullOrEmpty(title.FontFamily))
                title.FontFamily = "SilkscreenBold";
            if (string.IsNullOrEmpty(placeholder.FontFamily))
                placeholder.FontFamily = "SilkscreenRegular";

            scrollContent.Children.Add(title);
            scrollContent.Children.Add(placeholder);
            
            // Future: Add market items here
            // foreach (var item in marketItems)
            // {
            //     scrollContent.Children.Add(CreateMarketItem(item));
            // }
        }
        
        _marketPanelWrapper.Children.Add(_marketPanel);
        
        // Add wrapper to main grid (spans all columns)
        Grid.SetColumnSpan(_marketPanelWrapper, 3);
        MainGrid.Children.Add(_marketPanelWrapper);
        
        System.Diagnostics.Debug.WriteLine("HubPage: Market panel created");
    }
    
    private void CreateQuestPanel()
    {  
        var adaptive = screenProps.AdaptiveScale;
        double panelWidth = 1200 * adaptive;
        double panelHeight = 800 * adaptive;
        double cornerSize = 40 * adaptive;

        screenProps?.UpdateFontSizes(adaptive);
        
        // Create scroll-type panel with 9-slice styling
        _questPanel = new StyledPanel(
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
        
        // Create wrapper for positioning and animations
        _questPanelWrapper = new AbsoluteLayout
        {
            BackgroundColor = Colors.Transparent,
            IsVisible = false,
            ZIndex = 500,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        
        // Add tap gesture to wrapper background (closes panel)
        var wrapperTapGesture = new TapGestureRecognizer();
        wrapperTapGesture.Tapped += OnQuestPanelTapped;
        _questPanelWrapper.GestureRecognizers.Add(wrapperTapGesture);
        
        // Configure panel for animation
        _questPanel.AnchorX = 0.5;
        _questPanel.AnchorY = 0.5;
        _questPanel.Scale = 0;
        
        // Position panel at center of screen
        AbsoluteLayout.SetLayoutBounds(_questPanel, new Rect(0.5, 0.5, panelWidth, panelHeight));
        AbsoluteLayout.SetLayoutFlags(_questPanel, AbsoluteLayoutFlags.PositionProportional);
        
        // Add panel border tap gesture (stops propagation)
        var borderTapGesture = new TapGestureRecognizer();
        borderTapGesture.Tapped += OnQuestBorderTapped;
        _questPanel.GestureRecognizers.Add(borderTapGesture);
        
        // Add title and placeholder content to scroll container
        if (_questPanel.ScrollContainer?.Content is VerticalStackLayout scrollContent)
        {
            scrollContent.Children.Clear();
            
            var title = new Label
            {
                Text = "📡 Quest Console",
                FontSize = 50 * adaptive,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 0, 0, 20)
            };
            
            var placeholder = new Label
            {
                Text = "Missions and quests coming soon...",
                FontSize = 30 * adaptive,
                TextColor = Colors.White,
                Opacity = 0.7,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 40, 0, 0)
            };

            try
            {
                var appRes = Application.Current?.Resources;
                if (appRes != null && appRes.ContainsKey("ResourcePanelTitleFont"))
                    title.FontFamily = (string)appRes["ResourcePanelTitleFont"];
                else if (Resources != null && Resources.ContainsKey("ResourcePanelTitleFont"))
                    title.FontFamily = (string)Resources["ResourcePanelTitleFont"];
                if (appRes != null && appRes.ContainsKey("ResourcePanelBodyFont"))
                    placeholder.FontFamily = (string)appRes["ResourcePanelBodyFont"];
                else if (Resources != null && Resources.ContainsKey("ResourcePanelBodyFont"))
                    placeholder.FontFamily = (string)Resources["ResourcePanelBodyFont"];
            }
            catch { }
            // Fallback to known bundled font if resources are not available
            if (string.IsNullOrEmpty(title.FontFamily))
                title.FontFamily = "SilkscreenBold";
            if (string.IsNullOrEmpty(placeholder.FontFamily))
                placeholder.FontFamily = "SilkscreenRegular";
            
            scrollContent.Children.Add(title);
            scrollContent.Children.Add(placeholder);
            
            // Future: Add quest items here
            // foreach (var quest in questItems)
            // {
            //     scrollContent.Children.Add(CreateQuestItem(quest));
            // }
        }
        
        _questPanelWrapper.Children.Add(_questPanel);
        
        // Add wrapper to main grid (spans all columns)
        Grid.SetColumnSpan(_questPanelWrapper, 3);
        MainGrid.Children.Add(_questPanelWrapper);
        
        System.Diagnostics.Debug.WriteLine("HubPage: Quest panel created");
    }
    
    private void CreateStatsPanel()
    {
        var adaptive = screenProps.AdaptiveScale;
        double panelWidth = 1200 * adaptive;
        double panelHeight = 800 * adaptive;
        double cornerSize = 40 * adaptive;

        screenProps?.UpdateFontSizes(adaptive);
        
        // Create scroll-type panel with 9-slice styling
        _statsPanel = new StyledPanel(
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
        
        // Create wrapper for positioning and animations
        _statsPanelWrapper = new AbsoluteLayout
        {
            BackgroundColor = Colors.Transparent,
            IsVisible = false,
            ZIndex = 500,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        
        // Add tap gesture to wrapper background (closes panel)
        var wrapperTapGesture = new TapGestureRecognizer();
        wrapperTapGesture.Tapped += OnStatsPanelTapped;
        _statsPanelWrapper.GestureRecognizers.Add(wrapperTapGesture);
        
        // Configure panel for animation
        _statsPanel.AnchorX = 0.5;
        _statsPanel.AnchorY = 0.5;
        _statsPanel.Scale = 0;
        
        // Position panel at center of screen
        AbsoluteLayout.SetLayoutBounds(_statsPanel, new Rect(0.5, 0.5, panelWidth, panelHeight));
        AbsoluteLayout.SetLayoutFlags(_statsPanel, AbsoluteLayoutFlags.PositionProportional);
        
        // Add panel border tap gesture (stops propagation)
        var borderTapGesture = new TapGestureRecognizer();
        borderTapGesture.Tapped += OnStatsBorderTapped;
        _statsPanel.GestureRecognizers.Add(borderTapGesture);
        
        // Add title and placeholder content to scroll container
        if (_statsPanel.ScrollContainer?.Content is VerticalStackLayout scrollContent)
        {
            scrollContent.Children.Clear();
            
            var title = new Label
            {
                Text = "📊 Statistics & Collection",
                FontSize = 50 * adaptive,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 0, 0, 20)
            };
            
            var placeholder = new Label
            {
                Text = "Statistics and collection tracking coming soon...",
                FontSize = 30 * adaptive,
                TextColor = Colors.White,
                Opacity = 0.7,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 40, 0, 0)
            };

            try
            {
                var appRes = Application.Current?.Resources;
                if (appRes != null && appRes.ContainsKey("ResourcePanelTitleFont"))
                    title.FontFamily = (string)appRes["ResourcePanelTitleFont"];
                else if (Resources != null && Resources.ContainsKey("ResourcePanelTitleFont"))
                    title.FontFamily = (string)Resources["ResourcePanelTitleFont"];
                if (appRes != null && appRes.ContainsKey("ResourcePanelBodyFont"))
                    placeholder.FontFamily = (string)appRes["ResourcePanelBodyFont"];
                else if (Resources != null && Resources.ContainsKey("ResourcePanelBodyFont"))
                    placeholder.FontFamily = (string)Resources["ResourcePanelBodyFont"];
            }
            catch { }
            // Fallback to known bundled font if resources are not available
            if (string.IsNullOrEmpty(title.FontFamily))
                title.FontFamily = "SilkscreenBold";
            if (string.IsNullOrEmpty(placeholder.FontFamily))
                placeholder.FontFamily = "SilkscreenRegular";
            
            scrollContent.Children.Add(title);
            scrollContent.Children.Add(placeholder);
            
            // Future: Add statistics items here
            // foreach (var stat in statsItems)
            // {
            //     scrollContent.Children.Add(CreateStatItem(stat));
            // }
        }
        
        _statsPanelWrapper.Children.Add(_statsPanel);
        
        // Add wrapper to main grid (spans all columns)
        Grid.SetColumnSpan(_statsPanelWrapper, 3);
        MainGrid.Children.Add(_statsPanelWrapper);
        
        System.Diagnostics.Debug.WriteLine("HubPage: Stats panel created");
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

            // Ensure application font resources are updated for this adaptive scale
            try
            {
                screenProps.UpdateFontSizes(adaptive);
            }
            catch { }

            // Create Greenhouse button (left gutter)
            _greenhouseButtonWrapper = new AbsoluteLayout
            {
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Fill,
                Margin = new Thickness(20, 0, 0, 0),
                ZIndex = 1000
            };

            Grid.SetColumn(_greenhouseButtonWrapper, 0);
            if (!MainGrid.Children.Contains(_greenhouseButtonWrapper))
                MainGrid.Children.Add(_greenhouseButtonWrapper);

            // Create Greenhouse button using StyledPanel selection constructor
            _greenhouseButton = new StyledPanel(
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

            // Create icon for Greenhouse button
            var greenhouseIcon = new Image
            {
                Source = "ui__icon_greenhouse.png", // Replace with actual greenhouse icon if available
                Aspect = Aspect.AspectFit,
                WidthRequest = buttonHeight * 0.7,
                HeightRequest = buttonHeight * 0.7,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            if (_greenhouseButton.ContentGrid != null)
            {
#if WINDOWS
                // Configure ContentGrid to be 2x2 on Windows
                var cg = _greenhouseButton.ContentGrid;
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

                // Place greenhouseIcon to span both rows and columns
                Grid.SetRow(greenhouseIcon, 0);
                Grid.SetColumn(greenhouseIcon, 0);
                Grid.SetRowSpan(greenhouseIcon, 2);
                Grid.SetColumnSpan(greenhouseIcon, 2);
                greenhouseIcon.ZIndex = 0;
                cg.Children.Add(greenhouseIcon);

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
                // Fallback to known bundled font if resources are not available
                if (string.IsNullOrEmpty(tooltipLabel.FontFamily))
                    tooltipLabel.FontFamily = "SilkscreenRegular";
                tooltipLabel.FontSize = 50 * adaptive;
                Grid.SetRow(tooltipLabel, 1);
                Grid.SetColumn(tooltipLabel, 1);
                tooltipLabel.ZIndex = 1;
                cg.Children.Add(tooltipLabel);
#else
                _greenhouseButton.ContentGrid.Children.Add(greenhouseIcon);
#endif
            }

            // Position Greenhouse button
            _greenhouseButtonWrapper.Children.Add(_greenhouseButton.Panel);
#if WINDOWS
            AbsoluteLayout.SetLayoutBounds(_greenhouseButton.Panel, new Rect(0, 0.97, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
#else
            AbsoluteLayout.SetLayoutBounds(_greenhouseButton.Panel, new Rect(0, 0.925, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
#endif
            AbsoluteLayout.SetLayoutFlags(_greenhouseButton.Panel, AbsoluteLayoutFlags.YProportional);
            _greenhouseButton.Panel.AnchorX = 0.5;
            _greenhouseButton.Panel.AnchorY = 0.5;
            _greenhouseButton.Panel.IsVisible = true;
            _greenhouseButton.Panel.InputTransparent = false;
            
            // Ensure wrapper is interactive
            _greenhouseButtonWrapper.InputTransparent = false;
            _greenhouseButtonWrapper.IsVisible = true;

            // Attach tap gesture to Panel (skip on Windows — handled by keyboard 'Q')
#if !WINDOWS
            var greenhouseTap = new TapGestureRecognizer();
            greenhouseTap.Tapped += (s, e) => OnGreenhouseClicked(s ?? this, EventArgs.Empty);
            _greenhouseButton.Panel.GestureRecognizers.Add(greenhouseTap);
#endif

            // Create Laboratory button (right gutter)
            _laboratoryButtonWrapper = new AbsoluteLayout
            {
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Fill,
                Margin = new Thickness(0, 0, 20, 0),
                ZIndex = 1000
            };

            Grid.SetColumn(_laboratoryButtonWrapper, 2);
            if (!MainGrid.Children.Contains(_laboratoryButtonWrapper))
                MainGrid.Children.Add(_laboratoryButtonWrapper);

            // Create Laboratory button using StyledPanel selection constructor
            _laboratoryButton = new StyledPanel(
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

            // Create icon for Laboratory button
            var laboratoryIcon = new Image
            {
                Source = "ui__icon_lab.png", // Replace with actual laboratory icon if available
                Aspect = Aspect.AspectFit,
                WidthRequest = buttonHeight * 0.7,
                HeightRequest = buttonHeight * 0.7,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            if (_laboratoryButton.ContentGrid != null)
            {
#if WINDOWS
                // Configure ContentGrid to be 2x2 on Windows
                var cg = _laboratoryButton.ContentGrid;
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

                // Place laboratoryIcon to span both rows and columns
                Grid.SetRow(laboratoryIcon, 0);
                Grid.SetColumn(laboratoryIcon, 0);
                Grid.SetRowSpan(laboratoryIcon, 2);
                Grid.SetColumnSpan(laboratoryIcon, 2);
                laboratoryIcon.ZIndex = 0;
                cg.Children.Add(laboratoryIcon);

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
                // Fallback to known bundled font if resources are not available
                if (string.IsNullOrEmpty(tooltipLabel.FontFamily))
                    tooltipLabel.FontFamily = "SilkscreenRegular";
                tooltipLabel.FontSize = 50 * adaptive;
                Grid.SetRow(tooltipLabel, 1);
                Grid.SetColumn(tooltipLabel, 1);
                tooltipLabel.ZIndex = 1;
                cg.Children.Add(tooltipLabel);
#else
                _laboratoryButton.ContentGrid.Children.Add(laboratoryIcon);
#endif
            }

            // Position Laboratory button
            _laboratoryButtonWrapper.Children.Add(_laboratoryButton.Panel);
#if WINDOWS
            AbsoluteLayout.SetLayoutBounds(_laboratoryButton.Panel, new Rect(1, 0.97, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
#else
            AbsoluteLayout.SetLayoutBounds(_laboratoryButton.Panel, new Rect(1, 0.925, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
#endif
            AbsoluteLayout.SetLayoutFlags(_laboratoryButton.Panel, AbsoluteLayoutFlags.XProportional | AbsoluteLayoutFlags.YProportional);
            _laboratoryButton.Panel.AnchorX = 0.5;
            _laboratoryButton.Panel.AnchorY = 0.5;
            _laboratoryButton.Panel.IsVisible = true;
            _laboratoryButton.Panel.InputTransparent = false;
            
            // Ensure wrapper is interactive
            _laboratoryButtonWrapper.InputTransparent = false;
            _laboratoryButtonWrapper.IsVisible = true;

            // Attach tap gesture to Panel (skip on Windows — handled by keyboard 'E')
#if !WINDOWS
            var laboratoryTap = new TapGestureRecognizer();
            laboratoryTap.Tapped += (s, e) => OnLaboratoryClicked(s ?? this, EventArgs.Empty);
            _laboratoryButton.Panel.GestureRecognizers.Add(laboratoryTap);
#endif

            System.Diagnostics.Debug.WriteLine("[HubPage] Created navigation buttons dynamically");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HubPage] Failed to create navigation buttons: {ex.Message}");
        }
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
                
                // Set container size to reference size (design size)
                EnvironmentContainer.WidthRequest = ScreenProperties.ReferenceWidth;
                EnvironmentContainer.HeightRequest = ScreenProperties.ReferenceHeight;
                
                // Apply scale to wrapper instead of container to preserve LayoutBounds positioning
                // Scale from center (0.5, 0.5) to maintain centering with HorizontalOptions="Center"
                // This ensures proportional positioning works correctly on both platforms
                EnvironmentWrapper.AnchorX = 0.5;
                EnvironmentWrapper.AnchorY = 0.5;
                EnvironmentWrapper.Scale = screenProps.Scale;
                
                // Environment container settings
                EnvironmentContainer.InputTransparent = false;
                EnvironmentContainer.BackgroundColor = Colors.Transparent;
                
                // Set wrapper size to the reference size (before scaling)
                // The scale transform will make it the correct visual size
                EnvironmentWrapper.WidthRequest = ScreenProperties.ReferenceWidth;
                EnvironmentWrapper.HeightRequest = ScreenProperties.ReferenceHeight;
                
                // Apply translation to fine-tune centering
                EnvironmentWrapper.TranslationX = screenProps.OffsetX;
                EnvironmentWrapper.TranslationY = screenProps.OffsetY;
                
                // Update station object positions from absolute coordinates
                UpdateStationPositions();
                UpdateFurniturePositions();
                
                // Create dynamic panels if not already created
                if (!_panelsCreated)
                {
                    CreateDynamicPanels();
                }
            }
        }
#endif
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
    
    private async void OnGreenhouseClicked(object? sender, EventArgs e)
    {
        await NavigationService.NavigateWithFadeAsync("//GreenhousePage");
    }

    private async void OnLaboratoryClicked(object? sender, EventArgs e)
    {
        await NavigationService.NavigateWithFadeAsync("//LaboratoryPage");
    }

    private async void OnMarketClicked(object sender, EventArgs e)
    {
        if (_marketPanel == null || _marketPanelWrapper == null)
            return;
        
        // Check if this panel is animating
        if (_animatingPanels.Contains(_marketPanel))
            return;
        
        // If market panel is already open, close it
        if (_marketPanelWrapper.IsVisible)
        {
            await CloseMarketPanelWithAnimation();
            return;
        }
        
        // Close other panels first (only if they're visible)
        if (_questPanelWrapper != null && _questPanelWrapper.IsVisible)
            await CloseQuestPanelWithAnimation();
        if (_statsPanelWrapper != null && _statsPanelWrapper.IsVisible)
            await CloseStatsPanelWithAnimation();
        
        // Open market panel
        await OpenMarketPanelWithAnimation();
    }

    private async void OnQuestClicked(object sender, EventArgs e)
    {
        if (_questPanel == null || _questPanelWrapper == null)
            return;
        
        // Check if this panel is animating
        if (_animatingPanels.Contains(_questPanel))
            return;
        
        // If quest panel is already open, close it
        if (_questPanelWrapper.IsVisible)
        {
            await CloseQuestPanelWithAnimation();
            return;
        }
        
        // Close other panels first (only if they're visible)
        if (_marketPanelWrapper != null && _marketPanelWrapper.IsVisible)
            await CloseMarketPanelWithAnimation();
        if (_statsPanelWrapper != null && _statsPanelWrapper.IsVisible)
            await CloseStatsPanelWithAnimation();
        
        // Open quest panel
        await OpenQuestPanelWithAnimation();
    }

    private async void OnStatsClicked(object sender, EventArgs e)
    {
        if (_statsPanel == null || _statsPanelWrapper == null)
            return;
        
        // Check if this panel is animating
        if (_animatingPanels.Contains(_statsPanel))
            return;
        
        // If stats panel is already open, close it
        if (_statsPanelWrapper.IsVisible)
        {
            await CloseStatsPanelWithAnimation();
            return;
        }
        
        // Close other panels first (only if they're visible)
        if (_marketPanelWrapper != null && _marketPanelWrapper.IsVisible)
            await CloseMarketPanelWithAnimation();
        if (_questPanelWrapper != null && _questPanelWrapper.IsVisible)
            await CloseQuestPanelWithAnimation();
        
        // Open stats panel
        await OpenStatsPanelWithAnimation();
    }

    // Panel background taps (close panel)
    private async void OnMarketPanelTapped(object? sender, TappedEventArgs e)
    {
#if ANDROID
        // Only close panels on tap for Android (Windows uses Esc key)
        await CloseMarketPanelWithAnimation();
#else
        await Task.CompletedTask;
#endif
    }

    private async void OnQuestPanelTapped(object? sender, TappedEventArgs e)
    {
#if ANDROID
        // Only close panels on tap for Android (Windows uses Esc key)
        await CloseQuestPanelWithAnimation();
#else
        await Task.CompletedTask;
#endif
    }

    private async void OnStatsPanelTapped(object? sender, TappedEventArgs e)
    {
#if ANDROID
        // Only close panels on tap for Android (Windows uses Esc key)
        await CloseStatsPanelWithAnimation();
#else
        await Task.CompletedTask;
#endif
    }

    // Panel border taps (stop event propagation)
    private void OnMarketBorderTapped(object? sender, TappedEventArgs e)
    {
        // Stop event propagation (prevents panel tap from closing)
    }

    private void OnQuestBorderTapped(object? sender, TappedEventArgs e)
    {
        // Stop event propagation (prevents panel tap from closing)
    }

    private void OnStatsBorderTapped(object? sender, TappedEventArgs e)
    {
        // Stop event propagation (prevents panel tap from closing)
    }

    // ============================================================================
    // Panel Animation Methods
    // ============================================================================
    
    private async Task OpenMarketPanelWithAnimation()
    {
        if (_marketPanel == null || _marketPanelWrapper == null)
            return;
        
        // Add to animating set
        _animatingPanels.Add(_marketPanel);
        
        try
        {
            // Get base scale from screen properties
            var screenProps = ScreenProperties.Instance;
            double baseScale = screenProps.Scale;
            
            // Set anchor point to center for scale animation
            _marketPanel.AnchorX = 0.5;
            _marketPanel.AnchorY = 0.5;
            
            // Set initial scale to 0 and make visible
            _marketPanel.Scale = 0;
            _marketPanelWrapper.IsVisible = true;
            
            // Animate scale from 0 to 1 (expand animation)
            await _marketPanel.ScaleTo(1, 200, Easing.SpringOut);
        }
        finally
        {
            // Remove from animating set
            _animatingPanels.Remove(_marketPanel);
        }
    }
    
    private async Task OpenQuestPanelWithAnimation()
    {
        if (_questPanel == null || _questPanelWrapper == null)
            return;
        
        // Add to animating set
        _animatingPanels.Add(_questPanel);
        
        try
        {
            // Get base scale from screen properties
            var screenProps = ScreenProperties.Instance;
            double baseScale = screenProps.Scale;
            
            // Set anchor point to center for scale animation
            _questPanel.AnchorX = 0.5;
            _questPanel.AnchorY = 0.5;
            
            // Set initial scale to 0 and make visible
            _questPanel.Scale = 0;
            _questPanelWrapper.IsVisible = true;
            
            // Animate scale from 0 to 1 (expand animation)
            await _questPanel.ScaleTo(1, 200, Easing.SpringOut);
        }
        finally
        {
            // Remove from animating set
            _animatingPanels.Remove(_questPanel);
        }
    }
    
    private async Task OpenStatsPanelWithAnimation()
    {
        if (_statsPanel == null || _statsPanelWrapper == null)
            return;
        
        // Add to animating set
        _animatingPanels.Add(_statsPanel);
        
        try
        {
            // Get base scale from screen properties
            var screenProps = ScreenProperties.Instance;
            double baseScale = screenProps.Scale;
            
            // Set anchor point to center for scale animation
            _statsPanel.AnchorX = 0.5;
            _statsPanel.AnchorY = 0.5;
            
            // Set initial scale to 0 and make visible
            _statsPanel.Scale = 0;
            _statsPanelWrapper.IsVisible = true;
            
            // Animate scale from 0 to 1 (expand animation)
            await _statsPanel.ScaleTo(1, 200, Easing.SpringOut);
        }
        finally
        {
            // Remove from animating set
            _animatingPanels.Remove(_statsPanel);
        }
    }
    
    private async Task CloseMarketPanelWithAnimation()
    {
        if (_marketPanel == null || _marketPanelWrapper == null)
            return;
        
        if (!_marketPanelWrapper.IsVisible)
            return;
        
        // Check if already animating
        if (_animatingPanels.Contains(_marketPanel))
            return;
        
        // Add to animating set
        _animatingPanels.Add(_marketPanel);
        
        try
        {
            // Animate scale from current scale to 0 (shrink animation)
            await _marketPanel.ScaleTo(0, 200, Easing.SpringIn);
            
            // Hide after animation
            _marketPanelWrapper.IsVisible = false;
        }
        finally
        {
            // Remove from animating set
            _animatingPanels.Remove(_marketPanel);
        }
    }
    
    private async Task CloseQuestPanelWithAnimation()
    {
        if (_questPanel == null || _questPanelWrapper == null)
            return;
        
        if (!_questPanelWrapper.IsVisible)
            return;
        
        // Check if already animating
        if (_animatingPanels.Contains(_questPanel))
            return;
        
        // Add to animating set
        _animatingPanels.Add(_questPanel);
        
        try
        {
            // Animate scale from current scale to 0 (shrink animation)
            await _questPanel.ScaleTo(0, 200, Easing.SpringIn);
            
            // Hide after animation
            _questPanelWrapper.IsVisible = false;
        }
        finally
        {
            // Remove from animating set
            _animatingPanels.Remove(_questPanel);
        }
    }
    
    private async Task CloseStatsPanelWithAnimation()
    {
        if (_statsPanel == null || _statsPanelWrapper == null)
            return;
        
        if (!_statsPanelWrapper.IsVisible)
            return;
        
        // Check if already animating
        if (_animatingPanels.Contains(_statsPanel))
            return;
        
        // Add to animating set
        _animatingPanels.Add(_statsPanel);
        
        try
        {
            // Animate scale from current scale to 0 (shrink animation)
            await _statsPanel.ScaleTo(0, 200, Easing.SpringIn);
            
            // Hide after animation
            _statsPanelWrapper.IsVisible = false;
        }
        finally
        {
            // Remove from animating set
            _animatingPanels.Remove(_statsPanel);
        }
    }

    private async void CloseAllPanels()
    {
        // Close all panels with animation (panels can animate concurrently)
        var tasks = new List<Task>();
        
        if (_marketPanelWrapper != null && _marketPanelWrapper.IsVisible && _marketPanel != null && !_animatingPanels.Contains(_marketPanel))
        {
            tasks.Add(CloseMarketPanelWithAnimation());
        }
        
        if (_questPanelWrapper != null && _questPanelWrapper.IsVisible && _questPanel != null && !_animatingPanels.Contains(_questPanel))
        {
            tasks.Add(CloseQuestPanelWithAnimation());
        }
        
        if (_statsPanelWrapper != null && _statsPanelWrapper.IsVisible && _statsPanel != null && !_animatingPanels.Contains(_statsPanel))
        {
            tasks.Add(CloseStatsPanelWithAnimation());
        }
        
        await Task.WhenAll(tasks);
    }
}



