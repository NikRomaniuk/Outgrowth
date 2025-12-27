namespace Outgrowth.ViewModels;

public class MainMenuViewModel : BaseViewModel
{
    private bool _hasSaveGame;
    private string _lastSaveDate = string.Empty;

    public MainMenuViewModel()
    {
        // Initialize default values
        HasSaveGame = false;
        LastSaveDate = "No save game found";
    }

    public bool HasSaveGame
    {
        get => _hasSaveGame;
        set => SetProperty(ref _hasSaveGame, value);
    }

    public string LastSaveDate
    {
        get => _lastSaveDate;
        set => SetProperty(ref _lastSaveDate, value);
    }
}

