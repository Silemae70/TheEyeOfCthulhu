using System.Diagnostics;
using OpenCvSharp;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Core.Matching;
using TheEyeOfCthulhu.Sources.Processors;
using CvPoint = OpenCvSharp.Point;

namespace TheEyeOfCthulhu.Sources.Matching;

/// <summary>
/// Matcher basé sur le Template Matching d'OpenCV.
/// Simple et rapide, mais sensible aux rotations et changements d'échelle.
/// </summary>
public class TemplateSignMatcher : IElderSignMatcher
{
    private readonly MatcherOptions _options;

    public string Name => "TemplateMatch";

    /// <summary>
    /// Méthode de matching à utiliser.
    /// </summary>
    public TemplateMatchModes MatchMethod { get; set; } = TemplateMatchModes.CCoeffNormed;

    public TemplateSignMatcher(MatcherOptions? options = null)
    {
        _options = options ?? MatcherOptions.Default;
    }

    public ElderSignSearchResult Search(Frame frame, ElderSign elderSign)
    {
        var sw = Stopwatch.StartNew();

        using var imageMat = FrameMatConverter.ToMat(frame);
        using var templateMat = FrameMatConverter.ToMat(elderSign.Template);

        // Convertir en grayscale si nécessaire pour de meilleures performances
        using var imageGray = EnsureGrayscale(imageMat);
        using var templateGray = EnsureGrayscale(templateMat);

        // Appliquer ROI si définie
        var searchArea = _options.RegionOfInterest.HasValue
            ? new Mat(imageGray, ToRect(_options.RegionOfInterest.Value))
            : imageGray;

        try
        {
            var match = PerformMatch(searchArea, templateGray, elderSign);
            sw.Stop();

            // Ajuster la position si ROI
            if (_options.RegionOfInterest.HasValue && match != null)
            {
                var roi = _options.RegionOfInterest.Value;
                match = new ElderSignMatch(
                    match.ElderSign,
                    new PointF(match.Position.X + roi.X, match.Position.Y + roi.Y),
                    match.Score);
            }

            var matches = match != null ? new[] { match } : Array.Empty<ElderSignMatch>();
            return new ElderSignSearchResult(matches, sw.Elapsed.TotalMilliseconds);
        }
        finally
        {
            if (_options.RegionOfInterest.HasValue)
            {
                searchArea.Dispose();
            }
        }
    }

    public ElderSignSearchResult SearchMultiple(Frame frame, ElderSign elderSign, int maxMatches = 10)
    {
        var sw = Stopwatch.StartNew();

        using var imageMat = FrameMatConverter.ToMat(frame);
        using var templateMat = FrameMatConverter.ToMat(elderSign.Template);
        using var imageGray = EnsureGrayscale(imageMat);
        using var templateGray = EnsureGrayscale(templateMat);

        var matches = PerformMultiMatch(imageGray, templateGray, elderSign, maxMatches);
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

    private ElderSignMatch? PerformMatch(Mat image, Mat template, ElderSign elderSign)
    {
        // Vérifier que le template est plus petit que l'image
        if (template.Width > image.Width || template.Height > image.Height)
        {
            Log($"Template '{elderSign.Name}' is larger than search area");
            return null;
        }

        using var result = new Mat();
        Cv2.MatchTemplate(image, template, result, MatchMethod);

        // Trouver le meilleur match
        Cv2.MinMaxLoc(result, out double minVal, out double maxVal, out CvPoint minLoc, out CvPoint maxLoc);

        // Pour certaines méthodes, le minimum est le meilleur match
        var (score, location) = IsMinBest(MatchMethod)
            ? (1.0 - minVal, minLoc)
            : (maxVal, maxLoc);

        var minScore = _options.MinScore ?? elderSign.MinScore;

        if (score < minScore)
        {
            Log($"Match '{elderSign.Name}': score {score:P1} < min {minScore:P1}");
            return null;
        }

        Log($"Match '{elderSign.Name}': score {score:P1} at ({location.X}, {location.Y})");
        return new ElderSignMatch(elderSign, new PointF(location.X, location.Y), score);
    }

    private List<ElderSignMatch> PerformMultiMatch(Mat image, Mat template, ElderSign elderSign, int maxMatches)
    {
        var matches = new List<ElderSignMatch>();

        if (template.Width > image.Width || template.Height > image.Height)
        {
            return matches;
        }

        using var result = new Mat();
        Cv2.MatchTemplate(image, template, result, MatchMethod);

        var minScore = _options.MinScore ?? elderSign.MinScore;
        var suppressionRadius = Math.Min(template.Width, template.Height) / 2;

        // Masque pour la suppression des non-maximum
        using var mask = new Mat(result.Size(), MatType.CV_8UC1, Scalar.All(255));

        for (int i = 0; i < maxMatches; i++)
        {
            Cv2.MinMaxLoc(result, out double minVal, out double maxVal, out CvPoint minLoc, out CvPoint maxLoc, mask);

            var (score, location) = IsMinBest(MatchMethod)
                ? (1.0 - minVal, minLoc)
                : (maxVal, maxLoc);

            if (score < minScore)
            {
                break;
            }

            matches.Add(new ElderSignMatch(elderSign, new PointF(location.X, location.Y), score));

            // Supprimer cette zone pour trouver le prochain match (Non-Maximum Suppression)
            Cv2.Circle(mask, location, suppressionRadius, Scalar.All(0), -1);
        }

        return matches;
    }

    private static Mat EnsureGrayscale(Mat mat)
    {
        if (mat.Channels() == 1)
        {
            return mat.Clone();
        }

        var gray = new Mat();
        Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }

    private static bool IsMinBest(TemplateMatchModes method)
    {
        return method == TemplateMatchModes.SqDiff || method == TemplateMatchModes.SqDiffNormed;
    }

    private static Rect ToRect(Core.Matching.Rectangle r)
    {
        return new Rect(r.X, r.Y, r.Width, r.Height);
    }

    [Conditional("DEBUG")]
    private static void Log(string message)
    {
        Debug.WriteLine($"[TemplateMatch] {message}");
    }

    public void Dispose()
    {
        // Rien à disposer
        GC.SuppressFinalize(this);
    }
}
