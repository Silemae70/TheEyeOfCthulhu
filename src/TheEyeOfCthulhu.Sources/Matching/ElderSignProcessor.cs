using System.Diagnostics;
using OpenCvSharp;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Core.Matching;
using TheEyeOfCthulhu.Core.Processing;
using TheEyeOfCthulhu.Sources.Processors;

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
            var bbox = match.BoundingBox;

            // Rectangle autour du match
            Cv2.Rectangle(mat,
                new OpenCvSharp.Point(bbox.X, bbox.Y),
                new OpenCvSharp.Point(bbox.Right, bbox.Bottom),
                MatchColor, LineThickness);

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
                var label = $"{name}: {match.Score:P0}";
                var labelPos = new OpenCvSharp.Point(bbox.X, bbox.Y - 10);
                
                // Fond noir pour lisibilité
                var textSize = Cv2.GetTextSize(label, HersheyFonts.HersheySimplex, 0.6, 1, out _);
                Cv2.Rectangle(mat,
                    new OpenCvSharp.Point(labelPos.X - 2, labelPos.Y - textSize.Height - 2),
                    new OpenCvSharp.Point(labelPos.X + textSize.Width + 2, labelPos.Y + 4),
                    Scalar.Black, -1);
                
                Cv2.PutText(mat, label, labelPos, HersheyFonts.HersheySimplex, 0.6, MatchColor, 2);
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
