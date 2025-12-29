using System.Text.Json;

namespace Outgrowth.Models;

/// <summary>
/// JSON wrapper for liquid library deserialization
/// </summary>
internal class LiquidLibraryJson
{
    public List<LiquidData> Liquids { get; set; } = new();
}

/// <summary>
/// Central library containing all game liquids. Liquids are registered here and can be retrieved by ID
/// </summary>
public static class LiquidLibrary
{
    private static readonly Dictionary<string, LiquidData> _liquids = new();
    private static bool _isInitialized = false;
    
    /// <summary>
    /// Initializes the liquid library by loading data from JSON file
    /// </summary>
    public static async Task InitializeAsync()
    {
        if (_isInitialized) return;
        
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("Data/LiquidLibrary.json");
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var library = JsonSerializer.Deserialize<LiquidLibraryJson>(json, options);
            
            if (library?.Liquids != null)
            {
                foreach (var liquid in library.Liquids)
                {
                    RegisterLiquid(liquid);
                }
            }
            
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            // Log error or handle as needed
            System.Diagnostics.Debug.WriteLine($"Error loading LiquidLibrary.json: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Registers a liquid in the library
    /// </summary>
    public static void RegisterLiquid(LiquidData liquid)
    {
        if (string.IsNullOrEmpty(liquid.Id))
        {
            throw new ArgumentException("Liquid ID cannot be null or empty.", nameof(liquid));
        }
        
        if (_liquids.ContainsKey(liquid.Id))
        {
            throw new ArgumentException($"Liquid with ID '{liquid.Id}' already exists.", nameof(liquid));
        }
        
        _liquids[liquid.Id] = liquid;
    }
    
    /// <summary>
    /// Gets a liquid by its ID. Throws exception if library is not initialized
    /// </summary>
    public static LiquidData? GetLiquid(string id)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("LiquidLibrary is not initialized. Call InitializeAsync() first.");
        }
        
        return _liquids.TryGetValue(id, out var liquid) ? liquid : null;
    }
    
    /// <summary>
    /// Gets all registered liquids. Throws exception if library is not initialized
    /// </summary>
    public static IEnumerable<LiquidData> GetAllLiquids()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("LiquidLibrary is not initialized. Call InitializeAsync() first.");
        }
        
        return _liquids.Values;
    }
    
    /// <summary>
    /// Checks if a liquid exists in the library. Throws exception if library is not initialized
    /// </summary>
    public static bool LiquidExists(string id)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("LiquidLibrary is not initialized. Call InitializeAsync() first.");
        }
        
        return _liquids.ContainsKey(id);
    }
    
    /// <summary>
    /// Gets the count of registered liquids
    /// </summary>
    public static int LiquidCount => _isInitialized ? _liquids.Count : 0;
}

