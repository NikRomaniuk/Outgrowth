namespace Outgrowth.ViewModels;

public class GreenhouseViewModel : BaseViewModel
{
    private double _temperature;
    private double _oxygenLevel;
    private int _lightIntensity;
    private int _totalPlants;
    private int _maxCapacity;

    public GreenhouseViewModel()
    {
        // Initialize default environmental values
        Temperature = 22.0;
        OxygenLevel = 21.0;
        LightIntensity = 100;
        TotalPlants = 0;
        MaxCapacity = 20;
    }

    public double Temperature
    {
        get => _temperature;
        set => SetProperty(ref _temperature, value);
    }

    public double OxygenLevel
    {
        get => _oxygenLevel;
        set => SetProperty(ref _oxygenLevel, value);
    }

    public int LightIntensity
    {
        get => _lightIntensity;
        set => SetProperty(ref _lightIntensity, value);
    }

    public int TotalPlants
    {
        get => _totalPlants;
        set => SetProperty(ref _totalPlants, value);
    }

    public int MaxCapacity
    {
        get => _maxCapacity;
        set => SetProperty(ref _maxCapacity, value);
    }
}

