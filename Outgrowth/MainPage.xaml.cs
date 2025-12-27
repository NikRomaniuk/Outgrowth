using Outgrowth.ViewModels;

namespace Outgrowth;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BindingContext = new MainMenuViewModel();
        CheckForSaveGame();
    }

    private void CheckForSaveGame()
    {
        // TODO: Implement save game detection
    }

    private async void OnNewGameClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//HubPage");
    }

    private async void OnContinueClicked(object sender, EventArgs e)
    {
        // TODO: Load save game
        await Shell.Current.GoToAsync("//HubPage");
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        // TODO: Implement settings page
        await DisplayAlert("Settings", "Settings page coming soon!", "OK");
    }

    private void OnExitClicked(object sender, EventArgs e)
    {
        Application.Current?.Quit();
    }
}
