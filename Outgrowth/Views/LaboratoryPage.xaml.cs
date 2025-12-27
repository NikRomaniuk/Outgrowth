using Outgrowth.ViewModels;
using System.Linq;

namespace Outgrowth.Views;

public partial class LaboratoryPage : ContentPage
{
    // Element coordinates (1:1 pixel ratio, container center: X=960, Y=540)
    private readonly (int X, int Y, string Name)[] _elementCoordinates = 
    {
        (0, 200, "ResourceSlot"),   // Center, above
        (0, -200, "Extract")        // Center, below
    };
    
    public LaboratoryPage()
    {
        InitializeComponent();
        BindingContext = new LaboratoryViewModel();
        
        // Handle page size changes for scaling
        this.SizeChanged += OnPageSizeChanged;
        
        // Close panel when navigating away
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
        CloseResourcePanel();
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
                // Calculate 16:9 target size (fit to height, clamp to width)
                var targetHeight = pageHeight;
                var targetWidth = targetHeight * 16.0 / 9.0;
                if (targetWidth > pageWidth)
                {
                    targetWidth = pageWidth;
                    targetHeight = targetWidth * 9.0 / 16.0;
                }
                
                // Reference size: 1920x1080 (Windows base)
                const double referenceWidth = 1920.0;
                const double referenceHeight = 1080.0;
                
                EnvironmentContainer.WidthRequest = referenceWidth;
                EnvironmentContainer.HeightRequest = referenceHeight;
                
                // Calculate scale to fit reference size within target size
                var scaleX = targetWidth / referenceWidth;
                var scaleY = targetHeight / referenceHeight;
                var scale = Math.Min(scaleX, scaleY);
                
                // Scale environment wrapper from center to maintain centering
                EnvironmentWrapper.AnchorX = 0.5;
                EnvironmentWrapper.AnchorY = 0.5;
                EnvironmentWrapper.Scale = scale;
                EnvironmentWrapper.WidthRequest = referenceWidth;
                EnvironmentWrapper.HeightRequest = referenceHeight;
                
                EnvironmentContainer.InputTransparent = false;
                EnvironmentContainer.BackgroundColor = Colors.Transparent;
                
                // Center offset: account for scale transform
                var scaledWidth = referenceWidth * scale;
                var scaledHeight = referenceHeight * scale;
                var offsetX = (targetWidth - scaledWidth) / 2.0;
                var offsetY = (targetHeight - scaledHeight) / 2.0;
                EnvironmentWrapper.TranslationX = offsetX;
                EnvironmentWrapper.TranslationY = offsetY;
                
                // Scale HubButton from left edge (matches environment scale)
                HubButton.AnchorX = 0;
                HubButton.AnchorY = 0.5;
                HubButton.Scale = scale;
                
                // Scale placeholder from right edge (maintains column width, prevents offset)
                ResourceListPlaceholder.AnchorX = 1;
                ResourceListPlaceholder.AnchorY = 0.5;
                ResourceListPlaceholder.Scale = scale;
                
                // Update element positions from absolute coordinates
                UpdateElementPositions();
                
                // Font scale: based on screen width (1920px = scale 1.0)
                const double windowsBaseWidth = 1920.0;
                var fontScale = pageWidth / windowsBaseWidth;
                
                UpdateFontSizes(fontScale);
                UpdatePanelSize(fontScale, scale);
            }
        }
#endif
    }
    
    private void UpdatePanelSize(double fontScale, double _)
    {
        // Base dimensions: 300x500 (matches HubButton width for equal columns)
        const double baseWidth = 300.0;
        const double baseHeight = 500.0;
        const double baseMargin = 20.0;
        
        // Panel scales with fontScale (content sizing)
        ResourceListContainer.WidthRequest = baseWidth * fontScale;
        ResourceListContainer.HeightRequest = baseHeight * fontScale;
        ResourceListContainer.Margin = new Thickness(0, 0, baseMargin * fontScale, 0);
        
        // Placeholder scales with buttonScale (layout sizing, maintains column width)
        ResourceListPlaceholder.WidthRequest = baseWidth;
        ResourceListPlaceholder.HeightRequest = baseHeight;
        ResourceListPlaceholder.Margin = new Thickness(0, 0, baseMargin, 0);
    }
    
    private void UpdateFontSizes(double fontScale)
    {
        // Base font sizes (Windows 1920px = scale 1.0)
        const double baseTitleSize = 40.0;
        const double baseBodySize = 30.0;
        const double baseQtySize = 24.0;
        const double baseIconSize = 40.0;
        
        // Update DynamicResource bindings (auto-updates UI)
        Resources["ResourcePanelTitleSize"] = baseTitleSize * fontScale;
        Resources["ResourcePanelBodySize"] = baseBodySize * fontScale;
        Resources["ResourcePanelQtySize"] = baseQtySize * fontScale;
        Resources["ResourcePanelIconSize"] = baseIconSize * fontScale;
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
        
        foreach (var (logicalX, logicalY, name) in _elementCoordinates)
        {
            VerticalStackLayout? element = name switch
            {
                "ResourceSlot" => FindByName("ResourceSlotButton") as VerticalStackLayout,
                "Extract" => FindByName("ExtractButton") as VerticalStackLayout,
                _ => null
            };
            
            if (element == null)
                continue;
            
            // Convert center coordinates to pixel positions (1:1 ratio)
            double centerPixelX = containerCenterX + logicalX;
            double centerPixelY = containerCenterY - logicalY; // Negative Y = below
            
            // Element dimensions (Extract is 250x250, ResourceSlot is 300x300)
            double elementHalfWidth = (name == "Extract") ? 125.0 : 150.0;
            double elementHalfHeight = (name == "Extract") ? 125.0 : 150.0;
            
            // Convert to top-left corner for AbsoluteLayout
            double leftEdgeX = centerPixelX - elementHalfWidth;
            double topEdgeY = centerPixelY - elementHalfHeight;
            
            AbsoluteLayout.SetLayoutBounds(element, new Rect(leftEdgeX, topEdgeY, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
            AbsoluteLayout.SetLayoutFlags(element, 0);
        }
#endif
    }

    // Navigation
    private async void OnHubClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//HubPage");
    }

    // Panel controls
    private void OnResourceSlotClicked(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        if (ResourceListContainer != null && BackgroundOverlay != null)
        {
            ResourceListContainer.IsVisible = true;
            ResourceListContainer.InputTransparent = false;
            BackgroundOverlay.IsVisible = true;
            BackgroundOverlay.InputTransparent = false;
        }
#endif
    }

    private void OnExtractClicked(object sender, EventArgs e)
    {
        // TODO: Implement extract functionality
    }

    private void OnBackgroundOverlayTapped(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        CloseResourcePanel();
#endif
    }

    private void OnResourcePanelTapped(object sender, EventArgs e)
    {
        // Stop event propagation (prevents overlay tap from closing panel)
    }

    private void CloseResourcePanel()
    {
#if ANDROID || WINDOWS
        if (ResourceListContainer != null && BackgroundOverlay != null)
        {
            ResourceListContainer.IsVisible = false;
            ResourceListContainer.InputTransparent = true;
            BackgroundOverlay.IsVisible = false;
            BackgroundOverlay.InputTransparent = true;
        }
#endif
    }
}

