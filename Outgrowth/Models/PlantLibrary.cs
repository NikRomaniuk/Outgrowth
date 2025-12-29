using System.Text.Json;

namespace Outgrowth.Models;

/// <summary>
/// JSON wrapper for plant library deserialization
/// </summary>
internal class PlantLibraryJson
{
    public List<PlantData> Plants { get; set; } = new();
}

/// <summary>
/// Central library containing all plant types. Plants are registered here and can be retrieved by ID
/// </summary>
public static class PlantLibrary
{
    private static readonly Dictionary<string, PlantData> _plants = new();
    private static bool _isInitialized = false;
    private static readonly SemaphoreSlim _initSemaphore = new SemaphoreSlim(1, 1);
    
    /// <summary>
    /// Initializes the plant library by loading data from JSON file
    /// </summary>
    public static async Task InitializeAsync()
    {
        if (_isInitialized) return;
        
        await _initSemaphore.WaitAsync();
        try
        {
            if (_isInitialized) return; // Double-check after acquiring semaphore
            
            using var stream = await FileSystem.OpenAppPackageFileAsync("Data/PlantLibrary.json");
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var library = JsonSerializer.Deserialize<PlantLibraryJson>(json, options);
            
            if (library?.Plants != null)
            {
                foreach (var plant in library.Plants)
                {
                    // Validate arrays have matching lengths
                    if (plant.GrowthStageSprites.Length != plant.GrowthStageCycles.Length)
                    {
                        throw new ArgumentException($"Plant '{plant.Id}' has mismatched GrowthStageSprites and GrowthStageCycles arrays.");
                    }
                    
                    RegisterPlant(plant);
                }
            }
            
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            // Log error or handle as needed
            System.Diagnostics.Debug.WriteLine($"Error loading PlantLibrary.json: {ex.Message}");
            throw;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }
    
    /// <summary>
    /// Registers a plant in the library. If plant already exists, does nothing (idempotent)
    /// </summary>
    public static void RegisterPlant(PlantData plant)
    {
        if (string.IsNullOrEmpty(plant.Id))
        {
            throw new ArgumentException("Plant ID cannot be null or empty.", nameof(plant));
        }
        
        if (_plants.ContainsKey(plant.Id))
        {
            // Plant already registered, skip (idempotent operation)
            return;
        }
        
        _plants[plant.Id] = plant;
    }
    
    /// <summary>
    /// Gets a plant by its ID. Throws exception if library is not initialized
    /// </summary>
    public static PlantData? GetPlant(string id)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("PlantLibrary is not initialized. Call InitializeAsync() first.");
        }
        
        return _plants.TryGetValue(id, out var plant) ? plant : null;
    }
    
    /// <summary>
    /// Gets all plants of a specific size. Throws exception if library is not initialized
    /// </summary>
    public static IEnumerable<PlantData> GetPlantsBySize(string plantSize)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("PlantLibrary is not initialized. Call InitializeAsync() first.");
        }
        
        return _plants.Values.Where(p => p.PlantSize.Equals(plantSize, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Gets all registered plants. Throws exception if library is not initialized
    /// </summary>
    public static IEnumerable<PlantData> GetAllPlants()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("PlantLibrary is not initialized. Call InitializeAsync() first.");
        }
        
        return _plants.Values;
    }
    
    /// <summary>
    /// Checks if a plant exists in the library. Throws exception if library is not initialized
    /// </summary>
    public static bool PlantExists(string id)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("PlantLibrary is not initialized. Call InitializeAsync() first.");
        }
        
        return _plants.ContainsKey(id);
    }
    
    /// <summary>
    /// Gets the count of registered plants
    /// </summary>
    public static int PlantCount => _isInitialized ? _plants.Count : 0;
}

