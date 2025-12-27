using Outgrowth.ViewModels;
using Outgrowth.Models;
using System.Linq;
#if WINDOWS
using Outgrowth.Platforms.Windows;
#endif

namespace Outgrowth.Views;

public partial class LaboratoryPage : ContentPage
{
    // Station objects - created dynamically on page load
    private readonly List<StationObject> _stationObjects = new()
    {
        new StationObject("ResourceSlot", "Resource Slot", 0, 200, 300, 300, "🌾", Color.FromArgb("#4A4A4A")),
        new StationObject("Extract", "Extract", 0, -200, 250, 250, "⚗️", Color.FromArgb("#2C1A5F"))
    };
    
#if WINDOWS
    private WindowsInput? _windowsInput;
#endif
    
    public LaboratoryPage()
    {
        InitializeComponent();
        BindingContext = new LaboratoryViewModel();
        
        // Set up station object click handlers
        _stationObjects[0].Clicked += (s, e) => OnResourceSlotClicked(s ?? this, e);
        _stationObjects[0].InteractAction = () => System.Diagnostics.Debug.WriteLine("Resource Slot interacted");
        
        _stationObjects[1].Clicked += (s, e) => OnExtractClicked(s ?? this, e);
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
        // Create station objects and add to environment
        CreateStationObjects();
        // Initialize element positions from absolute coordinates
        UpdateStationPositions();
        
#if WINDOWS
        // Attach Windows keyboard input handler (only Esc for closing panels)
        _windowsInput = new WindowsInput(
            onLeftArrow: () => { }, // No action
            onRightArrow: () => { }, // No action
            onEscape: CloseResourcePanel  // Close panel with Esc key
        );
        _windowsInput.Attach();
#endif
    }
    
    private void OnPageDisappearing(object? sender, EventArgs e)
    {
        CloseResourcePanel();
        
#if WINDOWS
        // Detach Windows keyboard input handler
        _windowsInput?.Detach();
        _windowsInput = null;
#endif
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
                
                // Update station object positions from absolute coordinates
                UpdateStationPositions();
                
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

    private void OnExtractClicked(object? sender, EventArgs e)
    {
        // TODO: Implement extract functionality
        System.Diagnostics.Debug.WriteLine("Extract button clicked");
    }

    private void OnBackgroundOverlayTapped(object sender, EventArgs e)
    {
#if ANDROID
        // Only close panel on tap for Android (Windows uses Esc key)
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

