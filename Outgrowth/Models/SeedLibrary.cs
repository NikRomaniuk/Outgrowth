using Outgrowth.Services;

namespace Outgrowth.Models;

/// <summary>
/// Central library containing all game seeds. Delegates to GameDataManager for optimized loading
/// Maintained for backward compatibility with existing code
/// </summary>
public static class SeedLibrary
{
    /// <summary>
    /// Initializes the seed library. Now delegates to GameDataManager
    /// </summary>
    public static Task InitializeAsync() => GameDataManager.Seeds.InitializeAsync();
    
    /// <summary>
    /// Gets a seed by its ID
    /// </summary>
    public static SeedData? GetSeed(string id) => GameDataManager.Seeds.Get(id);
    
    /// <summary>
    /// Gets all registered seeds
    /// </summary>
    public static IEnumerable<SeedData> GetAllSeeds() => GameDataManager.Seeds.GetAll();
    
    /// <summary>
    /// Checks if a seed exists in the library
    /// </summary>
    public static bool SeedExists(string id) => GameDataManager.Seeds.Get(id) != null;
    
    /// <summary>
    /// Gets the count of registered seeds
    /// </summary>
    public static int SeedCount => GameDataManager.Seeds.Count;
}

