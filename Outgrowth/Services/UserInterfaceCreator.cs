using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace Outgrowth.Services;

public static class UserInterfaceCreator
{
    public sealed class PanelSizesResult
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public double Margin { get; set; }

        public double SelectedWidth { get; set; }
        public double SelectedHeight { get; set; }
        public double SelectedMarginLeft { get; set; }

        // Proportional Y position used by callers (0.0 - 1.0)
        public double PanelYPosition { get; set; }
    }

    /// <summary>
    /// Calculates panel dimensions and margins in a platform-agnostic way
    /// Callers provide base values and the adaptive scale; this method returns
    /// computed sizes that can be applied to platform UI elements
    /// </summary>
    public static PanelSizesResult GetPanelSizes(
        double adaptiveScale,
        double baseWidth,
        double baseHeight,
        double baseMargin,
        double selectedPanelWidth,
        double selectedPanelHeight,
        bool isAndroid)
    {
        var r = new PanelSizesResult();

        r.Width = baseWidth * adaptiveScale;
        r.Height = baseHeight * adaptiveScale;
        r.Margin = baseMargin * adaptiveScale;

        r.SelectedWidth = selectedPanelWidth * adaptiveScale;
        r.SelectedHeight = selectedPanelHeight * adaptiveScale;
        r.SelectedMarginLeft = baseMargin * adaptiveScale;

        r.PanelYPosition = isAndroid ? 0.7 : 0.5;

        return r;
    }

    /// <summary>
    /// Creates a platform-agnostic resource item Border to be used inside panels
    /// Uses two 9-slice panel backgrounds (normal and highlighted) that fade between states
    /// Callers provide content data (name, sprite) and sizes calculated from resources
    /// The returned Border already contains a tap gesture that invokes <paramref name="onTapped"/> when provided
    /// </summary>
    public static Border CreatePanelItem(string id, string name, string sprite, bool isSelected,
        double panelItemHeight, double qtyFontSize, double bodyFontSize, bool isAndroid, Action? onTapped,
        bool isEnabled = true, object? bindingContext = null)
    {
        // Create outer container (transparent border, no stroke)
        var border = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Colors.Transparent,
            Padding = new Thickness(0)
        };

        // Container grid to hold background panels and content
        var container = new Grid();

        // Corner size base (40px) scaled by screen adaptive scale so items fit on phones
        double cornerSize = 40 * (ScreenProperties.Instance?.AdaptiveScale ?? 1.0);

        // Compute total panel height: two corners plus the content area
        double panelHeight = panelItemHeight + (cornerSize * 2);

        // Create normal background (9-slice panel)
        var normalPanel = CreateNineSlicePanel(
            width: double.NaN, // Fill available space
            height: panelHeight,
            cornerSize: cornerSize,
            backgroundColor: Color.FromArgb("#1b1b35"),
            borderColor: Color.FromArgb("#40406e"),
            content: null,
            cornerImage: "ui__panel_item_corner.png",
            horizontalEdgeImage: "ui__panel_item_edge_horizontal.png",
            verticalEdgeImage: "ui__panel_item_edge_vertical.png",
            centerImage: "ui__panel_item_center.png"
        );
        normalPanel.AutomationId = "PanelBackground";
        normalPanel.Opacity = isSelected ? 0 : 1;
        normalPanel.HorizontalOptions = LayoutOptions.Fill;
        normalPanel.VerticalOptions = LayoutOptions.Fill;

        // Create highlighted background (9-slice panel with different images)
        var highlightedPanel = CreateNineSlicePanel(
            width: double.NaN, // Fill available space
            height: panelHeight,
            cornerSize: cornerSize,
            backgroundColor: Color.FromArgb("#2874a7"),
            borderColor: Color.FromArgb("#00d2ff"),
            content: null,
            cornerImage: "ui__panel_highlighted_corner.png",
            horizontalEdgeImage: "ui__panel_highlighted_edge_horizontal.png",
            verticalEdgeImage: "ui__panel_highlighted_edge_vertical.png",
            centerImage: "ui__panel_highlighted_center.png"
        );
        highlightedPanel.AutomationId = "HighlightedPanelBackground";
        highlightedPanel.Opacity = isSelected ? 1 : 0;
        highlightedPanel.HorizontalOptions = LayoutOptions.Fill;
        highlightedPanel.VerticalOptions = LayoutOptions.Fill;

        // Add background panels to container
        container.Children.Add(normalPanel);
        container.Children.Add(highlightedPanel);

        // Create content grid on top of backgrounds with margin to avoid corners
        // Use the same content padding ratio as CreateNineSlicePanel (70% of cornerSize)
        double contentPadding = cornerSize * 0.7;
        var contentGrid = new Grid
        {
            Padding = new Thickness(10),
            Margin = new Thickness(0, contentPadding, 0, contentPadding),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center
        };

        // Platform-specific layout: Android uses a two-column layout with
        // a square icon column followed by content; Windows uses a 2x2
        // layout so name and qty stack vertically
        if (isAndroid)
        {
            // Android: single-row grid where first column is the icon (square)
            contentGrid.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = new GridLength(panelItemHeight) },
                new ColumnDefinition { Width = GridLength.Star }
            };
            // let layout determine height so centering works correctly
            // Icon view: prefer PNG image, fallback to text/emoji label
            View iconView;
            if (!string.IsNullOrEmpty(sprite) && sprite.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                    iconView = new Image
                    {
                        Source = sprite,
                        Aspect = Aspect.AspectFit,
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Center,
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
            contentGrid.Children.Add(iconView);

            // Quantity label in the secondary column (right-aligned)
            var qtyLabel = new Label
            {
                FontSize = qtyFontSize * 1.5,
                TextColor = Colors.White,
                Opacity = 0.7,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.End
            };
            // Apply font family if available
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
            contentGrid.Children.Add(qtyLabel);
        }
        else
        {
            // Windows: icon on the left, name and qty stacked
            contentGrid.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = new GridLength(panelItemHeight) },
                new ColumnDefinition { Width = GridLength.Auto }
            };
            contentGrid.RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = new GridLength(panelItemHeight / 2) },
                new RowDefinition { Height = new GridLength(panelItemHeight / 2) }
            };

            // let layout determine height so centering works correctly

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
            contentGrid.Children.Add(iconView);

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
            contentGrid.Children.Add(nameLabel);

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
            contentGrid.Children.Add(qtyLabel);
        }

        // Add content grid on top of background panels
        container.Children.Add(contentGrid);

        // Ensure border/container reserve enough height for corners + content
        container.HeightRequest = panelHeight;
        border.HeightRequest = panelHeight;

        // Set container as border content
        border.Content = container;

        // Disable interaction and visually gray out when not enabled
        if (!isEnabled)
        {
            border.Opacity = 0.5;
            border.InputTransparent = true;
        }
        else
        {
            // Attach tap handler if provided by caller (selection callback)
            if (onTapped != null)
            {
                var tap = new TapGestureRecognizer();
                tap.Tapped += (s, e) => onTapped();
                border.GestureRecognizers.Add(tap);
            }
        }

        return border;
    }

    /// <summary>
    /// Enables or disables a panel item created by CreatePanelItem
    /// When disabled, the item is grayed out and non-interactive
    /// </summary>
    public static void SetPanelItemEnabled(Border panelItem, bool isEnabled)
    {
        if (panelItem == null)
            return;

        if (!isEnabled)
        {
            panelItem.Opacity = 0.5;
            panelItem.InputTransparent = true;
        }
        else
        {
            panelItem.Opacity = 1.0;
            panelItem.InputTransparent = false;
        }
    }

    /// <summary>
    /// Updates the selection state of a panel item by fading between normal and highlighted backgrounds
    /// </summary>
    public static void SetPanelItemSelected(Border panelItem, bool isSelected, bool animate = true)
    {
        if (panelItem?.Content is not Grid container)
            return;

        Grid? normalPanel = null;
        Grid? highlightedPanel = null;

        // Find background panels by AutomationId
        foreach (var child in container.Children)
        {
            if (child is Grid grid)
            {
                if (grid.AutomationId == "PanelBackground")
                    normalPanel = grid;
                else if (grid.AutomationId == "HighlightedPanelBackground")
                    highlightedPanel = grid;
            }
        }

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
    }

    /// <summary>
    /// Updates selection state for panels created by CreateNineSlicePanelWithSelection.
    /// Behavior is identical to SetPanelItemSelected; this helper exists for
    /// semantic clarity when working with selection-style panels.
    /// </summary>
    public static void SetPanelSelected(Border panel, bool isSelected, bool animate = true)
    {
        // Reuse existing implementation to keep behavior consistent
        SetPanelItemSelected(panel, isSelected, animate);
    }

    /// <summary>
    /// Creates a 9-Slice (Nine-Patch) panel container with pixel-art style borders
    /// The panel uses fixed-size corners and stretchable edges/center to maintain pixel-perfect appearance
    /// </summary>
    /// <param name="width">Total width of the panel</param>
    /// <param name="height">Total height of the panel</param>
    /// <param name="cornerSize">Size of each corner in pixels (default: 8)</param>
    /// <param name="backgroundColor">Background color for the center area</param>
    /// <param name="borderColor">Color for the border elements</param>
    /// <param name="content">Optional content to place in the center of the panel</param>
    /// <returns>A Grid configured as a 9-slice panel</returns>
    public static Grid CreateNineSlicePanel(
        double width,
        double height,
        double cornerSize = 8,
        Color? backgroundColor = null,
        Color? borderColor = null,
        View? content = null,
        // Simple API: single corner image (used for all four corners, rotated),
        // horizontal edge (for top/bottom), vertical edge (for left/right), and center image
        string? cornerImage = null,
        string? horizontalEdgeImage = null,
        string? verticalEdgeImage = null,
        string? centerImage = null)
    {
        backgroundColor ??= Color.FromArgb("#0f0c29");
        borderColor ??= Color.FromArgb("#302b63");

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

        // Use the simple API: reuse the same corner image for all corners (rotated),
        // horizontal edge for top/bottom (no rotation), vertical edge for left/right (no rotation)
        // If image is null, fallback to BoxView
        View cornerTL = CreateSliceView(borderColor, cornerImage, cornerSize, cornerSize, rotationDeg: 0);
        View cornerTR = CreateSliceView(borderColor, cornerImage, cornerSize, cornerSize, rotationDeg: 90);
        View cornerBR = CreateSliceView(borderColor, cornerImage, cornerSize, cornerSize, rotationDeg: 180);
        View cornerBL = CreateSliceView(borderColor, cornerImage, cornerSize, cornerSize, rotationDeg: 270);

        // Horizontal edges stretch horizontally (top/bottom)
        View edgeTop = CreateSliceView(borderColor, horizontalEdgeImage, null, null, rotationDeg: 0);
        View edgeBottom = CreateSliceView(borderColor, horizontalEdgeImage, null, null, rotationDeg: 180);
        
        // Vertical edges stretch vertically (left/right)
        View edgeLeft = CreateSliceView(borderColor, verticalEdgeImage, null, null, rotationDeg: 0);
        View edgeRight = CreateSliceView(borderColor, verticalEdgeImage, null, null, rotationDeg: 180);

        // Apply negative margins to edges so they overlap corners (removes visible gaps)
        double edgeOverlap = cornerSize * 0.001; // 0.1% overlap
        
        // Platform-specific adjustment for rotated edges (bottom and right with 180° rotation)
        // On Android: rotated edges need additional negative margin to close gap with center
        // On Windows: rotated edges need positive margin to prevent overlap with center
        bool isAndroid = DeviceInfo.Platform == DevicePlatform.Android;
        double rotatedEdgeAdjustment = isAndroid ? -0.5 : 0.5;
        
        // Don't know why it works, but it just workds... (Done through endless testing)
        edgeTop.Margin = new Thickness(-edgeOverlap, 0, rotatedEdgeAdjustment, 0);
        edgeBottom.Margin = new Thickness(-edgeOverlap, rotatedEdgeAdjustment, rotatedEdgeAdjustment, rotatedEdgeAdjustment);
        edgeLeft.Margin = new Thickness(0, -edgeOverlap, 0, rotatedEdgeAdjustment);
        edgeRight.Margin = new Thickness(rotatedEdgeAdjustment, -edgeOverlap, 0, rotatedEdgeAdjustment);

        View center = CreateSliceView(backgroundColor, centerImage, null, null, rotationDeg: 0);

        // Position corners
        Grid.SetRow(cornerTL, 0); Grid.SetColumn(cornerTL, 0);
        Grid.SetRow(cornerTR, 0); Grid.SetColumn(cornerTR, 2);
        Grid.SetRow(cornerBL, 2); Grid.SetColumn(cornerBL, 0);
        Grid.SetRow(cornerBR, 2); Grid.SetColumn(cornerBR, 2);

        // Position edges
        Grid.SetRow(edgeTop, 0); Grid.SetColumn(edgeTop, 1);
        Grid.SetRow(edgeBottom, 2); Grid.SetColumn(edgeBottom, 1);
        Grid.SetRow(edgeLeft, 1); Grid.SetColumn(edgeLeft, 0);
        Grid.SetRow(edgeRight, 1); Grid.SetColumn(edgeRight, 2);

        // Position center
        Grid.SetRow(center, 1); Grid.SetColumn(center, 1);

        // Add all slices to panel
        panel.Children.Add(cornerTL);
        panel.Children.Add(cornerTR);
        panel.Children.Add(cornerBL);
        panel.Children.Add(cornerBR);
        panel.Children.Add(edgeTop);
        panel.Children.Add(edgeBottom);
        panel.Children.Add(edgeLeft);
        panel.Children.Add(edgeRight);
        panel.Children.Add(center);

        // Add content if provided - span entire panel with padding to avoid covering edges
        if (content != null)
        {
            Grid.SetRow(content, 0);
            Grid.SetColumn(content, 0);
            Grid.SetRowSpan(content, 3);
            Grid.SetColumnSpan(content, 3);
            // Add margin to keep content away from edges (slightly overlapping)
            double contentPadding = cornerSize * 0.7; // 70% of corner size
            content.Margin = new Thickness(contentPadding);
            panel.Children.Add(content);
        }

        return panel;
    }

    /// <summary>
    /// Creates a slice element for the 9-slice panel. Returns an Image when <paramref name="imageSource"/>
    /// is provided, otherwise returns a BoxView filled with <paramref name="color"/>
    /// </summary>
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
                // Set anchor point to center for rotation
                AnchorX = 0.5,
                AnchorY = 0.5
            };

            // Don't set explicit WidthRequest/HeightRequest for images - let Grid handle sizing
            // This allows proper stretching for edges and correct sizing for corners

            // Apply rotation for reused corner/edge sprites
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

    /// <summary>
    /// Creates a 9-Slice panel container specifically for resource/seed/liquid panels.
    /// Returns both the outer panel and the inner content container where items should be added.
    /// </summary>
    /// <param name="width">Total width of the panel</param>
    /// <param name="height">Total height of the panel</param>
    /// <param name="cornerSize">Size of each corner in pixels</param>
    /// <param name="backgroundColor">Background color for the center area</param>
    /// <param name="borderColor">Color for the border elements</param>
    /// <returns>Tuple of (outerPanel, contentContainer) where contentContainer is a ScrollView for items</returns>
    public static (Grid outerPanel, ScrollView contentContainer) CreateNineSlicePanelWithScroll(
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
        backgroundColor ??= Color.FromArgb("#0f0c29");
        borderColor ??= Color.FromArgb("#302b63");

        // Create content container (ScrollView with VerticalStackLayout)
        var stackLayout = new VerticalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(5)
        };

        var scrollView = new ScrollView
        {
            Content = stackLayout,
            Orientation = ScrollOrientation.Vertical,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        // Create 9-slice panel with scroll view as content (forward simplified image params)
        var panel = CreateNineSlicePanel(
            width,
            height,
            cornerSize,
            backgroundColor,
            borderColor,
            scrollView,
            cornerImage,
            horizontalEdgeImage,
            verticalEdgeImage,
            centerImage
        );

        return (panel, scrollView);
    }

    /// <summary>
    /// Creates a panel that matches the visual structure used by CreatePanelItem
    /// but leaves the content area empty for callers to populate. Returns the
    /// outer Border and the inner content Grid so callers can add their own
    /// children (no icon/qty labels are created).
    /// </summary>
    public static (Border outerBorder, Grid contentGrid) CreateNineSlicePanelWithSelection(
        double width,
        double height,
        bool isSelected,
        double cornerSize = 40,
        Color? backgroundColor = null,
        Color? borderColor = null,
        string? cornerImage = "ui__panel_item_corner.png",
        string? horizontalEdgeImage = "ui__panel_item_edge_horizontal.png",
        string? verticalEdgeImage = "ui__panel_item_edge_vertical.png",
        string? centerImage = "ui__panel_item_center.png")
    {
        // Outer transparent border
        var border = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Colors.Transparent,
            Padding = new Thickness(0)
        };

        var container = new Grid();

        double panelWidth = width;
        double panelHeight = height;

        // Normal/background and highlighted panels (9-slice)
        var normalPanel = CreateNineSlicePanel(
            width: panelWidth,
            height: panelHeight,
            cornerSize: cornerSize,
            backgroundColor: Color.FromArgb("#1b1b35"),
            borderColor: Color.FromArgb("#40406e"),
            content: null,
            cornerImage: cornerImage,
            horizontalEdgeImage: horizontalEdgeImage,
            verticalEdgeImage: verticalEdgeImage,
            centerImage: centerImage
        );
        normalPanel.AutomationId = "PanelBackground";
        normalPanel.Opacity = isSelected ? 0 : 1;
        normalPanel.HorizontalOptions = LayoutOptions.Fill;
        normalPanel.VerticalOptions = LayoutOptions.Fill;

        var highlightedPanel = CreateNineSlicePanel(
            width: panelWidth,
            height: panelHeight,
            cornerSize: cornerSize,
            backgroundColor: Color.FromArgb("#2874a7"),
            borderColor: Color.FromArgb("#00d2ff"),
            content: null,
            cornerImage: "ui__panel_highlighted_corner.png",
            horizontalEdgeImage: "ui__panel_highlighted_edge_horizontal.png",
            verticalEdgeImage: "ui__panel_highlighted_edge_vertical.png",
            centerImage: "ui__panel_highlighted_center.png"
        );
        highlightedPanel.AutomationId = "HighlightedPanelBackground";
        highlightedPanel.Opacity = isSelected ? 1 : 0;
        highlightedPanel.HorizontalOptions = LayoutOptions.Fill;
        highlightedPanel.VerticalOptions = LayoutOptions.Fill;

        container.Children.Add(normalPanel);
        container.Children.Add(highlightedPanel);

        // Create an empty content grid
        double contentPadding = cornerSize * 0.7;
        var contentGrid = new Grid
        {
            Padding = new Thickness(10),
            Margin = new Thickness(0, contentPadding, 0, contentPadding),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center
        };

        // Add content grid on top
        container.Children.Add(contentGrid);

        // Reserve size for panel
        container.HeightRequest = panelHeight;
        container.WidthRequest = panelWidth;
        border.HeightRequest = panelHeight;
        border.WidthRequest = panelWidth;

        border.Content = container;

        return (border, contentGrid);
    }

    /// <summary>
    /// Checks whether UI elements with the given AutomationIds exist under the specified root element.
    /// Returns `false` when all requested elements are present; otherwise returns a <see cref="List{String}"/>
    /// containing the AutomationIds that were not found (so callers can create them).
    /// This is intentionally loosely typed so callers can branch on a boolean vs. a list result.
    /// </summary>
    public static object CheckUiElementsExist(VisualElement? root, IEnumerable<string> automationIds)
    {
        var missing = new List<string>();
        if (automationIds == null)
            return false;

        foreach (var id in automationIds)
        {
            if (string.IsNullOrEmpty(id))
                continue;

            if (root == null)
            {
                missing.Add(id);
                continue;
            }

            if (!FindByAutomationId(root, id))
                missing.Add(id);
        }

        return missing.Count == 0 ? (object)false : missing;
    }

    // Recursive search for AutomationId in common VisualElement containers
    private static bool FindByAutomationId(VisualElement element, string automationId)
    {
        try
        {
            if (element == null)
                return false;

            if (!string.IsNullOrEmpty(element.AutomationId) && element.AutomationId == automationId)
                return true;

            // Grid / Layouts
            if (element is Layout layout)
            {
                foreach (var child in layout.Children)
                {
                    if (child is VisualElement ve && FindByAutomationId(ve, automationId))
                        return true;
                }
            }

            // ContentView / Border / ScrollView / other single-child containers
            switch (element)
            {
                case ContentView cv when cv.Content is VisualElement cve:
                    if (FindByAutomationId(cve, automationId)) return true;
                    break;
                case Border b when b.Content is VisualElement bve:
                    if (FindByAutomationId(bve, automationId)) return true;
                    break;
                case ScrollView sv when sv.Content is VisualElement sve:
                    if (FindByAutomationId(sve, automationId)) return true;
                    break;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
