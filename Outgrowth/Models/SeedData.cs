using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Outgrowth.Services;

namespace Outgrowth.Models;

/// <summary>
/// Represents a seed resource with ID, description, and properties
/// </summary>
public class SeedData : IGameData, INotifyPropertyChanged
{
    /// <summary>
    /// Unique identifier for the seed
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name of the seed
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Detailed description of the seed
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Sprite/emoji or image path for the seed icon
    /// </summary>
    [JsonPropertyName("sprite")]
    public string Sprite { get; set; } = string.Empty;
    
    /// <summary>
    /// ID of the plant that will spawn when this seed is planted
    /// </summary>
    [JsonPropertyName("plantId")]
    public string PlantId { get; set; } = string.Empty;
    
    public SeedData() { }
    
    public SeedData(string id, string name, string description, string sprite, string plantId)
    {
        Id = id;
        Name = name;
        Description = description;
        Sprite = sprite;
        PlantId = plantId;
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

