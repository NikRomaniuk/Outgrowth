using Outgrowth.ViewModels;
using Outgrowth.Models;
using Outgrowth.Services;
using System.Linq;
#if WINDOWS
using Outgrowth.Platforms.Windows;
#endif

namespace Outgrowth.Views;

public partial class HubPage : ContentPage
{
    // Station objects - created dynamically on page load
    private readonly List<StationObject> _stationObjects = new()
    {
        new StationObject("Market", "Market", -480, 0, 300, 300, "📦", Color.FromArgb("#4A4A4A")),
        new StationObject("QuestConsole", "Quest Console", 0, 162, 300, 300, "📡", Color.FromArgb("#1E3A5F")),
        new StationObject("Statistics", "Statistics", 480, 0, 300, 300, "📊", Color.FromArgb("#2C2C2C"))
    };
    
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
        
#if WINDOWS
        // Attach Windows keyboard input handler (only Esc for closing panels)
        _windowsInput = new WindowsInput(
            onLeftArrow: () => { }, // No action
            onRightArrow: () => { }, // No action
            onEscape: CloseAllPanels  // Close panels with Esc key
        );
        _windowsInput.Attach();
#endif
    }
    
    private void OnPageDisappearing(object? sender, EventArgs e)
    {
        CloseAllPanels();
        
        // Clean up dynamically created elements to prevent memory leaks
        CleanupStationObjects();
        
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

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        if (EnvironmentWrapper != null && EnvironmentContainer != null && GreenhouseButton != null && LaboratoryButton != null
            && MarketPanelBorder != null && QuestPanelBorder != null && StatsPanelBorder != null)
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
                
                // Scale edge navigation buttons to match environment scale
                // This ensures they appear the same size as elements in the environment
                GreenhouseButton.AnchorX = 0;
                GreenhouseButton.AnchorY = 1;
                GreenhouseButton.Scale = screenProps.Scale;
                
                LaboratoryButton.AnchorX = 1;
                LaboratoryButton.AnchorY = 1;
                LaboratoryButton.Scale = screenProps.Scale;
                
                // Update station object positions from absolute coordinates
                UpdateStationPositions();
                
                // Scale overlay panels to match environment scale
                // This ensures panels appear the same size on both platforms
                MarketPanelBorder.AnchorX = 0.5;
                MarketPanelBorder.AnchorY = 0.5;
                MarketPanelBorder.Scale = screenProps.Scale;
                
                QuestPanelBorder.AnchorX = 0.5;
                QuestPanelBorder.AnchorY = 0.5;
                QuestPanelBorder.Scale = screenProps.Scale;
                
                StatsPanelBorder.AnchorX = 0.5;
                StatsPanelBorder.AnchorY = 0.5;
                StatsPanelBorder.Scale = screenProps.Scale;
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

    // Navigation to other pages
    private async void OnGreenhouseClicked(object sender, EventArgs e)
    {
        await NavigationService.NavigateWithFadeAsync("//GreenhousePage");
    }

    private async void OnLaboratoryClicked(object sender, EventArgs e)
    {
        await NavigationService.NavigateWithFadeAsync("//LaboratoryPage");
    }

    // Panel controls
    private void OnMarketClicked(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        if (MarketPanel != null)
        {
            MarketPanel.IsVisible = true;
        }
#endif
    }

    private void OnQuestClicked(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        if (QuestPanel != null)
        {
            QuestPanel.IsVisible = true;
        }
#endif
    }

    private void OnStatsClicked(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        if (StatsPanel != null)
        {
            StatsPanel.IsVisible = true;
        }
#endif
    }

    // Panel background taps (close panel)
    private void OnMarketPanelTapped(object sender, EventArgs e)
    {
#if ANDROID
        // Only close panels on tap for Android (Windows uses Esc key)
        CloseMarketPanel();
#endif
    }

    private void OnQuestPanelTapped(object sender, EventArgs e)
    {
#if ANDROID
        // Only close panels on tap for Android (Windows uses Esc key)
        CloseQuestPanel();
#endif
    }

    private void OnStatsPanelTapped(object sender, EventArgs e)
    {
#if ANDROID
        // Only close panels on tap for Android (Windows uses Esc key)
        CloseStatsPanel();
#endif
    }

    // Panel border taps (stop event propagation)
    private void OnMarketBorderTapped(object sender, EventArgs e)
    {
        // Stop event propagation (prevents panel tap from closing)
    }

    private void OnQuestBorderTapped(object sender, EventArgs e)
    {
        // Stop event propagation (prevents panel tap from closing)
    }

    private void OnStatsBorderTapped(object sender, EventArgs e)
    {
        // Stop event propagation (prevents panel tap from closing)
    }

    // Close panel methods
    private void CloseMarketPanel()
    {
#if ANDROID || WINDOWS
        if (MarketPanel != null)
        {
            MarketPanel.IsVisible = false;
        }
#endif
    }

    private void CloseQuestPanel()
    {
#if ANDROID || WINDOWS
        if (QuestPanel != null)
        {
            QuestPanel.IsVisible = false;
        }
#endif
    }

    private void CloseStatsPanel()
    {
#if ANDROID || WINDOWS
        if (StatsPanel != null)
        {
            StatsPanel.IsVisible = false;
        }
#endif
    }

    private void CloseAllPanels()
    {
        CloseMarketPanel();
        CloseQuestPanel();
        CloseStatsPanel();
    }
}

