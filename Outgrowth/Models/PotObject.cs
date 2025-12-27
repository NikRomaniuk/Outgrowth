namespace Outgrowth.Models;

/// <summary>
/// Pot object for the greenhouse - creates pot UI with icon, label, and separator
/// </summary>
public class PotObject : EnvObject
{
    public int PotNumber { get; set; }
    public EventHandler<TappedEventArgs>? OnClick { get; set; }
    
    public PotObject(int potNumber, int x, int y) 
        : base(x, y, 300, 300) // Pots are 300x300 pixels
    {
        PotNumber = potNumber;
    }
    
    /// <summary>
    /// Creates the pot UI element (border with icon, separator line, and label)
    /// </summary>
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
        
        if (OnClick != null)
        {
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += OnClick;
            border.GestureRecognizers.Add(tapGesture);
        }
        
        var grid = new Grid();
        grid.Children.Add(new BoxView { Color = Color.FromArgb("#5D4037"), CornerRadius = 10, Opacity = 0.5 });
        
        var iconLabel = new Label
        {
            Text = "🪴",
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

