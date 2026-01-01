using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Outgrowth.Services;

namespace Outgrowth.Models;

/// <summary>
/// Represents a liquid resource with ID, description, and properties
/// </summary>
public class LiquidData : IGameData, INotifyPropertyChanged
{
    /// <summary>
    /// Unique identifier for the liquid
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name of the liquid
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Detailed description of the liquid
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Sprite/emoji or image path for the liquid icon
    /// </summary>
    [JsonPropertyName("sprite")]
    public string Sprite { get; set; } = string.Empty;
    
    public LiquidData() { }
    
    public LiquidData(string id, string name, string description, string sprite)
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

