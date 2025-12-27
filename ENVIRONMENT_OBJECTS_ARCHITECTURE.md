# Environment Object System Architecture

## Overview

The environment object system uses a flexible architecture based on an abstract base class (`EnvObject`) and interfaces (`IInteractable`, `IAnimated`) to create extensible and modular game objects.

## Core Components

### 1. EnvObject (Abstract Base Class)

Base class for all environment objects (pots, furniture, decorations, etc.).

**Key Properties:**
- `Id` - Unique identifier
- `X, Y` - Center coordinates (1:1 pixel ratio)
  - GreenhousePage: X range -9600 to 9600, Y range -540 to 540
  - HubPage/LaboratoryPage: X range -960 to 960, Y range -540 to 540
- `Width, Height` - Object dimensions in pixels
- `VisualElement` - The MAUI View representing the object
- `BaseSprite` - Default icon/sprite (emoji or image path)

**Key Methods:**
- `CreateVisualElement()` - Abstract method to build the UI (must be implemented by derived classes)
- `UpdatePosition(double containerCenterX, double containerCenterY)` - Converts logical center coordinates to pixel positions for AbsoluteLayout
  - Formula: `leftEdgeX = containerCenterX + X - (Width / 2)`
  - Formula: `topEdgeY = containerCenterY - Y - (Height / 2)` (negative Y = below center)

### 2. IInteractable (Interface)

For objects that respond to user interaction (tap/click).

**Members:**
- `CanInteract` - Boolean indicating if interaction is currently allowed
- `OnInteract()` - Method called when the object is interacted with

### 3. IAnimated (Interface)

For objects with animation logic.

**Members:**
- `IsAnimating` - Boolean indicating if animation is currently running
- `StartAnimation()` - Starts the animation (async Task)
- `StopAnimation()` - Stops the animation

## Object Types

### PotObject (EnvObject + IInteractable)

Standard pot for growing plants.

**Features:**
- Clickable/tappable (implements `IInteractable`)
- Fires `Clicked` event (`EventHandler<TappedEventArgs>?`)
- Executes `InteractAction` delegate (`Action?`)
- Can be enabled/disabled via `CanInteract` property
- Uses DynamicResource for font sizes (ButtonIconSize, ButtonPlaceholderSize, ButtonLabelSize)

**Usage:**
```csharp
var pot = new PotObject(1, 9000, 0); // potNumber, x, y
pot.InteractAction = () => OpenPlantDialog(1);
pot.Clicked += (s, e) => Debug.WriteLine("Pot clicked!");
pot.CanInteract = true; // Enable/disable interaction
var visual = pot.CreateVisualElement();
```

### StationObject (EnvObject + IInteractable)

Interactive station elements for HubPage and LaboratoryPage (market, quest console, statistics, resource slots, etc.).

**Features:**
- Clickable/tappable interface elements (implements `IInteractable`)
- Customizable background color (`BackgroundColor` property)
- Customizable separator color (`SeparatorColor` property, defaults to `#4CAF50`)
- Display name and icon
- Fires `Clicked` event (`EventHandler?`)
- Executes `InteractAction` delegate (`Action?`)
- Can be enabled/disabled via `CanInteract` property
- Uses DynamicResource for font sizes (ButtonIconSize, ButtonPlaceholderSize, ButtonLabelSize)

**Usage:**
```csharp
var market = new StationObject(
    id: "Market",
    displayName: "Market",
    x: -480, y: 0,
    width: 300, height: 300,
    sprite: "📦",
    backgroundColor: Color.FromArgb("#4A4A4A"),
    separatorColor: Color.FromArgb("#4CAF50") // Optional, defaults to #4CAF50
);
market.InteractAction = () => OpenMarketPanel();
market.Clicked += (s, e) => Debug.WriteLine("Market clicked!");
market.CanInteract = true; // Enable/disable interaction
var visual = market.CreateVisualElement();
```

### AnimatedPotObject (EnvObject + IInteractable + IAnimated)

Pot with animation (pulse/glow effect).

**Features:**
- All features of PotObject (implements `IInteractable`)
- Pulse animation (fade in/out loop) via `IAnimated` interface
- Can start/stop animation dynamically
- Uses `CancellationTokenSource` for clean cancellation
- Animation targets border opacity (fades between 0.3 and 0.8)
- Resets opacity to 0.5 when animation stops

**Usage:**
```csharp
var pot = new AnimatedPotObject(1, 9000, 0); // potNumber, x, y
pot.InteractAction = () => WaterPlant(1);
pot.Clicked += (s, e) => Debug.WriteLine("Animated pot clicked!");
pot.CanInteract = true;

// Create visual element first (required for animation)
var visual = pot.CreateVisualElement();
container.Children.Add(visual);

// Start animation (must be called after visual element is created)
await pot.StartAnimation(); // Start pulsing

// ... later ...
pot.StopAnimation(); // Stop pulsing
```

### FurnitureObject (EnvObject only)

Simple decorative object without interaction or animation.

**Features:**
- Display-only (no interaction, does not implement interfaces)
- Customizable background color (`BackgroundColor` property, defaults to `#3E2723`)
- Optional display name label (only shown if `DisplayName` is not empty)
- Furniture type for categorization (`FurnitureType` property)
- Icon size scales proportionally to object width (40% of width)

**Usage:**
```csharp
var table = new FurnitureObject(
    id: "Table1",
    displayName: "Work Bench",
    furnitureType: "Table",
    x: 8000, y: -200,
    width: 400, height: 200,
    sprite: "🪑",
    backgroundColor: Color.FromArgb("#6D4C41")
);
var visual = table.CreateVisualElement();
```

## Architecture Benefits

### 1. Modularity
- Objects implement only the interfaces they need
- Clean separation of concerns (visual, interaction, animation)

### 2. Extensibility
- Easy to add new object types by inheriting from `EnvObject`
- Mix and match interfaces as needed
- Add new interfaces (e.g., `IGrowable`) without modifying existing code

### 3. Consistency
- All objects share the same coordinate system
- Unified position update mechanism
- Consistent creation and lifecycle management

### 4. Type Safety
- Interfaces allow type checking at compile time
- Can use pattern matching to handle different object types

## Creating New Object Types

### Example: Interactive Furniture (EnvObject + IInteractable)

```csharp
public class InteractiveFurnitureObject : EnvObject, IInteractable
{
    public bool CanInteract { get; set; } = true;
    public Action? InteractAction { get; set; }
    
    public InteractiveFurnitureObject(string id, int x, int y, string sprite)
        : base(id, x, y, 200, 200, sprite)
    {
    }
    
    public void OnInteract()
    {
        if (CanInteract)
            InteractAction?.Invoke();
    }
    
    public override View CreateVisualElement()
    {
        var border = new Border
        {
            StrokeThickness = 2,
            Stroke = Colors.White,
            BackgroundColor = Colors.Transparent,
            HeightRequest = Height,
            WidthRequest = Width
        };
        
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (s, e) => OnInteract();
        border.GestureRecognizers.Add(tapGesture);
        
        var icon = new Label
        {
            Text = BaseSprite,
            FontSize = 80,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        
        border.Content = icon;
        VisualElement = border;
        return border;
    }
}
```

### Example: Animated Decoration (EnvObject + IAnimated)

```csharp
public class AnimatedDecorationObject : EnvObject, IAnimated
{
    public bool IsAnimating { get; private set; }
    private CancellationTokenSource? _cts;
    private Label? _iconLabel;
    
    public AnimatedDecorationObject(string id, int x, int y, string sprite)
        : base(id, x, y, 100, 100, sprite)
    {
    }
    
    public async Task StartAnimation()
    {
        if (IsAnimating || _iconLabel == null) return;
        
        IsAnimating = true;
        _cts = new CancellationTokenSource();
        
        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                await _iconLabel.RotateTo(360, 2000);
                _iconLabel.Rotation = 0;
            }
        }
        catch (TaskCanceledException) { }
        finally
        {
            IsAnimating = false;
        }
    }
    
    public void StopAnimation()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        IsAnimating = false;
    }
    
    public override View CreateVisualElement()
    {
        _iconLabel = new Label
        {
            Text = BaseSprite,
            FontSize = 60,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        
        VisualElement = _iconLabel;
        return _iconLabel;
    }
}
```

## Integration with Pages

### Adding Objects to a Page

```csharp
// In HubPage.xaml.cs - Using StationObject
private readonly List<StationObject> _stationObjects = new()
{
    new StationObject("Market", "Market", -480, 0, 300, 300, "📦", Color.FromArgb("#4A4A4A")),
    new StationObject("QuestConsole", "Quest Console", 0, 162, 300, 300, "📡", Color.FromArgb("#1E3A5F")),
    new StationObject("Statistics", "Statistics", 480, 0, 300, 300, "📊", Color.FromArgb("#2C2C2C"))
};

private void OnPageLoaded(object? sender, EventArgs e)
{
    CreateStationObjects();
    UpdateStationPositions();
}

private void CreateStationObjects()
{
    foreach (var station in _stationObjects)
    {
        var visual = station.CreateVisualElement();
        EnvironmentContainer.Children.Add(visual);
        
        // Set up interaction
        station.Clicked += (s, e) => OnStationClicked(station);
        station.InteractAction = () => HandleStationInteraction(station.Id);
        station.CanInteract = true; // Enable interaction
    }
}

private void UpdateStationPositions()
{
    // HubPage/LaboratoryPage: container center is at (960, 540)
    const double containerCenterX = 960.0;
    const double containerCenterY = 540.0;
    
    foreach (var station in _stationObjects)
    {
        station.UpdatePosition(containerCenterX, containerCenterY);
    }
}

// For GreenhousePage with PotObject:
private readonly List<PotObject> _pots = new()
{
    new PotObject(1, 9400, 0),
    new PotObject(2, 9000, 0),
    new PotObject(3, 8600, 0)
};

private void CreatePotElements()
{
    foreach (var pot in _pots)
    {
        var visual = pot.CreateVisualElement();
        ContentContainer.Children.Add(visual);
        
        pot.InteractAction = () => HandlePotInteraction(pot.PotNumber);
        pot.Clicked += (s, e) => OnPotClicked(pot);
    }
}

private void UpdatePotPositions()
{
    // GreenhousePage: ContentContainer center is at (9600, 540)
    const double containerCenterX = 9600.0;
    const double containerCenterY = 540.0;
    
    foreach (var pot in _pots)
    {
        pot.UpdatePosition(containerCenterX, containerCenterY);
    }
}
```

## Future Expansion Ideas

### New Interfaces
- `IGrowable` - Objects that change over time (plants)

### New Object Types
- `PlantObject` - Growing plants with stages
- `ToolObject` - Interactive tools (watering can, pruners)

## Best Practices

1. **Keep interfaces focused** - Each interface should have a single responsibility
2. **Use composition over inheritance** - Prefer interfaces over deep inheritance hierarchies
3. **Handle cleanup** - Always stop animations and dispose resources in `StopAnimation()`
4. **Null checks** - Always check `VisualElement != null` before using it
5. **Async safety** - Use `CancellationTokenSource` for long-running animations, dispose properly
6. **Coordinate consistency** - Always use the same coordinate system (center-based, 1:1 pixel ratio)
7. **Container centers** - Use correct container center coordinates:
   - GreenhousePage ContentContainer: (9600, 540)
   - HubPage/LaboratoryPage EnvironmentContainer: (960, 540)
8. **Create before animate** - Always create visual element before starting animation (for `IAnimated` objects)
9. **Dynamic resources** - Use `SetDynamicResource()` for font sizes to support automatic scaling
10. **Event handlers** - Set up `Clicked` events and `InteractAction` delegates after creating visual elements

