using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using Outgrowth.Services;

namespace Outgrowth.Models;

/// <summary>
/// Represents a game resource with ID, description, and properties
/// </summary>
public class ResourceData : IGameData, INotifyPropertyChanged
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
    
    /// <summary>
    /// Liquids produced by extracting this resource: map of liquid ID -> quantity
    /// </summary>
    [JsonPropertyName("extractionResult")]
    public Dictionary<string, int>? ExtractionResult { get; set; }

    /// <summary>
    /// Amount of this resource required to perform an extraction
    /// </summary>
    [JsonPropertyName("amountForExtraction")]
    public int? AmountForExtraction { get; set; }
    
    public ResourceData() { }
    
    public ResourceData(string id, string name, string description, string sprite)
    {
        Id = id;
        Name = name;
        Description = description;
        Sprite = sprite;
    }

    /// <summary>
    /// Quantity owned by the player (for saves/inventory)
    /// </summary>
    [JsonPropertyName("quantity")]
    private int _quantity;
    public int Quantity
    {
        get => _quantity;
        set
        {
            if (_quantity == value) return;
            _quantity = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

