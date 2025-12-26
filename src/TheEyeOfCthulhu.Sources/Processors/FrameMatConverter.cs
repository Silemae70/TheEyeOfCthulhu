using OpenCvSharp;
using TheEyeOfCthulhu.Core;
using PixelFormat = TheEyeOfCthulhu.Core.PixelFormat;

namespace TheEyeOfCthulhu.Sources.Processors;

/// <summary>
/// Helper pour convertir entre Frame et Mat OpenCV.
/// </summary>
public static class FrameMatConverter
{
    /// <summary>
    /// Convertit une Frame en Mat OpenCV.
    /// </summary>
    public static Mat ToMat(Frame frame)
    {
        var matType = frame.Format switch
        {
            PixelFormat.Gray8 => MatType.CV_8UC1,
            PixelFormat.Bgr24 or PixelFormat.Rgb24 => MatType.CV_8UC3,
            PixelFormat.Bgra32 or PixelFormat.Rgba32 => MatType.CV_8UC4,
            _ => throw new NotSupportedException($"Unsupported format: {frame.Format}")
        };

        var mat = new Mat(frame.Height, frame.Width, matType);
        System.Runtime.InteropServices.Marshal.Copy(frame.RawBuffer, 0, mat.Data, frame.RawBuffer.Length);

        return mat;
    }

    /// <summary>
    /// Convertit un Mat en Frame.
    /// </summary>
    public static Frame ToFrame(Mat mat, long frameNumber)
    {
        int channels = mat.Channels();
        var format = channels switch
        {
            1 => PixelFormat.Gray8,
            3 => PixelFormat.Bgr24,
            4 => PixelFormat.Bgra32,
            _ => throw new NotSupportedException($"Unsupported channel count: {channels}")
        };

        int stride = (int)mat.Step();
        int dataSize = stride * mat.Rows;
        var data = new byte[dataSize];

        System.Runtime.InteropServices.Marshal.Copy(mat.Data, data, 0, dataSize);

        return new Frame(data, mat.Width, mat.Height, format, frameNumber, stride);
    }
}
