using Outgrowth.Services;

namespace Outgrowth.Models;

/// <summary>
/// Central library containing all plant types. Delegates to GameDataManager for optimized loading
/// Maintained for backward compatibility with existing code
/// </summary>
public static class PlantLibrary
{
    /// <summary>
    /// Initializes the plant library. Now delegates to GameDataManager
    /// </summary>
    public static Task InitializeAsync() => GameDataManager.Plants.InitializeAsync();
    
    /// <summary>
    /// Gets a plant by its ID
    /// </summary>
    public static PlantData? GetPlant(string id) => GameDataManager.Plants.Get(id);
    
    /// <summary>
    /// Gets all plants of a specific size
    /// </summary>
    public static IEnumerable<PlantData> GetPlantsBySize(string plantSize) =>
        GameDataManager.Plants.GetAll().Where(p => p.PlantSize.Equals(plantSize, StringComparison.OrdinalIgnoreCase));
    
    /// <summary>
    /// Gets all registered plants
    /// </summary>
    public static IEnumerable<PlantData> GetAllPlants() => GameDataManager.Plants.GetAll();
    
    /// <summary>
    /// Checks if a plant exists in the library
    /// </summary>
    public static bool PlantExists(string id) => GameDataManager.Plants.Get(id) != null;
    
    /// <summary>
    /// Gets the count of registered plants
    /// </summary>
    public static int PlantCount => GameDataManager.Plants.Count;
}

