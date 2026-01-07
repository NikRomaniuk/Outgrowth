using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;

namespace Outgrowth.Services;

public class StyledPanel : Border
{
    public Grid Panel { get; private set; }
    public string Type { get; private set; }
    public ScrollView? ScrollContainer { get; private set; }
    public Grid? ContentGrid { get; private set; }

    // For selection and panelItem types
    public bool? IsSelected { get; private set; }
    public bool ItemIsEnabled { get; private set; }
    // Stored background panels (used for selection/panelItem)
    private Grid? NormalPanel { get; set; }
    private Grid? HighlightedPanel { get; set; }

    // Default constructor - basic 9-slice panel
    public StyledPanel(
        double width,
        double height,
        double cornerSize = 8,
        Color? backgroundColor = null,
        Color? borderColor = null,
        View? content = null,
        string? cornerImage = null,
        string? horizontalEdgeImage = null,
        string? verticalEdgeImage = null,
        string? centerImage = null)
    {
        Type = "default";
        backgroundColor ??= Color.FromArgb("#0f0c29");
        borderColor ??= Color.FromArgb("#302b63");

        this.Padding = new Thickness(0);

        Panel = Build9SliceGrid(width, height, cornerSize, backgroundColor, borderColor, content, cornerImage, horizontalEdgeImage, verticalEdgeImage, centerImage);
        this.Content = Panel;
    }

    // Scroll constructor - panel with ScrollView and VerticalStackLayout
    public StyledPanel(
        string type,
        double width,
        double height,
        double cornerSize = 8,
        Color? backgroundColor = null,
        Color? borderColor = null,
        string? cornerImage = null,
        string? horizontalEdgeImage = null,
        string? verticalEdgeImage = null,
        string? centerImage = null)
    {
        if (type != "scroll")
            throw new ArgumentException("This constructor is only for 'scroll' type", nameof(type));

        Type = type;
        backgroundColor ??= Color.FromArgb("#0f0c29");
        borderColor ??= Color.FromArgb("#302b63");

        this.Padding = new Thickness(0);

        // Create scroll view with vertical stack layout
        var stackLayout = new VerticalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(5)
        };

        ScrollContainer = new ScrollView
        {
            Content = stackLayout,
            Orientation = ScrollOrientation.Vertical,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        Panel = Build9SliceGrid(width, height, cornerSize, backgroundColor, borderColor, ScrollContainer, cornerImage, horizontalEdgeImage, verticalEdgeImage, centerImage);
        this.Content = Panel;
    }

    // Selection constructor - panel with normal/highlighted backgrounds and empty ContentGrid
    public StyledPanel(
        string type,
        double width,
        double height,
        bool isSelected,
        double cornerSize = 40,
        string? cornerImage = "ui__panel_item_corner.png",
        string? horizontalEdgeImage = "ui__panel_item_edge_horizontal.png",
        string? verticalEdgeImage = "ui__panel_item_edge_vertical.png",
        string? centerImage = "ui__panel_item_center.png")
    {
        if (type != "selection")
            throw new ArgumentException("This constructor is only for 'selection' type", nameof(type));

        Type = type;
        IsSelected = isSelected;

        this.StrokeThickness = 0;
        this.BackgroundColor = Colors.Transparent;
        this.Padding = new Thickness(0);

        var container = new Grid();

        // Create normal background panel
        var normalPanel = Build9SliceGrid(
            width,
            height,
            cornerSize,
            Color.FromArgb("#1b1b35"),
            Color.FromArgb("#40406e"),
            null,
            cornerImage,
            horizontalEdgeImage,
            verticalEdgeImage,
            centerImage
        );
        this.NormalPanel = normalPanel;
        normalPanel.Opacity = isSelected ? 0 : 1;
        normalPanel.HorizontalOptions = LayoutOptions.Fill;
        normalPanel.VerticalOptions = LayoutOptions.Fill;

        // Create highlighted background panel
        var highlightedPanel = Build9SliceGrid(
            width,
            height,
            cornerSize,
            Color.FromArgb("#2874a7"),
            Color.FromArgb("#00d2ff"),
            null,
            "ui__panel_highlighted_corner.png",
            "ui__panel_highlighted_edge_horizontal.png",
            "ui__panel_highlighted_edge_vertical.png",
            "ui__panel_highlighted_center.png"
        );
        this.HighlightedPanel = highlightedPanel;
        highlightedPanel.Opacity = isSelected ? 1 : 0;
        highlightedPanel.HorizontalOptions = LayoutOptions.Fill;
        highlightedPanel.VerticalOptions = LayoutOptions.Fill;

        container.Children.Add(normalPanel);
        container.Children.Add(highlightedPanel);

        // Create content grid on top
        double contentPadding = cornerSize * 0.7;
        ContentGrid = new Grid
        {
            Padding = new Thickness(10),
            Margin = new Thickness(0, contentPadding, 0, contentPadding),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center
        };

        container.Children.Add(ContentGrid);

        container.HeightRequest = height;
        container.WidthRequest = width;
        this.HeightRequest = height;
        this.WidthRequest = width;

        this.Content = container;
        Panel = container;
    }

    // PanelItem constructor - full item with icon, name, quantity and platform-specific layout
    public StyledPanel(
        string type,
        string id,
        string name,
        string sprite,
        bool isSelected,
        double panelItemHeight,
        double qtyFontSize,
        double bodyFontSize,
        bool isAndroid,
        Action? onTapped = null,
        bool isEnabled = true,
        object? bindingContext = null)
    {
        if (type != "panelItem")
            throw new ArgumentException("This constructor is only for 'panelItem' type", nameof(type));

        Type = type;
        IsSelected = isSelected;
        ItemIsEnabled = isEnabled;
        this.StrokeThickness = 0;
        this.BackgroundColor = Colors.Transparent;
        this.Padding = new Thickness(0);

        // Save bindingContext to this StyledPanel's BindingContext
        if (bindingContext != null)
        {
            this.BindingContext = bindingContext;
        }

        var container = new Grid();

        // Corner size base (40px) scaled by screen adaptive scale
        double cornerSize = 40 * (ScreenProperties.Instance?.AdaptiveScale ?? 1.0);
        double panelHeight = panelItemHeight + (cornerSize * 2);

        // Create normal background
        var normalPanel = Build9SliceGrid(
            double.NaN,
            panelHeight,
            cornerSize,
            Color.FromArgb("#1b1b35"),
            Color.FromArgb("#40406e"),
            null,
            "ui__panel_item_corner.png",
            "ui__panel_item_edge_horizontal.png",
            "ui__panel_item_edge_vertical.png",
            "ui__panel_item_center.png"
        );
        this.NormalPanel = normalPanel;
        normalPanel.Opacity = isSelected ? 0 : 1;
        normalPanel.HorizontalOptions = LayoutOptions.Fill;
        normalPanel.VerticalOptions = LayoutOptions.Fill;

        // Create highlighted background
        var highlightedPanel = Build9SliceGrid(
            double.NaN,
            panelHeight,
            cornerSize,
            Color.FromArgb("#2874a7"),
            Color.FromArgb("#00d2ff"),
            null,
            "ui__panel_highlighted_corner.png",
            "ui__panel_highlighted_edge_horizontal.png",
            "ui__panel_highlighted_edge_vertical.png",
            "ui__panel_highlighted_center.png"
        );
        this.HighlightedPanel = highlightedPanel;
        highlightedPanel.Opacity = isSelected ? 1 : 0;
        highlightedPanel.HorizontalOptions = LayoutOptions.Fill;
        highlightedPanel.VerticalOptions = LayoutOptions.Fill;

        container.Children.Add(normalPanel);
        container.Children.Add(highlightedPanel);

        // Create content grid with icon and labels
        double contentPadding = cornerSize * 0.7;
        ContentGrid = new Grid
        {
            Padding = new Thickness(10),
            Margin = new Thickness(0, contentPadding, 0, contentPadding),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center
        };

        // Platform-specific layout
        if (isAndroid)
        {
            // Android: single-row grid (icon | qty)
            ContentGrid.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = new GridLength(panelItemHeight) },
                new ColumnDefinition { Width = GridLength.Star }
            };

            // Icon view
            View iconView;
            if (!string.IsNullOrEmpty(sprite) && sprite.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                iconView = new Image
                {
                    Source = sprite,
                    Aspect = Aspect.AspectFit,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Center
                };
            }
            else
            {
                iconView = new Label
                {
                    Text = sprite,
                    FontSize = panelItemHeight,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                };
            }
            Grid.SetColumn(iconView, 0);
            ContentGrid.Children.Add(iconView);

            // Quantity label (right-aligned)
            var qtyLabel = new Label
            {
                FontSize = qtyFontSize * 1.5,
                TextColor = Colors.White,
                Opacity = 0.7,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.End
            };
            try
            {
                var appRes = Application.Current?.Resources;
                if (appRes != null && appRes.ContainsKey("ResourcePanelQtyFont"))
                    qtyLabel.FontFamily = (string)appRes["ResourcePanelQtyFont"];
            }
            catch { }
            if (bindingContext != null)
            {
                qtyLabel.BindingContext = bindingContext;
                qtyLabel.SetBinding(Label.TextProperty, new Binding("Quantity"));
            }
            else
            {
                qtyLabel.Text = "0";
            }
            Grid.SetColumn(qtyLabel, 1);
            ContentGrid.Children.Add(qtyLabel);
        }
        else
        {
            // Windows: icon on left, name and qty stacked
            ContentGrid.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = new GridLength(panelItemHeight) },
                new ColumnDefinition { Width = GridLength.Auto }
            };
            ContentGrid.RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = new GridLength(panelItemHeight / 2) },
                new RowDefinition { Height = new GridLength(panelItemHeight / 2) }
            };

            // Icon view
            View iconView;
            if (!string.IsNullOrEmpty(sprite) && sprite.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                iconView = new Image
                {
                    Source = sprite,
                    Aspect = Aspect.AspectFit,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Center
                };
            }
            else
            {
                iconView = new Label
                {
                    Text = sprite,
                    FontSize = panelItemHeight * 0.7,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                };
            }
            Grid.SetColumn(iconView, 0);
            Grid.SetRow(iconView, 0);
            Grid.SetRowSpan(iconView, 2);
            ContentGrid.Children.Add(iconView);

            // Name label
            var nameLabel = new Label
            {
                Text = name,
                FontSize = bodyFontSize,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Start
            };
            try
            {
                var appRes = Application.Current?.Resources;
                if (appRes != null && appRes.ContainsKey("ResourcePanelBodyFont"))
                    nameLabel.FontFamily = (string)appRes["ResourcePanelBodyFont"];
            }
            catch { }
            Grid.SetColumn(nameLabel, 1);
            Grid.SetRow(nameLabel, 0);
            ContentGrid.Children.Add(nameLabel);

            // Quantity label
            var qtyLabel = new Label
            {
                FontSize = qtyFontSize,
                TextColor = Colors.White,
                Opacity = 0.7,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Start
            };
            try
            {
                var appRes = Application.Current?.Resources;
                if (appRes != null && appRes.ContainsKey("ResourcePanelQtyFont"))
                    qtyLabel.FontFamily = (string)appRes["ResourcePanelQtyFont"];
            }
            catch { }
            if (bindingContext != null)
            {
                qtyLabel.BindingContext = bindingContext;
                qtyLabel.SetBinding(Label.TextProperty, new Binding("Quantity") { StringFormat = "Qty: {0}" });
            }
            else
            {
                qtyLabel.Text = "Qty: 0";
            }
            Grid.SetColumn(qtyLabel, 1);
            Grid.SetRow(qtyLabel, 1);
            ContentGrid.Children.Add(qtyLabel);
        }

        container.Children.Add(ContentGrid);

        container.HeightRequest = panelHeight;
        this.HeightRequest = panelHeight;

        this.Content = container;
        Panel = container;

        // Handle enabled/disabled state
        this.IsEnabled = isEnabled;
        if (!isEnabled)
        {
            this.Opacity = 0.5;
            this.InputTransparent = true;
        }
        else
        {
            // Attach tap gesture
            if (onTapped != null)
            {
                var tap = new TapGestureRecognizer();
                tap.Tapped += (s, e) => onTapped();
                this.GestureRecognizers.Add(tap);
            }
        }
    }

    private Grid Build9SliceGrid(
        double width,
        double height,
        double cornerSize,
        Color backgroundColor,
        Color borderColor,
        View? content,
        string? cornerImage,
        string? horizontalEdgeImage,
        string? verticalEdgeImage,
        string? centerImage)
    {
        var panel = new Grid
        {
            WidthRequest = width,
            HeightRequest = height,
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = new GridLength(cornerSize, GridUnitType.Absolute) },
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = new GridLength(cornerSize, GridUnitType.Absolute) }
            },
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = new GridLength(cornerSize, GridUnitType.Absolute) },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = new GridLength(cornerSize, GridUnitType.Absolute) }
            }
        };

        View cornerTL = CreateSliceView(borderColor, cornerImage, cornerSize, cornerSize, rotationDeg: 0);
        View cornerTR = CreateSliceView(borderColor, cornerImage, cornerSize, cornerSize, rotationDeg: 90);
        View cornerBR = CreateSliceView(borderColor, cornerImage, cornerSize, cornerSize, rotationDeg: 180);
        View cornerBL = CreateSliceView(borderColor, cornerImage, cornerSize, cornerSize, rotationDeg: 270);

        View edgeTop = CreateSliceView(borderColor, horizontalEdgeImage, null, null, rotationDeg: 0);
        View edgeBottom = CreateSliceView(borderColor, horizontalEdgeImage, null, null, rotationDeg: 180);

        View edgeLeft = CreateSliceView(borderColor, verticalEdgeImage, null, null, rotationDeg: 0);
        View edgeRight = CreateSliceView(borderColor, verticalEdgeImage, null, null, rotationDeg: 180);

        double edgeOverlap = cornerSize * 0.001;
        bool isAndroid = DeviceInfo.Platform == DevicePlatform.Android;
        double rotatedEdgeAdjustment = isAndroid ? -0.5 : 0.5;

        edgeTop.Margin = new Thickness(-edgeOverlap, 0, rotatedEdgeAdjustment, 0);
        edgeBottom.Margin = new Thickness(-edgeOverlap, rotatedEdgeAdjustment, rotatedEdgeAdjustment, rotatedEdgeAdjustment);
        edgeLeft.Margin = new Thickness(0, -edgeOverlap, 0, rotatedEdgeAdjustment);
        edgeRight.Margin = new Thickness(rotatedEdgeAdjustment, -edgeOverlap, 0, rotatedEdgeAdjustment);

        View center = CreateSliceView(backgroundColor, centerImage, null, null, rotationDeg: 0);

        Grid.SetRow(cornerTL, 0); Grid.SetColumn(cornerTL, 0);
        Grid.SetRow(cornerTR, 0); Grid.SetColumn(cornerTR, 2);
        Grid.SetRow(cornerBL, 2); Grid.SetColumn(cornerBL, 0);
        Grid.SetRow(cornerBR, 2); Grid.SetColumn(cornerBR, 2);

        Grid.SetRow(edgeTop, 0); Grid.SetColumn(edgeTop, 1);
        Grid.SetRow(edgeBottom, 2); Grid.SetColumn(edgeBottom, 1);
        Grid.SetRow(edgeLeft, 1); Grid.SetColumn(edgeLeft, 0);
        Grid.SetRow(edgeRight, 1); Grid.SetColumn(edgeRight, 2);

        Grid.SetRow(center, 1); Grid.SetColumn(center, 1);

        panel.Children.Add(cornerTL);
        panel.Children.Add(cornerTR);
        panel.Children.Add(cornerBL);
        panel.Children.Add(cornerBR);
        panel.Children.Add(edgeTop);
        panel.Children.Add(edgeBottom);
        panel.Children.Add(edgeLeft);
        panel.Children.Add(edgeRight);
        panel.Children.Add(center);

        if (content != null)
        {
            Grid.SetRow(content, 0);
            Grid.SetColumn(content, 0);
            Grid.SetRowSpan(content, 3);
            Grid.SetColumnSpan(content, 3);
            double contentPadding = cornerSize * 0.7;
            content.Margin = new Thickness(contentPadding);
            panel.Children.Add(content);
        }

        return panel;
    }

    private static View CreateSliceView(Color color, string? imageSource = null, double? width = null, double? height = null, double rotationDeg = 0)
    {
        if (!string.IsNullOrEmpty(imageSource))
        {
            var img = new Image
            {
                Source = imageSource,
                Aspect = Aspect.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                AnchorX = 0.5,
                AnchorY = 0.5
            };

            if (rotationDeg != 0)
                img.Rotation = rotationDeg;

            return img;
        }

        var slice = new BoxView
        {
            Color = color,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        if (width.HasValue)
            slice.WidthRequest = width.Value;
        if (height.HasValue)
            slice.HeightRequest = height.Value;

        return slice;
    }

    // Enables or disables this panel when used as a panelItem
    public void SetPanelItemEnabled(bool isEnabled)
    {
        try
        {
            this.ItemIsEnabled = isEnabled;
            Panel.Opacity = isEnabled ? 1.0 : 0.5;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StyledPanel] SetPanelItemEnabled failed for ClassId='{this.ClassId}': {ex.Message}");
        }
    }

    // Updates the selection state by fading between normal and highlighted backgrounds
    public void SetPanelSelected(bool isSelected, bool animate = true)
    {
        if (Panel == null)
            return;
            
        var normalPanel = this.NormalPanel;
        var highlightedPanel = this.HighlightedPanel;

        if (normalPanel == null || highlightedPanel == null)
            return;

        if (animate)
        {
            if (isSelected)
            {
                _ = highlightedPanel.FadeTo(1, 120, Easing.Linear);
                _ = normalPanel.FadeTo(0, 120, Easing.Linear);
            }
            else
            {
                _ = highlightedPanel.FadeTo(0, 120, Easing.Linear);
                _ = normalPanel.FadeTo(1, 120, Easing.Linear);
            }
        }
        else
        {
            highlightedPanel.Opacity = isSelected ? 1 : 0;
            normalPanel.Opacity = isSelected ? 0 : 1;
        }

        IsSelected = isSelected;
    }
}
