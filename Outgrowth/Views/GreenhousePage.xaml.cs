using Outgrowth.ViewModels;
using Outgrowth.Models;
using System.Linq;

namespace Outgrowth.Views;

public partial class GreenhousePage : ContentPage
{
    // Current centered pot index (0 = Pot1 rightmost, increases left)
    // Navigation limited to indices 1-3 (Pot2, Pot3, Pot4) - starts at Pot2
    private int _currentItemIndex = 1;
    
    // All pots - created dynamically on page load
    private readonly List<PotObject> _pots =
    [
        new PotObject(1, 9400, 0),   // Pot 1 - rightmost, center vertically
        new PotObject(2, 9000, 0),   // Pot 2 - center vertically
        new PotObject(3, 8600, 0),   // Pot 3 - center, center vertically
        new PotObject(4, 8200, 0),   // Pot 4 - center vertically
        new PotObject(5, 7800, 0)    // Pot 5 - leftmost
    ];
    
    private int[] BaseItemPositions => _pots.Select(p => p.X).ToArray();
    
    public GreenhousePage()
    {
        InitializeComponent();
        BindingContext = new GreenhouseViewModel();
        
        // Set up pot click handlers
        _pots[0].OnClick = OnPot1Clicked;
        _pots[1].OnClick = OnPot2Clicked;
        _pots[2].OnClick = OnPot3Clicked;
        _pots[3].OnClick = OnPot4Clicked;
        _pots[4].OnClick = OnPot5Clicked;
        
        // Set MovePanel visibility based on platform
#if ANDROID
        if (MovePanel != null)
        {
            MovePanel.IsVisible = true;
        }
#endif
        
        this.SizeChanged += OnPageSizeChanged;
        this.Disappearing += OnPageDisappearing;
        this.Loaded += OnPageLoaded;
    }
    
    private void OnPageLoaded(object? sender, EventArgs e)
    {
        CreatePotElements();
        UpdatePotPositions();
        UpdateContentPosition(); // Center Pot2 on load
    }
    
    private void OnPageDisappearing(object? sender, EventArgs e)
    {
        CloseAllPanels();
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        if (EnvironmentWrapper != null && EnvironmentContainer != null && ContentContainer != null && HubButton != null 
            && LiquidsPanel != null && SeedsPanel != null && ToolsPanel != null 
            && LeftGutterPlaceholder != null)
        {
            var pageHeight = this.Height;
            var pageWidth = this.Width;
            
            if (pageHeight > 0 && pageWidth > 0)
            {
                var targetHeight = pageHeight;
                var targetWidth = targetHeight * 16.0 / 9.0;
                if (targetWidth > pageWidth)
                {
                    targetWidth = pageWidth;
                    targetHeight = targetWidth * 9.0 / 16.0;
                }
                
                const double referenceWidth = 1920.0;
                const double referenceHeight = 1080.0;
                
                EnvironmentContainer.WidthRequest = referenceWidth;
                EnvironmentContainer.HeightRequest = referenceHeight;
                
                var scaleX = targetWidth / referenceWidth;
                var scaleY = targetHeight / referenceHeight;
                var scale = Math.Min(scaleX, scaleY);
                
                EnvironmentWrapper.AnchorX = 0.5;
                EnvironmentWrapper.AnchorY = 0.5;
                EnvironmentWrapper.Scale = scale;
                EnvironmentWrapper.WidthRequest = referenceWidth;
                EnvironmentWrapper.HeightRequest = referenceHeight;
                
                EnvironmentContainer.InputTransparent = false;
                EnvironmentContainer.BackgroundColor = Colors.Transparent;
                
                var scaledWidth = referenceWidth * scale;
                var scaledHeight = referenceHeight * scale;
                var offsetX = (targetWidth - scaledWidth) / 2.0;
                var offsetY = (targetHeight - scaledHeight) / 2.0;
                EnvironmentWrapper.TranslationX = offsetX;
                EnvironmentWrapper.TranslationY = offsetY;
                
                HubButton.AnchorX = 1;
                HubButton.AnchorY = 0.5;
                HubButton.Scale = scale;
                
                LeftGutterPlaceholder.AnchorX = 0;
                LeftGutterPlaceholder.AnchorY = 0.5;
                LeftGutterPlaceholder.Scale = scale;
                
                const double windowsBaseWidth = 1920.0;
                var fontScale = pageWidth / windowsBaseWidth;
                
                UpdateFontSizes(fontScale);
                UpdatePanelSizes(fontScale);
                UpdateToolsPanelSize(fontScale);
                UpdateMovePanelSize(fontScale);
                
                // Update pot positions from absolute coordinates
                UpdatePotPositions();
                // Update content position after layout changes
                UpdateContentPosition();
            }
        }
#endif
    }
    
    private void UpdatePanelSizes(double fontScale)
    {
        const double baseWidth = 300.0;
        const double baseHeight = 500.0;
        const double baseMargin = 20.0;
        
        LiquidsPanel.WidthRequest = baseWidth * fontScale;
        LiquidsPanel.HeightRequest = baseHeight * fontScale;
        LiquidsPanel.Margin = new Thickness(baseMargin * fontScale, 0, 0, 0);
        
        SeedsPanel.WidthRequest = baseWidth * fontScale;
        SeedsPanel.HeightRequest = baseHeight * fontScale;
        SeedsPanel.Margin = new Thickness(baseMargin * fontScale, 0, 0, 0);
    }
    
    private void UpdateToolsPanelSize(double fontScale)
    {
        const double baseWidth = 600.0;
        const double baseHeight = 150.0;
        const double basePanelPadding = 15.0;
        const double baseSpacing = 20.0;
        
        if (ToolsPanel.Children.Count > 0 && ToolsPanel.Children[0] is Border toolsBorder)
        {
            var panelHeight = baseHeight * fontScale;
            var panelPadding = basePanelPadding * fontScale;
            
            toolsBorder.WidthRequest = baseWidth * fontScale;
            toolsBorder.HeightRequest = panelHeight;
            toolsBorder.Padding = panelPadding;
            
            // Calculate button size based on panel height: Button size = Panel height - margin (2 * padding)
            var buttonSize = panelHeight - (2 * panelPadding);
            
            // Scale button layout spacing
            if (ToolsButtonsLayout != null)
            {
                ToolsButtonsLayout.Spacing = baseSpacing * fontScale;
            }
            
            // Scale individual buttons (width and height = buttonSize for square buttons)
            if (LiquidsButton != null && SeedsButton != null && CancelButton != null)
            {
                // Button padding scales proportionally to button size
                var buttonPadding = buttonSize * 0.15; // 15% of button size
                // Icon size scales proportionally to button size
                var iconSize = buttonSize * 0.5; // 50% of button size
                
                LiquidsButton.WidthRequest = buttonSize;
                LiquidsButton.HeightRequest = buttonSize;
                LiquidsButton.Padding = buttonPadding;
                
                SeedsButton.WidthRequest = buttonSize;
                SeedsButton.HeightRequest = buttonSize;
                SeedsButton.Padding = buttonPadding;
                
                CancelButton.WidthRequest = buttonSize;
                CancelButton.HeightRequest = buttonSize;
                CancelButton.Padding = buttonPadding;
                
                // Scale icon font sizes
                if (LiquidsIcon != null && SeedsIcon != null && CancelIcon != null)
                {
                    LiquidsIcon.FontSize = iconSize;
                    SeedsIcon.FontSize = iconSize;
                    CancelIcon.FontSize = iconSize;
                }
            }
        }
    }
    
    private void UpdateMovePanelSize(double fontScale)
    {
#if ANDROID
        const double baseWidth = 300.0;
        const double baseHeight = 150.0;
        const double basePanelPadding = 15.0;
        const double baseSpacing = 20.0;
        
        if (MovePanel.Children.Count > 0 && MovePanel.Children[0] is Border moveBorder)
        {
            var panelHeight = baseHeight * fontScale;
            var panelPadding = basePanelPadding * fontScale;
            
            moveBorder.WidthRequest = baseWidth * fontScale;
            moveBorder.HeightRequest = panelHeight;
            moveBorder.Padding = panelPadding;
            
            // Calculate button size based on panel height: Button size = Panel height - margin (2 * padding)
            var buttonSize = panelHeight - (2 * panelPadding);
            
            // Scale button layout spacing
            if (MoveButtonsLayout != null)
            {
                MoveButtonsLayout.Spacing = baseSpacing * fontScale;
            }
            
            // Scale individual buttons (width and height = buttonSize for square buttons)
            if (LeftArrowButton != null && RightArrowButton != null)
            {
                // Button padding scales proportionally to button size
                var buttonPadding = buttonSize * 0.15; // 15% of button size
                // Icon size scales proportionally to button size
                var iconSize = buttonSize * 0.5; // 50% of button size
                
                LeftArrowButton.WidthRequest = buttonSize;
                LeftArrowButton.HeightRequest = buttonSize;
                LeftArrowButton.Padding = buttonPadding;
                
                RightArrowButton.WidthRequest = buttonSize;
                RightArrowButton.HeightRequest = buttonSize;
                RightArrowButton.Padding = buttonPadding;
                
                // Scale icon font sizes
                if (LeftArrowIcon != null && RightArrowIcon != null)
                {
                    LeftArrowIcon.FontSize = iconSize;
                    RightArrowIcon.FontSize = iconSize;
                }
            }
        }
#endif
    }
    
    private void UpdateFontSizes(double fontScale)
    {
        const double baseTitleSize = 40.0;
        const double baseBodySize = 30.0;
        const double baseQtySize = 24.0;
        const double baseIconSize = 40.0;
        
        Resources["ResourcePanelTitleSize"] = baseTitleSize * fontScale;
        Resources["ResourcePanelBodySize"] = baseBodySize * fontScale;
        Resources["ResourcePanelQtySize"] = baseQtySize * fontScale;
        Resources["ResourcePanelIconSize"] = baseIconSize * fontScale;
    }

    private async void OnHubClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//HubPage");
    }

    private void OnPot1Clicked(object? sender, TappedEventArgs e)
    {
    }

    private void OnPot2Clicked(object? sender, TappedEventArgs e)
    {
    }

    private void OnPot3Clicked(object? sender, TappedEventArgs e)
    {
    }

    private void OnPot4Clicked(object? sender, TappedEventArgs e)
    {
    }

    private void OnPot5Clicked(object? sender, TappedEventArgs e)
    {
    }

    private void OnLiquidsButtonClicked(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        if (LiquidsPanel != null && SeedsPanel != null && BackgroundOverlay != null)
        {
            SeedsPanel.IsVisible = false;
            SeedsPanel.InputTransparent = true;
            
            LiquidsPanel.IsVisible = true;
            LiquidsPanel.InputTransparent = false;
            
            BackgroundOverlay.IsVisible = true;
            BackgroundOverlay.InputTransparent = false;
        }
#endif
    }

    private void OnSeedsButtonClicked(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        if (LiquidsPanel != null && SeedsPanel != null && BackgroundOverlay != null)
        {
            LiquidsPanel.IsVisible = false;
            LiquidsPanel.InputTransparent = true;
            
            SeedsPanel.IsVisible = true;
            SeedsPanel.InputTransparent = false;
            
            BackgroundOverlay.IsVisible = true;
            BackgroundOverlay.InputTransparent = false;
        }
#endif
    }

    private void OnCancelButtonClicked(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        CloseAllPanels();
#endif
    }

    private void OnLeftArrowButtonClicked(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        // Move left (max index 3 = Pot4)
        if (_currentItemIndex < 3)
        {
            _currentItemIndex++;
            UpdateContentPosition();
        }
#endif
    }

    private void OnRightArrowButtonClicked(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        // Move right (min index 1 = Pot2)
        if (_currentItemIndex > 1)
        {
            _currentItemIndex--;
            UpdateContentPosition();
        }
#endif
    }
    
    /// <summary>
    /// Creates pot UI elements and adds them to ContentContainer
    /// </summary>
    private void CreatePotElements()
    {
#if ANDROID || WINDOWS
        if (ContentContainer == null)
            return;
        
        foreach (var pot in _pots)
        {
            var visualElement = pot.CreateVisualElement();
            ContentContainer.Children.Add(visualElement);
        }
#endif
    }
    
    /// <summary>
    /// Updates pot positions using 1:1 coordinate system (container center: X=9600, Y=540)
    /// </summary>
    private void UpdatePotPositions()
    {
#if ANDROID || WINDOWS
        if (ContentContainer == null)
            return;
        
        const double containerCenterX = 9600.0;
        const double containerCenterY = 540.0;
        
        foreach (var pot in _pots)
        {
            pot.UpdatePosition(containerCenterX, containerCenterY);
        }
#endif
    }
    
    /// <summary>
    /// Centers the selected pot by moving ContentContainer
    /// Formula: translationOffset = screenCenter - itemCenterX
    /// </summary>
    private void UpdateContentPosition()
    {
#if ANDROID || WINDOWS
        if (ContentContainer == null)
            return;
        
        int currentItemLogicalX = BaseItemPositions[_currentItemIndex];
        const double containerCenter = 9600.0;
        const double screenCenter = 960.0;
        
        // Convert to pixel position and center on screen
        double itemCenterX = containerCenter + currentItemLogicalX; // 1:1 ratio
        double translationOffset = screenCenter - itemCenterX;
        
        ContentContainer.TranslationX = translationOffset;
#endif
    }

    private void OnBackgroundOverlayTapped(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        CloseAllPanels();
#endif
    }

    private void OnLiquidsPanelTapped(object sender, EventArgs e)
    {
    }

    private void OnSeedsPanelTapped(object sender, EventArgs e)
    {
    }

    private void CloseAllPanels()
    {
#if ANDROID || WINDOWS
        if (LiquidsPanel != null && SeedsPanel != null && BackgroundOverlay != null)
        {
            LiquidsPanel.IsVisible = false;
            LiquidsPanel.InputTransparent = true;
            
            SeedsPanel.IsVisible = false;
            SeedsPanel.InputTransparent = true;
            
            BackgroundOverlay.IsVisible = false;
            BackgroundOverlay.InputTransparent = true;
        }
#endif
    }
}
