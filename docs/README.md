# 👁️ The Eye of Cthulhu

> Framework de vision industrielle .NET 8 / C# / OpenCvSharp4

**Repository:** https://github.com/Silemae70/TheEyeOfCthulhu

---

## 📋 Statut du Projet

| Module | Statut | Tests |
|--------|--------|-------|
| Core | ✅ Stable | 30/30 |
| Sources | ✅ Stable | 35/35 |
| Processing | ✅ Stable | 24/24 |
| Matching | ✅ Stable | 30+ |
| WPF | ✅ Fonctionnel | - |
| Lab | ✅ Fonctionnel | - |
| **Total** | **✅ Opérationnel** | **143+** |

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
│   │   ├── Processing/              # Pipeline de traitement
│   │   └── Matching/                # 🔯 ElderSign (Pattern Matching)
│   │       ├── ElderSign.cs         # Template de référence
│   │       ├── ElderSignMatch.cs    # Résultats de recherche
│   │       └── IElderSignMatcher.cs # Interface matcher
│   │
│   ├── TheEyeOfCthulhu.Sources/     # Implémentations OpenCV
│   │   ├── Common/                  # Base classes factorisées
│   │   ├── DroidCam/                # Source Android
│   │   ├── Webcam/                  # Source USB/virtuelle
│   │   ├── File/                    # Image/Vidéo/Séquence
│   │   ├── Processors/              # Processeurs d'image
│   │   │   ├── BasicProcessors.cs   # Grayscale, Blur, Threshold, Canny, Contours
│   │   │   ├── HoughCirclesProcessor.cs # 🔵 Détection de cercles
│   │   │   └── FrameMatConverter.cs # Conversion Frame ↔ Mat
│   │   ├── Recording/               # Snapshot vers fichier
│   │   ├── Utilities/               # Helpers
│   │   └── Matching/                # 🔯 Implémentations matchers
│   │       ├── TemplateSignMatcher.cs   # Template matching OpenCV
│   │       └── ElderSignProcessor.cs    # Processeur async pour pipeline
│   │
│   ├── TheEyeOfCthulhu.WPF/         # Contrôles UI réutilisables
│   │   └── Controls/
│   │       └── VisionView.xaml      # 🎥 Contrôle vidéo avec zoom/pan/ROI
│   │
│   ├── TheEyeOfCthulhu.Lab/         # Application de démo
│   ├── TheEyeOfCthulhu.Console/     # App console de test
│   └── TheEyeOfCthulhu.Tests/       # Tests xUnit
│
└── docs/
    └── README.md                    # Ce fichier
```

---

## 🎥 VisionView - Contrôle Vidéo

Le **VisionView** est le contrôle WPF principal pour afficher un flux vidéo avec des fonctionnalités avancées.

### Fonctionnalités

| Feature | Contrôle | Description |
|---------|----------|-------------|
| **Zoom** | 🖱️ Molette | Zoom 50% → 1000%, centré sur le curseur |
| **Pan** | 🖱️ Clic molette + glisser | Déplacer l'image zoomée |
| **Reset Zoom** | 🖱️ Double-clic droit | Retour à 100% |
| **ROI Selection** | 🖱️ Clic gauche + glisser | Sélectionner une zone d'intérêt |
| **Capture ROI** | Code | Capturer uniquement la zone sélectionnée |

### Utilisation

```csharp
// Définir la source
VisionView.SetSource(myFrameSource);

// Définir le pipeline de traitement (optionnel)
VisionView.SetPipeline(myPipeline);

// Démarrer
await VisionView.StartAsync();

// Activer la sélection ROI
VisionView.RoiSelectionEnabled = true;

// Capturer (ROI ou frame complète)
var frame = VisionView.CaptureRoi();
var originalFrame = VisionView.CaptureOriginalFrame(); // Avant pipeline
```

---

## 🔯 ElderSign - Pattern Matching

L'**ElderSign** est le système de pattern matching du framework. Il permet de retrouver un modèle de référence (template) dans une image.

### Concepts

- **ElderSign** : Le template/modèle à rechercher (le "golden sample")
- **ElderSignMatch** : Un résultat de recherche (position, score, angle...)
- **IElderSignMatcher** : Interface pour les algorithmes de matching

### Utilisation

```csharp
// 1. Créer un ElderSign depuis une image de référence
var templateFrame = LoadTemplateImage();
var elderSign = new ElderSign("MaPièce", templateFrame)
{
    MinScore = 0.8  // Score minimum pour valider un match
};

// 2. Créer un matcher
var matcher = new TemplateSignMatcher();

// 3. Rechercher dans une image
var result = matcher.Search(currentFrame, elderSign);

if (result.Found)
{
    var match = result.BestMatch;
    Console.WriteLine($"Trouvé à ({match.AnchorPosition.X}, {match.AnchorPosition.Y})");
    Console.WriteLine($"Score: {match.Score:P0}");
}
```

### Dans un Pipeline (Async, Non-bloquant)

```csharp
var processor = new ElderSignProcessor()
{
    FrameSkip = 3,      // Matcher toutes les 4 frames (performance)
    DrawMatches = true,
    ShowLabel = true
};
processor.AddElderSign(elderSign);

var pipeline = new ProcessingPipeline("Detection")
    .Add(processor);

// Les résultats sont dans les métadonnées
var result = pipeline.Process(frame);
var found = result.GetMetadata<bool>("ElderSignDetector", "MaPièce.Found");
var x = result.GetMetadata<double>("ElderSignDetector", "MaPièce.X");
var y = result.GetMetadata<double>("ElderSignDetector", "MaPièce.Y");
```

### Matchers Disponibles

| Matcher | Description | Rotation | Scale | Occlusion |
|---------|-------------|----------|-------|-----------|
| `TemplateSignMatcher` | Template matching classique | ❌ | ❌ | ❌ |
| `FeatureSignMatcher` | Feature matching (ORB/AKAZE) | ✅ | ✅ | ✅ | *À venir*
| `ShapeSignMatcher` | Matching de formes (PatMax-like) | ✅ | ✅ | ✅ | *À venir*

---

## 🔵 HoughCircles - Détection de Cercles

Le **HoughCirclesProcessor** détecte les cercles dans l'image avec la transformée de Hough.

### Utilisation

```csharp
var processor = new HoughCirclesProcessor
{
    MinRadius = 20,           // Rayon minimum
    MaxRadius = 200,          // Rayon maximum (0 = pas de max)
    AccumulatorThreshold = 50, // Sensibilité (plus bas = plus de détections)
    DrawCircles = true,
    ShowInfo = true
};

var pipeline = new ProcessingPipeline("Circles")
    .Add(processor);

var result = pipeline.Process(frame);

// Résultats
var count = result.GetMetadata<int>("HoughCircles", "CircleCount");
var largestRadius = result.GetMetadata<float>("HoughCircles", "LargestCircle.Radius");
var largestDiameter = result.GetMetadata<float>("HoughCircles", "LargestCircle.Diameter");
var centerX = result.GetMetadata<float>("HoughCircles", "LargestCircle.X");
var centerY = result.GetMetadata<float>("HoughCircles", "LargestCircle.Y");
```

### Paramètres

| Paramètre | Défaut | Description |
|-----------|--------|-------------|
| `Dp` | 1.0 | Résolution accumulateur (1 = même que image) |
| `MinDist` | 50 | Distance min entre centres |
| `CannyThreshold` | 100 | Seuil Canny interne |
| `AccumulatorThreshold` | 50 | Sensibilité (↓ = + détections) |
| `MinRadius` | 0 | Rayon minimum |
| `MaxRadius` | 0 | Rayon maximum (0 = illimité) |
| `MaxCircles` | 10 | Nombre max de cercles |
| `ApplyBlur` | true | Blur avant détection |

---

## 📦 Sources Disponibles

| Source | Description | État |
|--------|-------------|------|
| `DroidCamSource` | Flux MJPEG depuis Android via WiFi | ✅ |
| `WebcamSource` | Webcam USB ou virtuelle | ✅ |
| `FileSource` | Image, vidéo, ou séquence d'images | ✅ |

### DroidCam - Timeout et Messages d'erreur

```csharp
var config = DroidCamConfiguration.Create("192.168.1.57", 4747);
config.ConnectionTimeoutSeconds = 10; // Timeout connexion

var source = new DroidCamSource(config);
// Messages d'erreur détaillés avec checklist si échec
```

---

## ⚙️ Processeurs Disponibles

| Processeur | Description |
|------------|-------------|
| `GrayscaleProcessor` | Conversion niveaux de gris |
| `GaussianBlurProcessor` | Flou gaussien |
| `ThresholdProcessor` | Seuillage binaire (Otsu supporté) |
| `CannyEdgeProcessor` | Détection de contours |
| `ContourDetectorProcessor` | Extraction de contours |
| `HoughCirclesProcessor` | 🔵 Détection de cercles |
| `ElderSignProcessor` | 🔯 Détection de patterns (async) |

---

## 🧪 Tests

```bash
cd E:\DEV\TheEyeOfCthulhu
dotnet test
```

**143+ tests unitaires** couvrant Core, Sources, Processing et Matching.

---

## 📝 Changelog

### v0.4.0 (2024-12-27) - 🔵 CIRCLES & ZOOM
- ✨ **HoughCirclesProcessor** : Détection de cercles avec paramètres ajustables
- ✨ **VisionView Zoom** : Zoom molette (50%-1000%), pan clic molette, reset double-clic droit
- ✨ **VisionView ROI** : Sélection rectangulaire, capture ROI, coordonnées temps réel
- ✨ **ElderSignProcessor async** : Non-bloquant avec frame skip (UI fluide)
- ✨ **DroidCam timeout** : Messages d'erreur détaillés avec checklist
- 🔧 **Capture frame originale** : `CaptureOriginalFrame()` avant pipeline
- 🔧 **Lab UI** : Sliders HoughCircles avec mise à jour au relâchement
- 📝 Documentation mise à jour

### v0.3.0 (2024-12-27) - 🔯 THE ELDER FOR THE POWER
- ✨ Ajout système **ElderSign** (Pattern Matching)
  - `ElderSign` : Template de référence
  - `ElderSignMatch` : Résultats de recherche
  - `TemplateSignMatcher` : Template matching OpenCV
  - `ElderSignProcessor` : Intégration pipeline
- ✨ Types géométriques : `Point`, `PointF`, `Rectangle`
- 🧪 Tests unitaires ElderSign (~30 tests)
- 📝 Documentation mise à jour

### v0.2.0 (2024-12-27)
- ✨ Ajout projet `TheEyeOfCthulhu.Tests` (105 tests)
- ♻️ Refactoring : `VideoCaptureSourceBase`
- 🧹 Nettoyage code
- 📝 Ajout documentation projet
- 🔗 Repository GitHub créé

### v0.1.0 (2024-12-26)
- 🎉 Initial : Core, Sources, WPF, Lab, Console
- ✨ Sources : DroidCam, Webcam, File
- ✨ Processeurs de base
- ✨ Pipeline de processing

---

## 🎯 Roadmap

### Phase 2 : Matchers Avancés
- [ ] `FeatureSignMatcher` (ORB/AKAZE) - Rotation + Scale
- [ ] `ShapeSignMatcher` - Style PatMax
- [ ] Multi-scale / Multi-angle search

### Phase 3 : Outils de Vision
- [x] ~~Détection de cercles (HoughCircles)~~ ✅
- [ ] Détection de lignes (HoughLines)
- [ ] Blob detection
- [x] ~~ROI (Region of Interest)~~ ✅
- [ ] Mesures (distances, dimensions)

### Phase 4 : Calibration & Précision
- [ ] Calibration caméra
- [ ] Conversion pixels → mm
- [ ] Correction perspective

### Phase 5 : Intégration Industrielle
- [ ] Communication avec apps .NET 4.8
- [ ] Intégration Basler / AlliedVision

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
