# 🌱 Outgrowth

> A solitary space-faring botanical simulator built with .NET 9.0 MAUI

## 📖 Overview

**Outgrowth** As the lone occupant of a remote research station, your mission is to engineer and cultivate a resilient ecosystem of fictional plants capable of surviving the harsh vacuum of deep space. Through selective breeding and resource management, you must develop a self-sustaining "Green Ark" to terraform the void

## 🎮 Core Gameplay

### Space Station Areas
- **🛰️ Hub**: Command center for quests, trading, expeditions
- **🌿 Greenhouse**: Grow and maintain your plant collection
- **🔬 Laboratory**: Research, breeding, and chemical extraction

## 🛠️ Tech Stack

- **Framework**: .NET 9.0 MAUI (standard libraries only, no Community Toolkit)
- **Platforms**: Windows & Android
- **Architecture**: MVVM with data binding
- **Storage**: JSON files in local app data
- **Display**: 16:9 landscape (immersive fullscreen on Android)

## 📁 Project Structure

```
Outgrowth/
├── App.xaml / .cs        # Application entry point and lifecycle events
├── AppShell.xaml / .cs   # Navigation shell routing configuration
├── MauiProgram.cs        # App initialization and dependency setup
├── GlobalXmlns.cs        # Global XAML namespace definitions
├── Views/                # XAML pages
│   ├── HubPage.xaml          # Space station command center
│   ├── GreenhousePage.xaml   # Plant cultivation area
│   └── LaboratoryPage.xaml   # Research & breeding facility
├── ViewModels/           # MVVM view models with data binding
│   ├── BaseViewModel.cs      # Base class with INotifyPropertyChanged
│   ├── HubViewModel.cs       # Hub logic
│   ├── GreenhouseViewModel.cs # Greenhouse logic
│   └── LaboratoryViewModel.cs # Laboratory logic
├── Models/               # Data models and environment objects
│   ├── EnvObject.cs      # Base class for all environment objects
│   ├── PotObject.cs      # Interactive pot implementation
│   ├── PlantObject.cs    # Plant instance with growth mechanics
│   ├── PlantData.cs      # Plant type definitions
│   ├── PlantLibrary.cs   # Central plant library
│   ├── SeedData.cs       # Seed definitions
│   ├── SeedLibrary.cs    # Central seed library
│   ├── LiquidData.cs     # Liquid definitions
│   ├── LiquidLibrary.cs  # Central liquid library
│   ├── ResourceData.cs   # Resource definitions
│   ├── ResourceLibrary.cs # Central resource library
│   ├── StationObject.cs  # Interactive station elements
│   ├── FurnitureObject.cs # Decorative furniture
│   └── AnimatedPotObject.cs # Animated pot with pulse effect
├── Services/             # Application services
│   ├── GameDataManager.cs      # One-time load of all JSON data libraries, starter seed grants
│   ├── NavigationService.cs    # Animated page navigation with fade transitions
│   ├── ScreenProperties.cs     # Screen size and scale calculations
│   ├── PersistentTimer.cs      # Timer that persists across app sessions
│   ├── PlantsManager.cs        # Manages plant growth and cycles
│   ├── PlantsSaveService.cs    # Saves and loads plant states
│   ├── MaterialsSaveService.cs # Saves and loads material quantities
│   ├── UserInterfaceCreator.cs # Centralized panel item creation with enable/disable logic
│   └── StyledPanel.cs          # 9-slice pixel-art panels for UI consistency
├── Platforms/            # Platform-specific code (Android, Windows, iOS, MacCatalyst, Tizen)
├── Resources/            # Images, fonts, styles, and other assets
│   ├── AppIcon/          # Application icon assets
│   ├── Data/             # JSON data libraries (PlantLibrary, SeedLibrary, LiquidLibrary, ResourceLibrary)
│   ├── Fonts/            # Custom fonts (including Silkscreen for panels)
│   ├── Images/           # Sprites for plants, pots, seeds, liquids, resources, UI elements
│   ├── Splash/           # Splash screen assets
│   └── Styles/           # XAML style definitions (Colors, Styles)
├── Properties/           # Launch settings and configuration
└── .github/              # GitHub workflows and Copilot instructions
```

## 🚀 Getting Started

### Prerequisites

- Visual Studio 2022 (v17.8 or later)
- .NET 9.0 SDK
- Windows 10/11 (for Windows development)
- Android SDK (for Android development)

### Quick Start

```bash
git clone [repository-url]
cd Outgrowth
dotnet restore
dotnet build

# Run on Windows
dotnet build -f net9.0-windows

# Run on Android  
dotnet build -f net9.0-android
```

## ✨ Features

### Currently Implemented
- ✅ Cross-platform foundation (Windows & Android)
- ✅ MVVM architecture with reactive data binding and `INotifyPropertyChanged`
- ✅ Smooth page navigation with fade transitions
- ✅ Complete navigation system (Main Menu → Hub → Greenhouse/Laboratory)
- ✅ **Hub Page**: Interactive command center with Market, Quest Console, Statistics
- ✅ **Greenhouse Page**: Plant cultivation with 5 pots, seed planting, harvesting, and liquid application
- ✅ **Laboratory Page**: Machine-based research interface with resource extraction and animated display
- ✅ **Plant Cultivation System**:
  - Automatic growth based on cycles (1 cycle = 5 seconds)
  - Seed planting: Select seed → click empty pot to plant
  - Plant harvesting: Activate harvester → click plant to remove
  - Growth persists across app sessions
  - Multiple growth stages with sprite animations
- ✅ **Resource & Quantity System**:
  - Seeds, liquids, and resources with tracked quantities
  - Items selectable only when quantity > 0
  - Visual feedback for unavailable items (grayed out)
  - Stage-dependent drops: plants may drop different types and quantities of resources depending on their growth stage
  - Starter seeds: Players begin with 1 Grass and 1 Lumivial seeds (all availiable)
  - Real-time quantity updates across UI
  - Easy content authoring: add a sprite to Resources/Images and an entry to the appropriate JSON (SeedLibrary/PlantLibrary/LiquidLibrary/ResourceLibrary) — no code changes required! (There's might be a lot more content, but deadline...)
- ✅ **Machine & Laboratory Animations**:
  - Machine display with slide-in/out animations
  - Resource selection with visual machine content feedback
  - Resource extraction: consume resources to produce liquids
- ✅ **Data Libraries**: One-time load of plants, seeds, liquids, resources from JSON
- ✅ **UI System**: Pixel-art 9-slice panels with consistent styling across all interfaces
- ✅ **Data Libraries**: One-time load of plants, seeds, liquids, resources from JSON
- ✅ **Environment Object System**: Extensible architecture with `EnvObject` base class
  - `PotObject` - Interactive pots with plant slots
  - `PlantObject` - Growing plants with automatic stage progression
  - `StationObject` - Interactive station elements
  - `FurnitureObject` - Decorative furniture items with animations
  - Ilatform-specific input handling (keyboard on Windows, touch on Android)
- ✅ Persistent save/load for plants, materials, and game state
- ✅ Optimized UI updates to prevent duplicate element creation
- ✅ Persistent save/load for plants, materials, and game state

- ✅ Misc fixes and UI improvements (2026-01-02): fixed ResourceItem/ResourceSlot sprite flicker, preloaded MachineDisplay images and cross-faded to prevent flicker, added MachineDisplayContent (resource image + amount), registered Silkscreen fonts for panels, and added debug logs for display animations

### Coming Soon
- Research progression and chemical extraction
- Trading, quests, and expeditions

### Notes
- Many additional ideas (quests, breeding(mutating) systems, marketing, plant/resource/liquids/seeds collection overview with descriptions and a bit of lore(there's a descriptions for existingg plants in a JSON though)) were not implemented due to a very short development timeframe (and previous failed project)

## 🎨 Design

Calm, methodical gameplay with a scientific approach to plant breeding. Solo experience in a space station atmosphere

## 📝 Academic Project

Developed for ATU Year 2 Software Development (BSc Computing)

## 📄 License

This is an educational project. All rights reserved

## 👤 Author

**Nik Romaniuk** - [@NikRomaniuk](https://github.com/NikRomaniuk)

---

**Status**: 🚧 In Active Development | **Last Updated**: January 7, 2026

