# Outgrowth - Project Context Document

**Last Updated**: December 24, 2025  
**Framework**: .NET 9.0 MAUI  
**Platforms**: Windows & Android  
**Status**: Initial Setup Phase

---

## 1. Project Overview

### 1.1 Concept
**Outgrowth** As the lone occupant of a remote research station, your mission is to engineer and cultivate a resilient ecosystem of fictional plants capable of surviving the harsh vacuum of deep space. Through selective breeding and resource management, you must develop a self-sustaining "Green Ark" to terraform the void

### 1.2 Core Gameplay Loop
1. **Cultivation**: Not yet
2. **Breeding**: Not yet
3. **Trading**: Not yet

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
- No obsolete methods or controls

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
    ├── MainPage.xaml / MainPage.xaml.cs  # Default main page
    ├── GlobalXmlns.cs              # Global XAML namespace definitions
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
- Currently contains single ShellContent pointing to MainPage

---

## 5. Project Features

### 5.1 Core Features

Nothing in there yet

---

## 6. Known Limitations & Future Considerations

### 6.1 Current Limitations
- No online features (by design - solo experience)
- No real-time graphics rendering
- Limited by .NET MAUI standard library
- Android performance may vary on low-end devices

---

## 7. Quick Reference

### 7.1 Important File Locations

| Purpose | File Path |
|---------|-----------|
| Main navigation | `Outgrowth/AppShell.xaml` |
| App initialization | `Outgrowth/MauiProgram.cs` |
| Project config | `Outgrowth/Outgrowth.csproj` |
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

