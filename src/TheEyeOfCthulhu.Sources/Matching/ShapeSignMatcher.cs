using System.Diagnostics;
using OpenCvSharp;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Core.Matching;
using TheEyeOfCthulhu.Sources.Processors;
using CvPoint = OpenCvSharp.Point;

namespace TheEyeOfCthulhu.Sources.Matching;

/// <summary>
/// Matcher basé sur la comparaison de formes (contours) via les moments de Hu.
/// Idéal pour les formes simples en noir et blanc, invariant rotation/scale.
/// </summary>
public class ShapeSignMatcher : IElderSignMatcher
{
    private readonly MatcherOptions _options;
    private readonly ShapeMatchModes _matchMode;
    private readonly double _cannyThreshold1;
    private readonly double _cannyThreshold2;
    private readonly int _dilationSize;
    private readonly double _minContourArea;

    public string Name => "ShapeMatch";

    /// <summary>
    /// Crée un matcher de formes.
    /// </summary>
    public ShapeSignMatcher(
        MatcherOptions? options = null,
        ShapeMatchModes matchMode = ShapeMatchModes.I1,
        double cannyThreshold1 = 50,
        double cannyThreshold2 = 150,
        int dilationSize = 2,
        double minContourArea = 100)
    {
        _options = options ?? MatcherOptions.Default;
        _matchMode = matchMode;
        _cannyThreshold1 = cannyThreshold1;
        _cannyThreshold2 = cannyThreshold2;
        _dilationSize = dilationSize;
        _minContourArea = minContourArea;
    }

    public ElderSignSearchResult Search(Frame frame, ElderSign elderSign)
    {
        var sw = Stopwatch.StartNew();

        // Vérifier qu'on a un contour de référence
        if (elderSign.ContourPoints == null || elderSign.ContourPoints.Length < 3)
        {
            Log($"ElderSign '{elderSign.Name}' has no contour - cannot use ShapeMatch");
            sw.Stop();
            return ElderSignSearchResult.Empty(sw.Elapsed.TotalMilliseconds);
        }

        // Convertir le contour de référence en format OpenCV
        var referenceContour = elderSign.ContourPoints
            .Select(p => new CvPoint((int)p.X, (int)p.Y))
            .ToArray();

        using var frameMat = FrameMatConverter.ToMat(frame);
        
        // Appliquer ROI si définie
        Mat searchArea;
        bool usingRoi = false;
        int roiOffsetX = 0, roiOffsetY = 0;
        
        if (_options.RegionOfInterest.HasValue)
        {
            var roi = _options.RegionOfInterest.Value;
            roiOffsetX = roi.X;
            roiOffsetY = roi.Y;
            searchArea = new Mat(frameMat, new Rect(roi.X, roi.Y, roi.Width, roi.Height));
            usingRoi = true;
            Log($"Searching in ROI: {roi.X},{roi.Y} {roi.Width}x{roi.Height}");
        }
        else
        {
            searchArea = frameMat;
        }

        try
        {
            // Trouver les contours dans l'image (ou ROI)
            var contours = FindContours(searchArea);

            if (contours.Length == 0)
            {
                sw.Stop();
                return ElderSignSearchResult.Empty(sw.Elapsed.TotalMilliseconds);
            }

            // Chercher le meilleur match
            var match = FindBestMatch(contours, referenceContour, elderSign, roiOffsetX, roiOffsetY);
            sw.Stop();

            var matches = match != null ? new[] { match } : Array.Empty<ElderSignMatch>();
            return new ElderSignSearchResult(matches, sw.Elapsed.TotalMilliseconds);
        }
        finally
        {
            if (usingRoi)
            {
                searchArea.Dispose();
            }
        }
    }

    public ElderSignSearchResult SearchMultiple(Frame frame, ElderSign elderSign, int maxMatches = 10)
    {
        var sw = Stopwatch.StartNew();

        if (elderSign.ContourPoints == null || elderSign.ContourPoints.Length < 3)
        {
            sw.Stop();
            return ElderSignSearchResult.Empty(sw.Elapsed.TotalMilliseconds);
        }

        var referenceContour = elderSign.ContourPoints
            .Select(p => new CvPoint((int)p.X, (int)p.Y))
            .ToArray();

        using var frameMat = FrameMatConverter.ToMat(frame);
        var contours = FindContours(frameMat);

        if (contours.Length == 0)
        {
            sw.Stop();
            return ElderSignSearchResult.Empty(sw.Elapsed.TotalMilliseconds);
        }

        var matches = FindMultipleMatches(contours, referenceContour, elderSign, maxMatches);
        sw.Stop();

        return new ElderSignSearchResult(matches, sw.Elapsed.TotalMilliseconds);
    }

    public Dictionary<string, ElderSignSearchResult> SearchAll(Frame frame, IEnumerable<ElderSign> elderSigns)
    {
        var results = new Dictionary<string, ElderSignSearchResult>();

        foreach (var sign in elderSigns)
        {
            results[sign.Name] = Search(frame, sign);
        }

        return results;
    }

    private CvPoint[][] FindContours(Mat frameMat)
    {
        // Convertir en grayscale si nécessaire
        using var gray = frameMat.Channels() == 1
            ? frameMat.Clone()
            : frameMat.CvtColor(ColorConversionCodes.BGR2GRAY);

        // Blur pour réduire le bruit
        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);

        // Détection des contours avec Canny
        using var edges = new Mat();
        Cv2.Canny(blurred, edges, _cannyThreshold1, _cannyThreshold2);

        // Dilatation optionnelle pour connecter les contours
        using var processed = new Mat();
        if (_dilationSize > 0)
        {
            using var kernel = Cv2.GetStructuringElement(
                MorphShapes.Rect,
                new Size(_dilationSize * 2 + 1, _dilationSize * 2 + 1));
            Cv2.Dilate(edges, processed, kernel);
        }
        else
        {
            edges.CopyTo(processed);
        }

        // Trouver tous les contours (y compris intérieurs)
        Cv2.FindContours(processed, out var contours, out _,
            RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

        return contours;
    }

    private ElderSignMatch? FindBestMatch(CvPoint[][] contours, CvPoint[] referenceContour, ElderSign elderSign, int offsetX = 0, int offsetY = 0)
    {
        double bestMatchScore = double.MaxValue; // Plus bas = meilleur pour MatchShapes
        CvPoint[]? bestContour = null;

        foreach (var contour in contours)
        {
            // Filtrer par aire minimum
            var area = Cv2.ContourArea(contour);
            if (area < _minContourArea)
                continue;

            // Simplifier le contour
            var epsilon = 0.01 * Cv2.ArcLength(contour, true);
            var simplified = Cv2.ApproxPolyDP(contour, epsilon, true);

            // Comparer les formes avec les moments de Hu
            var matchScore = Cv2.MatchShapes(referenceContour, simplified, _matchMode, 0);

            if (matchScore < bestMatchScore)
            {
                bestMatchScore = matchScore;
                bestContour = simplified;
            }
        }

        if (bestContour == null)
        {
            return null;
        }

        return CreateMatch(bestContour, referenceContour, bestMatchScore, elderSign, offsetX, offsetY);
    }

    private List<ElderSignMatch> FindMultipleMatches(CvPoint[][] contours, CvPoint[] referenceContour, ElderSign elderSign, int maxMatches)
    {
        var candidates = new List<(CvPoint[] contour, double score)>();

        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < _minContourArea)
                continue;

            var epsilon = 0.01 * Cv2.ArcLength(contour, true);
            var simplified = Cv2.ApproxPolyDP(contour, epsilon, true);

            var matchScore = Cv2.MatchShapes(referenceContour, simplified, _matchMode, 0);
            candidates.Add((simplified, matchScore));
        }

        // Trier par score (plus bas = meilleur)
        var sorted = candidates
            .OrderBy(c => c.score)
            .Take(maxMatches)
            .ToList();

        var matches = new List<ElderSignMatch>();
        foreach (var (contour, score) in sorted)
        {
            var match = CreateMatch(contour, referenceContour, score, elderSign);
            if (match != null)
            {
                matches.Add(match);
            }
        }

        return matches;
    }

    private ElderSignMatch? CreateMatch(CvPoint[] matchedContour, CvPoint[] referenceContour, double rawScore, ElderSign elderSign, int offsetX = 0, int offsetY = 0)
    {
        // Convertir le score MatchShapes en score 0-1 (inversé car plus bas = meilleur)
        // MatchShapes retourne typiquement 0-0.5 pour des formes similaires, >1 pour très différentes
        var normalizedScore = 1.0 / (1.0 + rawScore * 5); // Ajuster le facteur pour plus de sensibilité

        var minScore = _options.MinScore ?? elderSign.MinScore;

        if (normalizedScore < minScore)
        {
            Log($"Shape match '{elderSign.Name}': score {normalizedScore:P1} < min {minScore:P1}");
            return null;
        }

        // Calculer le centre
        var moments = Cv2.Moments(matchedContour);
        PointF position;
        if (moments.M00 > 0)
        {
            position = new PointF(
                (float)(moments.M10 / moments.M00) + offsetX,
                (float)(moments.M01 / moments.M00) + offsetY);
        }
        else
        {
            var rect = Cv2.BoundingRect(matchedContour);
            position = new PointF(rect.X + rect.Width / 2f + offsetX, rect.Y + rect.Height / 2f + offsetY);
        }

        // Estimer l'échelle (ratio des aires)
        var matchArea = Cv2.ContourArea(matchedContour);
        var refArea = Cv2.ContourArea(referenceContour);
        var scale = refArea > 0 ? Math.Sqrt(matchArea / refArea) : 1.0;

        // Estimer l'angle via le rectangle englobant orienté
        var refRect = Cv2.MinAreaRect(referenceContour);
        var matchRect = Cv2.MinAreaRect(matchedContour);
        var angle = matchRect.Angle - refRect.Angle;

        // Normaliser l'angle
        while (angle > 180) angle -= 360;
        while (angle < -180) angle += 360;

        // Calculer les coins transformés (avec offset ROI)
        var boundingRect = Cv2.BoundingRect(matchedContour);
        var corners = new[]
        {
            new PointF(boundingRect.Left + offsetX, boundingRect.Top + offsetY),
            new PointF(boundingRect.Right + offsetX, boundingRect.Top + offsetY),
            new PointF(boundingRect.Right + offsetX, boundingRect.Bottom + offsetY),
            new PointF(boundingRect.Left + offsetX, boundingRect.Bottom + offsetY)
        };

        Log($"Shape match '{elderSign.Name}': score {normalizedScore:P1} at ({position.X:F0}, {position.Y:F0})");

        return new ElderSignMatch(elderSign, position, normalizedScore)
        {
            Angle = angle,
            Scale = scale,
            TransformedCorners = corners
        };
    }

    [Conditional("DEBUG")]
    private static void Log(string message)
    {
        Debug.WriteLine($"[ShapeMatch] {message}");
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
