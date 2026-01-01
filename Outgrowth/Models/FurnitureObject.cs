namespace Outgrowth.Models;

/// <summary>
/// Basic decorative furniture object (tables, shelves, lights, etc.)
/// </summary>
public class FurnitureObject : EnvObject
{
    public string ImageSource { get; set; }
    private int? _customZIndex;
    
    /// <summary>
    /// Visual element for positioning
    /// </summary>
    public View? VisualElement { get; set; }
    
    /// <summary>
    /// ZIndex for layering objects. Returns custom ZIndex if set, otherwise default (100)
    /// </summary>
    public override int ZIndex => _customZIndex ?? base.ZIndex;
    
    public FurnitureObject(string id, int x, int y, double width, double height, string imageSource) 
        : base(id, x, y, width, height)
    {
        ImageSource = imageSource;
    }
    
    /// <summary>
    /// Constructor with custom ZIndex
    /// </summary>
    public FurnitureObject(string id, int x, int y, double width, double height, string imageSource, int zIndex) 
        : base(id, x, y, width, height)
    {
        ImageSource = imageSource;
        _customZIndex = zIndex;
    }
    
    public override View CreateVisualElement()
    {
        var grid = new Grid
        {
            WidthRequest = Width,
            HeightRequest = Height
        };
        
        var image = new Image
        {
            Source = ImageSource,
            Aspect = Aspect.Fill,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        
        grid.Children.Add(image);
        
        VisualElement = grid;
        return grid;
    }
    
    /// <summary>
    /// Updates position of the visual element
    /// </summary>
    public override void UpdatePosition(double containerCenterX, double containerCenterY)
    {
        if (VisualElement == null) return;
        
        double centerPixelX = containerCenterX + X;
        double centerPixelY = containerCenterY - Y; // Negative Y = below center
        
        double leftEdgeX = centerPixelX - (Width / 2.0);
        double topEdgeY = centerPixelY - (Height / 2.0);
        
        AbsoluteLayout.SetLayoutBounds(VisualElement, new Rect(leftEdgeX, topEdgeY, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
        AbsoluteLayout.SetLayoutFlags(VisualElement, 0);
    }
}

