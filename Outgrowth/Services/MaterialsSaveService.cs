using System.Text.Json;
using System.Text.Json.Serialization;
using Outgrowth.Models;

namespace Outgrowth.Services;

/// <summary>
/// Service for saving and loading player materials (resources, seeds, liquids)
/// Stored as simple id/type/quantity records in JSON
/// </summary>
public static class MaterialsSaveService
{
    private static readonly string SaveFilePath = Path.Combine(FileSystem.AppDataDirectory, "materials_save.json");

    // last known mappings by category
    private static Dictionary<string, int>? _lastKnownResources;
    private static Dictionary<string, int>? _lastKnownLiquids;
    private static Dictionary<string, int>? _lastKnownSeeds;

    private class MaterialsSaveFile
    {
        [JsonPropertyName("resources")]
        public Dictionary<string, int> Resources { get; set; } = new();

        [JsonPropertyName("liquids")]
        public Dictionary<string, int> Liquids { get; set; } = new();

        [JsonPropertyName("seeds")]
        public Dictionary<string, int> Seeds { get; set; } = new();
    }

    /// <summary>
    /// Saves explicit mappings categorized by resources/liquids/seeds
    /// </summary>
    public static void SaveMaterials(Dictionary<string, int>? resources = null, Dictionary<string, int>? liquids = null, Dictionary<string, int>? seeds = null)
    {
        // Update last-known mappings
        if (resources != null)
            _lastKnownResources = new Dictionary<string, int>(resources);
        if (liquids != null)
            _lastKnownLiquids = new Dictionary<string, int>(liquids);
        if (seeds != null)
            _lastKnownSeeds = new Dictionary<string, int>(seeds);

        try
        {
            var saveData = new MaterialsSaveFile
            {
                Resources = resources ?? new Dictionary<string, int>(),
                Liquids = liquids ?? new Dictionary<string, int>(),
                Seeds = seeds ?? new Dictionary<string, int>()
            };

            var json = JsonSerializer.Serialize(saveData, new JsonSerializerOptions { WriteIndented = true });

            var directory = Path.GetDirectoryName(SaveFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var fileStream = new FileStream(SaveFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fileStream))
            {
                writer.Write(json);
                writer.Flush();
                fileStream.Flush(true);
            }

            System.Diagnostics.Debug.WriteLine($"[MaterialsSaveService] Saved materials to {SaveFilePath}: resources={saveData.Resources.Count}, liquids={saveData.Liquids.Count}, seeds={saveData.Seeds.Count}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MaterialsSaveService] Error saving materials: {ex.Message}");
        }
    }

    public class MaterialsLoadResult
    {
        public Dictionary<string, int> Resources { get; set; } = new();
        public Dictionary<string, int> Liquids { get; set; } = new();
        public Dictionary<string, int> Seeds { get; set; } = new();
    }

    /// <summary>
    /// Loads materials from file and returns categorized mappings
    /// </summary>
    public static MaterialsLoadResult LoadMaterials()
    {
        try
        {
            if (!File.Exists(SaveFilePath))
            {
                System.Diagnostics.Debug.WriteLine("[MaterialsSaveService] No save file found");
                return new MaterialsLoadResult();
            }

            var json = File.ReadAllText(SaveFilePath);
            var saveData = JsonSerializer.Deserialize<MaterialsSaveFile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (saveData == null)
            {
                System.Diagnostics.Debug.WriteLine("[MaterialsSaveService] Save file is empty or invalid");
                return new MaterialsLoadResult();
            }

            var result = new MaterialsLoadResult
            {
                Resources = saveData.Resources ?? new Dictionary<string, int>(),
                Liquids = saveData.Liquids ?? new Dictionary<string, int>(),
                Seeds = saveData.Seeds ?? new Dictionary<string, int>()
            };

            int total = result.Resources.Count + result.Liquids.Count + result.Seeds.Count;
            System.Diagnostics.Debug.WriteLine($"[MaterialsSaveService] Loaded {total} materials from {SaveFilePath} (resources={result.Resources.Count}, liquids={result.Liquids.Count}, seeds={result.Seeds.Count})");
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MaterialsSaveService] Error loading materials: {ex.Message}");
            return new MaterialsLoadResult();
        }
    }

    public static void SaveGameState()
    {
        int total = (_lastKnownResources?.Count ?? 0) + (_lastKnownLiquids?.Count ?? 0) + (_lastKnownSeeds?.Count ?? 0);
        if (total > 0)
        {
            System.Diagnostics.Debug.WriteLine($"[MaterialsSaveService] SaveGameState called with {total} materials (resources={_lastKnownResources?.Count ?? 0}, liquids={_lastKnownLiquids?.Count ?? 0}, seeds={_lastKnownSeeds?.Count ?? 0})");
            SaveMaterials(_lastKnownResources, _lastKnownLiquids, _lastKnownSeeds);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[MaterialsSaveService] SaveGameState called but no last known mappings exist - cannot save materials");
        }
    }

    public static bool SaveFileExists()
    {
        return File.Exists(SaveFilePath);
    }

    public static void DeleteSaveFile()
    {
        try
        {
            if (File.Exists(SaveFilePath))
            {
                File.Delete(SaveFilePath);
                System.Diagnostics.Debug.WriteLine("[MaterialsSaveService] Save file deleted");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MaterialsSaveService] Error deleting save file: {ex.Message}");
        }
    }
}
