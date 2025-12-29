using System.Text.Json;

namespace Outgrowth.Models;

/// <summary>
/// JSON wrapper for resource library deserialization
/// </summary>
internal class ResourceLibraryJson
{
    public List<ResourceData> Resources { get; set; } = new();
}

/// <summary>
/// Central library containing all game resources. Resources are registered here and can be retrieved by ID
/// </summary>
public static class ResourceLibrary
{
    private static readonly Dictionary<string, ResourceData> _resources = new();
    private static bool _isInitialized = false;
    
    /// <summary>
    /// Initializes the resource library by loading data from JSON file
    /// </summary>
    public static async Task InitializeAsync()
    {
        if (_isInitialized) return;
        
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("Data/ResourceLibrary.json");
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var library = JsonSerializer.Deserialize<ResourceLibraryJson>(json, options);
            
            if (library?.Resources != null)
            {
                foreach (var resource in library.Resources)
                {
                    RegisterResource(resource);
                }
            }
            
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            // Log error or handle as needed
            System.Diagnostics.Debug.WriteLine($"Error loading ResourceLibrary.json: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Registers a resource in the library
    /// </summary>
    public static void RegisterResource(ResourceData resource)
    {
        if (string.IsNullOrEmpty(resource.Id))
        {
            throw new ArgumentException("Resource ID cannot be null or empty", nameof(resource));
        }
        
        if (_resources.ContainsKey(resource.Id))
        {
            throw new ArgumentException($"Resource with ID '{resource.Id}' already exists", nameof(resource));
        }
        
        _resources[resource.Id] = resource;
    }
    
    /// <summary>
    /// Gets a resource by its ID. Throws exception if library is not initialized
    /// </summary>
    public static ResourceData? GetResource(string id)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("ResourceLibrary is not initialized. Call InitializeAsync() first");
        }
        
        return _resources.TryGetValue(id, out var resource) ? resource : null;
    }
    
    /// <summary>
    /// Gets all registered resources. Throws exception if library is not initialized
    /// </summary>
    public static IEnumerable<ResourceData> GetAllResources()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("ResourceLibrary is not initialized. Call InitializeAsync() first");
        }
        
        return _resources.Values;
    }
    
    /// <summary>
    /// Checks if a resource exists in the library. Throws exception if library is not initialized
    /// </summary>
    public static bool ResourceExists(string id)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("ResourceLibrary is not initialized. Call InitializeAsync() first");
        }
        
        return _resources.ContainsKey(id);
    }
    
    /// <summary>
    /// Gets the count of registered resources
    /// </summary>
    public static int ResourceCount => _isInitialized ? _resources.Count : 0;
}

