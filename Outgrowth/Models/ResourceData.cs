using System.Text.Json.Serialization;

namespace Outgrowth.Models;

/// <summary>
/// Represents a game resource with ID, description, and properties
/// </summary>
public class ResourceData
{
    /// <summary>
    /// Unique identifier for the resource
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name of the resource
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Detailed description of the resource
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Sprite/emoji or image path for the resource icon
    /// </summary>
    [JsonPropertyName("sprite")]
    public string Sprite { get; set; } = string.Empty;
    
    /// <summary>
    /// Base value for trading (optional)
    /// </summary>
    [JsonPropertyName("baseValue")]
    public double? BaseValue { get; set; }
    
    public ResourceData() { }
    
    public ResourceData(string id, string name, string description, string sprite)
    {
        Id = id;
        Name = name;
        Description = description;
        Sprite = sprite;
    }
}

