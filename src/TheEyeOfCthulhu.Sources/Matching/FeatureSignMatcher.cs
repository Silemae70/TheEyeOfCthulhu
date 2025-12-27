using System.Diagnostics;
using OpenCvSharp;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Core.Matching;
using TheEyeOfCthulhu.Sources.Processors;

namespace TheEyeOfCthulhu.Sources.Matching;

/// <summary>
/// Type de détecteur de features.
/// </summary>
public enum FeatureDetectorType
{
    /// <summary>
    /// ORB - Oriented FAST and Rotated BRIEF. Rapide, bon pour temps réel.
    /// </summary>
    ORB,
    
    /// <summary>
    /// AKAZE - Accelerated-KAZE. Plus lent mais plus robuste.
    /// </summary>
    AKAZE
}

/// <summary>
/// Matcher basé sur les features (keypoints + descriptors).
/// Invariant en rotation et en scale.
/// </summary>
public class FeatureSignMatcher : IElderSignMatcher, IDisposable
{
    private readonly FeatureDetectorType _detectorType;
    private readonly Feature2D _detector;
    private readonly BFMatcher _matcher;
    
    // Cache des descripteurs pour les ElderSigns (évite de recalculer)
    private readonly Dictionary<string, CachedTemplate> _templateCache = new();
    private readonly object _cacheLock = new();

    public string Name => $"FeatureMatch_{_detectorType}";

    /// <summary>
    /// Nombre minimum de bons matches pour valider une détection.
    /// </summary>
    public int MinGoodMatches { get; set; } = 10;

    /// <summary>
    /// Ratio test de Lowe (plus bas = plus strict).
    /// </summary>
    public float LoweRatioThreshold { get; set; } = 0.75f;

    /// <summary>
    /// Seuil RANSAC pour filtrer les outliers.
    /// </summary>
    public double RansacThreshold { get; set; } = 5.0;

    /// <summary>
    /// Nombre maximum de features à détecter.
    /// </summary>
    public int MaxFeatures { get; set; } = 500;

    public FeatureSignMatcher(FeatureDetectorType detectorType = FeatureDetectorType.ORB)
    {
        _detectorType = detectorType;
        
        _detector = detectorType switch
        {
            FeatureDetectorType.ORB => ORB.Create(MaxFeatures),
            FeatureDetectorType.AKAZE => AKAZE.Create(),
            _ => ORB.Create(MaxFeatures)
        };

        // BFMatcher avec la norme appropriée
        var normType = detectorType == FeatureDetectorType.ORB 
            ? NormTypes.Hamming 
            : NormTypes.L2;
        _matcher = new BFMatcher(normType, crossCheck: false);
    }

    public ElderSignSearchResult Search(Frame frame, ElderSign elderSign)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // Obtenir ou calculer le template caché
            var template = GetOrCreateTemplate(elderSign);
            if (template.Descriptors == null || template.Descriptors.Empty())
            {
                return ElderSignSearchResult.Empty(sw.ElapsedMilliseconds);
            }

            // Convertir la frame en Mat grayscale
            using var sceneMat = FrameMatConverter.ToMat(frame);
            using var sceneGray = sceneMat.Channels() == 1 
                ? sceneMat.Clone() 
                : sceneMat.CvtColor(ColorConversionCodes.BGR2GRAY);

            // Détecter les features dans la scène
            using var sceneKeypoints = new Mat();
            using var sceneDescriptors = new Mat();
            KeyPoint[] sceneKps;
            
            _detector.DetectAndCompute(sceneGray, null, out sceneKps, sceneDescriptors);

            if (sceneKps.Length < MinGoodMatches || sceneDescriptors.Empty())
            {
                return ElderSignSearchResult.Empty(sw.ElapsedMilliseconds);
            }

            // Matcher les descripteurs (KNN avec k=2 pour ratio test)
            var knnMatches = _matcher.KnnMatch(template.Descriptors, sceneDescriptors, k: 2);

            // Appliquer le ratio test de Lowe
            var goodMatches = new List<DMatch>();
            foreach (var knnMatch in knnMatches)
            {
                if (knnMatch.Length >= 2 && knnMatch[0].Distance < LoweRatioThreshold * knnMatch[1].Distance)
                {
                    goodMatches.Add(knnMatch[0]);
                }
            }

            if (goodMatches.Count < MinGoodMatches)
            {
                return ElderSignSearchResult.Empty(sw.ElapsedMilliseconds);
            }

            // Extraire les points correspondants
            var templatePoints = goodMatches
                .Select(m => template.Keypoints[m.QueryIdx].Pt)
                .ToArray();
            var scenePoints = goodMatches
                .Select(m => sceneKps[m.TrainIdx].Pt)
                .ToArray();

            // Calculer l'homographie avec RANSAC
            using var homography = Cv2.FindHomography(
                InputArray.Create(templatePoints),
                InputArray.Create(scenePoints),
                HomographyMethods.Ransac,
                RansacThreshold);

            if (homography.Empty())
            {
                return ElderSignSearchResult.Empty(sw.ElapsedMilliseconds);
            }

            // Calculer la position, rotation et scale à partir de l'homographie
            var result = ExtractTransformFromHomography(homography, elderSign, template);
            
            if (result == null)
            {
                return ElderSignSearchResult.Empty(sw.ElapsedMilliseconds);
            }

            // Calculer un score basé sur le nombre de inliers
            var score = Math.Min(1.0, (double)goodMatches.Count / (MinGoodMatches * 3));

            var match = new ElderSignMatch(elderSign, result.Value.Position, score)
            {
                Angle = result.Value.Angle,
                Scale = result.Value.Scale,
                TransformedCorners = result.Value.Corners
            };

            return new ElderSignSearchResult(new[] { match }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FeatureSignMatcher] Error: {ex.Message}");
            return ElderSignSearchResult.Empty(sw.ElapsedMilliseconds);
        }
    }

    public Dictionary<string, ElderSignSearchResult> SearchAll(Frame frame, IEnumerable<ElderSign> elderSigns)
    {
        var results = new Dictionary<string, ElderSignSearchResult>();
        
        foreach (var elderSign in elderSigns)
        {
            results[elderSign.Name] = Search(frame, elderSign);
        }
        
        return results;
    }

    public ElderSignSearchResult SearchMultiple(Frame frame, ElderSign elderSign, int maxMatches = 10)
    {
        // Pour l'instant, on ne supporte qu'un seul match
        // TODO: Implémenter la détection multiple avec clustering des matches
        return Search(frame, elderSign);
    }

    private CachedTemplate GetOrCreateTemplate(ElderSign elderSign)
    {
        lock (_cacheLock)
        {
            // Vérifier le cache
            if (_templateCache.TryGetValue(elderSign.Name, out var cached))
            {
                // Vérifier si le template a changé (hash simple basé sur la taille)
                if (cached.Width == elderSign.Width && cached.Height == elderSign.Height)
                {
                    return cached;
                }
            }

            // Calculer les features du template
            using var templateMat = FrameMatConverter.ToMat(elderSign.Template);
            using var templateGray = templateMat.Channels() == 1 
                ? templateMat.Clone() 
                : templateMat.CvtColor(ColorConversionCodes.BGR2GRAY);

            KeyPoint[] keypoints;
            var descriptors = new Mat();
            _detector.DetectAndCompute(templateGray, null, out keypoints, descriptors);

            var template = new CachedTemplate
            {
                Width = elderSign.Width,
                Height = elderSign.Height,
                Keypoints = keypoints,
                Descriptors = descriptors.Clone() // Clone pour garder après le using
            };

            _templateCache[elderSign.Name] = template;
            
            Debug.WriteLine($"[FeatureSignMatcher] Cached template '{elderSign.Name}': {keypoints.Length} keypoints");
            
            return template;
        }
    }

    private (PointF Position, double Angle, double Scale, PointF[] Corners)? ExtractTransformFromHomography(
        Mat homography, ElderSign elderSign, CachedTemplate template)
    {
        if (homography.Empty() || homography.Rows != 3 || homography.Cols != 3)
            return null;

        // Coins du template
        var corners = new Point2f[]
        {
            new(0, 0),
            new(template.Width, 0),
            new(template.Width, template.Height),
            new(0, template.Height)
        };

        // Transformer les coins
        var transformedCorners = Cv2.PerspectiveTransform(corners, homography);

        if (transformedCorners.Length != 4)
            return null;

        // Vérifier que le quadrilatère est valide (pas retourné, pas trop déformé)
        if (!IsValidQuadrilateral(transformedCorners))
            return null;

        // Position = coin supérieur gauche transformé
        var position = new PointF(transformedCorners[0].X, transformedCorners[0].Y);

        // Calculer l'angle (direction du bord supérieur)
        var dx = transformedCorners[1].X - transformedCorners[0].X;
        var dy = transformedCorners[1].Y - transformedCorners[0].Y;
        var angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;

        // Calculer le scale (longueur du bord supérieur vs original)
        var topEdgeLength = Math.Sqrt(dx * dx + dy * dy);
        var scale = topEdgeLength / template.Width;

        // Convertir les coins en PointF Core
        var coreCorners = transformedCorners
            .Select(c => new PointF(c.X, c.Y))
            .ToArray();

        return (position, angle, scale, coreCorners);
    }

    private bool IsValidQuadrilateral(Point2f[] corners)
    {
        if (corners.Length != 4) return false;

        // Vérifier que l'aire est positive (pas retourné)
        var area = Cv2.ContourArea(corners);
        if (area < 100) return false; // Trop petit

        // Vérifier que ce n'est pas trop déformé (ratio des côtés)
        var side1 = Distance(corners[0], corners[1]);
        var side2 = Distance(corners[1], corners[2]);
        var side3 = Distance(corners[2], corners[3]);
        var side4 = Distance(corners[3], corners[0]);

        var minSide = Math.Min(Math.Min(side1, side2), Math.Min(side3, side4));
        var maxSide = Math.Max(Math.Max(side1, side2), Math.Max(side3, side4));

        // Ratio max acceptable
        if (maxSide / minSide > 5.0) return false;

        return true;
    }

    private static double Distance(Point2f p1, Point2f p2)
    {
        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public void Dispose()
    {
        lock (_cacheLock)
        {
            foreach (var cached in _templateCache.Values)
            {
                cached.Descriptors?.Dispose();
            }
            _templateCache.Clear();
        }

        _detector.Dispose();
        _matcher.Dispose();
    }

    private class CachedTemplate
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public KeyPoint[] Keypoints { get; set; } = Array.Empty<KeyPoint>();
        public Mat? Descriptors { get; set; }
    }
}
