using OpenCvSharp;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Core.Matching;
using TheEyeOfCthulhu.Sources.Processors;

namespace TheEyeOfCthulhu.Sources.Matching;

/// <summary>
/// Utilitaires pour créer des ElderSigns à partir de différentes sources.
/// </summary>
public static class ElderSignFactory
{
    /// <summary>
    /// Crée un ElderSign à partir d'un fichier PNG avec transparence.
    /// Le canal alpha est utilisé pour créer le masque et extraire le contour.
    /// </summary>
    public static ElderSign FromPngWithAlpha(string name, string filePath)
    {
        if (!System.IO.File.Exists(filePath))
            throw new FileNotFoundException("PNG file not found", filePath);

        // Charger avec le canal alpha
        using var mat = Cv2.ImRead(filePath, ImreadModes.Unchanged);
        
        if (mat.Empty())
            throw new InvalidOperationException("Failed to load image");

        if (mat.Channels() != 4)
            throw new InvalidOperationException("Image must have an alpha channel (RGBA)");

        // Séparer les canaux
        var channels = mat.Split();
        try
        {
            using var bgr = new Mat();
            Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, bgr);
            
            var alphaMat = channels[3];

            // Créer la frame template (BGR)
            var templateFrame = MatToFrame(bgr);
            
            // Créer le masque à partir du canal alpha
            var maskFrame = MatToFrame(alphaMat);

            // Créer l'ElderSign
            var elderSign = new ElderSign(name, templateFrame);
            elderSign.SetMask(maskFrame);

            // Extraire le contour du masque
            var contour = ExtractContour(alphaMat);
            if (contour != null && contour.Length >= 3)
            {
                elderSign.SetContour(contour);
            }

            // Cleanup
            templateFrame.Dispose();
            maskFrame.Dispose();

            return elderSign;
        }
        finally
        {
            foreach (var ch in channels)
                ch.Dispose();
        }
    }

    /// <summary>
    /// Crée un ElderSign à partir d'une Frame en extrayant automatiquement le contour principal.
    /// Utilise la détection de contours pour trouver l'objet principal.
    /// </summary>
    public static ElderSign FromFrameWithAutoContour(string name, Frame frame, int threshold = 127)
    {
        var elderSign = new ElderSign(name, frame);

        using var mat = FrameMatConverter.ToMat(frame);
        using var gray = mat.Channels() == 1 
            ? mat.Clone() 
            : mat.CvtColor(ColorConversionCodes.BGR2GRAY);

        // Seuillage pour créer un masque binaire
        using var binary = new Mat();
        Cv2.Threshold(gray, binary, threshold, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        // Extraire le contour
        var contour = ExtractContour(binary);
        if (contour != null && contour.Length >= 3)
        {
            elderSign.SetContour(contour);
            
            // Créer aussi le masque
            using var mask = new Mat(mat.Size(), MatType.CV_8UC1, Scalar.Black);
            var cvPoints = contour.Select(p => new OpenCvSharp.Point((int)p.X, (int)p.Y)).ToArray();
            Cv2.FillPoly(mask, new[] { cvPoints }, Scalar.White);
            
            var maskFrame = MatToFrame(mask);
            elderSign.SetMask(maskFrame);
            maskFrame.Dispose();
        }

        return elderSign;
    }

    /// <summary>
    /// Crée un ElderSign à partir d'une Frame et de points de contour manuels.
    /// </summary>
    public static ElderSign FromFrameWithContour(string name, Frame frame, PointF[] contourPoints)
    {
        var elderSign = new ElderSign(name, frame);
        elderSign.SetContour(contourPoints);

        // Créer le masque à partir du contour
        using var mat = FrameMatConverter.ToMat(frame);
        using var mask = new Mat(mat.Size(), MatType.CV_8UC1, Scalar.Black);
        
        var cvPoints = contourPoints.Select(p => new OpenCvSharp.Point((int)p.X, (int)p.Y)).ToArray();
        Cv2.FillPoly(mask, new[] { cvPoints }, Scalar.White);
        
        var maskFrame = MatToFrame(mask);
        elderSign.SetMask(maskFrame);
        maskFrame.Dispose();

        return elderSign;
    }

    /// <summary>
    /// Extrait le contour principal d'un masque binaire.
    /// </summary>
    private static PointF[]? ExtractContour(Mat binary)
    {
        Cv2.FindContours(binary, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        if (contours.Length == 0)
            return null;

        // Trouver le plus grand contour
        var largestContour = contours
            .OrderByDescending(c => Cv2.ContourArea(c))
            .First();

        if (largestContour.Length < 3)
            return null;

        // Simplifier le contour (Douglas-Peucker)
        var epsilon = 0.01 * Cv2.ArcLength(largestContour, true);
        var simplified = Cv2.ApproxPolyDP(largestContour, epsilon, true);

        return simplified
            .Select(p => new PointF(p.X, p.Y))
            .ToArray();
    }

    private static Frame MatToFrame(Mat mat)
    {
        var format = mat.Channels() switch
        {
            1 => PixelFormat.Gray8,
            3 => PixelFormat.Bgr24,
            4 => PixelFormat.Bgra32,
            _ => PixelFormat.Bgr24
        };

        var data = new byte[mat.Total() * mat.ElemSize()];
        System.Runtime.InteropServices.Marshal.Copy(mat.Data, data, 0, data.Length);

        return new Frame(data, mat.Width, mat.Height, format, 0, (int)mat.Step());
    }
}
