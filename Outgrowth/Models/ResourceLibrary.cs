using Outgrowth.Services;

namespace Outgrowth.Models;

/// <summary>
/// Central library containing all game resources. Delegates to GameDataManager for optimized loading
/// Maintained for backward compatibility with existing code
/// </summary>
public static class ResourceLibrary
{
    /// <summary>
    /// Initializes the resource library. Now delegates to GameDataManager
    /// </summary>
    public static Task InitializeAsync() => GameDataManager.Resources.InitializeAsync();
    
    /// <summary>
    /// Gets a resource by its ID
    /// </summary>
    public static ResourceData? GetResource(string id) => GameDataManager.Resources.Get(id);
    
    /// <summary>
    /// Gets all registered resources
    /// </summary>
    public static IEnumerable<ResourceData> GetAllResources() => GameDataManager.Resources.GetAll();
    
    /// <summary>
    /// Checks if a resource exists in the library
    /// </summary>
    public static bool ResourceExists(string id) => GameDataManager.Resources.Get(id) != null;
    
    /// <summary>
    /// Gets the count of registered resources
    /// </summary>
    public static int ResourceCount => GameDataManager.Resources.Count;
}

