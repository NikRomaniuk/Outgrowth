namespace Outgrowth.ViewModels;

public class HubViewModel : BaseViewModel
{
    private string _stationStatus = string.Empty;
    private bool _isOnline;
    private bool _isMarketPanelVisible;
    private bool _isQuestPanelVisible;
    private bool _isStatsPanelVisible;

    public HubViewModel()
    {
        // Initialize default values
        StationStatus = "Online";
        IsOnline = true;
        IsMarketPanelVisible = false;
        IsQuestPanelVisible = false;
        IsStatsPanelVisible = false;
    }

    public string StationStatus
    {
        get => _stationStatus;
        set => SetProperty(ref _stationStatus, value);
    }

    public bool IsOnline
    {
        get => _isOnline;
        set => SetProperty(ref _isOnline, value);
    }

    public bool IsMarketPanelVisible
    {
        get => _isMarketPanelVisible;
        set => SetProperty(ref _isMarketPanelVisible, value);
    }

    public bool IsQuestPanelVisible
    {
        get => _isQuestPanelVisible;
        set => SetProperty(ref _isQuestPanelVisible, value);
    }

    public bool IsStatsPanelVisible
    {
        get => _isStatsPanelVisible;
        set => SetProperty(ref _isStatsPanelVisible, value);
    }
}

