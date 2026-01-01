## Quick Guide for AI coding agents

Purpose: help an AI contributor be productive quickly in this .NET MAUI project.

- **Big picture**: This is a .NET 9 MAUI app using MVVM with reactive data binding. UI pages live in `Outgrowth/Views`, view models in `Outgrowth/ViewModels`, and runtime services in `Outgrowth/Services`
- **Data binding**: View models inherit from `BaseViewModel` with `INotifyPropertyChanged`; UI elements bind to properties that auto-update when changed
- **Runtime patterns**: Singletons and static services are used instead of DI in many places (see `PlantsManager.Instance`, `PersistentTimer.Instance`, `GameDataManager.Instance`). See [Outgrowth/Services/PlantsManager.cs](Outgrowth/Services/PlantsManager.cs), [Outgrowth/Services/PersistentTimer.cs](Outgrowth/Services/PersistentTimer.cs), and [Outgrowth/Services/GameDataManager.cs](Outgrowth/Services/GameDataManager.cs)

- **Key responsibilities**:
  - Data initialization: `GameDataManager.Initialize()` loads all JSON data libraries (plants, seeds, liquids, resources) once at app startup
  - Plant lifecycle & game cycles: `PlantsManager` advances cycles every 5s (constant in that file)
  - Persistent time across sessions: `PersistentTimer` stores `gametimer.json` in `FileSystem.AppDataDirectory`
  - Game saves: `PlantsSaveService` reads/writes `plants_save.json`; `MaterialsSaveService` reads/writes `materials_save.json`
  - Resource & material tracking: Quantity properties on `SeedData`, `LiquidData`, `ResourceData` updated via `INotifyPropertyChanged` binding
  - UI element creation: `UserInterfaceCreator` centralizes panel item creation with consistent styling, enable/disable logic, and event bindings
  - Navigation animations: `NavigationService` contains platform branches (`#if WINDOWS`, `#elif ANDROID`, etc.) — modify per-platform behavior there: [Outgrowth/Services/NavigationService.cs](Outgrowth/Services/NavigationService.cs)
  - Machine animations: `LaboratoryPage` handles `MachineDisplay` slide/sprite transitions with `DisplayAnimationStarted`/`DisplayAnimationEnded` events

- **MVVM & Data Binding conventions**:
  - View models inherit from `BaseViewModel` and use `SetProperty` + `INotifyPropertyChanged`
  - XAML bindings (`{Binding PropertyName}`) automatically update when view model properties change
  - Models like `SeedData`, `LiquidData`, `ResourceData` implement `INotifyPropertyChanged` so UI reflects quantity changes
  - See [Outgrowth/ViewModels/BaseViewModel.cs](Outgrowth/ViewModels/BaseViewModel.cs) for implementation

- **Assets & data**:
  - Static libraries (plants, seeds, resources, liquids) live as JSON in `Resources/Data/` (`PlantLibrary.json`, `SeedLibrary.json`, etc.)
  - All JSON is loaded once via `GameDataManager.Initialize()` at app startup (in [Outgrowth/MauiProgram.cs](Outgrowth/MauiProgram.cs))
  - Add new items by editing JSON and registering sprite assets in `Resources/Images/Sprites/`
  - Quantity updates via data binding: change `Quantity` property → UI auto-updates, item enable/disable status changes

- **Build & run (discoverable from README)**:
  - Restore and build: `dotnet restore` then `dotnet build`.
  - Platform builds: `dotnet build -f net9.0-windows` or `dotnet build -f net9.0-android`.
  - Preferred dev environment: Visual Studio 2022 (v17.8+) for MAUI debugging.

- **Common edit patterns / quick examples**:
  - Change cycle frequency: update `UpdateIntervalMs` in [Outgrowth/Services/PlantsManager.cs](Outgrowth/Services/PlantsManager.cs)
  - Add a new plant type: add a `PlantData` entry in `Resources/Data/PlantLibrary.json` and register sprites in `Resources/Images/Sprites/`
  - Add a new resource: add a `ResourceData` entry in `Resources/Data/ResourceLibrary.json`; it will auto-load on app startup
  - Create a new panel item: use `UserInterfaceCreator.CreatePanelItem(...)` with `isEnabled: item.Quantity > 0` to auto-gray out when unavailable
  - Respond to quantity changes: bind to the `Quantity` property in XAML (e.g., `{Binding Quantity}`) — updates propagate automatically
  - Debugging logs: many services use `System.Diagnostics.Debug.WriteLine` and `builder.Logging.AddDebug()` is enabled in DEBUG in [Outgrowth/MauiProgram.cs](Outgrowth/MauiProgram.cs)

- **Platform differences to respect**:
  - Navigation overlays: Windows uses a shared overlay moved between pages; Android expects overlays per-page. See `NavigationService` for specifics.
  - Fullscreen/immersive mode: Android uses immersive fullscreen behavior; check platform folders under `Platforms/`.

- **Where to look first when making changes**:
  - App startup & data load: [Outgrowth/MauiProgram.cs](Outgrowth/MauiProgram.cs) (calls `GameDataManager.Initialize()`)
  - Data initialization: [Outgrowth/Services/GameDataManager.cs](Outgrowth/Services/GameDataManager.cs) (loads all JSON libraries)
  - Central game loop/timers: [Outgrowth/Services/PlantsManager.cs](Outgrowth/Services/PlantsManager.cs)
  - Persistence: [Outgrowth/Services/PersistentTimer.cs](Outgrowth/Services/PersistentTimer.cs), [Outgrowth/Services/PlantsSaveService.cs](Outgrowth/Services/PlantsSaveService.cs), and [Outgrowth/Services/MaterialsSaveService.cs](Outgrowth/Services/MaterialsSaveService.cs)
  - UI element creation: [Outgrowth/Services/UserInterfaceCreator.cs](Outgrowth/Services/UserInterfaceCreator.cs) (centralized panel items, enable/disable logic)
  - UI binding patterns: `Outgrowth/ViewModels/*` and `Outgrowth/Views/*` XAML files
  - Machine animations: [Outgrowth/Views/LaboratoryPage.xaml.cs](Outgrowth/Views/LaboratoryPage.xaml.cs) (display slide/sprite logic, events)

- **Do not assume DI registrations**: the project prefers singletons/static helpers rather than ASP.NET-style service collections. If you add DI, be explicit and update `MauiProgram.CreateMauiApp()` accordingly.

- **Files that contain authoritative behavior or conventions** (check before refactoring):
  - [Outgrowth/Services/GameDataManager.cs](Outgrowth/Services/GameDataManager.cs) — one-time load of all game data
  - [Outgrowth/Services/PlantsManager.cs](Outgrowth/Services/PlantsManager.cs) — cycle logic and plant lifecycle
  - [Outgrowth/Services/PlantsSaveService.cs](Outgrowth/Services/PlantsSaveService.cs) — plant persistence
  - [Outgrowth/Services/MaterialsSaveService.cs](Outgrowth/Services/MaterialsSaveService.cs) — resource/material persistence
  - [Outgrowth/Services/PersistentTimer.cs](Outgrowth/Services/PersistentTimer.cs) — cross-session time tracking
  - [Outgrowth/Services/UserInterfaceCreator.cs](Outgrowth/Services/UserInterfaceCreator.cs) — UI panel item creation and enable/disable
  - [Outgrowth/ViewModels/BaseViewModel.cs](Outgrowth/ViewModels/BaseViewModel.cs) — MVVM and data binding base
  - [Outgrowth/Views/LaboratoryPage.xaml.cs](Outgrowth/Views/LaboratoryPage.xaml.cs) — machine animation and resource selection logic

If anything here is unclear or you want a different level of detail (examples of common PRs, unit-testing strategy, or a commit checklist), tell me which area and I'll iterate.

## Project Context Highlights (migrated)

- **EnvObject System**: all interactive environment items inherit from `EnvObject` (properties: `Id`, `X`, `Y`, `Width`, `Height`, `VisualElement`). Use `CreateVisualElement()` to produce UI and `UpdatePosition(containerCenterX, containerCenterY)` to convert logical coordinates to pixel positions
- **Coordinate rules**: logical coordinates use a center-origin system where (0,0) is container center; X negative = left, X positive = right; Y negative = below center, positive = above center; convert with container center offsets (examples in `PROJECT_CONTEXT.md`)
- **Content containers**: Greenhouse uses a wide `ContentContainer` (19200×1080) with pots positioned via logical X in range -9600..9600; Hub/Laboratory use 1920×1080 environment containers with logical X range -960..960
- **Screen scaling**: use `ScreenProperties.Instance.UpdateScreenProperties(width,height)` to recalc `Scale` and `FontScale`; apply transforms to `EnvironmentWrapper` and update DynamicResources for font sizes
- **Data initialization**: `GameDataManager.Initialize()` loads all JSON libraries once on startup; add new content by editing JSON in `Resources/Data/` and registering sprites in `Resources/Images/Sprites/`
- **Panel & interaction conventions**: side panels close on outside click, use `IsVisible` toggles, and are populated dynamically from data libraries (LiquidsPanel, SeedsPanel, ResourceList)

## Environment Objects Quick Reference

- **EnvObject (base class)**: holds `Id`, `X`, `Y`, `Width`, `Height`, `VisualElement`; implement `CreateVisualElement()` to build the UI and `UpdatePosition(containerCenterX, containerCenterY)` to place it in an AbsoluteLayout
- **Coordinate rules**: center-origin coordinates where (0,0) = container center; Greenhouse `ContentContainer` center = (9600, 540), Hub/Laboratory `EnvironmentContainer` center = (960, 540); use formulas `left = centerX + X - Width/2`, `top = centerY - Y - Height/2`
- **IInteractable**: implement `CanInteract` and `OnInteract()` (or `InteractAction`) for clickable objects; attach TapGestureRecognizer to the `VisualElement` after creation
- **IAnimated**: implement `StartAnimation()`/`StopAnimation()` and use `CancellationTokenSource` for cancellable loops; create visual element before starting animations
- **PotObject / PlantObject rules**: `PotObject.PlantSlot` holds a `PlantObject`; after creating a `PlantObject` call `PlantsManager.Instance.RegisterPlant(plant)` and subscribe `StageChanged` for auto-save; call `UpdatePotVisualElement(pot)` after changing `PlantSlot`
- **Best practices**: always null-check `VisualElement` before accessing, dispose or cancel animations on cleanup, use dynamic resources for font sizes so UI scales when `ScreenProperties` updates


