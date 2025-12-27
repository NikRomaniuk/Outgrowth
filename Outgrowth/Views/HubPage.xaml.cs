using Outgrowth.ViewModels;
using System.Linq;

namespace Outgrowth.Views;

public partial class HubPage : ContentPage
{
    // Element coordinates (1:1 pixel ratio, container center: X=960, Y=540)
    private readonly (int X, int Y, string Name)[] _elementCoordinates = 
    {
        (-480, 0, "Market"),       // Left of center
        (0, 162, "QuestConsole"),  // Center, above
        (480, 0, "Statistics")     // Right of center
    };
    
    public HubPage()
    {
        InitializeComponent();
        BindingContext = new HubViewModel();
        
        // Handle page size changes for scaling
        this.SizeChanged += OnPageSizeChanged;
        
        // Close panels when navigating away
        this.Disappearing += OnPageDisappearing;
        
        // Update element positions on load
        this.Loaded += OnPageLoaded;
    }
    
    private void OnPageLoaded(object? sender, EventArgs e)
    {
        // Initialize element positions from absolute coordinates
        UpdateElementPositions();
    }
    
    private void OnPageDisappearing(object? sender, EventArgs e)
    {
        CloseAllPanels();
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
                // Calculate 16:9 aspect ratio based on available height
                var targetHeight = pageHeight;
                var targetWidth = targetHeight * 16.0 / 9.0;
                
                // If calculated width exceeds screen width, scale by width instead
                if (targetWidth > pageWidth)
                {
                    targetWidth = pageWidth;
                    targetHeight = targetWidth * 9.0 / 16.0;
                }
                
                // Set container size to reference size (design size)
                const double referenceWidth = 1920.0;
                const double referenceHeight = 1080.0;
                
                EnvironmentContainer.WidthRequest = referenceWidth;
                EnvironmentContainer.HeightRequest = referenceHeight;
                
                // Calculate scale factor to fit the container within available space
                var scaleX = targetWidth / referenceWidth;
                var scaleY = targetHeight / referenceHeight;
                var scale = Math.Min(scaleX, scaleY);
                
                // Apply scale to wrapper instead of container to preserve LayoutBounds positioning
                // Scale from center (0.5, 0.5) to maintain centering with HorizontalOptions="Center"
                // This ensures proportional positioning works correctly on both platforms
                EnvironmentWrapper.AnchorX = 0.5;
                EnvironmentWrapper.AnchorY = 0.5;
                EnvironmentWrapper.Scale = scale;
                
                // Environment container settings
                EnvironmentContainer.InputTransparent = false;
                EnvironmentContainer.BackgroundColor = Colors.Transparent;
                
                // Calculate the actual scaled size
                var scaledWidth = referenceWidth * scale;
                var scaledHeight = referenceHeight * scale;
                
                // Set wrapper size to the reference size (before scaling)
                // The scale transform will make it the correct visual size
                EnvironmentWrapper.WidthRequest = referenceWidth;
                EnvironmentWrapper.HeightRequest = referenceHeight;
                
                // Calculate center offset to ensure perfect centering after scaling
                // The wrapper is centered, but we need to account for the scale transform
                var offsetX = (targetWidth - scaledWidth) / 2.0;
                var offsetY = (targetHeight - scaledHeight) / 2.0;
                
                // Apply translation to fine-tune centering
                EnvironmentWrapper.TranslationX = offsetX;
                EnvironmentWrapper.TranslationY = offsetY;
                
                // Scale edge navigation buttons to match environment scale
                // This ensures they appear the same size as elements in the environment
                GreenhouseButton.AnchorX = 0;
                GreenhouseButton.AnchorY = 0.5;
                GreenhouseButton.Scale = scale;
                
                LaboratoryButton.AnchorX = 1;
                LaboratoryButton.AnchorY = 0.5;
                LaboratoryButton.Scale = scale;
                
                // Update element positions from absolute coordinates
                UpdateElementPositions();
                
                // Scale overlay panels to match environment scale
                // This ensures panels appear the same size on both platforms
                MarketPanelBorder.AnchorX = 0.5;
                MarketPanelBorder.AnchorY = 0.5;
                MarketPanelBorder.Scale = scale;
                
                QuestPanelBorder.AnchorX = 0.5;
                QuestPanelBorder.AnchorY = 0.5;
                QuestPanelBorder.Scale = scale;
                
                StatsPanelBorder.AnchorX = 0.5;
                StatsPanelBorder.AnchorY = 0.5;
                StatsPanelBorder.Scale = scale;
            }
        }
#endif
    }

    /// <summary>
    /// Updates element positions using 1:1 coordinate system (container center: X=960, Y=540)
    /// Converts center coordinates to top-left corner for AbsoluteLayout
    /// </summary>
    private void UpdateElementPositions()
    {
#if ANDROID || WINDOWS
        if (EnvironmentContainer == null)
            return;
        
        const double containerCenterX = 960.0;
        const double containerCenterY = 540.0;
        const double elementHalfWidth = 150.0;   // Elements are 300x300
        const double elementHalfHeight = 150.0;
        
        foreach (var (logicalX, logicalY, name) in _elementCoordinates)
        {
            VerticalStackLayout? element = name switch
            {
                "Market" => FindByName("MarketButton") as VerticalStackLayout,
                "QuestConsole" => FindByName("QuestConsoleButton") as VerticalStackLayout,
                "Statistics" => FindByName("StatisticsButton") as VerticalStackLayout,
                _ => null
            };
            
            if (element == null)
                continue;
            
            // Convert center coordinates to pixel positions (1:1 ratio)
            double centerPixelX = containerCenterX + logicalX;
            double centerPixelY = containerCenterY - logicalY; // Negative Y = below
            
            // Convert to top-left corner for AbsoluteLayout
            double leftEdgeX = centerPixelX - elementHalfWidth;
            double topEdgeY = centerPixelY - elementHalfHeight;
            
            AbsoluteLayout.SetLayoutBounds(element, new Rect(leftEdgeX, topEdgeY, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
            AbsoluteLayout.SetLayoutFlags(element, 0);
        }
#endif
    }

    // Navigation to other pages
    private async void OnGreenhouseClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//GreenhousePage");
    }

    private async void OnLaboratoryClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//LaboratoryPage");
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
#if ANDROID || WINDOWS
        CloseMarketPanel();
#endif
    }

    private void OnQuestPanelTapped(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        CloseQuestPanel();
#endif
    }

    private void OnStatsPanelTapped(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
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

