using System.Diagnostics;
using OpenCvSharp;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Core.Matching;
using TheEyeOfCthulhu.Core.Processing;
using TheEyeOfCthulhu.Sources.Processors;
using CvPoint = OpenCvSharp.Point;
using CorePointF = TheEyeOfCthulhu.Core.PointF;

namespace TheEyeOfCthulhu.Sources.Matching;

/// <summary>
/// Processeur qui recherche des ElderSigns dans les frames et dessine les résultats.
/// Non-bloquant : le matching tourne en arrière-plan et on dessine le dernier résultat connu.
/// </summary>
public class ElderSignProcessor : FrameProcessorBase
{
    private readonly IElderSignMatcher _matcher;
    private readonly List<ElderSign> _elderSigns = new();
    private readonly object _resultLock = new();
    
    // Cache du dernier résultat pour ne pas bloquer
    private Dictionary<string, ElderSignSearchResult>? _lastResults;
    private bool _isSearching;
    private long _lastSearchFrame;
    
    public override string Name => "ElderSignDetector";

    /// <summary>
    /// Nombre de frames à skipper entre chaque recherche (0 = chaque frame).
    /// </summary>
    public int FrameSkip { get; set; } = 3;

    /// <summary>
    /// Dessiner les matches sur l'image de sortie.
    /// </summary>
    public bool DrawMatches { get; set; } = true;

    /// <summary>
    /// Couleur du rectangle de match (BGR).
    /// </summary>
    public Scalar MatchColor { get; set; } = new Scalar(0, 255, 0); // Vert

    /// <summary>
    /// Couleur du point d'ancrage (BGR).
    /// </summary>
    public Scalar AnchorColor { get; set; } = new Scalar(0, 0, 255); // Rouge

    /// <summary>
    /// Couleur quand non trouvé (BGR).
    /// </summary>
    public Scalar NotFoundColor { get; set; } = new Scalar(0, 0, 128); // Rouge foncé

    /// <summary>
    /// Épaisseur des lignes.
    /// </summary>
    public int LineThickness { get; set; } = 2;

    /// <summary>
    /// Afficher le nom et le score sur l'image.
    /// </summary>
    public bool ShowLabel { get; set; } = true;

    /// <summary>
    /// Dessiner le template transformé (overlay semi-transparent) sur la détection.
    /// </summary>
    public bool DrawTemplateOverlay { get; set; } = false;

    /// <summary>
    /// Opacité de l'overlay du template (0.0 - 1.0).
    /// </summary>
    public double TemplateOverlayOpacity { get; set; } = 0.5;

    public ElderSignProcessor(IElderSignMatcher? matcher = null)
    {
        _matcher = matcher ?? new TemplateSignMatcher();
    }

    /// <summary>
    /// Ajoute un ElderSign à rechercher.
    /// </summary>
    public ElderSignProcessor AddElderSign(ElderSign elderSign)
    {
        _elderSigns.Add(elderSign);
        return this;
    }

    /// <summary>
    /// Retire un ElderSign.
    /// </summary>
    public bool RemoveElderSign(string name)
    {
        var sign = _elderSigns.FirstOrDefault(s => s.Name == name);
        if (sign != null)
        {
            _elderSigns.Remove(sign);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Vide la liste des ElderSigns.
    /// </summary>
    public void ClearElderSigns()
    {
        _elderSigns.Clear();
        lock (_resultLock)
        {
            _lastResults = null;
        }
    }

    /// <summary>
    /// Liste des ElderSigns enregistrés.
    /// </summary>
    public IReadOnlyList<ElderSign> ElderSigns => _elderSigns;

    protected override (Frame Frame, Dictionary<string, object>? Metadata) ProcessCore(Frame input)
    {
        if (_elderSigns.Count == 0)
        {
            return (input, null);
        }

        // Lancer une recherche si on n'est pas déjà en train de chercher et si assez de frames sont passées
        var shouldSearch = !_isSearching && (input.FrameNumber - _lastSearchFrame >= FrameSkip);
        
        if (shouldSearch)
        {
            _isSearching = true;
            _lastSearchFrame = input.FrameNumber;
            
            // Cloner la frame pour le thread de recherche
            var frameClone = input.Clone();
            var signsSnapshot = _elderSigns.ToList();
            
            // Lancer la recherche en arrière-plan
            Task.Run(() =>
            {
                try
                {
                    var results = _matcher.SearchAll(frameClone, signsSnapshot);
                    
                    lock (_resultLock)
                    {
                        _lastResults = results;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ElderSign] Search error: {ex.Message}");
                }
                finally
                {
                    frameClone.Dispose();
                    _isSearching = false;
                }
            });
        }

        // Utiliser le dernier résultat connu (peut être null au début)
        Dictionary<string, ElderSignSearchResult>? currentResults;
        lock (_resultLock)
        {
            currentResults = _lastResults;
        }

        // Construire les métadonnées
        var metadata = new Dictionary<string, object>();
        
        if (currentResults != null)
        {
            metadata["MatchCount"] = currentResults.Values.Sum(r => r.Found ? 1 : 0);
            metadata["TotalSearchTimeMs"] = currentResults.Values.Sum(r => r.SearchTimeMs);
            metadata["Results"] = currentResults;

            foreach (var (name, result) in currentResults)
            {
                if (result.Found && result.BestMatch != null)
                {
                    var match = result.BestMatch;
                    metadata[$"{name}.Found"] = true;
                    metadata[$"{name}.X"] = match.AnchorPosition.X;
                    metadata[$"{name}.Y"] = match.AnchorPosition.Y;
                    metadata[$"{name}.Score"] = match.Score;
                    metadata[$"{name}.Angle"] = match.Angle;
                    metadata[$"{name}.Scale"] = match.Scale;
                }
                else
                {
                    metadata[$"{name}.Found"] = false;
                }
            }
        }
        else
        {
            // Pas encore de résultat
            metadata["MatchCount"] = 0;
            foreach (var sign in _elderSigns)
            {
                metadata[$"{sign.Name}.Found"] = false;
            }
        }

        if (!DrawMatches || currentResults == null)
        {
            return (input, metadata);
        }

        // Dessiner les résultats
        using var mat = FrameMatConverter.ToMat(input);
        
        // Convertir en couleur si grayscale
        using var colorMat = mat.Channels() == 1 
            ? mat.CvtColor(ColorConversionCodes.GRAY2BGR) 
            : mat.Clone();

        foreach (var (name, result) in currentResults)
        {
            DrawResult(colorMat, name, result);
        }

        var outputFrame = FrameMatConverter.ToFrame(colorMat, input.FrameNumber);
        return (outputFrame, metadata);
    }

    private void DrawResult(Mat mat, string name, ElderSignSearchResult result)
    {
        if (result.Found && result.BestMatch != null)
        {
            var match = result.BestMatch;
            var elderSign = match.ElderSign;

            // Dessiner l'overlay du template si activé
            if (DrawTemplateOverlay && match.TransformedCorners != null && match.TransformedCorners.Length == 4)
            {
                DrawTransformedTemplate(mat, match);
            }

            // Dessiner le contour/forme
            if (elderSign.ContourPoints != null && elderSign.ContourPoints.Length >= 3 
                && match.TransformedCorners != null && match.TransformedCorners.Length == 4)
            {
                // On a un vrai contour ET une transformation -> dessiner le contour transformé
                DrawTransformedContour(mat, elderSign.ContourPoints, match.TransformedCorners, elderSign.Width, elderSign.Height, MatchColor);
            }
            else if (match.TransformedCorners != null && match.TransformedCorners.Length == 4)
            {
                // Pas de contour mais on a les coins transformés -> dessiner le quadrilatère
                DrawQuadrilateral(mat, match.TransformedCorners, MatchColor);
            }
            else
            {
                // Sinon, dessiner le rectangle axis-aligned
                var bbox = match.BoundingBox;
                Cv2.Rectangle(mat,
                    new OpenCvSharp.Point(bbox.X, bbox.Y),
                    new OpenCvSharp.Point(bbox.Right, bbox.Bottom),
                    MatchColor, LineThickness);
            }

            // Point d'ancrage
            var anchor = match.AnchorPosition;
            Cv2.Circle(mat, new OpenCvSharp.Point((int)anchor.X, (int)anchor.Y), 5, AnchorColor, -1);

            // Croix sur l'ancrage
            var crossSize = 10;
            Cv2.Line(mat,
                new OpenCvSharp.Point((int)anchor.X - crossSize, (int)anchor.Y),
                new OpenCvSharp.Point((int)anchor.X + crossSize, (int)anchor.Y),
                AnchorColor, 2);
            Cv2.Line(mat,
                new OpenCvSharp.Point((int)anchor.X, (int)anchor.Y - crossSize),
                new OpenCvSharp.Point((int)anchor.X, (int)anchor.Y + crossSize),
                AnchorColor, 2);

            // Label
            if (ShowLabel)
            {
                var bbox = match.BoundingBox;
                var label = $"{name}: {match.Score:P0}";
                
                // Ajouter angle/scale si significatifs
                if (Math.Abs(match.Angle) > 1 || Math.Abs(match.Scale - 1.0) > 0.05)
                {
                    label += $" ({match.Angle:F0}°, x{match.Scale:F2})";
                }
                
                var labelPos = new OpenCvSharp.Point(bbox.X, bbox.Y - 10);
                
                // Fond noir pour lisibilité
                var textSize = Cv2.GetTextSize(label, HersheyFonts.HersheySimplex, 0.5, 1, out _);
                Cv2.Rectangle(mat,
                    new OpenCvSharp.Point(labelPos.X - 2, labelPos.Y - textSize.Height - 2),
                    new OpenCvSharp.Point(labelPos.X + textSize.Width + 2, labelPos.Y + 4),
                    Scalar.Black, -1);
                
                Cv2.PutText(mat, label, labelPos, HersheyFonts.HersheySimplex, 0.5, MatchColor, 1);
            }
        }
        else if (ShowLabel)
        {
            // Afficher "NOT FOUND" en haut de l'image
            var label = $"{name}: NOT FOUND";
            var signIndex = _elderSigns.FindIndex(s => s.Name == name);
            var yOffset = 30 + (signIndex * 25);
            Cv2.PutText(mat, label, new OpenCvSharp.Point(10, yOffset), 
                HersheyFonts.HersheySimplex, 0.6, NotFoundColor, 2);
        }
    }

    /// <summary>
    /// Dessine le contour de l'objet transformé selon la détection.
    /// </summary>
    private void DrawTransformedContour(Mat mat, CorePointF[] contourPoints, 
        CorePointF[] transformedCorners, int templateWidth, int templateHeight, Scalar color)
    {
        try
        {
            // Calculer la matrice de transformation à partir des coins
            var srcCorners = new Point2f[]
            {
                new(0, 0),
                new(templateWidth, 0),
                new(templateWidth, templateHeight),
                new(0, templateHeight)
            };

            var dstCorners = new Point2f[]
            {
                new((float)transformedCorners[0].X, (float)transformedCorners[0].Y),
                new((float)transformedCorners[1].X, (float)transformedCorners[1].Y),
                new((float)transformedCorners[2].X, (float)transformedCorners[2].Y),
                new((float)transformedCorners[3].X, (float)transformedCorners[3].Y)
            };

            using var perspectiveMatrix = Cv2.GetPerspectiveTransform(srcCorners, dstCorners);

            // Transformer chaque point du contour
            var srcPoints = contourPoints.Select(p => new Point2f((float)p.X, (float)p.Y)).ToArray();
            var transformedPoints = Cv2.PerspectiveTransform(srcPoints, perspectiveMatrix);

            // Dessiner le contour transformé
            var cvPoints = transformedPoints.Select(p => new OpenCvSharp.Point((int)p.X, (int)p.Y)).ToArray();
            
            // Dessiner le polygone fermé
            Cv2.Polylines(mat, new[] { cvPoints }, isClosed: true, color, LineThickness);

            // Optionnel: dessiner les points du contour
            foreach (var p in cvPoints)
            {
                Cv2.Circle(mat, p, 3, color, -1);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ElderSign] DrawTransformedContour error: {ex.Message}");
            // Fallback au quadrilatère
            DrawQuadrilateral(mat, transformedCorners, color);
        }
    }

    private void DrawTransformedTemplate(Mat mat, ElderSignMatch match)
    {
        try
        {
            var corners = match.TransformedCorners!;
            
            // Charger le template en Mat
            using var templateMat = FrameMatConverter.ToMat(match.ElderSign.Template);
            
            // S'assurer que le template est en couleur (même format que mat)
            using var templateColor = templateMat.Channels() == 1
                ? templateMat.CvtColor(ColorConversionCodes.GRAY2BGR)
                : templateMat.Clone();

            // Coins source (template original)
            var srcCorners = new Point2f[]
            {
                new(0, 0),
                new(templateMat.Width, 0),
                new(templateMat.Width, templateMat.Height),
                new(0, templateMat.Height)
            };

            // Coins destination (position détectée)
            var dstCorners = new Point2f[]
            {
                new((float)corners[0].X, (float)corners[0].Y),
                new((float)corners[1].X, (float)corners[1].Y),
                new((float)corners[2].X, (float)corners[2].Y),
                new((float)corners[3].X, (float)corners[3].Y)
            };

            // Calculer la transformation perspective
            using var perspectiveMatrix = Cv2.GetPerspectiveTransform(srcCorners, dstCorners);

            // Appliquer la transformation au template
            using var warpedTemplate = new Mat();
            Cv2.WarpPerspective(templateColor, warpedTemplate, perspectiveMatrix, mat.Size());

            // Créer un masque pour le template transformé
            using var mask = new Mat(templateMat.Size(), MatType.CV_8UC1, Scalar.White);
            using var warpedMask = new Mat();
            Cv2.WarpPerspective(mask, warpedMask, perspectiveMatrix, mat.Size());

            // Convertir le masque en 3 canaux
            using var warpedMask3Ch = new Mat();
            Cv2.CvtColor(warpedMask, warpedMask3Ch, ColorConversionCodes.GRAY2BGR);
            
            // Normaliser le masque (0-1)
            using var maskFloat = new Mat();
            warpedMask3Ch.ConvertTo(maskFloat, MatType.CV_32FC3, 1.0 / 255.0);

            // Convertir les images en float
            using var matFloat = new Mat();
            using var warpedFloat = new Mat();
            mat.ConvertTo(matFloat, MatType.CV_32FC3);
            warpedTemplate.ConvertTo(warpedFloat, MatType.CV_32FC3);

            // Blend: result = mat * (1 - mask*alpha) + warped * (mask*alpha)
            var alpha = TemplateOverlayOpacity;
            using var alphaScaled = maskFloat * alpha;
            using var oneMinusAlpha = new Mat();
            Cv2.Subtract(Scalar.All(1.0), alphaScaled, oneMinusAlpha);
            
            using var part1 = new Mat();
            using var part2 = new Mat();
            Cv2.Multiply(matFloat, oneMinusAlpha, part1);
            Cv2.Multiply(warpedFloat, alphaScaled, part2);
            
            using var blended = new Mat();
            Cv2.Add(part1, part2, blended);
            
            // Reconvertir en 8-bit
            blended.ConvertTo(mat, MatType.CV_8UC3);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ElderSign] Overlay error: {ex.Message}");
        }
    }

    private void DrawQuadrilateral(Mat mat, CorePointF[] corners, Scalar color)
    {
        // Dessiner les 4 côtés du quadrilatère
        for (int i = 0; i < 4; i++)
        {
            var p1 = corners[i];
            var p2 = corners[(i + 1) % 4];
            
            Cv2.Line(mat,
                new OpenCvSharp.Point((int)p1.X, (int)p1.Y),
                new OpenCvSharp.Point((int)p2.X, (int)p2.Y),
                color, LineThickness);
        }

        // Dessiner les coins avec des cercles
        for (int i = 0; i < 4; i++)
        {
            var p = corners[i];
            Cv2.Circle(mat, new OpenCvSharp.Point((int)p.X, (int)p.Y), 4, color, -1);
        }
    }

    public override void Dispose()
    {
        // Attendre que la recherche en cours se termine
        var timeout = 0;
        while (_isSearching && timeout < 50)
        {
            Thread.Sleep(10);
            timeout++;
        }
        
        _matcher.Dispose();
        foreach (var sign in _elderSigns)
        {
            sign.Dispose();
        }
        _elderSigns.Clear();
        base.Dispose();
    }
}
