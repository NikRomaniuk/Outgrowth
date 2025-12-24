# 🌱 Outgrowth

> A solitary space-faring botanical simulator built with .NET 9.0 MAUI

## 📖 Overview

**Outgrowth** As the lone occupant of a remote research station, your mission is to engineer and cultivate a resilient ecosystem of fictional plants capable of surviving the harsh vacuum of deep space. Through selective breeding and resource management, you must develop a self-sustaining "Green Ark" to terraform the void

## 🎮 Core Gameplay

- **Cultivation**: Not yet
- **Breeding**: Not yet
- **Trading**: Not yet

## 🛠️ Technical Details

- **Framework**: .NET 9.0 MAUI
- **Platforms**: Windows & Android
- **Architecture**: MVVM (Model-View-ViewModel)
- **Data Persistence**: JSON-based local storage
- **Language**: C#

## 📁 Project Structure

```
Outgrowth/
├── Models/          # Data models (Plant, Trait, Resource, etc.)
├── ViewModels/      # MVVM view models with data binding
├── Views/           # XAML pages (Garden, Breeding, Research, etc.)
├── Services/        # Business logic (PlantService, BreedingService, DataService)
├── Helpers/         # Utility classes and extensions
├── Platforms/       # Platform-specific code
└── Resources/       # Images, fonts, styles, and other assets
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
- ✅ Basic project structure
- ✅ Cross-platform foundation (Windows & Android)

### Planned (In Development)
- ⏳ A lot

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

**Last Updated**: December 24, 2025
