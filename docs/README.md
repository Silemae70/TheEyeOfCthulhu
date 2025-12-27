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
| Matching | ✅ Nouveau | 30+ |
| WPF | ✅ Fonctionnel | - |
| Lab | ✅ Fonctionnel | - |
| **Total** | **✅ Opérationnel** | **135+** |

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
│   │   ├── Recording/               # Snapshot vers fichier
│   │   ├── Utilities/               # Helpers
│   │   └── Matching/                # 🔯 Implémentations matchers
│   │       ├── TemplateSignMatcher.cs   # Template matching OpenCV
│   │       └── ElderSignProcessor.cs    # Processeur pour pipeline
│   │
│   ├── TheEyeOfCthulhu.WPF/         # Contrôles UI réutilisables
│   ├── TheEyeOfCthulhu.Lab/         # Application de démo
│   ├── TheEyeOfCthulhu.Console/     # App console de test
│   └── TheEyeOfCthulhu.Tests/       # Tests xUnit
│
└── docs/
    └── README.md                    # Ce fichier
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
var templateFrame = LoadTemplateImage(); // Ta méthode pour charger l'image
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

### Dans un Pipeline

```csharp
var processor = new ElderSignProcessor()
    .AddElderSign(elderSign1)
    .AddElderSign(elderSign2);

var pipeline = new ProcessingPipeline("Detection")
    .Add(new GrayscaleProcessor())
    .Add(processor);

var result = pipeline.Process(frame);
var found = result.GetMetadata<bool>("MaPièce.Found");
var x = result.GetMetadata<double>("MaPièce.X");
var y = result.GetMetadata<double>("MaPièce.Y");
```

### Matchers Disponibles

| Matcher | Description | Rotation | Scale | Occlusion |
|---------|-------------|----------|-------|-----------|
| `TemplateSignMatcher` | Template matching classique | ❌ | ❌ | ❌ |
| `FeatureSignMatcher` | Feature matching (ORB/AKAZE) | ✅ | ✅ | ✅ | *À venir*
| `ShapeSignMatcher` | Matching de formes (PatMax-like) | ✅ | ✅ | ✅ | *À venir*

---

## 📦 Sources Disponibles

| Source | Description | État |
|--------|-------------|------|
| `DroidCamSource` | Flux MJPEG depuis Android via WiFi | ✅ |
| `WebcamSource` | Webcam USB ou virtuelle | ✅ |
| `FileSource` | Image, vidéo, ou séquence d'images | ✅ |

---

## ⚙️ Processeurs Disponibles

| Processeur | Description |
|------------|-------------|
| `GrayscaleProcessor` | Conversion niveaux de gris |
| `GaussianBlurProcessor` | Flou gaussien |
| `ThresholdProcessor` | Seuillage binaire |
| `CannyEdgeProcessor` | Détection de contours |
| `ContourDetectorProcessor` | Extraction de contours |
| `ElderSignProcessor` | 🔯 Détection de patterns |

---

## 🧪 Tests

```bash
cd E:\DEV\TheEyeOfCthulhu
dotnet test
```

---

## 📝 Changelog

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
- [ ] Détection de cercles (HoughCircles)
- [ ] Détection de lignes (HoughLines)
- [ ] Blob detection
- [ ] ROI (Region of Interest)
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
