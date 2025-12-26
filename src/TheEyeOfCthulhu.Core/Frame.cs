namespace TheEyeOfCthulhu.Core;

/// <summary>
/// Représente une frame capturée depuis une source.
/// Immutable par design.
/// </summary>
public sealed class Frame : IDisposable
{
    private bool _disposed;
    private readonly byte[] _data;

    /// <summary>
    /// Données brutes de l'image.
    /// </summary>
    public ReadOnlySpan<byte> Data => _data;

    /// <summary>
    /// Accès au buffer interne (pour interop avec libs externes).
    /// À utiliser avec précaution.
    /// </summary>
    public byte[] RawBuffer => _data;

    /// <summary>
    /// Largeur en pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Hauteur en pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Format des pixels.
    /// </summary>
    public PixelFormat Format { get; }

    /// <summary>
    /// Timestamp de capture (UTC).
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Numéro de séquence de la frame.
    /// </summary>
    public long FrameNumber { get; }

    /// <summary>
    /// Stride (bytes par ligne). Peut inclure du padding.
    /// </summary>
    public int Stride { get; }

    public Frame(byte[] data, int width, int height, PixelFormat format, long frameNumber, int? stride = null)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        Width = width;
        Height = height;
        Format = format;
        FrameNumber = frameNumber;
        Timestamp = DateTime.UtcNow;
        Stride = stride ?? CalculateStride(width, format);

        ValidateDataSize();
    }

    private static int CalculateStride(int width, PixelFormat format)
    {
        int bytesPerPixel = format switch
        {
            PixelFormat.Gray8 => 1,
            PixelFormat.Bgr24 or PixelFormat.Rgb24 => 3,
            PixelFormat.Bgra32 or PixelFormat.Rgba32 => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        return width * bytesPerPixel;
    }

    private void ValidateDataSize()
    {
        int expectedSize = Stride * Height;
        if (_data.Length < expectedSize)
        {
            throw new ArgumentException(
                $"Data size ({_data.Length}) is smaller than expected ({expectedSize}). " +
                $"Width={Width}, Height={Height}, Stride={Stride}, Format={Format}");
        }
    }

    /// <summary>
    /// Crée une copie profonde de la frame.
    /// </summary>
    public Frame Clone()
    {
        var dataCopy = new byte[_data.Length];
        Array.Copy(_data, dataCopy, _data.Length);
        return new Frame(dataCopy, Width, Height, Format, FrameNumber, Stride);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Le byte[] sera collecté par le GC
    }
}
