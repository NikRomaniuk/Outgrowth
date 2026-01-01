namespace Outgrowth.ViewModels;

public class LaboratoryViewModel : BaseViewModel
{
    private string _selectedResourceName = string.Empty;

    public LaboratoryViewModel()
    {
        // Nothing to initialize yet
    }

    /// <summary>
    /// Name of currently selected resource (bound to SelectedResourcePanel label)
    /// </summary>
    public string SelectedResourceName
    {
        get => _selectedResourceName;
        set => SetProperty(ref _selectedResourceName, value);
    }
}

