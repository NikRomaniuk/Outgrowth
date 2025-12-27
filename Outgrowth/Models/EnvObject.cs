namespace Outgrowth.Models;

/// <summary>
/// Base class for environment objects (pots, furniture, etc.)
/// Coordinates use 1:1 pixel ratio - X and Y represent the center of the object
/// </summary>
public abstract class EnvObject
{
    /// <summary>
    /// X coordinate (center of object)
    /// GreenhousePage: -9600 to 9600, HubPage/Lab: -960 to 960, 0 = center
    /// </summary>
    public int X { get; set; }
    
    /// <summary>
    /// Y coordinate (center of object)
    /// Range: -540 to 540, 0 = center, positive = above, negative = below
    /// </summary>
    public int Y { get; set; }
    
    public double Width { get; set; }
    public double Height { get; set; }
    
    /// <summary>
    /// The visual element for this object
    /// </summary>
    public View? VisualElement { get; set; }
    
    protected EnvObject(int x, int y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
    
    /// <summary>
    /// Updates visual element position - converts center coordinates to top-left corner for AbsoluteLayout
    /// </summary>
    public virtual void UpdatePosition(double containerCenterX, double containerCenterY)
    {
        if (VisualElement == null)
            return;
        
        // Convert center coordinates to pixel positions
        double centerPixelX = containerCenterX + X;
        double centerPixelY = containerCenterY - Y; // Negative Y = below center
        
        // Calculate top-left corner (AbsoluteLayout uses top-left)
        double leftEdgeX = centerPixelX - (Width / 2.0);
        double topEdgeY = centerPixelY - (Height / 2.0);
        
        AbsoluteLayout.SetLayoutBounds(VisualElement, new Rect(leftEdgeX, topEdgeY, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
        AbsoluteLayout.SetLayoutFlags(VisualElement, 0);
    }
    
    /// <summary>
    /// Creates the visual representation - must be implemented by derived classes
    /// </summary>
    public abstract View CreateVisualElement();
}

