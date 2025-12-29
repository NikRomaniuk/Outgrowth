using Outgrowth.Models;

namespace Outgrowth.Services;

/// <summary>
/// Manages all plants in the game. Handles growth updates via events
/// Uses singleton pattern for global access
/// </summary>
public class PlantsManager
{
    private static PlantsManager? _instance;
    private readonly List<PlantObject> _plants = new();
    private System.Timers.Timer? _updateTimer;
    private const int UpdateIntervalMs = 5000; // Update every 5 seconds (1 cycle)
    private int _currentCycle = 0;
    
    /// <summary>
    /// Event raised when plants should update their growth
    /// </summary>
    public event EventHandler? GrowthUpdate;
    
    /// <summary>
    /// Gets the current cycle number (increments every 5 seconds)
    /// </summary>
    public int CurrentCycle => _currentCycle;
    
    /// <summary>
    /// Sets the current cycle number (used when loading from save)
    /// </summary>
    public void SetCurrentCycle(int cycle)
    {
        _currentCycle = cycle;
        System.Diagnostics.Debug.WriteLine($"[PlantsManager] CurrentCycle set to {_currentCycle}");
    }
    
    /// <summary>
    /// Gets the singleton instance of PlantsManager
    /// </summary>
    public static PlantsManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new PlantsManager();
            }
            return _instance;
        }
    }
    
    private PlantsManager()
    {
        // Initialize periodic update timer
        _updateTimer = new System.Timers.Timer(UpdateIntervalMs);
        _updateTimer.Elapsed += (sender, e) =>
        {
            _currentCycle++;
            System.Diagnostics.Debug.WriteLine($"[PlantsManager] Cycle {_currentCycle} started");
            // Update UI on main thread (System.Timers.Timer runs on background thread)
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateAllPlants();
            });
        };
        _updateTimer.AutoReset = true;
    }
    
    /// <summary>
    /// Starts periodic growth updates
    /// </summary>
    public void StartPeriodicUpdates()
    {
        if (_updateTimer != null && !_updateTimer.Enabled)
        {
            _updateTimer.Start();
            System.Diagnostics.Debug.WriteLine("[PlantsManager] Started periodic growth updates (interval: 5s = 1 cycle)");
        }
    }
    
    /// <summary>
    /// Stops periodic growth updates
    /// </summary>
    public void StopPeriodicUpdates()
    {
        if (_updateTimer != null && _updateTimer.Enabled)
        {
            _updateTimer.Stop();
            System.Diagnostics.Debug.WriteLine("[PlantsManager] Stopped periodic growth updates");
        }
    }
    
    /// <summary>
    /// Registers a plant to be managed
    /// </summary>
    public void RegisterPlant(PlantObject plant)
    {
        if (!_plants.Contains(plant))
        {
            _plants.Add(plant);
            // Subscribe to growth update event
            GrowthUpdate += plant.OnGrowthUpdate;
            System.Diagnostics.Debug.WriteLine($"[PlantsManager] Registered plant: {plant.Id} (PlantId: {plant.PlantId}), Total plants: {_plants.Count}");
        }
    }
    
    /// <summary>
    /// Unregisters a plant from management
    /// </summary>
    public void UnregisterPlant(PlantObject plant)
    {
        if (_plants.Contains(plant))
        {
            GrowthUpdate -= plant.OnGrowthUpdate;
            _plants.Remove(plant);
        }
    }
    
    /// <summary>
    /// Triggers growth update for all registered plants
    /// </summary>
    public void UpdateAllPlants()
    {
        System.Diagnostics.Debug.WriteLine($"[PlantsManager] UpdateAllPlants called, registered plants: {_plants.Count}");
        if (GrowthUpdate == null)
        {
            System.Diagnostics.Debug.WriteLine("[PlantsManager] GrowthUpdate event is null - no plants subscribed");
            return;
        }
        System.Diagnostics.Debug.WriteLine($"[PlantsManager] Invoking GrowthUpdate event with {GrowthUpdate.GetInvocationList().Length} subscribers");
        GrowthUpdate?.Invoke(this, EventArgs.Empty);
        System.Diagnostics.Debug.WriteLine("[PlantsManager] GrowthUpdate event invoked");
    }
    
    /// <summary>
    /// Gets all registered plants
    /// </summary>
    public IEnumerable<PlantObject> GetAllPlants() => _plants;
    
    /// <summary>
    /// Gets the count of registered plants
    /// </summary>
    public int PlantCount => _plants.Count;
    
    /// <summary>
    /// Clears all registered plants
    /// </summary>
    public void Clear()
    {
        StopPeriodicUpdates();
        foreach (var plant in _plants)
        {
            GrowthUpdate -= plant.OnGrowthUpdate;
        }
        _plants.Clear();
    }
    
    /// <summary>
    /// Resets the singleton instance (useful for testing or resetting the game)
    /// </summary>
    public static void ResetInstance()
    {
        _instance?.Clear();
        _instance = null;
    }
}

