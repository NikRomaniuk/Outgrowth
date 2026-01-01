namespace Outgrowth.Models;

// Interactive pot for the greenhouse
public class PotObject : EnvObject, IInteractable
{
    public int PotNumber { get; set; }
    public string ImageSource { get; set; }
    public event EventHandler<TappedEventArgs>? Clicked;
    public Action? InteractAction { get; set; }
    public bool CanInteract { get; set; } = true;
    public override int ZIndex => 200;
    
    /// <summary>
    /// Visual element for positioning
    /// </summary>
    public View? VisualElement { get; set; }
    
    /// <summary>
    /// Plant object placed in this pot's slot. Null if slot is empty
    /// </summary>
    public PlantObject? PlantSlot { get; set; }
    
    public PotObject(int potNumber, int x, int y, string imageSource) 
        : base($"Pot{potNumber}", x, y, 320, 320)
    {
        PotNumber = potNumber;
        ImageSource = imageSource;
    }
    
    public void OnInteract()
    {
        if (!CanInteract) return;
        InteractAction?.Invoke();
    }
    
    public override View CreateVisualElement()
    {
        var mainGrid = new Grid
        {
            WidthRequest = Width,
            HeightRequest = Height
        };
        
        var potBorder = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Colors.Transparent,
            HeightRequest = Height,
            WidthRequest = Width
        };
        
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (sender, e) =>
        {
            Clicked?.Invoke(sender, e);
            OnInteract();
        };
        potBorder.GestureRecognizers.Add(tapGesture);
        
        var potImage = new Image
        {
            Source = ImageSource,
            Aspect = Aspect.Fill,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        
        potBorder.Content = potImage;
        mainGrid.Children.Add(potBorder);
        
        // Plant slot: only create and add to visual tree when plant exists
        if (PlantSlot != null)
        {
            // Plant slot: bottom edge touches pot center (Margin: -Height/2)
            var slotBorder = new Border
            {
                BackgroundColor = Colors.Transparent,
                StrokeThickness = 0,
                HeightRequest = Height,
                WidthRequest = Width,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, -Height / 2, 0, 0)
            };
            
            // Create visual element for the plant (coordinates are relative to slot, centered)
            PlantSlot.X = 0;
            PlantSlot.Y = 0;
            var plantView = PlantSlot.CreateVisualElement();
            slotBorder.Content = plantView;
            
            mainGrid.Children.Add(slotBorder);
        }
        
        VisualElement = mainGrid;
        return mainGrid;
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

