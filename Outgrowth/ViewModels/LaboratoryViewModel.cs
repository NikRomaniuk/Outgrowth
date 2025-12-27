namespace Outgrowth.ViewModels;

public class LaboratoryViewModel : BaseViewModel
{
    private int _seedTypes;
    private double _reagentAmount;
    private int _researchSamples;
    private int _activeResearchCount;

    public LaboratoryViewModel()
    {
        // Initialize default inventory values
        SeedTypes = 0;
        ReagentAmount = 0.0;
        ResearchSamples = 0;
        ActiveResearchCount = 0;
    }

    public int SeedTypes
    {
        get => _seedTypes;
        set => SetProperty(ref _seedTypes, value);
    }

    public double ReagentAmount
    {
        get => _reagentAmount;
        set => SetProperty(ref _reagentAmount, value);
    }

    public int ResearchSamples
    {
        get => _researchSamples;
        set => SetProperty(ref _researchSamples, value);
    }

    public int ActiveResearchCount
    {
        get => _activeResearchCount;
        set => SetProperty(ref _activeResearchCount, value);
    }
}

