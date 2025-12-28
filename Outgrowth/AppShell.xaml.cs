using Outgrowth.Views;
using Outgrowth.Services;

namespace Outgrowth;

public partial class AppShell : Shell
{
    private BoxView? _fadeOverlay;
    
    public AppShell()
    {
        InitializeComponent();

        // Register routes for navigation
        Routing.RegisterRoute(nameof(HubPage), typeof(HubPage));
        Routing.RegisterRoute(nameof(GreenhousePage), typeof(GreenhousePage));
        Routing.RegisterRoute(nameof(LaboratoryPage), typeof(LaboratoryPage));
        
        // Create fade overlay for navigation transitions
        CreateFadeOverlay();
    }
    
    private void CreateFadeOverlay()
    {
        _fadeOverlay = new BoxView
        {
            Color = Colors.Black,
            Opacity = 0,
            IsVisible = false,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            InputTransparent = false,
            ZIndex = 10000
        };
        
        // Add overlay to Shell's visual tree
        // This is a workaround: we'll add it to Window.Overlays in NavigationService
        NavigationService.Initialize(_fadeOverlay, this);
    }
}
