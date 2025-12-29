using System.Text.Json;

namespace Outgrowth.Models;

/// <summary>
/// JSON wrapper for seed library deserialization
/// </summary>
internal class SeedLibraryJson
{
    public List<SeedData> Seeds { get; set; } = new();
}

/// <summary>
/// Central library containing all game seeds. Seeds are registered here and can be retrieved by ID
/// </summary>
public static class SeedLibrary
{
    private static readonly Dictionary<string, SeedData> _seeds = new();
    private static bool _isInitialized = false;
    
    /// <summary>
    /// Initializes the seed library by loading data from JSON file
    /// </summary>
    public static async Task InitializeAsync()
    {
        if (_isInitialized) return;
        
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("Data/SeedLibrary.json");
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var library = JsonSerializer.Deserialize<SeedLibraryJson>(json, options);
            
            if (library?.Seeds != null)
            {
                foreach (var seed in library.Seeds)
                {
                    RegisterSeed(seed);
                }
            }
            
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            // Log error or handle as needed
            System.Diagnostics.Debug.WriteLine($"Error loading SeedLibrary.json: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Registers a seed in the library
    /// </summary>
    public static void RegisterSeed(SeedData seed)
    {
        if (string.IsNullOrEmpty(seed.Id))
        {
            throw new ArgumentException("Seed ID cannot be null or empty.", nameof(seed));
        }
        
        if (_seeds.ContainsKey(seed.Id))
        {
            throw new ArgumentException($"Seed with ID '{seed.Id}' already exists.", nameof(seed));
        }
        
        _seeds[seed.Id] = seed;
    }
    
    /// <summary>
    /// Gets a seed by its ID. Throws exception if library is not initialized
    /// </summary>
    public static SeedData? GetSeed(string id)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("SeedLibrary is not initialized. Call InitializeAsync() first.");
        }
        
        return _seeds.TryGetValue(id, out var seed) ? seed : null;
    }
    
    /// <summary>
    /// Gets all registered seeds. Throws exception if library is not initialized
    /// </summary>
    public static IEnumerable<SeedData> GetAllSeeds()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("SeedLibrary is not initialized. Call InitializeAsync() first.");
        }
        
        return _seeds.Values;
    }
    
    /// <summary>
    /// Checks if a seed exists in the library. Throws exception if library is not initialized
    /// </summary>
    public static bool SeedExists(string id)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("SeedLibrary is not initialized. Call InitializeAsync() first.");
        }
        
        return _seeds.ContainsKey(id);
    }
    
    /// <summary>
    /// Gets the count of registered seeds
    /// </summary>
    public static int SeedCount => _isInitialized ? _seeds.Count : 0;
}

