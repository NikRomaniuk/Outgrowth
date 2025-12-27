# 🌱 Outgrowth

> A solitary space-faring botanical simulator built with .NET 9.0 MAUI

## 📖 Overview

**Outgrowth** As the lone occupant of a remote research station, your mission is to engineer and cultivate a resilient ecosystem of fictional plants capable of surviving the harsh vacuum of deep space. Through selective breeding and resource management, you must develop a self-sustaining "Green Ark" to terraform the void

## 🎮 Core Gameplay

### Space Station Areas
- **🛰️ Hub**: Central command center for managing all station external operations
- **🌿 Greenhouse**: Cultivation area for growing and maintaining your plant collection
- **🔬 Laboratory**: Research facility for breeding, genetic modification, and chemical extraction

### Gameplay Systems
- **Cultivation**: Growing, watering, and harvesting plants (coming soon)
- **Breeding**: Cross-breeding to create new plant varieties (coming soon)
- **Trading**: Exchange resources with other space platforms (coming soon)
- **Research**: Unlock new extracts (coming soon)
- **Quests**: Complete missions and objectives (coming soon)
- **Expeditions**: Explore and gather resources (coming soon)

## 🛠️ Technical Details

- **Framework**: .NET 9.0 MAUI
- **Platforms**: Windows & Android
- **Architecture**: MVVM (Model-View-ViewModel)
- **Data Persistence**: JSON-based local storage
- **Language**: C#
- **Display**: 16:9 aspect ratio (landscape only)
- **Android**: SensorLandscape orientation (rotates between landscape orientations)
- **Windows**: Non-resizable window (windowed fullscreen)

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
│   ├── EnvObject.cs      # Base class for environment objects
│   └── PotObject.cs      # Pot object implementation
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

### Building & Running

```bash
# Clone the repository
git clone [repository-url]

# Navigate to the project
cd Outgrowth

# Restore packages
dotnet restore

# Build the project
dotnet build

# Run on Windows
dotnet build -f net9.0-windows

# Run on Android
dotnet build -f net9.0-android
```

## 📚 Documentation

For detailed information about the project architecture and implementation details, see:

- **[PROJECT_CONTEXT.md](PROJECT_CONTEXT.md)** - Comprehensive project documentation

## ✨ Features

### Currently Implemented
- ✅ Cross-platform foundation (Windows & Android)
- ✅ MVVM architecture with data binding
- ✅ Complete navigation system
- ✅ Main Menu with game launcher interface
- ✅ Hub (Command Center) page with interactive environment
  - Interactive elements (Market, Quest Console, Statistics) with overlay panels
  - Navigation buttons to Greenhouse and Laboratory
  - Consistent 16:9 layout across platforms
- ✅ Greenhouse page with complete layout and navigation
  - Dynamic pot system using `PotObject` instances (pots created programmatically, not hardcoded)
  - 5 pots with navigation system (Android only: left/right arrows, restricted to middle 3 pots)
  - ToolsPanel and MovePanel with automatic scaling
  - Side panels for liquids and seeds (mutually exclusive)
  - Hub navigation button
- ✅ Laboratory page with interactive environment
  - Resource slot and extract button
  - Scrollable resource list panel
  - Automatic scaling for different screen sizes
- ✅ MVVM architecture with BaseViewModel and individual ViewModels
- ✅ Generic environment object system (`EnvObject` base class for pots and future furniture)
- ✅ Responsive design with automatic scaling for Android and Windows
- ✅ Android immersive fullscreen mode

### Planned (In Development)
- ⏳ Plant data models and genetics system
- ⏳ Cultivation mechanics (growing, watering, harvesting)
- ⏳ Breeding system with trait inheritance
- ⏳ Chemical extraction and reagent system
- ⏳ Research progression
- ⏳ Trading system
- ⏳ Quest and mission system
- ⏳ Expedition mechanics
- ⏳ Save/Load game functionality
- ⏳ Settings page

## 🎨 Design Philosophy

- **Calm & Methodical**: Relaxing gameplay focused on careful planning
- **Scientific Approach**: Realistic genetics simulation (simplified Mendelian inheritance)
- **Solo Experience**: Single-player focused, no multiplayer pressure
- **Space Atmosphere**: Sci-fi terminal aesthetic with ambient visuals

## 📝 Academic Context

This project is being developed as part of the ATU Year 2 Software Development course (BSc in Computing)

## 📄 License

This is an educational project. All rights reserved

## 👤 Author

**Nik Romaniuk**
- GitHub: [@NikRomaniuk](https://github.com/NikRomaniuk)

## 🙏 Acknowledgments

- Instructor: DonH-ITS (on GitHub)
- Course: BSc Computing in Software Development - Year 2
- Institution: Atlantic Technical University
- Framework: .NET MAUI by Microsoft

---

**Status**: 🚧 In Active Development

**Last Updated**: December 26, 2025

