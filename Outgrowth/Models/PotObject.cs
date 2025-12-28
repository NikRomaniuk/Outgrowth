namespace Outgrowth.Models;

// Interactive pot for the greenhouse
public class PotObject : EnvObject, IInteractable
{
    public int PotNumber { get; set; }
    public string ImageSource { get; set; }
    public event EventHandler<TappedEventArgs>? Clicked;
    public Action? InteractAction { get; set; }
    public bool CanInteract { get; set; } = true;
    
    public object? PlantSlot { get; set; } // Will be PlantObject when implemented
    
    public PotObject(int potNumber, int x, int y, string imageSource) 
        : base($"Pot{potNumber}", x, y, 320, 320, "")
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
        
        // Plant slot: bottom edge touches pot center (Margin: -Height/2)
        var slotBorder = new Border
        {
            Stroke = Color.FromArgb("#4CAF50"),
            StrokeThickness = 2,
            BackgroundColor = Colors.Transparent,
            HeightRequest = Height,
            WidthRequest = Width,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, -Height / 2, 0, 0)
        };
        
        var slotLabel = new Label
        {
            Text = "[Small Plant Slot]",
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb("#4CAF50"),
            Opacity = 0.5
        };
        slotLabel.SetDynamicResource(Label.FontSizeProperty, "ButtonPlaceholderSize");
        
        slotBorder.Content = slotLabel;
        mainGrid.Children.Add(slotBorder);
        
        VisualElement = mainGrid;
        return mainGrid;
    }
}

