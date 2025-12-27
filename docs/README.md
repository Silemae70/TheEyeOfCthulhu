# The Eye of Cthulhu - Documentation

## Version 1.0.0

### Features
- **DroidCam Support**: Connect to Android phone camera via WiFi
- **Webcam Support**: USB camera support
- **Auto-Reconnection**: Automatically reconnects when camera goes to sleep
- **ElderSign Pattern Matching**: 
  - Template matching (fast, no rotation)
  - ORB feature matching (rotation + scale invariant)
  - AKAZE feature matching (robust)
- **Background Remover**: Magic wand tool to extract object contours
- **ROI Selection**: Draw rectangles to select regions of interest
- **Processing Pipeline**: Grayscale, Blur, Threshold, Canny, Contours, Hough Circles
- **Zoom & Pan**: Mouse wheel zoom, middle-click/right-click pan

### Requirements
- Windows 10/11 (64-bit)
- .NET 8 Desktop Runtime
- DroidCam app on Android (for phone camera)

### Quick Start
1. Install The Eye of Cthulhu
2. Launch the application
3. **Step 1**: Connect to camera (DroidCam or Webcam)
4. **Step 2**: Create a model (load image or capture from camera)
5. **Step 3**: Enable detection

### Building from Source
```bash
# Debug build
build-debug.bat

# Release build + Setup
build.bat
```

### License
Copyright © Laser Cheval 2024-2025
