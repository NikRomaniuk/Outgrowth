using System.Text.Json;
using System.Text.Json.Serialization;
using Outgrowth.Models;

namespace Outgrowth.Services;

/// <summary>
/// Service for saving and loading plant states to/from JSON files
/// </summary>
public static class PlantsSaveService
{
    private static readonly string SaveFilePath = Path.Combine(FileSystem.AppDataDirectory, "plants_save.json");
    
    // Store last known pot-to-plant mapping for saving without GreenhousePage instance
    private static Dictionary<int, PlantObject>? _lastKnownPotMapping;
    
    /// <summary>
    /// Data structure for saving plant state
    /// </summary>
    public class PlantSaveData
    {
        [JsonPropertyName("plantId")]
        public string PlantId { get; set; } = string.Empty;
        
        [JsonPropertyName("potNumber")]
        public int PotNumber { get; set; }
        
        [JsonPropertyName("plantedAtCycle")]
        public int PlantedAtCycle { get; set; }
        
        [JsonPropertyName("cyclesLived")]
        public int CyclesLived { get; set; }
    }
    
    /// <summary>
    /// Data structure for saving all plants
    /// </summary>
    private class PlantsSaveFile
    {
        [JsonPropertyName("plants")]
        public List<PlantSaveData> Plants { get; set; } = new();
        
        [JsonPropertyName("lastSavedCycle")]
        public int LastSavedCycle { get; set; }
        
        [JsonPropertyName("currentCycle")]
        public int CurrentCycle { get; set; }
    }
    
    /// <summary>
    /// Updates the last known pot-to-plant mapping. Called whenever plants change.
    /// </summary>
    public static void UpdatePotMapping(Dictionary<int, PlantObject> potNumberToPlant)
    {
        _lastKnownPotMapping = new Dictionary<int, PlantObject>(potNumberToPlant);
        System.Diagnostics.Debug.WriteLine($"[PlantsSaveService] Updated pot mapping with {potNumberToPlant.Count} plants");
    }
    
    /// <summary>
    /// Saves plants with pot mapping (potNumber -> plant data)
    /// Also stores the mapping for future saves without GreenhousePage instance
    /// </summary>
    public static void SavePlantsWithPotMapping(Dictionary<int, PlantObject> potNumberToPlant)
    {
        // Store the mapping for future use
        UpdatePotMapping(potNumberToPlant);
        
        try
        {
            var saveData = new PlantsSaveFile
            {
                Plants = new List<PlantSaveData>(),
                LastSavedCycle = PlantsManager.Instance.CurrentCycle,
                CurrentCycle = PlantsManager.Instance.CurrentCycle
            };
            
            foreach (var kvp in potNumberToPlant)
            {
                var plant = kvp.Value;
                saveData.Plants.Add(new PlantSaveData
                {
                    PlantId = plant.PlantId,
                    PotNumber = kvp.Key,
                    PlantedAtCycle = plant.PlantedAtCycle,
                    CyclesLived = plant.CyclesLived
                });
            }
            
            var json = JsonSerializer.Serialize(saveData, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            // Ensure directory exists before writing
            var directory = Path.GetDirectoryName(SaveFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // Write to file synchronously and flush to ensure data is written to disk
            using (var fileStream = new FileStream(SaveFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fileStream))
            {
                writer.Write(json);
                writer.Flush();
                fileStream.Flush(true); // Flush to disk
            }
            
            System.Diagnostics.Debug.WriteLine($"[PlantsSaveService] Saved {saveData.Plants.Count} plants to {SaveFilePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlantsSaveService] Error saving plants: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Calculates total cycles elapsed based on PersistentTimer elapsed time
    /// Returns total cycles since timer started (not cycles passed since save)
    /// </summary>
    private static int CalculateCyclesPassedSinceSave(int unusedParameter)
    {
        try
        {
            // Get elapsed seconds from PersistentTimer
            double elapsedSeconds = PersistentTimer.Instance.ElapsedSeconds;
            
            // Convert seconds to cycles (1 cycle = 5 seconds)
            const double secondsPerCycle = 5.0;
            int totalCycles = (int)(elapsedSeconds / secondsPerCycle);
            
            System.Diagnostics.Debug.WriteLine($"[PlantsSaveService] Elapsed seconds: {elapsedSeconds}, total cycles: {totalCycles}");
            return totalCycles;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlantsSaveService] Error calculating cycles: {ex.Message}");
            return 0;
        }
    }
    
    /// <summary>
    /// Loads plants from file and returns list of plant data with pot numbers
    /// </summary>
    public static List<(PlantSaveData plantData, int cyclesPassed)> LoadPlants()
    {
        try
        {
            if (!File.Exists(SaveFilePath))
            {
                System.Diagnostics.Debug.WriteLine("[PlantsSaveService] No save file found");
                return new List<(PlantSaveData, int)>();
            }
            
            var json = File.ReadAllText(SaveFilePath);
            var saveData = JsonSerializer.Deserialize<PlantsSaveFile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (saveData == null || saveData.Plants == null)
            {
                System.Diagnostics.Debug.WriteLine("[PlantsSaveService] Save file is empty or invalid");
                return new List<(PlantSaveData, int)>();
            }
            
            // Calculate current cycle based on elapsed time from PersistentTimer
            // This accounts for time passed while the app was closed
            int currentCycleFromTime = CalculateCyclesPassedSinceSave(0); // Total cycles elapsed
            int lastSavedCycle = saveData.LastSavedCycle;
            
            // Calculate cycles passed since last save
            int cyclesPassed = currentCycleFromTime - lastSavedCycle;
            if (cyclesPassed < 0) cyclesPassed = 0; // Safety check
            
            // Restore CurrentCycle to the saved value, then it will continue incrementing from there
            int savedCurrentCycle = saveData.CurrentCycle;
            if (savedCurrentCycle > 0)
            {
                // Set to saved cycle, but we need to account for time passed
                // Actually, we should set it to currentCycleFromTime to match elapsed time
                PlantsManager.Instance.SetCurrentCycle(currentCycleFromTime);
                System.Diagnostics.Debug.WriteLine($"[PlantsSaveService] Restored CurrentCycle to {currentCycleFromTime} (from elapsed time), savedCurrentCycle was {savedCurrentCycle}, lastSavedCycle: {lastSavedCycle}, cyclesPassed: {cyclesPassed}");
            }
            else
            {
                // If no saved cycle, start from calculated cycle
                PlantsManager.Instance.SetCurrentCycle(currentCycleFromTime);
                System.Diagnostics.Debug.WriteLine($"[PlantsSaveService] No saved CurrentCycle, using calculated: {currentCycleFromTime}, cyclesPassed: {cyclesPassed}");
            }
            
            return saveData.Plants.Select(p => (p, cyclesPassed)).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlantsSaveService] Error loading plants: {ex.Message}");
            return new List<(PlantSaveData, int)>();
        }
    }
    
    /// <summary>
    /// Saves game state using last known pot mapping (can be called without GreenhousePage instance)
    /// Note: This uses references to PlantObject instances, so current values (like CyclesLived) will be saved
    /// </summary>
    public static void SaveGameState()
    {
        if (_lastKnownPotMapping != null && _lastKnownPotMapping.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"[PlantsSaveService] SaveGameState called with {_lastKnownPotMapping.Count} plants from last known mapping");
            // Update cycles lived before saving (since objects are updated continuously)
            foreach (var plant in _lastKnownPotMapping.Values)
            {
                // Update CyclesLived to current value
                plant.CyclesLived = PlantsManager.Instance.CurrentCycle - plant.PlantedAtCycle;
            }
            SavePlantsWithPotMapping(_lastKnownPotMapping);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[PlantsSaveService] SaveGameState called but no last known mapping exists - cannot save plants");
        }
    }
    
    /// <summary>
    /// Checks if a save file exists
    /// </summary>
    public static bool SaveFileExists()
    {
        return File.Exists(SaveFilePath);
    }
    
    /// <summary>
    /// Deletes the save file
    /// </summary>
    public static void DeleteSaveFile()
    {
        try
        {
            if (File.Exists(SaveFilePath))
            {
                File.Delete(SaveFilePath);
                System.Diagnostics.Debug.WriteLine("[PlantsSaveService] Save file deleted");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlantsSaveService] Error deleting save file: {ex.Message}");
        }
    }
}

