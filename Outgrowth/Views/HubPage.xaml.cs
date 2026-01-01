using Outgrowth.ViewModels;
using Outgrowth.Models;
using Outgrowth.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
#if WINDOWS
using Outgrowth.Platforms.Windows;
#endif

namespace Outgrowth.Views;

public partial class HubPage : ContentPage
{
    // Station objects - created dynamically on page load
    private readonly List<StationObject> _stationObjects = new()
    {
        new StationObject("Market", -480, 0, 300, 300),
        new StationObject("QuestConsole", 0, 162, 300, 300),
        new StationObject("Statistics", 480, 0, 300, 300)
    };
    
    // Animation lock to prevent opening other panels during animation
    private bool _isAnimating = false;
    
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
        
        // Initialize panel scales for animation (set to 0 if not visible, baseScale if visible)
        var screenProps = ScreenProperties.Instance;
        screenProps.UpdateScreenProperties(this.Width, this.Height);
        double baseScale = screenProps.Scale;
        
        if (MarketPanelBorder != null)
        {
            MarketPanelBorder.AnchorX = 0.5;
            MarketPanelBorder.AnchorY = 0.5;
            MarketPanelBorder.Scale = MarketPanel != null && MarketPanel.IsVisible ? baseScale : 0;
        }
        if (QuestPanelBorder != null)
        {
            QuestPanelBorder.AnchorX = 0.5;
            QuestPanelBorder.AnchorY = 0.5;
            QuestPanelBorder.Scale = QuestPanel != null && QuestPanel.IsVisible ? baseScale : 0;
        }
        if (StatsPanelBorder != null)
        {
            StatsPanelBorder.AnchorX = 0.5;
            StatsPanelBorder.AnchorY = 0.5;
            StatsPanelBorder.Scale = StatsPanel != null && StatsPanel.IsVisible ? baseScale : 0;
        }
        
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
        // Unsubscribe from timer events
        // Timer.Tick -= OnTimerTick;
        
        // Reset animation flag to allow closing (page is disappearing)
        _isAnimating = false;
        // Close panels immediately without animation
        CloseMarketPanel();
        CloseQuestPanel();
        CloseStatsPanel();
        
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
                // Only update scale if not animating (to avoid interfering with animation)
                if (!_isAnimating)
                {
                    MarketPanelBorder.AnchorX = 0.5;
                    MarketPanelBorder.AnchorY = 0.5;
                    if (MarketPanel != null && !MarketPanel.IsVisible)
                        MarketPanelBorder.Scale = 0;
                    else
                        MarketPanelBorder.Scale = screenProps.Scale;
                    
                    QuestPanelBorder.AnchorX = 0.5;
                    QuestPanelBorder.AnchorY = 0.5;
                    if (QuestPanel != null && !QuestPanel.IsVisible)
                        QuestPanelBorder.Scale = 0;
                    else
                        QuestPanelBorder.Scale = screenProps.Scale;
                    
                    StatsPanelBorder.AnchorX = 0.5;
                    StatsPanelBorder.AnchorY = 0.5;
                    if (StatsPanel != null && !StatsPanel.IsVisible)
                        StatsPanelBorder.Scale = 0;
                    else
                        StatsPanelBorder.Scale = screenProps.Scale;
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
    private async void OnMarketClicked(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        if (MarketPanel == null || _isAnimating)
            return;
        
        // If market panel is already open, close it
        if (MarketPanel.IsVisible)
        {
            await CloseMarketPanelWithAnimation();
            return;
        }
        
        // Close other panels first (only if they're visible)
        if (QuestPanel != null && QuestPanel.IsVisible)
            await CloseQuestPanelWithAnimation();
        if (StatsPanel != null && StatsPanel.IsVisible)
            await CloseStatsPanelWithAnimation();
        
        // Open market panel
        await OpenMarketPanelWithAnimation();
#endif
    }

    private async void OnQuestClicked(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        if (QuestPanel == null || _isAnimating)
            return;
        
        // If quest panel is already open, close it
        if (QuestPanel.IsVisible)
        {
            await CloseQuestPanelWithAnimation();
            return;
        }
        
        // Close other panels first (only if they're visible)
        if (MarketPanel != null && MarketPanel.IsVisible)
            await CloseMarketPanelWithAnimation();
        if (StatsPanel != null && StatsPanel.IsVisible)
            await CloseStatsPanelWithAnimation();
        
        // Open quest panel
        await OpenQuestPanelWithAnimation();
#endif
    }

    private async void OnStatsClicked(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        if (StatsPanel == null || _isAnimating)
            return;
        
        // If stats panel is already open, close it
        if (StatsPanel.IsVisible)
        {
            await CloseStatsPanelWithAnimation();
            return;
        }
        
        // Close other panels first (only if they're visible)
        if (MarketPanel != null && MarketPanel.IsVisible)
            await CloseMarketPanelWithAnimation();
        if (QuestPanel != null && QuestPanel.IsVisible)
            await CloseQuestPanelWithAnimation();
        
        // Open stats panel
        await OpenStatsPanelWithAnimation();
#endif
    }

    // Panel background taps (close panel)
    private async void OnMarketPanelTapped(object sender, EventArgs e)
    {
#if ANDROID
        // Only close panels on tap for Android (Windows uses Esc key)
        await CloseMarketPanelWithAnimation();
#else
        await Task.CompletedTask;
#endif
    }

    private async void OnQuestPanelTapped(object sender, EventArgs e)
    {
#if ANDROID
        // Only close panels on tap for Android (Windows uses Esc key)
        await CloseQuestPanelWithAnimation();
#else
        await Task.CompletedTask;
#endif
    }

    private async void OnStatsPanelTapped(object sender, EventArgs e)
    {
#if ANDROID
        // Only close panels on tap for Android (Windows uses Esc key)
        await CloseStatsPanelWithAnimation();
#else
        await Task.CompletedTask;
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

    // Animation methods for panels
    private async Task OpenMarketPanelWithAnimation()
    {
#if ANDROID || WINDOWS
        if (MarketPanel == null || _isAnimating)
            return;
        
        _isAnimating = true;
        
        // Get base scale from screen properties
        var screenProps = ScreenProperties.Instance;
        double baseScale = screenProps.Scale;
        
        // Set anchor point to center for scale animation
        MarketPanelBorder.AnchorX = 0.5;
        MarketPanelBorder.AnchorY = 0.5;
        
        // Set initial scale to 0 and make visible
        MarketPanelBorder.Scale = 0;
        MarketPanel.IsVisible = true;
        
        // Animate scale from 0 to baseScale (expand animation)
        await MarketPanelBorder.ScaleTo(baseScale, 200, Easing.SpringOut);
        
        _isAnimating = false;
#else
        await Task.CompletedTask;
#endif
    }
    
    private async Task OpenQuestPanelWithAnimation()
    {
#if ANDROID || WINDOWS
        if (QuestPanel == null || _isAnimating)
            return;
        
        _isAnimating = true;
        
        // Get base scale from screen properties
        var screenProps = ScreenProperties.Instance;
        double baseScale = screenProps.Scale;
        
        // Set anchor point to center for scale animation
        QuestPanelBorder.AnchorX = 0.5;
        QuestPanelBorder.AnchorY = 0.5;
        
        // Set initial scale to 0 and make visible
        QuestPanelBorder.Scale = 0;
        QuestPanel.IsVisible = true;
        
        // Animate scale from 0 to baseScale (expand animation)
        await QuestPanelBorder.ScaleTo(baseScale, 200, Easing.SpringOut);
        
        _isAnimating = false;
#else
        await Task.CompletedTask;
#endif
    }
    
    private async Task OpenStatsPanelWithAnimation()
    {
#if ANDROID || WINDOWS
        if (StatsPanel == null || _isAnimating)
            return;
        
        _isAnimating = true;
        
        // Get base scale from screen properties
        var screenProps = ScreenProperties.Instance;
        double baseScale = screenProps.Scale;
        
        // Set anchor point to center for scale animation
        StatsPanelBorder.AnchorX = 0.5;
        StatsPanelBorder.AnchorY = 0.5;
        
        // Set initial scale to 0 and make visible
        StatsPanelBorder.Scale = 0;
        StatsPanel.IsVisible = true;
        
        // Animate scale from 0 to baseScale (expand animation)
        await StatsPanelBorder.ScaleTo(baseScale, 200, Easing.SpringOut);
        
        _isAnimating = false;
#else
        await Task.CompletedTask;
#endif
    }
    
    private async Task CloseMarketPanelWithAnimation()
    {
#if ANDROID || WINDOWS
        if (MarketPanel == null || !MarketPanel.IsVisible || _isAnimating)
            return;
        
        _isAnimating = true;
        
        // Animate scale from current scale to 0 (shrink animation)
        await MarketPanelBorder.ScaleTo(0, 200, Easing.SpringIn);
        
        // Hide after animation
        MarketPanel.IsVisible = false;
        
        _isAnimating = false;
#else
        await Task.CompletedTask;
#endif
    }
    
    private async Task CloseQuestPanelWithAnimation()
    {
#if ANDROID || WINDOWS
        if (QuestPanel == null || !QuestPanel.IsVisible || _isAnimating)
            return;
        
        _isAnimating = true;
        
        // Animate scale from current scale to 0 (shrink animation)
        await QuestPanelBorder.ScaleTo(0, 200, Easing.SpringIn);
        
        // Hide after animation
        QuestPanel.IsVisible = false;
        
        _isAnimating = false;
#else
        await Task.CompletedTask;
#endif
    }
    
    private async Task CloseStatsPanelWithAnimation()
    {
#if ANDROID || WINDOWS
        if (StatsPanel == null || !StatsPanel.IsVisible || _isAnimating)
            return;
        
        _isAnimating = true;
        
        // Animate scale from current scale to 0 (shrink animation)
        await StatsPanelBorder.ScaleTo(0, 200, Easing.SpringIn);
        
        // Hide after animation
        StatsPanel.IsVisible = false;
        
        _isAnimating = false;
#else
        await Task.CompletedTask;
#endif
    }
    
    // Close panel methods (immediate, no animation)
    private void CloseMarketPanel()
    {
#if ANDROID || WINDOWS
        if (MarketPanel != null && !_isAnimating)
        {
            // Cannot close during animation - animation must complete first
            MarketPanelBorder.Scale = 0;
            MarketPanel.IsVisible = false;
        }
#endif
    }

    private void CloseQuestPanel()
    {
#if ANDROID || WINDOWS
        if (QuestPanel != null && !_isAnimating)
        {
            // Cannot close during animation - animation must complete first
            QuestPanelBorder.Scale = 0;
            QuestPanel.IsVisible = false;
        }
#endif
    }

    private void CloseStatsPanel()
    {
#if ANDROID || WINDOWS
        if (StatsPanel != null && !_isAnimating)
        {
            // Cannot close during animation - animation must complete first
            StatsPanelBorder.Scale = 0;
            StatsPanel.IsVisible = false;
        }
#endif
    }

    private async void CloseAllPanels()
    {
        // Close all panels with animation (but only if not already animating)
        if (!_isAnimating)
        {
            await CloseMarketPanelWithAnimation();
            await CloseStatsPanelWithAnimation();
            await CloseQuestPanelWithAnimation();
        }
        // If animating, do nothing - panels cannot be closed during animation
    }
}

