using OpenCvSharp;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Sources.Processors;
using PixelFormat = TheEyeOfCthulhu.Core.PixelFormat;

namespace TheEyeOfCthulhu.Sources.Utilities;

/// <summary>
/// Utilitaires pour afficher des frames avec OpenCV HighGUI.
/// Principalement utilisé pour les tests et la console.
/// </summary>
public static class FrameViewer
{
    /// <summary>
    /// Convertit une Frame en Mat OpenCV (avec correction RGB->BGR si nécessaire).
    /// </summary>
    public static Mat FrameToMat(Frame frame)
    {
        var mat = FrameMatConverter.ToMat(frame);

        // OpenCV utilise BGR, convertir si nécessaire
        if (frame.Format == PixelFormat.Rgb24)
        {
            Cv2.CvtColor(mat, mat, ColorConversionCodes.RGB2BGR);
        }
        else if (frame.Format == PixelFormat.Rgba32)
        {
            Cv2.CvtColor(mat, mat, ColorConversionCodes.RGBA2BGRA);
        }

        return mat;
    }

    /// <summary>
    /// Affiche une frame dans une fenêtre OpenCV.
    /// </summary>
    public static void ShowFrame(Frame frame, string windowName = "The Eye of Cthulhu")
    {
        using var mat = FrameToMat(frame);
        Cv2.ImShow(windowName, mat);
    }

    /// <summary>
    /// Affiche une frame avec overlay d'informations.
    /// </summary>
    public static void ShowFrameWithInfo(Frame frame, double fps, string windowName = "The Eye of Cthulhu")
    {
        using var mat = FrameToMat(frame);

        var info = $"Frame #{frame.FrameNumber} | {frame.Width}x{frame.Height} | {fps:F1} FPS";
        Cv2.PutText(mat, info, new Point(10, 30), HersheyFonts.HersheySimplex, 0.7, Scalar.LimeGreen, 2);

        Cv2.ImShow(windowName, mat);
    }

    /// <summary>
    /// Crée une fenêtre OpenCV.
    /// </summary>
    public static void CreateWindow(string windowName, WindowFlags flags = WindowFlags.AutoSize)
    {
        Cv2.NamedWindow(windowName, flags);
    }

    /// <summary>
    /// Ferme une fenêtre.
    /// </summary>
    public static void DestroyWindow(string windowName)
    {
        Cv2.DestroyWindow(windowName);
    }

    /// <summary>
    /// Ferme toutes les fenêtres.
    /// </summary>
    public static void DestroyAllWindows()
    {
        Cv2.DestroyAllWindows();
    }

    /// <summary>
    /// Attend une touche. Retourne le code de la touche ou -1.
    /// </summary>
    public static int WaitKey(int delayMs = 1)
    {
        return Cv2.WaitKey(delayMs);
    }
}
