# Outgrowth - Project Context Document

**Last Updated**: December 26, 2025

## 10. Automatic Scaling Systems

### 10.1 Font Size Scaling

Font sizes automatically adjust based on screen dimensions:
- **Base Reference**: Windows 1920px width = scale 1.0
- **Scale Calculation**: `fontScale = pageWidth / 1920.0`
- **Implementation**: Resources updated dynamically in `OnPageSizeChanged` handler
- **Resources**: 
  - `ResourcePanelTitleSize` (base: 40)
  - `ResourcePanelBodySize` (base: 30)
  - `ResourcePanelQtySize` (base: 24)
  - `ResourcePanelIconSize` (base: 40)

### 10.2 Panel Size Scaling

Panel dimensions automatically adjust based on screen dimensions:
- **Base Reference**: Windows 1920x1080
- **Content Scaling**: Panel scales with `fontScale` (based on screen width)
- **Layout Scaling**: Placeholder scales with `buttonScale` (environment scale)
- **Column Stability**: Placeholder maintains consistent column width to prevent layout shifts

### 10.3 Implementation Pattern

```csharp
// Calculate font scale
const double windowsBaseWidth = 1920.0;
var fontScale = pageWidth / windowsBaseWidth;

// Update resources dynamically
Resources["ResourcePanelTitleSize"] = baseTitleSize * fontScale;

// Update panel size
ResourceListContainer.WidthRequest = baseWidth * fontScale;
ResourceListPlaceholder.WidthRequest = baseWidth; // Scales with buttonScale
```

## 9. Page Implementation Details

### 9.1 HubPage Implementation

#### 9.1.1 Layout Architecture

The HubPage uses a 3-column Grid structure to ensure proper click handling and visual consistency:

```
Grid (ColumnDefinitions="Auto,*,Auto")
├── Column 0: Left Gutter (Greenhouse Button)
│   └── AbsoluteLayout (ZIndex="1000")
│       └── VerticalStackLayout (GreenhouseButton)
├── Column 1: Center Environment
│   └── ContentView (EnvironmentWrapper)
│       └── AbsoluteLayout (EnvironmentContainer, ZIndex="100")
│           ├── Market Button (0.25, 0.5)
│           ├── Quest Console (0.5, 0.3)
│           └── Statistics (0.75, 0.5)
└── Column 2: Right Gutter (Laboratory Button)
    └── AbsoluteLayout (ZIndex="1000")
        └── VerticalStackLayout (LaboratoryButton)
```

#### 9.1.2 Scale Transform System

To ensure identical appearance on Android and Windows:

1. **Reference Size**: Environment container uses fixed size 1920x1080 (design size)
2. **Scale Calculation**: `scale = targetWidth / 1920.0`
3. **Application**: Scale transform applied to:
   - EnvironmentWrapper (scales entire environment)
   - GreenhouseButton and LaboratoryButton (edge navigation)
   - Overlay panel borders (MarketPanelBorder, QuestPanelBorder, StatsPanelBorder)
4. **Anchor Points**: 
   - EnvironmentWrapper: (0.5, 0.5) - scales from center
   - Edge buttons: (0, 0.5) for left, (1, 0.5) for right - scales from edge

#### 9.1.3 Font Size Resources

Font sizes are defined in `Resources/Styles/Styles.xaml` using OnPlatform markup:

```xml
<OnPlatform x:Key="ButtonIconSize" x:TypeArguments="x:Double">
    <On Platform="Android" Value="140" />
    <On Platform="WinUI" Value="120" />
</OnPlatform>
```

All font sizes in HubPage use DynamicResource:
- `{DynamicResource ButtonIconSize}` - For emoji icons
- `{DynamicResource ButtonLabelSize}` - For button labels
- `{DynamicResource ButtonPlaceholderSize}` - For placeholder text
- `{DynamicResource PanelTitleSize}` - For panel titles
- `{DynamicResource PanelBodySize}` - For panel body text
- `{DynamicResource PanelButtonSize}` - For panel close buttons

#### 9.1.4 Android Fullscreen Implementation

Immersive mode is enabled in `Platforms/Android/MainActivity.cs`:

```csharp
// Hide system bars (status bar and navigation bar)
insetsController.Hide(WindowInsetsCompat.Type.SystemBars());
// Enable immersive sticky mode
insetsController.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
```

This provides a distraction-free fullscreen experience on Android devices.

#### 9.1.5 Click Handling Solution

The Grid column structure combined with ZIndex ensures proper click handling:

- **Edge buttons** are in separate columns (0 and 2), preventing overlap
- **ZIndex="1000"** on edge button containers ensures they receive clicks first
- **ZIndex="100"** on environment container keeps it below edge buttons
- **Environment buttons** (Market, Quest, Statistics) work within their container

This solution fixed click handling issues on Windows while maintaining functionality on Android.

#### 9.1.6 Panel Interaction System

All panels (Market, Quest, Statistics) use the same interaction pattern:
- **No close buttons**: Panels close by clicking outside (on transparent background)
- **Background overlay**: Transparent Grid with tap gesture (ZIndex="500")
- **Panel border**: Tap gesture stops event propagation (prevents closing when clicking panel)
- **Auto-close**: Panels close when navigating away (Disappearing event)
- **Visibility**: Uses `IsVisible`

### 9.3 GreenhousePage Implementation

#### 9.3.1 Layout Architecture

The GreenhousePage uses the same 3-column Grid structure as HubPage and LaboratoryPage:

```
Grid (ColumnDefinitions="Auto,*,Auto")
├── Column 0: Left Gutter (Placeholder + Panels)
│   ├── Grid (LeftGutterPlaceholder - invisible, maintains column width)
│   ├── AbsoluteLayout (LiquidsPanelWrapper, ZIndex="1000")
│   │   └── Grid (LiquidsPanel - scrollable liquid resources)
│   └── AbsoluteLayout (SeedsPanelWrapper, ZIndex="1000")
│       └── Grid (SeedsPanel - scrollable seed resources)
├── Column 1: Center Environment
│   └── ContentView (EnvironmentWrapper, ZIndex="100")
│       └── AbsoluteLayout (EnvironmentContainer)
│           └── AbsoluteLayout (ContentContainer - moves as whole unit)
│               └── Pots created dynamically from PotObject instances
│                   ├── Pot 1 (X: 9400, Y: 0)
│                   ├── Pot 2 (X: 9000, Y: 0)
│                   ├── Pot 3 (X: 8600, Y: 0)
│                   ├── Pot 4 (X: 8200, Y: 0)
│                   └── Pot 5 (X: 7800, Y: 0)
└── Column 2: Right Gutter (Hub Button)
    └── AbsoluteLayout (ZIndex="1000")
        └── VerticalStackLayout (HubButton)

Bottom (Grid.ColumnSpan="3", ZIndex="1500"):
├── Grid (BottomPanelsContainer, ColumnDefinitions="*,Auto,Auto,*")
│   ├── Column 1: ToolsPanel (always visible)
│   │   └── Border (3 icon-only buttons: Liquids, Seeds, Cancel)
│   └── Column 2: MovePanel (Android only, left/right arrow buttons)
```

**Note**: Pots are not hardcoded in XAML. They are created dynamically in code-behind using `PotObject` instances and added to `ContentContainer` on page load via `CreatePotElements()` method.

#### 9.3.2 ContentContainer System

The ContentContainer is a key architectural feature that provides a generic, scalable solution for managing interactive items:

- **Purpose**: Single AbsoluteLayout container that holds all pots and future furniture items
- **Dimensions**: 19200px × 1080px
- **Movement**: Entire container moves as a unit using `TranslationX` for navigation
- **Dynamic Object System**: Uses `EnvObject`-based system for object management
  - All objects (pots, future furniture) inherit from `EnvObject` base class
  - Objects are created dynamically via `CreateVisualElement()` method
  - Objects are added to `ContentContainer` programmatically, not in XAML
  - Position updates handled by `EnvObject.UpdatePosition()` method
- **Benefits**: 
  - Clean, generic implementation - no hardcoded XAML elements
  - Easy to add new furniture items - create new class inheriting from `EnvObject`
  - Consistent coordinate system and position calculation across all objects
  - Centralized logic for position management
- **Coordinate System**: 
  - Absolute coordinates defined in object instances (e.g., `PotObject` instances in `_pots` list)
  - X Range: -9600 to 9600 logical units (container width: 19200px, 1:1 ratio)
  - Y Range: -540 to 540 logical units (container height: 1080px, 1:1 ratio)
  - **1:1 Pixel-to-Logical-Unit Ratio**: 1 logical unit = 1 pixel (simplified system)
  - Coordinates automatically converted to pixel positions via `EnvObject.UpdatePosition()`
- **Item Positions**: Stored in object properties (e.g., `PotObject.X`, `PotObject.Y`)
- **Current Item Tracking**: `_currentItemIndex` tracks which item is currently centered
- **Position Management**: Visual elements created dynamically and positioned programmatically on load and size change

#### 9.3.3 Pots Navigation System

The GreenhousePage implements a navigation system for browsing pots using the `PotObject` system:

- **Pot Count**: 5 pots created dynamically from `PotObject` instances
- **PotObject System**: 
  - Pots are defined as `PotObject` instances in `_pots` list in code-behind
  - Each pot is created via `PotObject.CreateVisualElement()` method
  - Pots are added to `ContentContainer` dynamically in `CreatePotElements()` method
  - No pots are hardcoded in XAML - all created programmatically
- **Absolute Coordinates**: Pots use absolute coordinate system with 1:1 pixel-to-logical-unit ratio:
  - Pot 1: X=9400, Y=0 (rightmost, centered vertically)
  - Pot 2: X=9000, Y=0 (second from right - starting position)
  - Pot 3: X=8600, Y=0 (center)
  - Pot 4: X=8200, Y=0 (second from left)
  - Pot 5: X=7800, Y=0 (leftmost, centered vertically)
- **Coordinate System**: 
  - X Range: -9600 to 9600 (ContentContainer width: 19200px, 1:1 ratio)
  - Y Range: -540 to 540 (ContentContainer height: 1080px, 1:1 ratio)
  - **1:1 Pixel-to-Logical-Unit Ratio**: 1 logical unit = 1 pixel (simplified coordinate system)
  - Coordinates represent CENTER of pots, automatically converted to pixel positions via `PotObject.UpdatePosition()`
- **Position Updates**: `UpdatePotPositions()` calls `pot.UpdatePosition()` for each pot in the list
- **Navigation Controls**: 
  - Left/Right arrow buttons in MovePanel (Android only)
  - Left arrow: Moves to next pot on the left (increases index)
  - Right arrow: Moves to next pot on the right (decreases index)
  - **Navigation Restricted**: Only Pot2, Pot3, Pot4 are accessible (indices 1-3)
  - Pot1 and Pot5 are not accessible via navigation (indices 0 and 4)
- **Centering Logic**: `UpdateContentPosition()` calculates offset to center selected pot
  - Target center: X = 960px (screen center in EnvironmentContainer)
  - Uses `TranslationX` to shift entire ContentContainer
  - Calculates offset from pot's center position to screen center

#### 9.3.4 Scale Transform System

Uses the same scale transform system as HubPage and LaboratoryPage:
- **Reference Size**: 1920x1080 (Windows base)
- **Environment Scale**: Calculated to fit screen, applied to EnvironmentWrapper
- **HubButton Scale**: Scales with environment scale
- **Placeholder Scale**: LeftGutterPlaceholder scales with button scale to maintain column width

#### 9.3.5 Bottom Panels System

The GreenhousePage has two bottom panels centered together:

**ToolsPanel (Always Visible)**:
- **Position**: Bottom center (Grid.Column="1")
- **Content**: 3 icon-only buttons in HorizontalStackLayout
  - Liquids Button (💧): Opens LiquidsPanel, closes SeedsPanel
  - Seeds Button (🌱): Opens SeedsPanel, closes LiquidsPanel
  - Cancel Button (❌): Closes all panels
- **Scaling**: Buttons scale automatically based on panel height
  - Button size = Panel height - (2 × panel padding)
  - Icon size = 50% of button size
  - Button padding = 15% of button size
- **Base Dimensions**: 600x150 (Windows 1920px base)

**MovePanel (Android Only)**:
- **Position**: Right of ToolsPanel (Grid.Column="2")
- **Visibility**: Set to visible in constructor using `#if ANDROID`
- **Content**: 2 arrow buttons in HorizontalStackLayout
  - Left Arrow Button (⬅️): Navigate to left pot
  - Right Arrow Button (➡️): Navigate to right pot
- **Scaling**: Same automatic scaling as ToolsPanel buttons
- **Base Dimensions**: 300x150 (Windows 1920px base)

**Layout Structure**:
- Grid with ColumnDefinitions="*,Auto,Auto,*" to center both panels together
- ColumnSpacing="20" provides spacing between panels
- Both panels scale with `fontScale` (based on screen width)

#### 9.3.6 Side Panels System

**LiquidsPanel and SeedsPanel**:
- **Position**: Left gutter (Grid.Column="0"), overlapping
- **Mutually Exclusive**: Only one panel visible at a time
- **LiquidsPanel Content**: 
  - Water (💧)
  - Fertilizer (🧪)
  - Growth Serum (⚗️)
- **SeedsPanel Content**:
  - Grass Seed (🌾)
  - Flower Seed (🌸)
  - Tree Seed (🌳)
- **Scrolling**: Both panels use ScrollView for content
- **Font Resources**: Use DynamicResource (ResourcePanelTitleSize, ResourcePanelBodySize, ResourcePanelQtySize, ResourcePanelIconSize)
- **Close Behavior**: 
  - Cancel button closes all panels
  - Background overlay closes panels
  - Auto-closes when navigating away
- **Scaling**: Panel dimensions scale with `fontScale`, fonts scale automatically

#### 9.3.7 Automatic Scaling Systems

**Button Scaling** (ToolsPanel and MovePanel):
- **Base Dimensions**: Panel height 150px, button size calculated from panel height
- **Calculation**: `buttonSize = panelHeight - (2 × panelPadding)`
- **Icon Scaling**: Icon font size = 50% of button size
- **Padding Scaling**: Button padding = 15% of button size
- **Implementation**: `UpdateToolsPanelSize()` and `UpdateMovePanelSize()` methods

**Panel Scaling**:
- **Font Scale**: Based on screen width (Windows 1920px = scale 1.0)
- **Panel Dimensions**: Scale with `fontScale`
- **Content Sizing**: All content scales proportionally

### 9.2 LaboratoryPage Implementation

#### 9.2.1 Layout Architecture

The LaboratoryPage uses the same 3-column Grid structure as HubPage:

```
Grid (ColumnDefinitions="Auto,*,Auto")
├── Column 0: Left Gutter (Hub Button)
│   └── AbsoluteLayout (ZIndex="1000")
│       └── VerticalStackLayout (HubButton)
├── Column 1: Center Environment
│   └── ContentView (EnvironmentWrapper, ZIndex="100")
│       └── AbsoluteLayout (EnvironmentContainer)
│           ├── ResourceSlotButton (0.5, 0.35)
│           └── ExtractButton (0.5, 0.65)
└── Column 2: Right Gutter (Resource List Panel)
    └── AbsoluteLayout (ResourceListWrapper, ZIndex="1000")
        ├── Grid (ResourceListPlaceholder - invisible, maintains column width)
        └── Grid (ResourceListContainer - scrollable resource list)
```

#### 9.2.2 Scale Transform System

Uses the same scale transform system as HubPage:
- **Reference Size**: 1920x1080 (Windows base)
- **Environment Scale**: Calculated to fit screen, applied to EnvironmentWrapper
- **Button Scale**: HubButton scales with environment scale
- **Placeholder Scale**: ResourceListPlaceholder scales with button scale to maintain column width

#### 9.2.3 Automatic Font Sizing System

Font sizes automatically scale based on screen width:
- **Base Reference**: Windows 1920px width = scale 1.0
- **Scale Calculation**: `fontScale = pageWidth / 1920.0`
- **Dynamic Resources**: Font sizes updated in code-behind using Resources dictionary
- **Resources**: `ResourcePanelTitleSize`, `ResourcePanelBodySize`, `ResourcePanelQtySize`, `ResourcePanelIconSize`
- **Base Values**: Title=40, Body=30, Qty=24, Icon=40 (Windows base)

#### 9.2.4 Automatic Panel Sizing System

Panel dimensions automatically scale based on screen dimensions:
- **Base Dimensions**: 300x500 (matches HubButton width for equal columns)
- **Font Scale**: Panel content scales with `fontScale` (based on screen width)
- **Button Scale**: Placeholder scales with `buttonScale` (environment scale) to maintain layout
- **Column Width**: Placeholder ensures consistent column width, preventing environment offset

#### 9.2.5 Resource Panel Features

- **Scrollable**: Grid layout with ScrollView for resource items
- **Resource Items**: Grass, Lum, Coal (with icons and quantity displays)
- **Close Behavior**: Closes on outside click (transparent background overlay)
- **Auto-close**: Closes when navigating away
- **No close button**: Cleaner UI without close button
- **Placeholder System**: Invisible Grid maintains column width when panel is hidden

#### 9.2.6 Panel Interaction System

- **Background Overlay**: Transparent Grid (ZIndex="500") catches outside clicks
- **Panel Border**: Tap gesture stops event propagation
- **Visibility**: Uses `IsVisible` for proper showing/hiding
- **Layout Stability**: Placeholder Grid maintains column width, preventing environment offset

---

## 1. Project Overview

### 1.1 Concept
**Outgrowth** As the lone occupant of a remote research station, your mission is to engineer and cultivate a resilient ecosystem of fictional plants capable of surviving the harsh vacuum of deep space. Through selective breeding and resource management, you must develop a self-sustaining "Green Ark" to terraform the void

### 1.2 Core Gameplay Loop
1. **Hub Management**: Central command for all station operations
2. **Cultivation**: Growing, watering, harvesting plants in the Greenhouse
3. **Research & Breeding**: Laboratory work on plant genetics and modifications
4. **Trading & Expeditions**: Communication with other space platforms

### 1.3 Unique Selling Points
- Solo experience focused on calm, methodical gameplay
- Scientific approach to plant breeding
- Space station atmosphere with ambient sounds and visual effects

---

## 2. Technical Requirements

### 2.1 Technical Constraints
- No .NET MAUI Community Toolkit allowed
- Only standard .NET MAUI libraries
- Must compile and run on both Windows and Android
- No obsolete methods or controls (use Border instead of Frame)

### 2.2 Platform-Specific Requirements
- **Android**:
  - Orientation: SensorLandscape (allows rotation between landscape orientations, blocks portrait)
  - Responsive design with smaller fonts and spacing
  - 16:9 aspect ratio environment
- **Windows**:
  - Window: Non-resizable (windowed fullscreen)
  - Window size controlled via settings (future feature)
  - 16:9 aspect ratio environment

---

## 3. Current Project Structure

### 3.1 Solution Structure
```
Outgrowth/
├── Outgrowth.sln                    # Solution file
├── README.md                        # Project readme
├── PROJECT_CONTEXT.md              # This file
└── Outgrowth/                      # Main project folder
    ├── Outgrowth.csproj            # Project configuration
    ├── MauiProgram.cs              # App initialization
    ├── App.xaml / App.xaml.cs      # Application entry point
    ├── AppShell.xaml / AppShell.xaml.cs  # Shell navigation
    ├── MainPage.xaml / MainPage.xaml.cs  # Main menu page
    ├── GlobalXmlns.cs              # Global XAML namespace definitions
    ├── Views/                       # XAML pages
    │   ├── HubPage.xaml / .cs           # Space station hub (command center)
    │   ├── GreenhousePage.xaml / .cs    # Plant cultivation area
    │   └── LaboratoryPage.xaml / .cs    # Research & breeding facility
    ├── ViewModels/                  # MVVM ViewModels
    │   ├── BaseViewModel.cs             # Base class with INotifyPropertyChanged
    │   ├── MainMenuViewModel.cs         # Main menu logic
    │   ├── HubViewModel.cs              # Hub page logic
    │   ├── GreenhouseViewModel.cs       # Greenhouse logic
    │   └── LaboratoryViewModel.cs       # Laboratory logic
    ├── Models/                      # Data models and environment objects
    │   ├── EnvObject.cs                 # Base class for environment objects
    │   ├── PotObject.cs                 # Pot object implementation
    │   └── FurnitureObject.cs           # Furniture object implementation
    ├── Platforms/                   # Platform-specific code
    │   ├── Android/
    │   ├── iOS/
    │   ├── MacCatalyst/
    │   ├── Tizen/
    │   └── Windows/
    ├── Properties/
    │   └── launchSettings.json
    └── Resources/                   # App resources
        ├── AppIcon/                 # App icons
        ├── Fonts/                   # Custom fonts
        ├── Images/                  # Image assets
        ├── Raw/                     # Raw files
        ├── Splash/                  # Splash screen
        └── Styles/                  # XAML styles
```

### 3.2 Key Configuration Files

#### Outgrowth.csproj
- Target Framework: `net9.0` for Android, iOS, MacCatalyst, Windows
- ApplicationId: `com.companyname.outgrowth`
- Version: 1.0
- References: Microsoft.Maui.Controls, Microsoft.Extensions.Logging.Debug

#### AppShell.xaml
- Navigation structure using Shell
- Routes defined for all pages:
  - MainPage: Main menu / game launcher
  - HubPage: Space station command center
  - GreenhousePage: Plant cultivation area
  - LaboratoryPage: Research & breeding facility
- FlyoutBehavior set to Disabled for immersive experience

#### Platform-Specific Configuration
- **Android (MainActivity.cs)**: 
  - `ScreenOrientation = ScreenOrientation.SensorLandscape`
  - Immersive fullscreen mode (hides status bar and navigation bar)
  - Sticky immersive mode (auto-hides after user swipe)
- **Windows (App.xaml.cs)**: Window size set to fill screen, non-resizable
- **Windows (App.xaml.cs - Platform)**: OverlappedPresenter with `IsResizable = false`

---

## 5. Project Features

### 5.1 Implemented Features

#### Navigation System
- **MainPage (Main Menu)**: Game launcher with Start New Game, Continue, Settings, and Exit options
  - Responsive design with platform-specific font sizes and spacing
- **HubPage**: Central command center with interactive environment
  - **Layout Structure**: Grid with 3 columns
    - Column 0 (Left Gutter): Greenhouse navigation button (ZIndex="1000")
    - Column 1 (Center): EnvironmentWrapper with 16:9 environment container (ZIndex="100")
    - Column 2 (Right Gutter): Laboratory navigation button (ZIndex="1000")
    - Overlay Panels: Span all columns (Grid.ColumnSpan="3")
  - **Interactive Elements**:
    - **Edge Navigation** (in separate Grid columns):
      - Greenhouse (left gutter, navigation to GreenhousePage)
      - Laboratory (right gutter, navigation to LaboratoryPage)
    - **Environment Elements** (positioned with AbsoluteLayout in center column using absolute coordinates):
      - Market/Package (X: -50, Y: 0 - left of center, opens Market panel)
      - Quest Console (X: 0, Y: 30 - center, above center, opens Quest panel)
      - Statistics Blackboard (X: 50, Y: 0 - right of center, opens Statistics panel)
      - **Absolute Coordinate System**: X Range: -100 to 100, Y Range: -100 to 100 (container: 1920px × 1080px)
      - **Position Updates**: Coordinates defined in code-behind, positions set programmatically via `UpdateElementPositions()`
  - **16:9 Aspect Ratio**: Environment container uses reference size (1920x1080) with scale transform
  - **Cross-Platform Consistency**: Scale transform system ensures identical appearance on Android and Windows
  - **Font Size System**: Uses DynamicResource references defined in Styles.xaml
  - **Button Sizes**: 300x300 for interactive buttons, 1200x800 for overlay panels
  - **ZIndex Management**: Side buttons (1000) above environment (100) for proper click handling
- **GreenhousePage**: Complete plant cultivation interface
  - **Layout Structure**: Same 3-column Grid as HubPage and LaboratoryPage
    - Column 0 (Left Gutter): LeftGutterPlaceholder + LiquidsPanel/SeedsPanel (mutually exclusive, ZIndex="1000")
    - Column 1 (Center): EnvironmentWrapper with 16:9 environment container (ZIndex="100")
      - ContentContainer: AbsoluteLayout containing all pots and future furniture (moves as whole unit)
      - Container dimensions: 19200px × 1080px (19.2 × 1000 for X, 10.8 × 100 for Y)
      - 5 pots positioned using absolute coordinates defined in code-behind (X: 600, 300, 0, -300, -600; Y: 0 for all)
    - Column 2 (Right Gutter): Hub navigation button (ZIndex="1000")
  - **Absolute Coordinate System**: 
    - X Range: -1000 to 1000 logical units (container width: 19200px)
    - Y Range: -100 to 100 logical units (container height: 1080px)
    - Coordinates defined only in code-behind, automatically converted to pixel positions
  - **ContentContainer System**: Generic container for all interactive items (pots, future furniture)
    - Moves as a whole unit using TranslationX for navigation
    - Generic implementation - easy to add new furniture items by adding coordinates to array
    - Positions set programmatically via `UpdatePotPositions()` method
  - **Pots Navigation**: 
    - Left/Right arrow buttons (Android only) navigate between pots
    - Selected pot automatically centers on screen using calculated TranslationX offset
    - Initial state: Pot 1 (rightmost) centered on page load
  - **Bottom Panels**:
    - ToolsPanel: Always visible, bottom center, 3 icon-only buttons (Liquids, Seeds, Cancel)
    - MovePanel: Android-only, right of ToolsPanel, 2 arrow buttons (Left, Right)
    - Both panels centered together using Grid layout
  - **Side Panels** (Left Gutter):
    - LiquidsPanel: Water, Fertilizer, Growth Serum (scrollable)
    - SeedsPanel: Grass Seed, Flower Seed, Tree Seed (scrollable)
    - Mutually exclusive - only one visible at a time
  - **Automatic Scaling**:
    - Buttons scale automatically based on panel height
    - Panels and fonts scale based on screen dimensions (Windows 1920px base)
  - **Back navigation to Hub**
- **LaboratoryPage**: Research and breeding facility
  - **Layout Structure**: Same 3-column Grid as HubPage
    - Column 0 (Left Gutter): Hub navigation button (ZIndex="1000")
    - Column 1 (Center): EnvironmentWrapper with 16:9 environment container (ZIndex="100")
    - Column 2 (Right Gutter): Resource list panel with placeholder (ZIndex="1000")
  - **Interactive Elements**:
    - **Edge Navigation**: Hub button (left gutter, navigation to HubPage)
    - **Environment Elements**: ResourceSlotButton (X: 0, Y: 20 - center, above center), ExtractButton (X: 0, Y: -20 - center, below center)
    - **Absolute Coordinate System**: X Range: -100 to 100, Y Range: -100 to 100 (container: 1920px × 1080px)
    - **Position Updates**: Coordinates defined in code-behind, positions set programmatically via `UpdateElementPositions()`
  - **Resource Panel**: Scrollable list with Grass, Lum, Coal items
  - **Automatic Scaling**: Font sizes and panel dimensions scale based on screen size (Windows 1920px base)
  - **Panel Behavior**: Closes on outside click, auto-closes when navigating away, no close button
  - **Back navigation to Hub**

#### MVVM Architecture
- BaseViewModel with INotifyPropertyChanged implementation
- Dedicated ViewModels for each page:
  - MainMenuViewModel
  - HubViewModel
  - GreenhouseViewModel
  - LaboratoryViewModel
- Data binding ready for future features
- Platform-specific code guards using `#if ANDROID || WINDOWS` directives

#### Environment Object System
- **Generic Base Class**: `EnvObject` abstract class for all environment objects (pots, furniture, etc.)
  - X and Y coordinates using 1:1 pixel-to-logical-unit ratio
  - Width and Height properties
  - `UpdatePosition()` method for automatic coordinate conversion
  - Abstract `CreateVisualElement()` method for visual representation
  - **Interfaces**: Supports `IInteractable` (for clickable objects) and `IAnimated` (for animated objects)
- **PotObject Implementation**: Concrete implementation for greenhouse pots
  - Inherits from `EnvObject` and implements `IInteractable`
  - Creates pot UI elements dynamically (Border, icon, label, separator)
  - Event handler support for pot interactions (`Clicked` event, `InteractAction` callback)
  - `CanInteract` property controls interaction availability
  - Uses DynamicResource for font sizes (ButtonIconSize, ButtonPlaceholderSize, ButtonLabelSize)
- **FurnitureObject Implementation**: Concrete implementation for decorative furniture (tables, shelves, lights, etc.)
  - Inherits from `EnvObject`
  - Creates furniture UI elements dynamically (Border, icon, optional display name)
  - Supports custom background colors and sprites
  - Icon size scales proportionally to object width
  - Optional display name label for identification
- **Dynamic Creation**: Objects are created programmatically from object instances, not hardcoded in XAML
- **Benefits**: 
  - Cleaner XAML (no hardcoded elements)
  - Easy to add new object types by inheriting from `EnvObject`
  - Centralized position calculation logic
  - Consistent coordinate system across all objects
  - Interface-based design for extensibility (IInteractable, IAnimated)

### 5.2 Planned Features (Not Yet Implemented)

#### Cultivation System
- Plant growth mechanics
- Watering and maintenance
- Harvesting and resource collection
- Environmental control effects

#### Breeding System
- Cross-breeding mechanics
- Genetic modification

#### Trading System
- Resource exchange
- Trading terminal interface

#### Quest System
- Mission objectives
- Reward system

#### Expedition System
- Resource gathering missions
- Discovery mechanics
- Risk/reward balance

---

## 6. Known Limitations & Future Considerations

### 6.1 Current Limitations
- No online features (by design - solo experience)
- No real-time graphics rendering
- Limited by .NET MAUI standard library
- Android performance may vary on low-end devices
- Window size on Windows is fixed (will be configurable via settings in future)
- Environment images are placeholders (actual sprites to be added)

### 6.2 Design Decisions
- **16:9 Aspect Ratio**: Game environment maintains 16:9 ratio, uses reference size (1920x1080) with scale transform
- **Landscape Only**: All gameplay is designed for landscape orientation
- **Mixed UI/Environment**: Interactive environment elements mixed with UI overlays
- **Cross-Platform Consistency**: Scale transform system ensures identical visual appearance on both platforms
- **Grid Column Layout**: 3-column Grid separates edge buttons from environment for proper click handling
- **ZIndex Layering**: Side buttons (ZIndex=1000) above environment (ZIndex=100) ensures click priority
- **Font Size Resources**: Centralized font management using DynamicResource in Styles.xaml
- **Automatic Font Sizing**: Font sizes scale automatically based on screen width (Windows 1920px = base)
- **Automatic Panel Sizing**: Panel dimensions scale automatically based on screen dimensions
- **Panel Interaction**: Panels close on outside click, no close buttons, auto-close on navigation
- **Placeholder System**: Invisible elements maintain column width to prevent layout shifts
- **Android Fullscreen**: Immersive mode provides distraction-free gameplay experience

---

## 7. Quick Reference

### 7.1 Important File Locations

| Purpose | File Path |
|---------|-----------|
| Main navigation | `Outgrowth/AppShell.xaml` |
| App initialization | `Outgrowth/MauiProgram.cs` |
| Project config | `Outgrowth/Outgrowth.csproj` |
| Main menu | `Outgrowth/MainPage.xaml` |
| Hub page | `Outgrowth/Views/HubPage.xaml` |
| Greenhouse | `Outgrowth/Views/GreenhousePage.xaml` |
| Laboratory | `Outgrowth/Views/LaboratoryPage.xaml` |
| Base ViewModel | `Outgrowth/ViewModels/BaseViewModel.cs` |
| Images | `Outgrowth/Resources/Images/` |

### 7.2 Useful Commands

```bash
# Clean build
dotnet clean
dotnet build

# Run on specific platform
dotnet build -f net9.0-windows
dotnet build -f net9.0-android

# Restore packages
dotnet restore

# Run app
dotnet run
```

### 7.3 Common MAUI Namespaces

```csharp
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
```

---

## 8. Contact & Resources

- **GitHub Repository**: https://github.com/NikRomaniuk/Outgrowth
- **Instructor GitHub**: DonH-ITS
- **Submission Deadline**: January 7, 2026

---

**END OF PROJECT CONTEXT DOCUMENT**






# Outgrowth - Project Context & Technical Documentation

## Table of Contents
1. [Project Overview](#project-overview)
2. [Architecture](#architecture)
3. [Absolute Coordinate System](#absolute-coordinate-system)
4. [Page Layouts](#page-layouts)
5. [Implementation Details](#implementation-details)
6. [Development Guidelines](#development-guidelines)

## Project Overview

**Outgrowth** is a space botanical simulator game built with .NET 9.0 MAUI, targeting Windows and Android platforms. The game combines space station management with plant cultivation, breeding, and research mechanics.

### Key Technologies
- **Framework**: .NET 9.0 MAUI (Standard libraries only, no Community Toolkit)
- **Platforms**: Windows 10/11, Android (API 21+)
- **Pattern**: MVVM with data binding
- **Storage**: JSON files in local app data directory

## Architecture

### MVVM Pattern
All pages follow the MVVM (Model-View-ViewModel) pattern:
- **Views**: XAML pages in `Views/` folder
- **ViewModels**: Business logic in `ViewModels/` folder (inherit from `BaseViewModel`)
- **Models**: Data classes and environment objects in `Models/` folder

### Environment Object System

The project uses a generic environment object system for managing interactive elements:

#### EnvObject Base Class
- **Location**: `Outgrowth/Models/EnvObject.cs`
- **Purpose**: Abstract base class for all environment objects (pots, furniture, decorations, etc.)
- **Properties**:
  - `Id`: Unique identifier for the object
  - `X`, `Y`: Logical coordinates (center of object)
  - `Width`, `Height`: Object dimensions in pixels
  - `VisualElement`: The UI representation (View)
  - `BaseSprite`: Base sprite/icon for the object
- **Methods**:
  - `UpdatePosition(double containerCenterX, double containerCenterY)`: Converts logical coordinates to pixel positions
  - `CreateVisualElement()`: Abstract method to create visual representation (must be implemented by derived classes)
- **Interfaces**:
  - `IInteractable`: Interface for objects that can be interacted with (clicked, tapped)
    - `OnInteract()`: Method called when object is interacted with
    - `CanInteract`: Property indicating if interaction is currently available
  - `IAnimated`: Interface for objects that support animation
    - `StartAnimation()`: Starts the animation
    - `StopAnimation()`: Stops the animation
    - `IsAnimating`: Property indicating if animation is currently running
- **Coordinate System**: Uses 1:1 pixel-to-logical-unit ratio for simplified calculations

#### PotObject Implementation
- **Location**: `Outgrowth/Models/PotObject.cs`
- **Purpose**: Concrete implementation for greenhouse pots
- **Inheritance**: Inherits from `EnvObject` and implements `IInteractable`
- **Properties**:
  - `PotNumber`: Pot identifier (1-5)
  - `Clicked`: Event handler for pot interactions (`EventHandler<TappedEventArgs>?`)
  - `InteractAction`: Optional callback action for interactions (`Action?`)
  - `CanInteract`: Property controlling whether pot can be interacted with (from `IInteractable`)
- **Implementation**: 
  - Creates VerticalStackLayout with Border, icon, separator, and label
  - Uses DynamicResource for font sizes (ButtonIconSize, ButtonPlaceholderSize, ButtonLabelSize)
  - Implements tap gesture recognizer for interactions
  - Calls `OnInteract()` when tapped, which invokes `InteractAction` if set

#### FurnitureObject Implementation
- **Location**: `Outgrowth/Models/FurnitureObject.cs`
- **Purpose**: Concrete implementation for decorative furniture (tables, shelves, lights, etc.)
- **Inheritance**: Inherits from `EnvObject` (does not implement interfaces by default)
- **Properties**:
  - `FurnitureType`: Type identifier for the furniture (e.g., "table", "shelf", "light")
  - `DisplayName`: Optional display name for the furniture item
  - `BackgroundColor`: Custom background color (defaults to `#3E2723` if not specified)
- **Implementation**: 
  - Creates VerticalStackLayout with Border, icon, and optional display name label
  - Icon size scales proportionally to object width (40% of width)
  - Background uses semi-transparent color with rounded corners
  - Display name label only shown if `DisplayName` is not empty

#### Benefits
- **Clean XAML**: No hardcoded elements - objects created programmatically
- **Scalable**: Easy to add new object types by inheriting from `EnvObject`
- **Consistent**: Centralized position calculation logic
- **Generic**: Same coordinate system and update logic for all objects
- **Extensible**: Interface-based design allows adding interaction and animation capabilities

### Page Structure
All environment pages use a consistent 3-column Grid layout:
```
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚ Left Gutter â”‚  Center (Env)    â”‚ Right Gutterâ”‚
â”‚   (Auto)    â”‚       (*)        â”‚   (Auto)    â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”´â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”´â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
```

## Absolute Coordinate System

### Overview
All environment elements are positioned using an absolute coordinate system defined in code-behind. Coordinates are logical units that are automatically converted to pixel positions.

### GreenhousePage Coordinate System

#### Specifications
- **Container**: ContentContainer (AbsoluteLayout)
- **Dimensions**: 19200px × 1080px
- **X Range**: -9600 to 9600 logical units
- **Y Range**: -540 to 540 logical units
- **Container Center**: 
  - X: 9600px (where logical X = 0)
  - Y: 540px (where logical Y = 0)

#### Scaling
- **1:1 Pixel-to-Logical-Unit Ratio**: 1 logical unit = 1 pixel (simplified coordinate system)
- **X Scaling**: 1 logical unit = 1 pixel
- **Y Scaling**: 1 logical unit = 1 pixel
- **Formula**:
  ```csharp
  centerPixelX = 9600 + (logicalX * 1.0)
  centerPixelY = 540 - (logicalY * 1.0)  // Negative Y = below center
  ```

#### Implementation
```csharp
// PotObject instances defined in code-behind
private readonly List<PotObject> _pots =
[
    new PotObject(1, 9400, 0),   // Pot 1: rightmost, centered vertically
    new PotObject(2, 9000, 0),   // Pot 2: second from right
    new PotObject(3, 8600, 0),   // Pot 3: center
    new PotObject(4, 8200, 0),   // Pot 4: second from left
    new PotObject(5, 7800, 0)    // Pot 5: leftmost
];

// Pots created dynamically and added to ContentContainer
private void CreatePotElements()
{
    foreach (var pot in _pots)
    {
        var visualElement = pot.CreateVisualElement();
        ContentContainer.Children.Add(visualElement);
    }
}

// Automatic position conversion via EnvObject.UpdatePosition()
private void UpdatePotPositions()
{
    const double containerCenterX = 9600.0;
    const double containerCenterY = 540.0;
    
    foreach (var pot in _pots)
    {
        pot.UpdatePosition(containerCenterX, containerCenterY);
    }
}
```

### HubPage & LaboratoryPage Coordinate System

#### Specifications
- **Container**: EnvironmentContainer (AbsoluteLayout)
- **Dimensions**: 1920px × 1080px
- **X Range**: -960 to 960 logical units
- **Y Range**: -540 to 540 logical units
- **Container Center**:
  - X: 960px (where logical X = 0)
  - Y: 540px (where logical Y = 0)

#### Scaling
- **1:1 Pixel-to-Logical-Unit Ratio**: 1 logical unit = 1 pixel (simplified coordinate system)
- **X Scaling**: 1 logical unit = 1 pixel
- **Y Scaling**: 1 logical unit = 1 pixel
- **Formula**:
  ```csharp
  centerPixelX = 960 + (logicalX * 1.0)
  centerPixelY = 540 - (logicalY * 1.0)  // Negative Y = below center
  ```

#### Implementation
```csharp
// HubPage example (updated coordinates with 1:1 ratio)
private readonly (int X, int Y, string Name)[] _elementCoordinates = 
{
    (-480, 0, "Market"),       // Left of center (was -50)
    (0, 162, "QuestConsole"),  // Center, above center (was 0, 30)
    (480, 0, "Statistics")     // Right of center (was 50)
};

// LaboratoryPage example (updated coordinates with 1:1 ratio)
private readonly (int X, int Y, string Name)[] _elementCoordinates = 
{
    (0, 108, "ResourceSlot"),  // Center, above center (was 0, 20)
    (0, -108, "Extract")       // Center, below center (was 0, -20)
};
```

### Coordinate Rules

1. **Center Reference**: (0, 0) always represents the container center
2. **Element Center**: Coordinates represent the CENTER of elements, not top-left corner
3. **Y Axis**: Negative values = below center, positive = above center
4. **X Axis**: Negative values = left of center, positive = right of center
5. **Conversion**: Logical coordinates are converted to pixel positions automatically
6. **Code-Only Definition**: All coordinates are defined ONLY in code-behind arrays, never hardcoded in XAML
7. **Automatic Updates**: Positions are set programmatically on page load and size changes
8. **XAML Initial Position**: Elements in XAML have initial position (0,0) - replaced by programmatic positioning

### Adding New Elements

#### Step 1: Add Coordinates
Add coordinate tuple to the appropriate array in code-behind:
```csharp
// For GreenhousePage
private readonly (int X, int Y)[] _potCoordinates = 
{
    // ... existing coordinates ...
    (0, -50)  // New element at center, below center
};

// For HubPage/LaboratoryPage
private readonly (int X, int Y, string Name)[] _elementCoordinates = 
{
    // ... existing coordinates ...
    (25, -25, "NewElement")  // New element
};
```

#### Step 2: Add XAML Element
Add the element to XAML with initial position (0,0) - will be updated programmatically:
```xml
<!-- Position set programmatically from absolute coordinates (X=25, Y=-25) -->
<VerticalStackLayout x:Name="NewElement"
                     AbsoluteLayout.LayoutBounds="0,0,AutoSize,AutoSize"
                     AbsoluteLayout.LayoutFlags="None">
    <!-- Element content -->
</VerticalStackLayout>
```

**Important**: Never hardcode pixel positions in XAML. Always use (0,0) as initial position - it will be updated automatically.

#### Step 3: Update Position Method
For HubPage/LaboratoryPage, add element to the switch statement in `UpdateElementPositions()`:
```csharp
VerticalStackLayout? element = name switch
{
    // ... existing cases ...
    "NewElement" => FindByName("NewElement") as VerticalStackLayout,
    _ => null
};
```

## Page Layouts

### HubPage

#### Layout Structure
- **Left Gutter**: Greenhouse navigation button
- **Center**: EnvironmentContainer with Market, Quest Console, Statistics buttons
- **Right Gutter**: Laboratory navigation button

#### Elements
- **Market**: X = -50, Y = 0 (left of center, centered vertically)
- **Quest Console**: X = 0, Y = 30 (center, above center)
- **Statistics**: X = 50, Y = 0 (right of center, centered vertically)
- **Coordinate System**: All elements use absolute coordinates defined in code-behind
- **Position Updates**: `UpdateElementPositions()` converts logical coordinates to pixel positions automatically

#### Panels
- Market Panel, Quest Panel, Statistics Panel (overlay panels)
- Close on outside click
- Scale with environment scale

### GreenhousePage

#### Layout Structure
- **Left Gutter**: LiquidsPanel/SeedsPanel (mutually exclusive), placeholder
- **Center**: EnvironmentContainer â†’ ContentContainer (scrollable container for pots)
- **Right Gutter**: Hub navigation button
- **Bottom**: ToolsPanel (always visible), MovePanel (Android only)

#### Pots
- **Count**: 5 pots
- **Positions**: All at Y = 0 (centered vertically)
- **X Positions**: 600, 300, 0, -300, -600
- **Navigation**: Left/Right arrows (Android) move ContentContainer

#### ContentContainer System
- **Purpose**: Scrollable container for pots and future furniture
- **Dimensions**: 19200px Ã— 1080px
- **Navigation**: TranslationX shifts entire container
- **Centering**: Selected pot automatically centers on screen

#### Panels
- **LiquidsPanel**: Shows Water, Fertilizer, Growth Serum
- **SeedsPanel**: Shows Grass Seed, Flower Seed, Tree Seed
- **ToolsPanel**: Liquids, Seeds, Cancel buttons
- **MovePanel**: Left/Right navigation arrows (Android only)

### LaboratoryPage

#### Layout Structure
- **Left Gutter**: Hub navigation button
- **Center**: EnvironmentContainer with Resource Slot and Extract button
- **Right Gutter**: Resource list panel with placeholder

#### Elements
- **Resource Slot**: X = 0, Y = 20 (center, above center)
- **Extract Button**: X = 0, Y = -20 (center, below center)
- **Coordinate System**: All elements use absolute coordinates defined in code-behind
- **Position Updates**: `UpdateElementPositions()` converts logical coordinates to pixel positions automatically

#### Panels
- **Resource List Panel**: Scrollable list of resources (Grass, Lum, Coal)

## Implementation Details

### Scale Transform System

All pages use scale transforms for cross-platform consistency:

```csharp
// Reference size
const double referenceWidth = 1920.0;
const double referenceHeight = 1080.0;

// Calculate scale to fit screen
var scale = Math.Min(targetWidth / referenceWidth, targetHeight / referenceHeight);

// Apply scale to wrapper
EnvironmentWrapper.Scale = scale;
EnvironmentWrapper.AnchorX = 0.5;
EnvironmentWrapper.AnchorY = 0.5;
```

### Font Scaling

Font sizes scale automatically based on screen width:

```csharp
const double windowsBaseWidth = 1920.0;
var fontScale = pageWidth / windowsBaseWidth;

// Update DynamicResource values
Resources["ResourcePanelTitleSize"] = baseTitleSize * fontScale;
```

### Panel Interaction

All panels use consistent interaction pattern:
- Close on outside click (background overlay)
- No close buttons (cleaner UI)
- Auto-close on navigation away
- Visibility managed via `IsVisible` property

### Platform-Specific Features

#### Android
- **Fullscreen**: Immersive mode enabled
- **Orientation**: SensorLandscape (allows rotation between landscape orientations)
- **MovePanel**: Visible only on Android for pot navigation

#### Windows
- **Window**: Non-resizable (windowed fullscreen)
- **MovePanel**: Hidden (navigation via mouse/keyboard in future)

## Development Guidelines

### Adding New Environment Elements

1. **Define Coordinates**: Add to coordinate array in code-behind
2. **Create XAML Element**: Add element to XAML with x:Name
3. **Update Position Method**: Add element to position update method
4. **Test**: Verify positioning on both Windows and Android

### Coordinate System Best Practices

1. **Use Logical Coordinates**: Never hardcode pixel positions
2. **Center Reference**: Always use (0, 0) as center reference
3. **Element Sizes**: Account for element size when calculating positions
4. **Consistent Naming**: Use descriptive names in coordinate arrays

### Code Organization

1. **Coordinate Arrays**: Define at top of class
2. **Update Methods**: Separate methods for position updates
3. **Comments**: Document coordinate system and formulas
4. **Consistency**: Use same pattern across all pages

### Testing Checklist

- [ ] Elements position correctly on Windows
- [ ] Elements position correctly on Android
- [ ] Coordinates are converted correctly
- [ ] Elements center properly when selected
- [ ] Scale transforms work correctly
- [ ] Font scaling works correctly

## Future Enhancements

- **Y Coordinate Range**: Consider expanding Y range for more vertical positioning options
- **Animation**: Add smooth transitions when elements move
- **Zoom**: Implement pinch-to-zoom for GreenhousePage
- **Custom Elements**: Allow dynamic element creation at runtime
- **Coordinate Validation**: Add bounds checking for coordinates

## References

- `.cursorrules`: Quick reference for AI assistants
- `README.md`: Project overview and getting started guide
- MAUI Documentation: https://learn.microsoft.com/dotnet/maui/

---

**Last Updated**: December 26, 2025

**Recent Updates**:
- Added `EnvObject` and `PotObject` generic environment object system
- Added `FurnitureObject` for decorative furniture items
- Added `IInteractable` and `IAnimated` interfaces for extensible object behavior
- Updated coordinate system to use 1:1 pixel-to-logical-unit ratio across all pages
- Pots are now created dynamically from `PotObject` instances, not hardcoded in XAML
- Navigation restricted to middle three pots (Pot2, Pot3, Pot4) in GreenhousePage
- `PotObject` now implements `IInteractable` interface with `InteractAction` callback support



