using Outgrowth.ViewModels;
using Outgrowth.Views;
using Outgrowth.Services;

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
        // Save game state before quitting
        System.Diagnostics.Debug.WriteLine("[MainPage] OnExitClicked - saving game state before quit");
        
        try
        {
            // Try to save from GreenhousePage if loaded (most up-to-date state)
            GreenhousePage.SavePlantsIfLoaded();
            
            // Also try to save from last known mapping (fallback if page not loaded)
            PlantsSaveService.SaveGameState();
            
            // Save timer state (this will flush to disk)
            PersistentTimer.Instance.Stop();
            
            System.Diagnostics.Debug.WriteLine("[MainPage] Game state saved before quit");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] Error saving game state: {ex.Message}");
        }
        
        Application.Current?.Quit();
    }
}
