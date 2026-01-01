using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Outgrowth.Services;

namespace Outgrowth.Models;

/// <summary>
/// Represents a plant type with growth stages. Each stage has its own sprite and growth time
/// </summary>
public class PlantData : IGameData
{
    /// <summary>
    /// Unique identifier for the plant
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name of the plant
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Detailed description of the plant
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Size category of the plant (e.g., "Small", "Medium", "Large")
    /// </summary>
    [JsonPropertyName("plantSize")]
    public string PlantSize { get; set; } = string.Empty;
    
    /// <summary>
    /// Array of growth stage sprites (emojis or image paths). Index 0 = seed, last index = fully grown
    /// </summary>
    [JsonPropertyName("growthStageSprites")]
    public string[] GrowthStageSprites { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Array of growth cycles required for each stage. Length should match GrowthStageSprites
    /// Index 0 = cycles from seed to stage 1, index 1 = cycles from stage 1 to stage 2, etc
    /// Each cycle = 5 seconds. Must be whole numbers (integers).
    /// </summary>
    [JsonPropertyName("growthStageCycles")]
    public int[] GrowthStageCycles { get; set; } = Array.Empty<int>();
    
    public PlantData() { }
    
    public PlantData(string id, string name, string description, string plantSize,
                 string[] growthStageSprites, int[] growthStageCycles)
    {
        Id = id;
        Name = name;
        Description = description;
        PlantSize = plantSize;
        GrowthStageSprites = growthStageSprites ?? Array.Empty<string>();
        GrowthStageCycles = growthStageCycles ?? Array.Empty<int>();
        
        // Validate arrays have matching lengths
        if (GrowthStageSprites.Length != GrowthStageCycles.Length)
        {
            throw new ArgumentException("GrowthStageSprites and GrowthStageCycles arrays must have the same length.");
        }
    }
    
    /// <summary>
    /// Gets the total number of growth stages
    /// </summary>
    public int TotalStages => GrowthStageSprites.Length;
    
    /// <summary>
    /// Gets the sprite for a specific growth stage
    /// </summary>
    public string GetSpriteForStage(int stage)
    {
        if (stage < 0 || stage >= GrowthStageSprites.Length)
        {
            return GrowthStageSprites.Length > 0 ? GrowthStageSprites[0] : "";
        }
        return GrowthStageSprites[stage];
    }
    
    /// <summary>
    /// Gets the growth cycles required for a specific stage transition
    /// </summary>
    public int GetGrowthCyclesForStage(int stage)
    {
        if (stage < 0 || stage >= GrowthStageCycles.Length)
        {
            return 0;
        }
        return GrowthStageCycles[stage];
    }

    /// <summary>
    /// Defines a material drop for a plant stage: material id and min/max quantity
    /// </summary>
    public class MaterialDrop
    {
        [JsonPropertyName("materialId")]
        public string MaterialId { get; set; } = string.Empty;

        [JsonPropertyName("min")]
        public int Min { get; set; }

        [JsonPropertyName("max")]
        public int Max { get; set; }
    }

    /// <summary>
    /// StageDrops is a list where each index corresponds to a growth stage (0-based)
    /// Each entry is a list of MaterialDrop describing what can drop when harvesting that stage
    /// If a stage has no entry or an empty list, nothing drops for that stage
    /// </summary>
    [JsonPropertyName("stageDrops")]
    public List<List<MaterialDrop>> StageDrops { get; set; } = new();

    /// <summary>
    /// Returns the configured drops for the given stage index (empty list if none)
    /// </summary>
    public IReadOnlyList<MaterialDrop> GetDropsForStage(int stage)
    {
        if (stage < 0) return Array.Empty<MaterialDrop>();
        if (stage < StageDrops.Count && StageDrops[stage] != null)
            return StageDrops[stage];
        return Array.Empty<MaterialDrop>();
    }

    /// <summary>
    /// Helper that generates actual drop quantities for a stage using provided RNG
    /// Returns dictionary materialId -> quantity (only entries with quantity>0)
    /// </summary>
    public Dictionary<string, int> GenerateDropsForStage(int stage, Random? rng = null)
    {
        var result = new Dictionary<string, int>();
        var drops = GetDropsForStage(stage);
        if (drops.Count == 0) return result;
        rng ??= new Random();
        foreach (var d in drops)
        {
            if (string.IsNullOrEmpty(d.MaterialId)) continue;
            var min = Math.Max(0, d.Min);
            var max = Math.Max(min, d.Max);
            int qty = 0;
            if (max > min)
            {
                qty = rng.Next(min, max + 1);
            }
            else
            {
                qty = min; // min==max or max<min handled as min
            }
            if (qty > 0)
                result[d.MaterialId] = qty;
        }
        return result;
    }
}

