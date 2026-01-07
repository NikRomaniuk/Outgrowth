using System.Text.Json;
using Outgrowth.Models;

namespace Outgrowth.Services;

/// <summary>
/// Marker interface for all game data items that can be loaded from JSON libraries
/// Ensures each data type has an Id property for dictionary key lookup
/// </summary>
public interface IGameData
{
    string Id { get; }
}

/// <summary>
/// Generic data library for loading and caching game data from JSON files
/// Thread-safe with one-time initialization guarantee
/// </summary>
public class DataLibrary<T> where T : IGameData
{
    private readonly Dictionary<string, T> _data = new();
    private bool _isInitialized = false;
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);
    private readonly string _jsonFileName;
    private readonly string _jsonArrayPropertyName;

    public DataLibrary(string jsonFileName, string jsonArrayPropertyName)
    {
        _jsonFileName = jsonFileName;
        _jsonArrayPropertyName = jsonArrayPropertyName;
    }

    /// <summary>
    /// Initialize library by loading JSON data once. Subsequent calls are no-op
    /// Thread-safe with double-check locking pattern
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        await _initSemaphore.WaitAsync();
        try
        {
            if (_isInitialized) return;

            using var stream = await FileSystem.OpenAppPackageFileAsync($"Data/{_jsonFileName}");
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            using var doc = JsonDocument.Parse(json);
            
            if (doc.RootElement.TryGetProperty(_jsonArrayPropertyName, out var arrayElement))
            {
                var items = JsonSerializer.Deserialize<List<T>>(arrayElement.GetRawText(), options);
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        if (string.IsNullOrEmpty(item.Id))
                            throw new ArgumentException($"Item in {_jsonFileName} has null or empty Id");
                        
                        _data[item.Id] = item;
                    }
                }
            }

            _isInitialized = true;
            System.Diagnostics.Debug.WriteLine($"[GameDataManager] Loaded {_data.Count} items from {_jsonFileName}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameDataManager] Error loading {_jsonFileName}: {ex.Message}");
            throw;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    /// <summary>
    /// Get item by ID. Returns null if not found
    /// Throws if library not initialized
    /// </summary>
    public T? Get(string id)
    {
        if (!_isInitialized)
            throw new InvalidOperationException($"Library {_jsonFileName} not initialized. Call InitializeAsync() first.");
        
        return _data.TryGetValue(id, out var item) ? item : default;
    }

    /// <summary>
    /// Get all items. Returns empty collection if none exist
    /// Throws if library not initialized
    /// </summary>
    public IEnumerable<T> GetAll()
    {
        if (!_isInitialized)
            throw new InvalidOperationException($"Library {_jsonFileName} not initialized. Call InitializeAsync() first.");
        
        return _data.Values;
    }

    /// <summary>
    /// Check if library is initialized without throwing
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Get count of loaded items (0 if not initialized)
    /// </summary>
    public int Count => _data.Count;
}

/// <summary>
/// Central manager for all game data libraries
/// Loads all JSON data once at application startup
/// Provides static access to typed data libraries
/// </summary>
public static class GameDataManager
{
    public static DataLibrary<PlantData> Plants { get; } = new("PlantLibrary.json", "plants");
    public static DataLibrary<SeedData> Seeds { get; } = new("SeedLibrary.json", "seeds");
    public static DataLibrary<LiquidData> Liquids { get; } = new("LiquidLibrary.json", "liquids");
    public static DataLibrary<ResourceData> Resources { get; } = new("ResourceLibrary.json", "resources");

    private static bool _isInitialized = false;
    private static readonly SemaphoreSlim _initSemaphore = new(1, 1);

    /// <summary>
    /// Initialize all game data libraries in parallel
    /// Call once at application startup before any pages load
    /// Subsequent calls are no-op (idempotent)
    /// </summary>
    public static async Task InitializeAllAsync()
    {
        if (_isInitialized) return;

        await _initSemaphore.WaitAsync();
        try
        {
            if (_isInitialized) return;

            var startTime = DateTime.Now;
            System.Diagnostics.Debug.WriteLine("[GameDataManager] Starting parallel initialization of all libraries...");

            // Load all libraries in parallel for faster startup
            await Task.WhenAll(
                Plants.InitializeAsync(),
                Seeds.InitializeAsync(),
                Liquids.InitializeAsync(),
                Resources.InitializeAsync()
            );

            // After libraries are loaded, apply saved material quantities (if any)
            try
            {
                var saved = MaterialsSaveService.LoadMaterials();

                // Seeds
                if (Seeds.IsInitialized)
                {
                    foreach (var seed in Seeds.GetAll())
                    {
                        if (saved.Seeds != null && saved.Seeds.TryGetValue(seed.Id, out var qty))
                        {
                            seed.Quantity = qty;
                        }
                    }
                }

                // Liquids
                if (Liquids.IsInitialized)
                {
                    foreach (var liquid in Liquids.GetAll())
                    {
                        if (saved.Liquids != null && saved.Liquids.TryGetValue(liquid.Id, out var qty))
                        {
                            liquid.Quantity = qty;
                        }
                    }
                }

                // Resources
                if (Resources.IsInitialized)
                {
                    foreach (var res in Resources.GetAll())
                    {
                        if (saved.Resources != null && saved.Resources.TryGetValue(res.Id, out var qty))
                        {
                            res.Quantity = qty;
                        }
                    }
                }

                int total = (saved.Resources?.Count ?? 0) + (saved.Liquids?.Count ?? 0) + (saved.Seeds?.Count ?? 0);
                System.Diagnostics.Debug.WriteLine($"[GameDataManager] Applied saved material quantities ({total} items)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameDataManager] Error applying saved material quantities: {ex.Message}");
            }

            // Ensure player has starter seeds on first run if none exist
            try
            {
                if (Seeds.IsInitialized)
                {
                    bool needsSave = false;
                    
                    // Grant 1 grass_seed if player has none
                    var grassSeed = Seeds.Get("grass_seed");
                    if (grassSeed != null && grassSeed.Quantity <= 0)
                    {
                        grassSeed.Quantity = 1;
                        System.Diagnostics.Debug.WriteLine("[GameDataManager] No grass seeds found; granting 1 grass_seed on startup.");
                        needsSave = true;
                    }
                    
                    // Grant 1 lumivial_seed if player has none
                    var lumivialSeed = Seeds.Get("lumivial_seed");
                    if (lumivialSeed != null && lumivialSeed.Quantity <= 0)
                    {
                        lumivialSeed.Quantity = 1;
                        System.Diagnostics.Debug.WriteLine("[GameDataManager] No lumivial seeds found; granting 1 lumivial_seed on startup.");
                        needsSave = true;
                    }
                    
                    // Persist the granted seeds immediately so save files reflect the change
                    if (needsSave)
                    {
                        SaveMaterialsState();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameDataManager] Error ensuring starter seeds: {ex.Message}");
            }

            _isInitialized = true;
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            System.Diagnostics.Debug.WriteLine($"[GameDataManager] All libraries initialized in {elapsed:F0}ms");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameDataManager] FATAL: Failed to initialize game data: {ex.Message}");
            throw;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    /// <summary>
    /// Check if all libraries are initialized
    /// </summary>
    public static bool IsInitialized => _isInitialized;

    /// <summary>
    /// Collects current material quantities from data libraries and saves them to disk
    /// </summary>
    public static void SaveMaterialsState()
    {
        try
        {
            Dictionary<string, int>? resources = null;
            Dictionary<string, int>? liquids = null;
            Dictionary<string, int>? seeds = null;

            if (Resources.IsInitialized)
                resources = Resources.GetAll().ToDictionary(r => r.Id, r => r.Quantity);
            if (Liquids.IsInitialized)
                liquids = Liquids.GetAll().ToDictionary(l => l.Id, l => l.Quantity);
            if (Seeds.IsInitialized)
                seeds = Seeds.GetAll().ToDictionary(s => s.Id, s => s.Quantity);

            MaterialsSaveService.SaveMaterials(resources, liquids, seeds);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameDataManager] Error saving materials state: {ex.Message}");
        }
    }
}
