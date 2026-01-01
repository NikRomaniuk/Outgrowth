namespace Outgrowth.Models;

/// <summary>
/// Animated pot object - demonstrates combining IInteractable and IAnimated interfaces
/// Example: A pot that glows or pulses when it needs watering
/// </summary>
public class AnimatedPotObject : EnvObject, IInteractable, IAnimated
{
    public int PotNumber { get; set; }
    
    /// <summary>
    /// Event fired when the pot is clicked
    /// </summary>
    public event EventHandler<TappedEventArgs>? Clicked;
    
    /// <summary>
    /// Action to execute when the pot is interacted with
    /// </summary>
    public Action? InteractAction { get; set; }
    
    /// <summary>
    /// Determines if the pot can be interacted with
    /// </summary>
    public bool CanInteract { get; set; } = true;
    
    /// <summary>
    /// Indicates if the animation is currently running
    /// </summary>
    public bool IsAnimating { get; private set; }
    
    /// <summary>
    /// Animation cancellation token source
    /// </summary>
    private CancellationTokenSource? _animationCts;
    
    /// <summary>
    /// Border element for animation (pulse/glow effect)
    /// </summary>
    private Border? _animatedBorder;
    
    /// <summary>
    /// Visual element for positioning
    /// </summary>
    public View? VisualElement { get; set; }
    
    /// <summary>
    /// Base sprite (emoji or text)
    /// </summary>
    public string BaseSprite { get; set; }
    
    public AnimatedPotObject(int potNumber, int x, int y) 
        : base($"AnimatedPot{potNumber}", x, y, 300, 300)
    {
        PotNumber = potNumber;
        BaseSprite = "🌱"; // Growing plant icon
    }
    
    /// <summary>
    /// Handles interaction with the pot
    /// </summary>
    public void OnInteract()
    {
        if (!CanInteract)
            return;
            
        InteractAction?.Invoke();
    }
    
    /// <summary>
    /// Starts the pulse animation
    /// </summary>
    public async Task StartAnimation()
    {
        if (IsAnimating || _animatedBorder == null)
            return;
        
        IsAnimating = true;
        _animationCts = new CancellationTokenSource();
        
        try
        {
            // Pulse animation loop
            while (!_animationCts.Token.IsCancellationRequested)
            {
                // Fade in (glow)
                await _animatedBorder.FadeTo(0.8, 1000, Easing.SinInOut);
                
                if (_animationCts.Token.IsCancellationRequested)
                    break;
                
                // Fade out
                await _animatedBorder.FadeTo(0.3, 1000, Easing.SinInOut);
            }
        }
        catch (TaskCanceledException)
        {
            // Animation was cancelled, this is expected
        }
        finally
        {
            IsAnimating = false;
        }
    }
    
    /// <summary>
    /// Stops the animation
    /// </summary>
    public void StopAnimation()
    {
        if (!IsAnimating)
            return;
        
        _animationCts?.Cancel();
        _animationCts?.Dispose();
        _animationCts = null;
        IsAnimating = false;
        
        // Reset opacity to default
        if (_animatedBorder != null)
        {
            _animatedBorder.Opacity = 0.5;
        }
    }
    
    /// <summary>
    /// Creates the animated pot UI element
    /// </summary>
    public override View CreateVisualElement()
    {
        var stackLayout = new VerticalStackLayout { Spacing = 10 };
        
        // Main border with pot icon (animated)
        _animatedBorder = new Border
        {
            StrokeThickness = 2,
            Stroke = Color.FromArgb("#4CAF50"),
            BackgroundColor = Colors.Transparent,
            HeightRequest = 300,
            WidthRequest = 300,
            Opacity = 0.5
        };
        
        // Add tap gesture for interactivity
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (sender, e) =>
        {
            Clicked?.Invoke(sender, e);
            OnInteract();
        };
        _animatedBorder.GestureRecognizers.Add(tapGesture);
        
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
            Text = $"[Animated Pot {PotNumber}]",
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 0, 10),
            TextColor = Colors.White,
            Opacity = 0.7
        };
        placeholderLabel.SetDynamicResource(Label.FontSizeProperty, "ButtonPlaceholderSize");
        grid.Children.Add(placeholderLabel);
        
        _animatedBorder.Content = grid;
        stackLayout.Children.Add(_animatedBorder);
        
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
            Text = $"Animated Pot {PotNumber}",
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            TextColor = Colors.White
        };
        potLabel.SetDynamicResource(Label.FontSizeProperty, "ButtonLabelSize");
        stackLayout.Children.Add(potLabel);
        
        VisualElement = stackLayout;
        return stackLayout;
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

