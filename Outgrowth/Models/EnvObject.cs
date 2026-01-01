namespace Outgrowth.Models;

public interface IInteractable
{
    void OnInteract();
    bool CanInteract { get; }
}

public interface IAnimated
{
    Task StartAnimation();
    void StopAnimation();
    bool IsAnimating { get; }
}

/// <summary>
/// Base class for environment objects. Coordinates use 1:1 pixel ratio
/// X and Y represent the center of the object
/// </summary>
public abstract class EnvObject
{
    public string Id { get; set; }
    
    /// <summary>
    /// X coordinate (center). GreenhousePage: -9600 to 9600, HubPage/Lab: -960 to 960, 0 = center
    /// </summary>
    public int X { get; set; }
    
    /// <summary>
    /// Y coordinate (center). Range: -540 to 540, 0 = center, positive = above, negative = below
    /// </summary>
    public int Y { get; set; }
    
    public double Width { get; set; }
    public double Height { get; set; }
    
    /// <summary>
    /// ZIndex for layering objects (default: 100 for furniture, 200 for interactive objects)
    /// </summary>
    public virtual int ZIndex { get; } = 100;
    
    protected EnvObject(string id, int x, int y, double width, double height)
    {
        Id = id;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
    
    /// <summary>
    /// Converts center coordinates to top-left corner for AbsoluteLayout
    /// Formula: leftEdgeX = centerPixelX - (Width / 2), topEdgeY = centerPixelY - (Height / 2)
    /// </summary>
    public virtual void UpdatePosition(double containerCenterX, double containerCenterY)
    {
        // Override in derived classes that have visual elements
    }
    
    /// <summary>
    /// Creates the visual element for this object. Must be implemented by derived classes
    /// </summary>
    public abstract View CreateVisualElement();
}

