namespace Outgrowth.Models;

/// <summary>
/// Basic decorative furniture object (tables, shelves, lights, etc.)
/// </summary>
public class FurnitureObject : EnvObject
{
    public string FurnitureType { get; set; }
    public string DisplayName { get; set; }
    public Color BackgroundColor { get; set; }
    
    public FurnitureObject(string id, string displayName, string furnitureType, int x, int y, 
                          double width, double height, string sprite, Color? backgroundColor = null) 
        : base(id, x, y, width, height, sprite)
    {
        DisplayName = displayName;
        FurnitureType = furnitureType;
        BackgroundColor = backgroundColor ?? Color.FromArgb("#3E2723");
    }
    
    public override View CreateVisualElement()
    {
        var stackLayout = new VerticalStackLayout 
        { 
            Spacing = 5,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        
        // Main container
        var border = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Colors.Transparent,
            HeightRequest = Height,
            WidthRequest = Width
        };
        
        var grid = new Grid();
        
        // Background
        grid.Children.Add(new BoxView 
        { 
            Color = BackgroundColor, 
            CornerRadius = 10, 
            Opacity = 0.6 
        });
        
        // Icon/Sprite
        var iconLabel = new Label
        {
            Text = BaseSprite,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            FontSize = Width * 0.4 // Icon size proportional to width
        };
        grid.Children.Add(iconLabel);
        
        border.Content = grid;
        stackLayout.Children.Add(border);
        
        // Display name label (optional, only if display name is set)
        if (!string.IsNullOrEmpty(DisplayName))
        {
            var nameLabel = new Label
            {
                Text = DisplayName,
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                TextColor = Colors.White,
                Opacity = 0.8
            };
            stackLayout.Children.Add(nameLabel);
        }
        
        VisualElement = stackLayout;
        return stackLayout;
    }
}

