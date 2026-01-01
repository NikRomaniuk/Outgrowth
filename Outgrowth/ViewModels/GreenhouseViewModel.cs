namespace Outgrowth.ViewModels;

public class GreenhouseViewModel : BaseViewModel
{
    private string _selectedLiquidName = string.Empty;
    private string _selectedSeedName = string.Empty;

    public GreenhouseViewModel()
    {
        // Nothing to initialize yet
    }

    /// <summary>
    /// Name of currently selected liquid (bound to SelectedLiquidPanel label)
    /// </summary>
    public string SelectedLiquidName
    {
        get => _selectedLiquidName;
        set => SetProperty(ref _selectedLiquidName, value);
    }

    /// <summary>
    /// Name of currently selected seed (bound to SelectedSeedPanel label)
    /// </summary>
    public string SelectedSeedName
    {
        get => _selectedSeedName;
        set => SetProperty(ref _selectedSeedName, value);
    }
}

