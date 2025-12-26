using OpenCvSharp;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Core.Processing;
using PixelFormat = TheEyeOfCthulhu.Core.PixelFormat;

namespace TheEyeOfCthulhu.Sources.Processors;

/// <summary>
/// Convertit une frame en niveaux de gris.
/// </summary>
public sealed class GrayscaleProcessor : FrameProcessorBase
{
    public override string Name => "Grayscale";

    protected override (Frame Frame, Dictionary<string, object>? Metadata) ProcessCore(Frame input)
    {
        // Déjà en grayscale ?
        if (input.Format == PixelFormat.Gray8)
        {
            return (input, null);
        }

        using var mat = FrameMatConverter.ToMat(input);
        using var gray = new Mat();
        
        var code = input.Format switch
        {
            PixelFormat.Bgr24 => ColorConversionCodes.BGR2GRAY,
            PixelFormat.Rgb24 => ColorConversionCodes.RGB2GRAY,
            PixelFormat.Bgra32 => ColorConversionCodes.BGRA2GRAY,
            PixelFormat.Rgba32 => ColorConversionCodes.RGBA2GRAY,
            _ => throw new NotSupportedException($"Cannot convert {input.Format} to grayscale")
        };

        Cv2.CvtColor(mat, gray, code);

        return (FrameMatConverter.ToFrame(gray, input.FrameNumber), null);
    }
}

/// <summary>
/// Applique un flou gaussien.
/// </summary>
public sealed class GaussianBlurProcessor : FrameProcessorBase
{
    private int _kernelSize = 5;

    public override string Name => "GaussianBlur";

    /// <summary>
    /// Taille du kernel (doit être impair et >= 1).
    /// </summary>
    public int KernelSize
    {
        get => _kernelSize;
        set
        {
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(value), "KernelSize must be >= 1");
            if (value % 2 == 0)
                throw new ArgumentException("KernelSize must be odd", nameof(value));
            _kernelSize = value;
        }
    }

    /// <summary>
    /// Sigma X (0 = calculé automatiquement).
    /// </summary>
    public double SigmaX { get; set; } = 0;

    protected override (Frame Frame, Dictionary<string, object>? Metadata) ProcessCore(Frame input)
    {
        using var mat = FrameMatConverter.ToMat(input);
        using var blurred = new Mat();

        Cv2.GaussianBlur(mat, blurred, new Size(_kernelSize, _kernelSize), SigmaX);

        return (FrameMatConverter.ToFrame(blurred, input.FrameNumber), null);
    }
}

/// <summary>
/// Applique un seuillage (nécessite une image grayscale en entrée).
/// </summary>
public sealed class ThresholdProcessor : FrameProcessorBase
{
    private double _thresholdValue = 127;
    private double _maxValue = 255;

    public override string Name => "Threshold";

    /// <summary>
    /// Valeur de seuil (0-255).
    /// </summary>
    public double ThresholdValue
    {
        get => _thresholdValue;
        set
        {
            if (value < 0 || value > 255)
                throw new ArgumentOutOfRangeException(nameof(value), "ThresholdValue must be between 0 and 255");
            _thresholdValue = value;
        }
    }

    /// <summary>
    /// Valeur max après seuillage (0-255).
    /// </summary>
    public double MaxValue
    {
        get => _maxValue;
        set
        {
            if (value < 0 || value > 255)
                throw new ArgumentOutOfRangeException(nameof(value), "MaxValue must be between 0 and 255");
            _maxValue = value;
        }
    }

    /// <summary>
    /// Type de seuillage.
    /// </summary>
    public ThresholdTypes Type { get; set; } = ThresholdTypes.Binary;

    /// <summary>
    /// Utiliser Otsu pour calculer le seuil automatiquement.
    /// </summary>
    public bool UseOtsu { get; set; } = false;

    protected override (Frame Frame, Dictionary<string, object>? Metadata) ProcessCore(Frame input)
    {
        using var mat = FrameMatConverter.ToMat(input);

        // Convertir en grayscale si nécessaire
        using var gray = input.Format == PixelFormat.Gray8 
            ? mat.Clone() 
            : mat.CvtColor(ColorConversionCodes.BGR2GRAY);

        using var thresholded = new Mat();
        var type = UseOtsu ? Type | ThresholdTypes.Otsu : Type;
        var actualThreshold = Cv2.Threshold(gray, thresholded, _thresholdValue, _maxValue, type);

        var metadata = new Dictionary<string, object>
        {
            ["ActualThreshold"] = actualThreshold
        };

        return (FrameMatConverter.ToFrame(thresholded, input.FrameNumber), metadata);
    }
}

/// <summary>
/// Détection de contours avec Canny.
/// </summary>
public sealed class CannyEdgeProcessor : FrameProcessorBase
{
    private double _threshold1 = 50;
    private double _threshold2 = 150;
    private int _apertureSize = 3;

    public override string Name => "CannyEdge";

    /// <summary>
    /// Seuil bas pour l'hystérésis (0-255).
    /// </summary>
    public double Threshold1
    {
        get => _threshold1;
        set
        {
            if (value < 0 || value > 255)
                throw new ArgumentOutOfRangeException(nameof(value), "Threshold1 must be between 0 and 255");
            _threshold1 = value;
        }
    }

    /// <summary>
    /// Seuil haut pour l'hystérésis (0-255).
    /// </summary>
    public double Threshold2
    {
        get => _threshold2;
        set
        {
            if (value < 0 || value > 255)
                throw new ArgumentOutOfRangeException(nameof(value), "Threshold2 must be between 0 and 255");
            _threshold2 = value;
        }
    }

    /// <summary>
    /// Taille de l'opérateur Sobel (3, 5 ou 7).
    /// </summary>
    public int ApertureSize
    {
        get => _apertureSize;
        set
        {
            if (value != 3 && value != 5 && value != 7)
                throw new ArgumentOutOfRangeException(nameof(value), "ApertureSize must be 3, 5 or 7");
            _apertureSize = value;
        }
    }

    protected override (Frame Frame, Dictionary<string, object>? Metadata) ProcessCore(Frame input)
    {
        using var mat = FrameMatConverter.ToMat(input);

        // Convertir en grayscale si nécessaire
        using var gray = input.Format == PixelFormat.Gray8
            ? mat.Clone()
            : mat.CvtColor(ColorConversionCodes.BGR2GRAY);

        using var edges = new Mat();
        Cv2.Canny(gray, edges, _threshold1, _threshold2, _apertureSize);

        return (FrameMatConverter.ToFrame(edges, input.FrameNumber), null);
    }
}

/// <summary>
/// Détection de contours et extraction de leurs propriétés.
/// </summary>
public sealed class ContourDetectorProcessor : FrameProcessorBase
{
    private double _minArea = 100;

    public override string Name => "ContourDetector";

    /// <summary>
    /// Mode de récupération des contours.
    /// </summary>
    public RetrievalModes RetrievalMode { get; set; } = RetrievalModes.External;

    /// <summary>
    /// Méthode d'approximation des contours.
    /// </summary>
    public ContourApproximationModes ApproximationMethod { get; set; } = ContourApproximationModes.ApproxSimple;

    /// <summary>
    /// Aire minimale pour filtrer les petits contours (>= 0).
    /// </summary>
    public double MinArea
    {
        get => _minArea;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "MinArea must be >= 0");
            _minArea = value;
        }
    }

    /// <summary>
    /// Dessiner les contours sur l'image de sortie.
    /// </summary>
    public bool DrawContours { get; set; } = true;

    /// <summary>
    /// Couleur des contours dessinés (BGR).
    /// </summary>
    public Scalar ContourColor { get; set; } = new Scalar(0, 255, 0); // Vert

    /// <summary>
    /// Couleur des centroïdes dessinés (BGR).
    /// </summary>
    public Scalar CentroidColor { get; set; } = new Scalar(0, 0, 255); // Rouge

    /// <summary>
    /// Épaisseur des contours dessinés.
    /// </summary>
    public int ContourThickness { get; set; } = 2;

    protected override (Frame Frame, Dictionary<string, object>? Metadata) ProcessCore(Frame input)
    {
        using var mat = FrameMatConverter.ToMat(input);

        // Préparer image binaire pour findContours
        using var binary = input.Format == PixelFormat.Gray8
            ? mat.Clone()
            : mat.CvtColor(ColorConversionCodes.BGR2GRAY);

        // Seuillage si pas déjà binaire
        using var thresholded = new Mat();
        Cv2.Threshold(binary, thresholded, 127, 255, ThresholdTypes.Binary);

        // Trouver les contours
        Cv2.FindContours(thresholded, out var contours, out _, RetrievalMode, ApproximationMethod);

        // Filtrer par aire
        var filteredContours = contours
            .Where(c => Cv2.ContourArea(c) >= _minArea)
            .ToArray();

        // Extraire les données des contours
        var contourData = ExtractContourData(filteredContours);

        // Créer l'image de sortie
        var result = CreateOutputFrame(mat, input, filteredContours, contourData);

        var metadata = new Dictionary<string, object>
        {
            ["ContourCount"] = filteredContours.Length,
            ["Contours"] = contourData
        };

        return (result, metadata);
    }

    private List<ContourInfo> ExtractContourData(Point[][] contours)
    {
        var result = new List<ContourInfo>(contours.Length);

        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            var perimeter = Cv2.ArcLength(contour, true);
            var boundingRect = Cv2.BoundingRect(contour);
            var moments = Cv2.Moments(contour);

            double centroidX = moments.M00 > 0 ? moments.M10 / moments.M00 : 0;
            double centroidY = moments.M00 > 0 ? moments.M01 / moments.M00 : 0;

            result.Add(new ContourInfo
            {
                Area = area,
                Perimeter = perimeter,
                BoundingRect = boundingRect,
                CentroidX = centroidX,
                CentroidY = centroidY,
                PointCount = contour.Length
            });
        }

        return result;
    }

    private Frame CreateOutputFrame(Mat originalMat, Frame input, Point[][] contours, List<ContourInfo> contourData)
    {
        if (!DrawContours)
        {
            return FrameMatConverter.ToFrame(originalMat, input.FrameNumber);
        }

        // Convertir en couleur si nécessaire pour dessiner
        using var output = input.Format == PixelFormat.Gray8
            ? originalMat.CvtColor(ColorConversionCodes.GRAY2BGR)
            : originalMat.Clone();

        // Dessiner les contours
        Cv2.DrawContours(output, contours, -1, ContourColor, ContourThickness);

        // Dessiner les centroïdes
        foreach (var data in contourData)
        {
            var center = new Point((int)data.CentroidX, (int)data.CentroidY);
            Cv2.Circle(output, center, 5, CentroidColor, -1);
        }

        return FrameMatConverter.ToFrame(output, input.FrameNumber);
    }
}

/// <summary>
/// Informations extraites d'un contour.
/// </summary>
public sealed class ContourInfo
{
    public double Area { get; init; }
    public double Perimeter { get; init; }
    public Rect BoundingRect { get; init; }
    public double CentroidX { get; init; }
    public double CentroidY { get; init; }
    public int PointCount { get; init; }
}
