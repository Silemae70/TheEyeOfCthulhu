# 👁️ The Eye of Cthulhu

A modern industrial vision framework for .NET 8, providing pattern matching, image processing, and camera integration capabilities.

![Version](https://img.shields.io/badge/version-1.0.0-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/license-MIT-green)

## ✨ Features

### 📷 Camera Sources
- **DroidCam** - Use your Android phone as a wireless HD camera
- **Webcam** - USB camera support
- **Auto-Reconnection** - Automatically reconnects when camera goes to sleep

### 🔯 ElderSign Pattern Matching
- **Template Matching** - Fast, ideal for static objects
- **ORB Features** - Rotation and scale invariant
- **AKAZE Features** - Robust matching for complex scenes
- **Contour Detection** - Real object shape tracking (not just bounding boxes)

### 🪄 Background Removal Tool
- **Magic Wand** - Click to remove background (like Photoshop)
- **Auto Threshold** - Automatic background detection
- **GrabCut** - Smart foreground/background separation

### 🎨 Image Processing Pipeline
- Grayscale conversion
- Gaussian blur
- Threshold (Otsu)
- Canny edge detection
- Contour detection
- Hough circles detection

### 🖱️ User Interface
- Real-time video display with zoom/pan
- ROI (Region of Interest) selection
- Help wizard for beginners
- Dark theme UI

## 🚀 Quick Start

### Requirements
- Windows 10/11 (64-bit)
- .NET 8 Desktop Runtime
- DroidCam app on Android (for phone camera) - optional

### Installation
1. Download the latest release from [Releases](https://github.com/Silemae70/TheEyeOfCthulhu/releases)
2. Run `TheEyeOfCthulhu_Setup_1.0.0.exe`
3. Follow the installation wizard

### First Steps
1. **Connect Camera** - Enter DroidCam IP or select Webcam
2. **Create Model** - Load an image or capture from camera
3. **Enable Detection** - Watch the magic happen!

## 🔧 Building from Source

### Prerequisites
- Visual Studio 2022 or VS Code
- .NET 8 SDK
- Inno Setup 6 (for creating installer)

### Build Commands
```bash
# Debug build
build-debug.bat

# Release build + Setup
build.bat
```

## 📁 Project Structure

```
TheEyeOfCthulhu/
├── src/
│   ├── TheEyeOfCthulhu.Core/       # Core interfaces and types
│   ├── TheEyeOfCthulhu.Sources/    # Camera sources and OpenCV processors
│   ├── TheEyeOfCthulhu.WPF/        # WPF controls (VisionView)
│   └── TheEyeOfCthulhu.Lab/        # Main application
├── setup/                           # Inno Setup scripts
├── docs/                            # Documentation
└── build.bat                        # Build scripts
```

## 🎮 Controls

| Action | Control |
|--------|---------|
| Zoom | Mouse wheel |
| Pan | Middle-click + drag or Right-click + drag |
| Reset zoom | Double right-click |
| Select ROI | Left-click + drag (when ROI mode enabled) |

## 📝 License

Copyright © Laser Cheval 2024-2025

## 🙏 Acknowledgments

- [OpenCvSharp](https://github.com/shimat/opencvsharp) - OpenCV wrapper for .NET
- [DroidCam](https://www.dev47apps.com/) - Android camera app

---

*Ph'nglui mglw'nafh Cthulhu R'lyeh wgah'nagl fhtagn* 🐙
