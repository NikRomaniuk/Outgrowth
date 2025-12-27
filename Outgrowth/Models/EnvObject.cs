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
/// Base class for environment objects. Coordinates use 1:1 pixel ratio.
/// X and Y represent the center of the object.
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
    public View? VisualElement { get; set; }
    public string BaseSprite { get; set; }
    
    protected EnvObject(string id, int x, int y, double width, double height, string baseSprite = "")
    {
        Id = id;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        BaseSprite = baseSprite;
    }
    
    /// <summary>
    /// Converts center coordinates to top-left corner for AbsoluteLayout.
    /// Formula: leftEdgeX = centerPixelX - (Width / 2), topEdgeY = centerPixelY - (Height / 2)
    /// </summary>
    public virtual void UpdatePosition(double containerCenterX, double containerCenterY)
    {
        if (VisualElement == null) return;
        
        double centerPixelX = containerCenterX + X;
        double centerPixelY = containerCenterY - Y; // Negative Y = below center
        
        double leftEdgeX = centerPixelX - (Width / 2.0);
        double topEdgeY = centerPixelY - (Height / 2.0);
        
        AbsoluteLayout.SetLayoutBounds(VisualElement, new Rect(leftEdgeX, topEdgeY, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
        AbsoluteLayout.SetLayoutFlags(VisualElement, 0);
    }
    
    public abstract View CreateVisualElement();
}

