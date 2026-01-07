namespace Outgrowth.Models;

/// <summary>
/// Station object for interactive elements (market, quest console, statistics, resource slots, etc.)
/// Implements IInteractable for tap/click interactions. Invisible object that only captures taps/clicks.
/// </summary>
public class StationObject : EnvObject, IInteractable
{
    /// <summary>
    /// Event fired when the station element is clicked
    /// </summary>
    public event EventHandler? Clicked;
    
    /// <summary>
    /// Action to execute when the station element is interacted with
    /// </summary>
    public Action? InteractAction { get; set; }
    
    /// <summary>
    /// Determines if the station element can be interacted with
    /// </summary>
    public bool CanInteract { get; set; } = true;
    
    /// <summary>
    /// Visual element for positioning and interaction
    /// </summary>
    public View? VisualElement { get; set; }
    
    public override int ZIndex => 200;
    
    public StationObject(string id, int x, int y, double width, double height) 
        : base(id, x, y, width, height)
    {
    }
    
    /// <summary>
    /// Handles interaction with the station element
    /// </summary>
    public void OnInteract()
    {
        if (!CanInteract)
            return;
            
        InteractAction?.Invoke();
    }
    
    /// <summary>
    /// Creates an invisible hit area for tap/click detection
    /// </summary>
    public override View CreateVisualElement()
    {
        // Create an AbsoluteLayout that contains the station image and a transparent hit area on top
        var container = new AbsoluteLayout
        {
            WidthRequest = Width,
            HeightRequest = Height
        };

        // Image for visual representation. Station Id may be an image filename.
        var img = new Image
        {
            Source = Id,
            Aspect = Aspect.AspectFit,
            WidthRequest = Width,
            HeightRequest = Height
        };

        AbsoluteLayout.SetLayoutBounds(img, new Rect(0, 0, Width, Height));
        AbsoluteLayout.SetLayoutFlags(img, 0);

        // Transparent border on top to capture taps
        var border = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Colors.Transparent,
            HeightRequest = Height,
            WidthRequest = Width
        };
        AbsoluteLayout.SetLayoutBounds(border, new Rect(0, 0, Width, Height));
        AbsoluteLayout.SetLayoutFlags(border, 0);

        // Add tap gesture for interactivity
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (sender, e) =>
        {
            Clicked?.Invoke(sender, e);
            OnInteract();
        };
        border.GestureRecognizers.Add(tapGesture);

        // Add image first, then border overlay
        container.Children.Add(img);
        container.Children.Add(border);

        VisualElement = container;
        return container;
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

