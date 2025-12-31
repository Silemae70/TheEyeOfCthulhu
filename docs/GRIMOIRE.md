# 📜 The Grimoire - Lexique Sacré de TheEyeOfCthulhu

> *"Ph'nglui mglw'nafh Cthulhu R'lyeh wgah'nagl fhtagn"*
> *"Dans sa demeure de R'lyeh, le défunt Cthulhu attend en rêvant"*

Ce document établit la correspondance entre les termes industriels standards 
et notre nomenclature unique inspirée du mythe de Cthulhu.

---

## 🔮 Termes Principaux

| Notre Terme | Industrie (Cognex/InVision) | Description |
|-------------|----------------------------|-------------|
| **Ritual** | Job, Program, Sequence | Programme de vision complet à exécuter |
| **Rune** | Tool, Task, Operation | Opération élémentaire de vision |
| **ElderSign** | Pattern, Template, Model | Modèle/template de référence pour matching |
| **Glyph** | ROI, Region, Zone | Zone d'intérêt dans l'image |
| **Tome** | Golden Sample, Reference | Image de référence "parfaite" |
| **Prophecy** | Result, Verdict | Résultat global d'un Ritual |
| **Vision** | Task Result | Résultat d'une Rune individuelle |
| **Resonance** | Score, Confidence, Match% | Niveau de correspondance (0-100%) |
| **Awakened** | Pass, Found, OK | Détection réussie / Contrôle OK |
| **Dormant** | Fail, Not Found, NOK | Détection échouée / Contrôle NOK |
| **Cultist** | Operator, User | Utilisateur du système |
| **Altar** | Station, Camera Setup | Configuration caméra/éclairage |
| **Summoning** | Trigger, Acquisition | Déclenchement capture image |

---

## 🔷 Types de Runes

| Notre Rune | Équivalent Industrie | Fonction | Status |
|------------|---------------------|----------|--------|
| **SummonRune** | PatMax, Locate, Find | Localiser une pièce/pattern | ✅ Implémenté |
| **ElderSignRune** | Pattern Match, Template Match | Matcher un ElderSign | ✅ Implémenté |
| **PresenceRune** | Presence/Absence Check | Vérifier présence/absence | ✅ Implémenté |
| **WhisperRune** | OCR, Text Read | Reconnaissance de caractères | ⏳ À faire |
| **MeasureRune** | Caliper, Measure, Gauge | Mesures dimensionnelles | ⏳ À faire |
| **GazeRune** | Inspect, Defect Detection | Inspection défauts/qualité | ⏳ À faire |
| **PortalRune** | Barcode, QR, DataMatrix | Lecture codes-barres/2D | ⏳ À faire |
| **CountRune** | Blob, Count | Comptage d'éléments | ⏳ À faire |
| **ColorRune** | Color Check, Histogram | Analyse couleur | ⏳ À faire |
| **GeometryRune** | Edge, Circle, Line Find | Détection formes géométriques | ⏳ À faire |

---

## 🌟 États et Résultats

| Notre Terme | Standard | Usage |
|-------------|----------|-------|
| **Awakened** | PASS | Le contrôle est validé |
| **Dormant** | FAIL | Le contrôle a échoué |
| **Uncertain** | WARN | Résultat incertain (score limite) |
| **Void** | ERROR | Erreur d'exécution |
| **Resonance** | Score | 0.0 à 1.0 (ou 0% à 100%) |
| **Threshold** | Min Score | Seuil minimum de Resonance |

---

## 📁 Structure des Fichiers

| Extension | Contenu |
|-----------|---------|
| `.ritual` | Définition d'un Ritual (JSON) |
| `.eldersign` | ElderSign sauvegardé (JSON + PNG) |
| `.tome` | Golden sample avec métadonnées |
| `.glyph` | Définition de zones ROI |

---

## 🏛️ Architecture Conceptuelle

```
                    ┌─────────────────┐
                    │     RITUAL      │
                    │  (Programme)    │
                    └────────┬────────┘
                             │
            ┌────────────────┼────────────────┐
            │                │                │
       ┌────▼────┐     ┌────▼────┐     ┌────▼────┐
       │  RUNE   │     │  RUNE   │     │  RUNE   │
       │ Summon  │────▶│ Elder   │────▶│ Whisper │
       └────┬────┘     └────┬────┘     └────┬────┘
            │                │                │
       ┌────▼────┐     ┌────▼────┐     ┌────▼────┐
       │ VISION  │     │ VISION  │     │ VISION  │
       │(Result) │     │(Result) │     │(Result) │
       └────┬────┘     └────┬────┘     └────┬────┘
            │                │                │
            └────────────────┼────────────────┘
                             │
                    ┌────────▼────────┐
                    │    PROPHECY     │
                    │ (Résultat Final)│
                    │  Awakened/Dormant│
                    └─────────────────┘
```

---

## 💡 Exemples d'Usage

### Ritual simple : Vérifier présence logo "OCB"
```
Ritual: "OCB_Check"
├── Rune 1: SummonRune (localiser le paquet)
├── Rune 2: ElderSignRune (matcher "OCB")
└── Prophecy: Awakened si Resonance > 75%
```

### Ritual complexe : Contrôle qualité complet
```
Ritual: "QC_Complete"
├── Rune 1: SummonRune (localiser pièce)
├── Rune 2: WhisperRune (lire numéro série)
├── Rune 3: MeasureRune (vérifier dimensions)
├── Rune 4: GazeRune (détecter défauts)
├── Rune 5: PortalRune (lire DataMatrix)
└── Prophecy: Awakened si TOUTES les Visions sont Awakened
```

---

## 🔄 Changelog Nomenclature

| Version | Date | Changements |
|---------|------|-------------|
| 1.0 | 2024-12-30 | Création initiale du Grimoire |

---

*"That is not dead which can eternal lie, and with strange aeons even death may die."*
— Abdul Alhazred, Necronomicon
