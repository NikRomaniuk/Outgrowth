namespace Outgrowth.Models;

/// <summary>
/// Interactive pot for the greenhouse. Clickable with icon, label, and separator.
/// </summary>
public class PotObject : EnvObject, IInteractable
{
    public int PotNumber { get; set; }
    public event EventHandler<TappedEventArgs>? Clicked;
    public Action? InteractAction { get; set; }
    public bool CanInteract { get; set; } = true;
    
    public PotObject(int potNumber, int x, int y) 
        : base($"Pot{potNumber}", x, y, 300, 300, "🪴")
    {
        PotNumber = potNumber;
    }
    
    public void OnInteract()
    {
        if (!CanInteract) return;
        InteractAction?.Invoke();
    }
    
    public override View CreateVisualElement()
    {
        var stackLayout = new VerticalStackLayout { Spacing = 10 };
        
        // Main border with pot icon
        var border = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Colors.Transparent,
            HeightRequest = 300,
            WidthRequest = 300
        };
        
        // Add tap gesture for interactivity
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (sender, e) =>
        {
            Clicked?.Invoke(sender, e);
            OnInteract();
        };
        border.GestureRecognizers.Add(tapGesture);
        
        var grid = new Grid();
        grid.Children.Add(new BoxView { Color = Color.FromArgb("#5D4037"), CornerRadius = 10, Opacity = 0.5 });
        
        var iconLabel = new Label
        {
            Text = BaseSprite,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        iconLabel.SetDynamicResource(Label.FontSizeProperty, "ButtonIconSize");
        grid.Children.Add(iconLabel);
        
        var placeholderLabel = new Label
        {
            Text = $"[Pot {PotNumber}]",
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 0, 10),
            TextColor = Colors.White,
            Opacity = 0.7
        };
        placeholderLabel.SetDynamicResource(Label.FontSizeProperty, "ButtonPlaceholderSize");
        grid.Children.Add(placeholderLabel);
        
        border.Content = grid;
        stackLayout.Children.Add(border);
        
        // Separator line and label
        stackLayout.Children.Add(new BoxView
        {
            Color = Color.FromArgb("#4CAF50"),
            HeightRequest = 3,
            WidthRequest = 300,
            HorizontalOptions = LayoutOptions.Center
        });
        
        var potLabel = new Label
        {
            Text = $"Pot {PotNumber}",
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            TextColor = Colors.White
        };
        potLabel.SetDynamicResource(Label.FontSizeProperty, "ButtonLabelSize");
        stackLayout.Children.Add(potLabel);
        
        VisualElement = stackLayout;
        return stackLayout;
    }
}

