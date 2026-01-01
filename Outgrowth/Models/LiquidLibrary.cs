using Outgrowth.Services;

namespace Outgrowth.Models;

/// <summary>
/// Central library containing all game liquids. Delegates to GameDataManager for optimized loading
/// Maintained for backward compatibility with existing code
/// </summary>
public static class LiquidLibrary
{
    /// <summary>
    /// Initializes the liquid library. Now delegates to GameDataManager
    /// </summary>
    public static Task InitializeAsync() => GameDataManager.Liquids.InitializeAsync();
    
    /// <summary>
    /// Gets a liquid by its ID
    /// </summary>
    public static LiquidData? GetLiquid(string id) => GameDataManager.Liquids.Get(id);
    
    /// <summary>
    /// Gets all registered liquids
    /// </summary>
    public static IEnumerable<LiquidData> GetAllLiquids() => GameDataManager.Liquids.GetAll();
    
    /// <summary>
    /// Checks if a liquid exists in the library
    /// </summary>
    public static bool LiquidExists(string id) => GameDataManager.Liquids.Get(id) != null;
    
    /// <summary>
    /// Gets the count of registered liquids
    /// </summary>
    public static int LiquidCount => GameDataManager.Liquids.Count;
}

