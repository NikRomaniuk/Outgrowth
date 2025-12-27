using Outgrowth.Views;

namespace Outgrowth;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes for navigation
        Routing.RegisterRoute(nameof(HubPage), typeof(HubPage));
        Routing.RegisterRoute(nameof(GreenhousePage), typeof(GreenhousePage));
        Routing.RegisterRoute(nameof(LaboratoryPage), typeof(LaboratoryPage));
    }
}
