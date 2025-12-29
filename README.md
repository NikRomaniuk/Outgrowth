# 🌱 Outgrowth

> A solitary space-faring botanical simulator built with .NET 9.0 MAUI

## 📖 Overview

**Outgrowth** As the lone occupant of a remote research station, your mission is to engineer and cultivate a resilient ecosystem of fictional plants capable of surviving the harsh vacuum of deep space. Through selective breeding and resource management, you must develop a self-sustaining "Green Ark" to terraform the void

## 🎮 Core Gameplay

### Space Station Areas
- **🛰️ Hub**: Command center for quests, trading, expeditions
- **🌿 Greenhouse**: Grow and maintain your plant collection
- **🔬 Laboratory**: Research, breeding, and chemical extraction

### Coming Soon
- Breeding system with trait inheritance
- Trading and expeditions
- Quest system

## 🛠️ Tech Stack

- **Framework**: .NET 9.0 MAUI (standard libraries only, no Community Toolkit)
- **Platforms**: Windows & Android
- **Architecture**: MVVM with data binding
- **Storage**: JSON files in local app data
- **Display**: 16:9 landscape (immersive fullscreen on Android)

## 📁 Project Structure

```
Outgrowth/
├── Views/                # XAML pages
│   ├── HubPage.xaml          # Space station command center
│   ├── GreenhousePage.xaml   # Plant cultivation area
│   └── LaboratoryPage.xaml   # Research & breeding facility
├── ViewModels/           # MVVM view models with data binding
│   ├── BaseViewModel.cs      # Base class with INotifyPropertyChanged
│   ├── MainMenuViewModel.cs  # Main menu logic
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
│   ├── NavigationService.cs    # Animated page navigation with fade transitions
│   ├── ScreenProperties.cs     # Screen size and scale calculations
│   ├── PersistentTimer.cs       # Timer that persists across app sessions
│   ├── PlantsManager.cs         # Manages plant growth and cycles
│   └── PlantsSaveService.cs     # Saves and loads plant states
├── Platforms/            # Platform-specific code
└── Resources/            # Images, fonts, styles, and other assets
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

## 📚 Documentation

- **[PROJECT_CONTEXT.md](PROJECT_CONTEXT.md)** - Full project documentation
- **[ENVIRONMENT_OBJECTS_ARCHITECTURE.md](ENVIRONMENT_OBJECTS_ARCHITECTURE.md)** - Environment object system architecture

## ✨ Features

### Currently Implemented
- ✅ Cross-platform foundation (Windows & Android)
- ✅ MVVM architecture with data binding
- ✅ Smooth page navigation with fade transitions
- ✅ Complete navigation system (Main Menu → Hub → Greenhouse/Laboratory)
- ✅ **Hub Page**: Interactive command center with Market, Quest Console, Statistics
- ✅ **Greenhouse Page**: Plant cultivation with 5 pots, seed planting, and harvesting
- ✅ **Laboratory Page**: Research interface with resource management
- ✅ **Plant Cultivation System**:
  - Automatic growth based on cycles (1 cycle = 5 seconds)
  - Seed planting: Select seed → click empty pot to plant
  - Plant harvesting: Activate harvester → click plant to remove
  - Growth persists across app sessions
  - Multiple growth stages with sprite animations
- ✅ **Data Libraries**: Centralized management for plants, seeds, liquids, and resources
- ✅ **Environment Object System**: Extensible architecture with `EnvObject` base class
  - `PotObject` - Interactive pots with plant slots
  - `PlantObject` - Growing plants with automatic stage progression
  - `StationObject` - Interactive station elements
  - `FurnitureObject` - Decorative furniture items
  - Interfaces: `IInteractable`, `IAnimated` for extensibility
- ✅ Responsive design with automatic scaling (16:9 aspect ratio)
- ✅ Android immersive fullscreen mode
- ✅ Save/Load system for plant states

### Coming Soon
- Breeding system with trait inheritance
- Research progression and chemical extraction
- Trading, quests, and expeditions

## 🎨 Design

Calm, methodical gameplay with a scientific approach to plant breeding. Solo experience in a space station atmosphere

## 📝 Academic Project

Developed for ATU Year 2 Software Development (BSc Computing)

## 📄 License

This is an educational project. All rights reserved

## 👤 Author

**Nik Romaniuk** - [@NikRomaniuk](https://github.com/NikRomaniuk)

---

**Status**: 🚧 In Active Development | **Last Updated**: December 29, 2025

