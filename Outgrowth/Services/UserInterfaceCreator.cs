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
    /// Calculates panel dimensions and margins in a platform-agnostic way.
    /// Callers provide base values and the adaptive scale; this method returns
    /// computed sizes that can be applied to platform UI elements.
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
    /// Creates a platform-agnostic resource item Border to be used inside panels.
    /// Callers provide content data (name, sprite) and sizes calculated from resources.
    /// The returned Border already contains a tap gesture that invokes <paramref name="onTapped"/> when provided.
    /// </summary>
    public static Border CreatePanelItem(string id, string name, string sprite, bool isSelected,
        double panelItemHeight, double qtyFontSize, double bodyFontSize, bool isAndroid, Action? onTapped,
        bool isEnabled = true, object? bindingContext = null)
    {
        // Create the outer border that contains the item content
        var border = new Border
        {
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#4CAF50"),
            BackgroundColor = Color.FromArgb("#0F1F2F"),
            Padding = new Thickness(10)
        };

        // Rounded corners for the border
        border.StrokeShape = new RoundRectangle { CornerRadius = 8 };

        // If the item is selected, use a highlighted stroke
        if (isSelected)
        {
            border.Stroke = Color.FromArgb("#FFD700");
            border.StrokeThickness = 2;
        }

        // Platform-specific layout: Android uses a two-column layout with
        // a square icon column followed by content; Windows uses a 2x2
        // layout so name and qty stack vertically.
        if (isAndroid)
        {
            // Android: single-row grid where first column is the icon (square)
            var contentGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = new GridLength(panelItemHeight) },
                    new ColumnDefinition { Width = GridLength.Star }
                }
            };
            contentGrid.HeightRequest = panelItemHeight;
            // Icon view: prefer PNG image, fallback to text/emoji label
            View iconView;
            if (!string.IsNullOrEmpty(sprite) && sprite.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                iconView = new Image
                {
                    Source = sprite,
                    Aspect = Aspect.AspectFit,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
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

            border.Content = contentGrid;
        }
        else
        {
            // Windows: icon on the left, name and qty stacked
            var contentGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = new GridLength(panelItemHeight) },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                RowDefinitions = new RowDefinitionCollection
                {
                    new RowDefinition { Height = new GridLength(panelItemHeight / 2) },
                    new RowDefinition { Height = new GridLength(panelItemHeight / 2) }
                }
            };

            contentGrid.HeightRequest = panelItemHeight;

            View iconView;
            if (!string.IsNullOrEmpty(sprite) && sprite.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                iconView = new Image
                {
                    Source = sprite,
                    Aspect = Aspect.AspectFit,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill
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

            border.Content = contentGrid;
        }

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
    /// Enables or disables a panel item created by CreatePanelItem.
    /// When disabled, the item is grayed out and non-interactive.
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
}
