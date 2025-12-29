using Outgrowth.ViewModels;
using Outgrowth.Models;
using Outgrowth.Services;
using System.Linq;
using Microsoft.Maui.Controls.Shapes;
#if WINDOWS
using Outgrowth.Platforms.Windows;
#endif

namespace Outgrowth.Views;

public partial class LaboratoryPage : ContentPage
{
    // Station objects - created dynamically on page load
    private readonly List<StationObject> _stationObjects = new()
    {
        new StationObject("ResourceSlot", "Resource Slot", 0, 200, 300, 300, "🌾", Color.FromArgb("#4A4A4A")),
        new StationObject("Extract", "Extract", 0, -200, 250, 250, "⚗️", Color.FromArgb("#2C1A5F"))
    };
    
    // Selected resource for interaction
    private ResourceData? _selectedResource;
    
#if WINDOWS
    private WindowsInput? _windowsInput;
#endif
    
    public LaboratoryPage()
    {
        InitializeComponent();
        BindingContext = new LaboratoryViewModel();
        
        // Set up station object click handlers
        _stationObjects[0].Clicked += (s, e) => OnResourceSlotClicked(s ?? this, e);
        _stationObjects[0].InteractAction = () => System.Diagnostics.Debug.WriteLine("Resource Slot interacted");
        
        _stationObjects[1].Clicked += (s, e) => OnExtractClicked(s ?? this, e);
        _stationObjects[1].InteractAction = () => System.Diagnostics.Debug.WriteLine("Extract interacted");
        
        // Handle page size changes for scaling
        this.SizeChanged += OnPageSizeChanged;
        
        // Close panel when navigating away
        this.Disappearing += OnPageDisappearing;
        
        // Update element positions on load
        this.Loaded += OnPageLoaded;
    }
    
    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        // Ensure ResourceLibrary is initialized before creating resource items
        // InitializeAsync() is idempotent and thread-safe, so safe to call multiple times
        await ResourceLibrary.InitializeAsync();
        
        // Create station objects and add to environment
        CreateStationObjects();
        // Initialize element positions from absolute coordinates
        UpdateStationPositions();
        
        // Create dynamic resource panel from library
        UpdateResourcePanel();
        
#if WINDOWS
        // Attach Windows keyboard input handler (only Esc for closing panels)
        _windowsInput = new WindowsInput(
            onLeftArrow: () => { }, // No action
            onRightArrow: () => { }, // No action
            onEscape: CloseResourcePanel  // Close panel with Esc key
        );
        _windowsInput.Attach();
#endif
    }
    
    private void OnPageDisappearing(object? sender, EventArgs e)
    {
        CloseResourcePanel();
        
        // Clean up dynamically created elements to prevent memory leaks
        CleanupStationObjects();
        
#if WINDOWS
        // Detach Windows keyboard input handler
        _windowsInput?.Detach();
        _windowsInput = null;
#endif
    }
    
    /// <summary>
    /// Cleans up station objects and their visual elements to prevent memory leaks
    /// </summary>
    private void CleanupStationObjects()
    {
#if ANDROID || WINDOWS
        if (EnvironmentContainer == null)
            return;
        
        // Remove all dynamically created elements from container
        foreach (var station in _stationObjects)
        {
            if (station.VisualElement != null && EnvironmentContainer.Children.Contains(station.VisualElement))
            {
                // Clear gesture recognizers to break circular references
                if (station.VisualElement is Border border && border.GestureRecognizers.Count > 0)
                {
                    border.GestureRecognizers.Clear();
                }
                
                EnvironmentContainer.Children.Remove(station.VisualElement);
            }
        }
#endif
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        if (EnvironmentWrapper != null && EnvironmentContainer != null && HubButton != null 
            && ResourceListContainer != null && ResourceListPlaceholder != null && ResourceListWrapper != null)
        {
            var pageHeight = this.Height;
            var pageWidth = this.Width;
            
            if (pageHeight > 0 && pageWidth > 0)
            {
                // Update screen properties
                var screenProps = ScreenProperties.Instance;
                screenProps.UpdateScreenProperties(pageWidth, pageHeight);
                
                EnvironmentContainer.WidthRequest = ScreenProperties.ReferenceWidth;
                EnvironmentContainer.HeightRequest = ScreenProperties.ReferenceHeight;
                
                // Scale environment wrapper from center to maintain centering
                EnvironmentWrapper.AnchorX = 0.5;
                EnvironmentWrapper.AnchorY = 0.5;
                EnvironmentWrapper.Scale = screenProps.Scale;
                EnvironmentWrapper.WidthRequest = ScreenProperties.ReferenceWidth;
                EnvironmentWrapper.HeightRequest = ScreenProperties.ReferenceHeight;
                
                EnvironmentContainer.InputTransparent = false;
                EnvironmentContainer.BackgroundColor = Colors.Transparent;
                
                // Center offset: account for scale transform
                EnvironmentWrapper.TranslationX = screenProps.OffsetX;
                EnvironmentWrapper.TranslationY = screenProps.OffsetY;
                
                // Scale HubButton from left edge (matches environment scale)
                HubButton.AnchorX = 0;
                HubButton.AnchorY = 1;
                HubButton.Scale = screenProps.Scale;
                
                // Scale placeholder from right edge (maintains column width, prevents offset)
                ResourceListPlaceholder.AnchorX = 1;
                ResourceListPlaceholder.AnchorY = 0.5;
                ResourceListPlaceholder.Scale = screenProps.Scale;
                
                // Update station object positions from absolute coordinates
                UpdateStationPositions();
                
                UpdateFontSizes(screenProps.FontScale);
                UpdatePanelSize(screenProps.FontScale);
                
                // Update resource panel to refresh font sizes for dynamically created items
                UpdateResourcePanel();
            }
        }
#endif
    }
    
    private void UpdatePanelSize(double fontScale)
    {
        // Panel width - different for Android and Windows (same as GreenhousePage)
#if ANDROID
        const double baseWidth = 250.0;
#else
        const double baseWidth = 300.0;
#endif
        const double baseHeight = 500.0;
        const double baseMargin = 20.0;
        
        // Panel scales with fontScale (content sizing)
        ResourceListContainer.WidthRequest = baseWidth * fontScale;
        ResourceListContainer.HeightRequest = baseHeight * fontScale;
        ResourceListContainer.Margin = new Thickness(0, 0, baseMargin * fontScale, 0);
        
        // Update panel Y position - lower on Android (same as GreenhousePage)
#if ANDROID
        const double panelYPosition = 0.7; // Lower position on Android
#else
        const double panelYPosition = 0.5; // Center position on Windows
#endif
        
        if (ResourceListWrapper != null && ResourceListContainer != null)
        {
            AbsoluteLayout.SetLayoutBounds(ResourceListContainer, new Rect(1, panelYPosition, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
            AbsoluteLayout.SetLayoutFlags(ResourceListContainer, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.XProportional | Microsoft.Maui.Layouts.AbsoluteLayoutFlags.YProportional);
        }
        
        // Placeholder scales with buttonScale (layout sizing, maintains column width)
        // Placeholder always uses 300px to match HubButton width (for equal columns)
        ResourceListPlaceholder.WidthRequest = 300.0;
        ResourceListPlaceholder.HeightRequest = baseHeight;
        ResourceListPlaceholder.Margin = new Thickness(0, 0, baseMargin, 0);
        
        // Update selected resource panel (same width as main panel)
#if ANDROID
        const double selectedPanelWidth = 250.0;
#else
        const double selectedPanelWidth = 300.0;
#endif
        const double selectedPanelHeight = 160.0;
        
        if (SelectedResourcePanel != null)
        {
            SelectedResourcePanel.WidthRequest = selectedPanelWidth * fontScale;
            SelectedResourcePanel.HeightRequest = selectedPanelHeight * fontScale;
            SelectedResourcePanel.Margin = new Thickness(0, 0, baseMargin * fontScale, 0);
        }
    }
    
    private void UpdateFontSizes(double fontScale)
    {
        // Base font sizes (Windows 1920px = scale 1.0)
        const double baseTitleSize = 40.0;
        const double baseBodySize = 30.0;
        const double baseQtySize = 24.0;
        const double baseIconSize = 40.0;
        
        // Update DynamicResource bindings (auto-updates UI)
        Resources["ResourcePanelTitleSize"] = baseTitleSize * fontScale;
        Resources["ResourcePanelBodySize"] = baseBodySize * fontScale;
        Resources["ResourcePanelQtySize"] = baseQtySize * fontScale;
        Resources["ResourcePanelIconSize"] = baseIconSize * fontScale;
    }

    /// <summary>
    /// Creates station object UI elements and adds them to EnvironmentContainer
    /// </summary>
    private void CreateStationObjects()
    {
#if ANDROID || WINDOWS
        if (EnvironmentContainer == null)
            return;
        
        foreach (var station in _stationObjects)
        {
            var visualElement = station.CreateVisualElement();
            EnvironmentContainer.Children.Add(visualElement);
        }
#endif
    }
    
    /// <summary>
    /// Updates station positions using 1:1 coordinate system (container center: X=960, Y=540)
    /// </summary>
    private void UpdateStationPositions()
    {
#if ANDROID || WINDOWS
        if (EnvironmentContainer == null)
            return;
        
        const double containerCenterX = 960.0;
        const double containerCenterY = 540.0;
        
        foreach (var station in _stationObjects)
        {
            station.UpdatePosition(containerCenterX, containerCenterY);
        }
#endif
    }

    // Navigation
    private async void OnHubClicked(object sender, EventArgs e)
    {
        await NavigationService.NavigateWithFadeAsync("//HubPage");
    }

    // Panel controls
    private void OnResourceSlotClicked(object sender, EventArgs e)
    {
#if ANDROID || WINDOWS
        if (ResourceListContainer != null && BackgroundOverlay != null)
        {
            ResourceListContainer.IsVisible = true;
            ResourceListContainer.InputTransparent = false;
            BackgroundOverlay.IsVisible = true;
            BackgroundOverlay.InputTransparent = false;
            
            // Update selected resource panel visibility when opening panel
            UpdateSelectedResourcePanelVisibility();
        }
#endif
    }

    private void OnExtractClicked(object? sender, EventArgs e)
    {
        // TODO: Implement extract functionality
        System.Diagnostics.Debug.WriteLine("Extract button clicked");
    }

    private void OnBackgroundOverlayTapped(object sender, EventArgs e)
    {
#if ANDROID
        // Only close panel on tap for Android (Windows uses Esc key)
        CloseResourcePanel();
#endif
    }

    private void OnResourcePanelTapped(object sender, EventArgs e)
    {
        // Stop event propagation (prevents overlay tap from closing panel)
    }

    private void CloseResourcePanel()
    {
#if ANDROID || WINDOWS
        if (ResourceListContainer != null && BackgroundOverlay != null)
        {
            ResourceListContainer.IsVisible = false;
            ResourceListContainer.InputTransparent = true;
            BackgroundOverlay.IsVisible = false;
            BackgroundOverlay.InputTransparent = true;
            
            // Clear selection when closing panel
            _selectedResource = null;
            if (SelectedResourcePanel != null)
            {
                SelectedResourcePanel.IsVisible = false;
            }
            
            // Refresh panel to remove visual highlighting
            UpdateResourcePanel();
        }
#endif
    }
    
    /// <summary>
    /// Updates the ResourcePanel with all available resources from ResourceLibrary
    /// </summary>
    private void UpdateResourcePanel()
    {
#if ANDROID || WINDOWS
        if (ResourcesList == null)
        {
            System.Diagnostics.Debug.WriteLine("[LaboratoryPage] UpdateResourcePanel: ResourcesList is null");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine("[LaboratoryPage] UpdateResourcePanel: Updating resource panel");
        
        // Clear existing items
        ResourcesList.Children.Clear();
        
        try
        {
            var resources = ResourceLibrary.GetAllResources();
            foreach (var resource in resources)
            {
                var resourceItem = CreateResourceItem(resource);
                ResourcesList.Children.Add(resourceItem);
            }
            
            System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Updated ResourcePanel with {resources.Count()} resources");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Error updating ResourcePanel: {ex.Message}");
        }
#endif
    }
    
    /// <summary>
    /// Creates a UI element for a resource item
    /// Layout matches SeedsPanel/LiquidsPanel exactly
    /// </summary>
    private Border CreateResourceItem(ResourceData resource)
    {
        // Check if this resource is selected
        bool isSelected = _selectedResource?.Id == resource.Id;
        System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] CreateResourceItem: {resource.Name}, isSelected={isSelected}, _selectedResource={_selectedResource?.Name ?? "null"}");
        
        var border = new Border
        {
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#4CAF50"),
            BackgroundColor = Color.FromArgb("#0F1F0F"),
            Padding = new Thickness(10)
        };
        
        border.StrokeShape = new RoundRectangle { CornerRadius = 8 };
        
        if (isSelected)
        {
            border.Stroke = Color.FromArgb("#FFD700");
            border.StrokeThickness = 2;
            System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] CreateResourceItem: {resource.Name} marked as selected (gold border)");
        }
        
#if ANDROID
        // On Android, use Grid to position icon on left and quantity on right (same as SeedsPanel/LiquidsPanel)
        var contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };
        
        var iconLabel = new Label
        {
            Text = resource.Sprite,
            FontSize = (double)Resources["ResourcePanelIconSize"],
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Start
        };
        Grid.SetColumn(iconLabel, 0);
        contentGrid.Children.Add(iconLabel);
        
        var qtyLabel = new Label
        {
            Text = "0", // TODO: Get actual quantity from inventory
            FontSize = (double)Resources["ResourcePanelQtySize"],
            TextColor = Colors.White,
            Opacity = 0.7,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        };
        Grid.SetColumn(qtyLabel, 1);
        contentGrid.Children.Add(qtyLabel);
        
        border.Content = contentGrid;
#else
        // On Windows, show icon, name, and quantity (same as SeedsPanel/LiquidsPanel)
        var horizontalStack = new HorizontalStackLayout
        {
            Spacing = 10
        };
        
        var iconLabel = new Label
        {
            Text = resource.Sprite,
            FontSize = (double)Resources["ResourcePanelIconSize"],
            VerticalOptions = LayoutOptions.Center
        };
        var verticalStack = new VerticalStackLayout
        {
            Spacing = 3
        };
        
        var nameLabel = new Label
        {
            Text = resource.Name,
            FontSize = (double)Resources["ResourcePanelBodySize"],
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        };
        
        var qtyLabel = new Label
        {
            Text = "Qty: 0", // TODO: Get actual quantity from inventory
            FontSize = (double)Resources["ResourcePanelQtySize"],
            TextColor = Colors.White,
            Opacity = 0.7
        };
        
        verticalStack.Children.Add(nameLabel);
        verticalStack.Children.Add(qtyLabel);
        
        horizontalStack.Children.Add(iconLabel);
        horizontalStack.Children.Add(verticalStack);
        
        border.Content = horizontalStack;
#endif
        
        // Add tap gesture to select this resource
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (s, e) => OnResourceSelected(resource);
        border.GestureRecognizers.Add(tapGesture);
        
        return border;
    }
    
    /// <summary>
    /// Handles selection of a resource item
    /// </summary>
    private void OnResourceSelected(ResourceData resource)
    {
        System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] OnResourceSelected: {resource.Name}");
        
        _selectedResource = resource;
        UpdateSelectedResourcePanelVisibility();
        
        if (SelectedResourceName != null)
        {
            SelectedResourceName.Text = resource.Name;
        }
        
        // Refresh the resource panel to update selection highlighting
        UpdateResourcePanel();
        
        System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] Selected resource: {resource.Name} (ID: {resource.Id})");
    }
    
    /// <summary>
    /// Updates visibility of selected resource panel based on selection state
    /// </summary>
    private void UpdateSelectedResourcePanelVisibility()
    {
#if ANDROID
        if (SelectedResourcePanel == null)
        {
            System.Diagnostics.Debug.WriteLine("[LaboratoryPage] UpdateSelectedResourcePanelVisibility: SelectedResourcePanel is null");
            return;
        }
        
        bool shouldBeVisible = _selectedResource != null && ResourceListContainer != null && ResourceListContainer.IsVisible;
        SelectedResourcePanel.IsVisible = shouldBeVisible;
        
        System.Diagnostics.Debug.WriteLine($"[LaboratoryPage] UpdateSelectedResourcePanelVisibility: shouldBeVisible={shouldBeVisible}, _selectedResource={_selectedResource?.Name ?? "null"}, ResourceListContainer.IsVisible={ResourceListContainer?.IsVisible ?? false}, SelectedResourcePanel.IsVisible={SelectedResourcePanel.IsVisible}");
#elif WINDOWS
        // On Windows, always hide the selected panel (not needed, names are shown in main panel)
        if (SelectedResourcePanel != null)
        {
            SelectedResourcePanel.IsVisible = false;
        }
#endif
    }
}

