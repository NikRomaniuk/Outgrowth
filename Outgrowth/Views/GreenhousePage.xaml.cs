using Outgrowth.ViewModels;
using Outgrowth.Models;
using Outgrowth.Services;
using System.Linq;
#if WINDOWS
using Outgrowth.Platforms.Windows;
#endif

namespace Outgrowth.Views;

public partial class GreenhousePage : ContentPage
{
    // Current centered pot index (0=Pot1 rightmost, increases left)
    // Navigation limited to indices 1-3 (Pot2, Pot3, Pot4), starts at Pot2
    private int _currentItemIndex = 1;
    
#if WINDOWS
    private WindowsInput? _windowsInput;
#endif
    
    private readonly List<PotObject> _pots =
    [
        new PotObject(1, 9400, -200, "pot_object_s001.png"),   // Pot 1 - rightmost
        new PotObject(2, 9000, -200, "pot_object_s001.png"),   // Pot 2 - starting position
        new PotObject(3, 8600, -200, "pot_object_s001.png"),   // Pot 3 - center
        new PotObject(4, 8200, -200, "pot_object_s001.png"),   // Pot 4
        new PotObject(5, 7800, -200, "pot_object_s001.png")    // Pot 5 - leftmost
    ];
    
    private int[] BaseItemPositions => _pots.Select(p => p.X).ToArray();
    
    public GreenhousePage()
    {
        InitializeComponent();
        BindingContext = new GreenhouseViewModel();
        
        foreach (var pot in _pots)
        {
            pot.Clicked += (sender, e) => OnPotClicked(pot);
            pot.InteractAction = () => HandlePotInteraction(pot.PotNumber);
        }
        
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
        
#if WINDOWS
        _windowsInput = new WindowsInput(
            onLeftArrow: OnLeftArrowPressed,
            onRightArrow: OnRightArrowPressed,
            onEscape: CloseAllPanels
        );
        _windowsInput.Attach();
#endif
    }
    
    private void OnPageDisappearing(object? sender, EventArgs e)
    {
        CloseAllPanels();
        CleanupPotElements();
        
#if WINDOWS
        _windowsInput?.Detach();
        _windowsInput = null;
#endif
    }
    
    private void CleanupPotElements()
    {
#if ANDROID || WINDOWS
        if (ContentContainer == null)
            return;
        
        foreach (var pot in _pots)
        {
            if (pot.VisualElement != null && ContentContainer.Children.Contains(pot.VisualElement))
            {
                // Clear gesture recognizers to prevent memory leaks
                if (pot.VisualElement is Grid grid)
                {
                    foreach (var child in grid.Children)
                    {
                        if (child is Border border && border.GestureRecognizers.Count > 0)
                        {
                            border.GestureRecognizers.Clear();
                        }
                    }
                }
                
                ContentContainer.Children.Remove(pot.VisualElement);
            }
        }
#endif
    }

#if WINDOWS
    private void OnLeftArrowPressed()
    {
        System.Diagnostics.Debug.WriteLine($"[Windows] Left Arrow/A pressed, current index: {_currentItemIndex}");
        
        // Move left (increase index, max = pots.Count - 2)
        if (_currentItemIndex < _pots.Count - 2)
        {
            _currentItemIndex++;
            System.Diagnostics.Debug.WriteLine($"[Windows] Moving left, new index: {_currentItemIndex}");
            UpdateContentPosition();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[Windows] Cannot move left, already at max index");
        }
    }
    
    private void OnRightArrowPressed()
    {
        System.Diagnostics.Debug.WriteLine($"[Windows] Right Arrow/D pressed, current index: {_currentItemIndex}");
        
        // Move right (decrease index, min = 1)
        if (_currentItemIndex > 1)
        {
            _currentItemIndex--;
            System.Diagnostics.Debug.WriteLine($"[Windows] Moving right, new index: {_currentItemIndex}");
            UpdateContentPosition();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[Windows] Cannot move right, already at min index");
        }
    }
#endif

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
                var screenProps = ScreenProperties.Instance;
                screenProps.UpdateScreenProperties(pageWidth, pageHeight);
                
                EnvironmentContainer.WidthRequest = ScreenProperties.ReferenceWidth;
                EnvironmentContainer.HeightRequest = ScreenProperties.ReferenceHeight;
                
                EnvironmentWrapper.AnchorX = 0.5;
                EnvironmentWrapper.AnchorY = 0.5;
                EnvironmentWrapper.Scale = screenProps.Scale;
                EnvironmentWrapper.WidthRequest = ScreenProperties.ReferenceWidth;
                EnvironmentWrapper.HeightRequest = ScreenProperties.ReferenceHeight;
                
                EnvironmentContainer.InputTransparent = false;
                EnvironmentContainer.BackgroundColor = Colors.Transparent;
                
                EnvironmentWrapper.TranslationX = screenProps.OffsetX;
                EnvironmentWrapper.TranslationY = screenProps.OffsetY;
                
                HubButton.AnchorX = 1;
                HubButton.AnchorY = 1;
                HubButton.Scale = screenProps.Scale;
                
                LeftGutterPlaceholder.AnchorX = 0;
                LeftGutterPlaceholder.AnchorY = 0.5;
                LeftGutterPlaceholder.Scale = screenProps.Scale;
                
                UpdateFontSizes(screenProps.FontScale);
                UpdatePanelSizes(screenProps.FontScale);
                UpdateToolsPanelSize(screenProps.FontScale);
                UpdateMovePanelSize(screenProps.FontScale);
                
                UpdatePotPositions();
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
            
            // Button size = panel height - (2 * padding)
            var buttonSize = panelHeight - (2 * panelPadding);
            
            if (ToolsButtonsLayout != null)
            {
                ToolsButtonsLayout.Spacing = baseSpacing * fontScale;
            }
            
            if (LiquidsButton != null && SeedsButton != null && CancelButton != null)
            {
                var buttonPadding = buttonSize * 0.15; // 15% of button size
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
            
            // Button size = panel height - (2 * padding)
            var buttonSize = panelHeight - (2 * panelPadding);
            
            if (MoveButtonsLayout != null)
            {
                MoveButtonsLayout.Spacing = baseSpacing * fontScale;
            }
            
            if (LeftArrowButton != null && RightArrowButton != null)
            {
                var buttonPadding = buttonSize * 0.15; // 15% of button size
                var iconSize = buttonSize * 0.5; // 50% of button size
                
                LeftArrowButton.WidthRequest = buttonSize;
                LeftArrowButton.HeightRequest = buttonSize;
                LeftArrowButton.Padding = buttonPadding;
                
                RightArrowButton.WidthRequest = buttonSize;
                RightArrowButton.HeightRequest = buttonSize;
                RightArrowButton.Padding = buttonPadding;
                
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
        await NavigationService.NavigateWithFadeAsync("//HubPage");
    }

    private void HandlePotInteraction(int potNumber)
    {
        // TODO: Add pot interaction logic
        System.Diagnostics.Debug.WriteLine($"Pot {potNumber} interacted via IInteractable.OnInteract()");
    }

    private void OnPotClicked(PotObject pot)
    {
        System.Diagnostics.Debug.WriteLine($"Pot {pot.PotNumber} (ID: {pot.Id}) clicked");
        // TODO: Add pot-specific click logic
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
        // Move left (max = pots.Count - 2)
        if (_currentItemIndex < _pots.Count - 2)
        {
            _currentItemIndex++;
            UpdateContentPosition();
        }
#endif
    }

    private void OnRightArrowButtonClicked(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        // Move right (min = 1, Pot2)
        if (_currentItemIndex > 1)
        {
            _currentItemIndex--;
            UpdateContentPosition();
        }
#endif
    }
    
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
    
    // Updates pot positions (container center: X=9600, Y=540, 1:1 ratio)
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
    
    // Centers selected pot: translationOffset = screenCenter - itemCenterX
    private void UpdateContentPosition()
    {
#if ANDROID || WINDOWS
        if (ContentContainer == null)
            return;
        
        int currentItemLogicalX = BaseItemPositions[_currentItemIndex];
        const double containerCenter = 9600.0;
        const double screenCenter = 960.0;
        
        double itemCenterX = containerCenter + currentItemLogicalX; // 1:1 ratio
        double translationOffset = screenCenter - itemCenterX;
        
        ContentContainer.TranslationX = translationOffset;
#endif
    }

    private void OnBackgroundOverlayTapped(object sender, EventArgs e)
    {
#if ANDROID
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
