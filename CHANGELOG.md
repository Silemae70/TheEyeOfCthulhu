# Changelog

All notable changes to The Eye of Cthulhu will be documented in this file.

## [1.0.0] - 2024-12-27

### 🎉 Initial Release

#### Camera Sources
- DroidCam support via WiFi (MJPEG stream)
- Webcam support via USB
- Auto-reconnection when camera disconnects or phone goes to sleep
- Visual feedback during reconnection attempts

#### Pattern Matching (ElderSign)
- Template matching for fast, static object detection
- ORB feature matching for rotation/scale invariant detection
- AKAZE feature matching for robust detection in complex scenes
- Real contour tracking (not just bounding boxes)
- Save/Load models to disk (JSON + PNG)

#### Background Removal Tool
- Magic Wand (flood fill) - click on background to remove
- Auto Threshold (Otsu) - automatic detection
- GrabCut - smart foreground/background separation
- Contour extraction and simplification
- Zoom and pan controls

#### Image Processing Pipeline
- Grayscale conversion
- Gaussian blur
- Threshold (Otsu adaptive)
- Canny edge detection
- Contour detection with area filtering
- Hough circles detection with configurable parameters

#### User Interface
- Modern dark theme
- VisionView control with zoom/pan
- ROI (Region of Interest) selection
- Real-time FPS and resolution display
- Help wizard for beginners
- French/English support

#### Build System
- Inno Setup installer
- Build scripts for Debug and Release
- Version info in all assemblies
