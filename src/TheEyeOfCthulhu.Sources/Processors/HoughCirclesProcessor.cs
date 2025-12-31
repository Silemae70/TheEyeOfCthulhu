using OpenCvSharp;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Core.Processing;
using CvPoint = OpenCvSharp.Point;
using CvSize = OpenCvSharp.Size;

namespace TheEyeOfCthulhu.Sources.Processors;

/// <summary>
/// Détecte les cercles dans l'image avec la transformée de Hough.
/// </summary>
public class HoughCirclesProcessor : FrameProcessorBase
{
    public override string Name => "HoughCircles";

    /// <summary>
    /// Ratio inverse de la résolution de l'accumulateur (1 = même résolution que l'image).
    /// Plus petit = plus précis mais plus lent.
    /// </summary>
    public double Dp { get; set; } = 1.0;

    /// <summary>
    /// Distance minimum entre les centres des cercles détectés.
    /// </summary>
    public double MinDist { get; set; } = 50;

    /// <summary>
    /// Seuil haut pour Canny (utilisé en interne par HoughCircles).
    /// </summary>
    public double CannyThreshold { get; set; } = 100;

    /// <summary>
    /// Seuil de l'accumulateur pour la détection.
    /// Plus bas = plus de cercles détectés (mais plus de faux positifs).
    /// </summary>
    public double AccumulatorThreshold { get; set; } = 50;

    /// <summary>
    /// Rayon minimum des cercles à détecter (0 = pas de minimum).
    /// </summary>
    public int MinRadius { get; set; } = 0;

    /// <summary>
    /// Rayon maximum des cercles à détecter (0 = pas de maximum).
    /// </summary>
    public int MaxRadius { get; set; } = 0;

    /// <summary>
    /// Nombre maximum de cercles à retourner (0 = tous).
    /// </summary>
    public int MaxCircles { get; set; } = 10;

    /// <summary>
    /// Dessiner les cercles détectés sur l'image.
    /// </summary>
    public bool DrawCircles { get; set; } = true;

    /// <summary>
    /// Couleur des cercles (BGR).
    /// </summary>
    public Scalar CircleColor { get; set; } = new Scalar(0, 255, 0); // Vert

    /// <summary>
    /// Couleur du centre (BGR).
    /// </summary>
    public Scalar CenterColor { get; set; } = new Scalar(0, 0, 255); // Rouge

    /// <summary>
    /// Épaisseur du trait.
    /// </summary>
    public int Thickness { get; set; } = 2;

    /// <summary>
    /// Afficher les infos (rayon, diamètre) sur chaque cercle.
    /// </summary>
    public bool ShowInfo { get; set; } = true;

    /// <summary>
    /// Appliquer un blur avant la détection (réduit le bruit).
    /// </summary>
    public bool ApplyBlur { get; set; } = true;

    /// <summary>
    /// Taille du kernel de blur (doit être impair).
    /// </summary>
    public int BlurSize { get; set; } = 5;

    protected override (Frame Frame, Dictionary<string, object>? Metadata) ProcessCore(Frame input)
    {
        using var mat = FrameMatConverter.ToMat(input);

        // Convertir en grayscale si nécessaire
        using var gray = mat.Channels() == 1 
            ? mat.Clone() 
            : mat.CvtColor(ColorConversionCodes.BGR2GRAY);

        // Appliquer un blur pour réduire le bruit
        using var blurred = ApplyBlur 
            ? gray.GaussianBlur(new CvSize(BlurSize, BlurSize), 0) 
            : gray.Clone();

        // Détecter les cercles
        var circles = Cv2.HoughCircles(
            blurred,
            HoughModes.Gradient,
            Dp,
            MinDist,
            CannyThreshold,
            AccumulatorThreshold,
            MinRadius,
            MaxRadius);

        // Limiter le nombre de cercles
        var detectedCircles = circles.ToList();
        if (MaxCircles > 0 && detectedCircles.Count > MaxCircles)
        {
            // Trier par rayon décroissant et garder les N plus grands
            detectedCircles = detectedCircles
                .OrderByDescending(c => c.Radius)
                .Take(MaxCircles)
                .ToList();
        }

        // Construire les métadonnées
        var metadata = new Dictionary<string, object>
        {
            ["CircleCount"] = detectedCircles.Count,
            ["Circles"] = detectedCircles.Select((c, i) => new CircleInfo
            {
                Index = i,
                CenterX = c.Center.X,
                CenterY = c.Center.Y,
                Radius = c.Radius,
                Diameter = c.Radius * 2
            }).ToList()
        };

        // Ajouter les infos du plus grand cercle
        if (detectedCircles.Count > 0)
        {
            var largest = detectedCircles.OrderByDescending(c => c.Radius).First();
            metadata["LargestCircle.X"] = largest.Center.X;
            metadata["LargestCircle.Y"] = largest.Center.Y;
            metadata["LargestCircle.Radius"] = largest.Radius;
            metadata["LargestCircle.Diameter"] = largest.Radius * 2;
        }

        if (!DrawCircles)
        {
            return (input, metadata);
        }

        // Dessiner sur l'image
        using var colorMat = mat.Channels() == 1 
            ? mat.CvtColor(ColorConversionCodes.GRAY2BGR) 
            : mat.Clone();

        foreach (var circle in detectedCircles)
        {
            var center = new CvPoint((int)circle.Center.X, (int)circle.Center.Y);
            var radius = (int)circle.Radius;

            // Dessiner le cercle
            Cv2.Circle(colorMat, center, radius, CircleColor, Thickness);

            // Dessiner le centre
            Cv2.Circle(colorMat, center, 3, CenterColor, -1);

            // Afficher les infos
            if (ShowInfo)
            {
                var info = $"R:{radius} D:{radius * 2}";
                var textPos = new CvPoint(center.X - 40, center.Y - radius - 10);
                
                // Fond pour lisibilité
                var textSize = Cv2.GetTextSize(info, HersheyFonts.HersheySimplex, 0.5, 1, out _);
                Cv2.Rectangle(colorMat,
                    new CvPoint(textPos.X - 2, textPos.Y - textSize.Height - 2),
                    new CvPoint(textPos.X + textSize.Width + 2, textPos.Y + 4),
                    Scalar.Black, -1);
                
                Cv2.PutText(colorMat, info, textPos, HersheyFonts.HersheySimplex, 0.5, CircleColor, 1);
            }
        }

        var outputFrame = FrameMatConverter.ToFrame(colorMat, input.FrameNumber);
        return (outputFrame, metadata);
    }
}

/// <summary>
/// Informations sur un cercle détecté.
/// </summary>
public class CircleInfo
{
    public int Index { get; set; }
    public float CenterX { get; set; }
    public float CenterY { get; set; }
    public float Radius { get; set; }
    public float Diameter { get; set; }
}
