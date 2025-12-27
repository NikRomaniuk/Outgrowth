namespace Outgrowth.Models;

/// <summary>
/// Station object for interactive elements (market, quest console, statistics, resource slots, etc.)
/// Implements IInteractable for tap/click interactions
/// </summary>
public class StationObject : EnvObject, IInteractable
{
    /// <summary>
    /// Display name for the station element
    /// </summary>
    public string DisplayName { get; set; }
    
    /// <summary>
    /// Background color for the station element
    /// </summary>
    public Color BackgroundColor { get; set; }
    
    /// <summary>
    /// Separator color (line below icon)
    /// </summary>
    public Color SeparatorColor { get; set; }
    
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
    
    public StationObject(string id, string displayName, int x, int y, double width, double height, 
                        string sprite, Color? backgroundColor = null, Color? separatorColor = null) 
        : base(id, x, y, width, height, sprite)
    {
        DisplayName = displayName;
        BackgroundColor = backgroundColor ?? Color.FromArgb("#4A4A4A");
        SeparatorColor = separatorColor ?? Color.FromArgb("#4CAF50");
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
    /// Creates the station UI element (border with icon, separator line, and label)
    /// </summary>
    public override View CreateVisualElement()
    {
        var stackLayout = new VerticalStackLayout { Spacing = 10 };
        
        // Main border with icon
        var border = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Colors.Transparent,
            HeightRequest = Height,
            WidthRequest = Width
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
        
        // Background box
        grid.Children.Add(new BoxView 
        { 
            Color = BackgroundColor, 
            CornerRadius = 10, 
            Opacity = 0.5 
        });
        
        // Icon label
        var iconLabel = new Label
        {
            Text = BaseSprite,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        iconLabel.SetDynamicResource(Label.FontSizeProperty, "ButtonIconSize");
        grid.Children.Add(iconLabel);
        
        // Placeholder label (for development)
        var placeholderLabel = new Label
        {
            Text = $"[{DisplayName}]",
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
        
        // Separator line
        stackLayout.Children.Add(new BoxView
        {
            Color = SeparatorColor,
            HeightRequest = 3,
            WidthRequest = Width,
            HorizontalOptions = LayoutOptions.Center
        });
        
        // Display name label
        var nameLabel = new Label
        {
            Text = DisplayName,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            TextColor = Colors.White
        };
        nameLabel.SetDynamicResource(Label.FontSizeProperty, "ButtonLabelSize");
        stackLayout.Children.Add(nameLabel);
        
        VisualElement = stackLayout;
        return stackLayout;
    }
}

