# 👁️ The Eye of Cthulhu

> Framework de vision industrielle .NET 8 / C# / OpenCvSharp4

---

## 📋 Statut du Projet

| Module | Statut | Tests |
|--------|--------|-------|
| Core | ✅ Stable | 30/30 |
| Sources | ✅ Stable | 35/35 |
| Processing | ✅ Stable | 24/24 |
| WPF | ✅ Fonctionnel | - |
| Lab | ✅ Fonctionnel | - |
| **Total** | **✅ Opérationnel** | **105/105** |

**Dernière mise à jour :** 2024-12-27

---

## 🏗️ Architecture

```
TheEyeOfCthulhu/
├── src/
│   ├── TheEyeOfCthulhu.Core/        # Interfaces, zéro dépendance
│   │   ├── Frame.cs                 # Structure de données image
│   │   ├── IFrameSource.cs          # Interface source vidéo
│   │   ├── IFrameRecorder.cs        # Interface enregistrement
│   │   ├── FrameSourceFactory.cs    # Factory pattern
│   │   └── Processing/
│   │       ├── IFrameProcessor.cs   # Interface processeur
│   │       ├── ProcessingResult.cs  # Résultat + métadonnées
│   │       └── ProcessingPipeline.cs # Chaînage processeurs
│   │
│   ├── TheEyeOfCthulhu.Sources/     # Implémentations OpenCV
│   │   ├── Common/
│   │   │   └── VideoCaptureSourceBase.cs  # Base factorisée
│   │   ├── DroidCam/                # Source Android
│   │   ├── Webcam/                  # Source USB/virtuelle
│   │   ├── File/                    # Image/Vidéo/Séquence
│   │   ├── Processors/              # Processeurs d'image
│   │   ├── Recording/               # Snapshot vers fichier
│   │   └── Utilities/               # Helpers (FrameViewer)
│   │
│   ├── TheEyeOfCthulhu.WPF/         # Contrôles UI réutilisables
│   │   └── Controls/
│   │       └── VisionView.xaml      # Affichage live + overlay
│   │
│   ├── TheEyeOfCthulhu.Lab/         # Application de démo
│   │   └── MainWindow.xaml          # Interface complète
│   │
│   ├── TheEyeOfCthulhu.Console/     # App console de test
│   │
│   └── TheEyeOfCthulhu.Tests/       # Tests xUnit
│
└── docs/
    └── README.md                    # Ce fichier
```

---

## 📦 Sources Disponibles

| Source | Description | État |
|--------|-------------|------|
| `DroidCamSource` | Flux MJPEG depuis Android via WiFi | ✅ |
| `WebcamSource` | Webcam USB ou virtuelle | ✅ |
| `FileSource` | Image, vidéo, ou séquence d'images | ✅ |

### Utilisation

```csharp
// DroidCam
var source = new DroidCamSource(DroidCamConfiguration.Create("192.168.1.57", 4747));

// Webcam
var source = new WebcamSource(WebcamConfiguration.Create(0));

// Fichier
var source = new FileSource(FileSourceConfiguration.FromFile("image.png"));
```

---

## ⚙️ Processeurs Disponibles

| Processeur | Description | Paramètres |
|------------|-------------|------------|
| `GrayscaleProcessor` | Conversion niveaux de gris | - |
| `GaussianBlurProcessor` | Flou gaussien | `KernelSize` (impair), `SigmaX` |
| `ThresholdProcessor` | Seuillage binaire | `ThresholdValue`, `MaxValue`, `UseOtsu` |
| `CannyEdgeProcessor` | Détection de contours | `Threshold1`, `Threshold2`, `ApertureSize` |
| `ContourDetectorProcessor` | Extraction de contours | `MinArea`, `DrawContours`, `ContourColor` |

### Utilisation Pipeline

```csharp
var pipeline = new ProcessingPipeline("Mon Pipeline")
    .Add(new GrayscaleProcessor())
    .Add(new GaussianBlurProcessor { KernelSize = 5 })
    .Add(new ThresholdProcessor { UseOtsu = true })
    .Add(new CannyEdgeProcessor { Threshold1 = 50, Threshold2 = 150 })
    .Add(new ContourDetectorProcessor { MinArea = 500, DrawContours = true });

var result = pipeline.Process(frame);
var contourCount = result.GetMetadata<int>("ContourDetector", "ContourCount");
```

---

## 🎮 Contrôle WPF

```xml
<eye:VisionView x:Name="Vision" 
                ShowInfo="True"
                ImageClicked="OnImageClicked" />
```

```csharp
Vision.SetSource(mySource);
Vision.SetPipeline(myPipeline);
await Vision.StartAsync();

// Snapshot
var frame = Vision.CaptureFrame();
```

---

## 🧪 Tests

```bash
cd E:\DEV\TheEyeOfCthulhu
dotnet test
```

**Couverture :**
- Frame : création, clone, dispose, validation
- Factory : register, create, case-insensitive
- Pipeline : add, remove, process, fluent API, metadata
- Processeurs : paramètres, validation, defaults
- FrameMatConverter : round-trip

---

## 📝 Changelog

### v0.2.0 (2024-12-27)
- ✨ Ajout projet `TheEyeOfCthulhu.Tests` (105 tests)
- ♻️ Refactoring : `VideoCaptureSourceBase` pour factoriser DroidCam/Webcam
- 🧹 Nettoyage code : logs conditionnels, validation paramètres
- 📝 Ajout documentation projet

### v0.1.0 (2024-12-26)
- 🎉 Initial : Core, Sources, WPF, Lab, Console
- ✨ Sources : DroidCam, Webcam, File
- ✨ Processeurs : Grayscale, Blur, Threshold, Canny, Contours
- ✨ Pipeline de processing modulaire
- ✨ FrameRecorder (snapshot PNG/JPEG/BMP/TIFF)
- ✨ VisionView contrôle WPF

---

## 🎯 Roadmap

### Phase 2 : Outils de Vision
- [ ] Détection de cercles (HoughCircles)
- [ ] Détection de lignes (HoughLines)
- [ ] Template matching
- [ ] Blob detection
- [ ] ROI (Region of Interest)
- [ ] Mesures (distances, dimensions)

### Phase 3 : Calibration & Précision
- [ ] Calibration caméra (distorsion)
- [ ] Conversion pixels → mm
- [ ] Correction perspective

### Phase 4 : Intégration Industrielle
- [ ] Communication avec apps .NET 4.8 (named pipes / TCP)
- [ ] Intégration Basler (Pylon SDK)
- [ ] Intégration AlliedVision (Vimba SDK)

---

## 🔧 Commandes Utiles

```bash
# Build
dotnet build

# Tests
dotnet test

# Run Lab
dotnet run --project src/TheEyeOfCthulhu.Lab

# Run Console
dotnet run --project src/TheEyeOfCthulhu.Console
```

---

## 📄 License

Projet interne Laser Cheval - Antoine
