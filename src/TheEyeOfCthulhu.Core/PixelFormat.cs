namespace TheEyeOfCthulhu.Core;

/// <summary>
/// Format des pixels d'une frame.
/// </summary>
public enum PixelFormat
{
    /// <summary>BGR 24 bits (OpenCV default)</summary>
    Bgr24,
    
    /// <summary>RGB 24 bits</summary>
    Rgb24,
    
    /// <summary>Grayscale 8 bits</summary>
    Gray8,
    
    /// <summary>BGRA 32 bits avec alpha</summary>
    Bgra32,
    
    /// <summary>RGBA 32 bits avec alpha</summary>
    Rgba32
}
