# 🌱 Outgrowth

> A solitary space-faring botanical simulator built with .NET 9.0 MAUI

## 📖 Overview

**Outgrowth** As the lone occupant of a remote research station, your mission is to engineer and cultivate a resilient ecosystem of fictional plants capable of surviving the harsh vacuum of deep space. Through selective breeding and resource management, you must develop a self-sustaining "Green Ark" to terraform the void

## 🎮 Core Gameplay

### Space Station Areas
- **🛰️ Hub**: Central command center for managing all station external operations
- **🌿 Greenhouse**: Cultivation area for growing and maintaining your plant collection
- **🔬 Laboratory**: Research facility for breeding, genetic modification, and chemical extraction

### Gameplay Systems (Planned)
- Cultivation, breeding, trading, research, quests, and expeditions

## 🛠️ Tech Stack

- **Framework**: .NET 9.0 MAUI (standard libraries only)
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
│   ├── StationObject.cs  # Interactive station elements
│   ├── FurnitureObject.cs # Decorative furniture
│   └── AnimatedPotObject.cs # Animated pot with pulse effect
├── Services/             # Business logic (coming soon)
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
- ✅ Complete navigation system (Main Menu → Hub → Greenhouse/Laboratory)
- ✅ **Hub Page**: Interactive command center with Market, Quest Console, Statistics
- ✅ **Greenhouse Page**: Dynamic pot system (5 pots, navigation on Android), resource panels
- ✅ **Laboratory Page**: Research interface with resource management
- ✅ **Environment Object System**: Extensible architecture with `EnvObject` base class
  - `PotObject` - Interactive pots with click handlers
  - `StationObject` - Interactive station elements (market, consoles, etc.)
  - `FurnitureObject` - Decorative furniture items
  - `AnimatedPotObject` - Pots with pulse animation
  - Interfaces: `IInteractable`, `IAnimated` for extensibility
- ✅ Responsive design with automatic scaling (16:9 aspect ratio)
- ✅ Android immersive fullscreen mode

### Coming Soon
- Plant genetics and cultivation mechanics
- Breeding system with trait inheritance
- Research progression and chemical extraction
- Trading, quests, and expeditions
- Save/Load functionality

## 🎨 Design

Calm, methodical gameplay with a scientific approach to plant breeding. Solo experience in a space station atmosphere-

## 📝 Academic Project

Developed for ATU Year 2 Software Development (BSc Computing)

## 📄 License

This is an educational project. All rights reserved

## 👤 Author

**Nik Romaniuk** - [@NikRomaniuk](https://github.com/NikRomaniuk)

---

**Status**: 🚧 In Active Development | **Last Updated**: December 26, 2025

