using System.Diagnostics;
using OpenCvSharp;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Sources.Processors;

namespace TheEyeOfCthulhu.Sources.Recording;

/// <summary>
/// Enregistreur de frames utilisant OpenCV.
/// Thread-safe pour les appels concurrents.
/// </summary>
public sealed class FrameRecorder : IFrameRecorder
{
    private readonly object _lock = new();
    private long _framesSaved;

    public RecordingOptions Options { get; }
    public long FramesSaved => Interlocked.Read(ref _framesSaved);

    public FrameRecorder(RecordingOptions? options = null)
    {
        Options = options ?? new RecordingOptions();
        EnsureDirectoryExists();
    }

    public FrameRecorder(string outputDirectory) 
        : this(new RecordingOptions { OutputDirectory = outputDirectory })
    {
    }

    public string SaveSnapshot(Frame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var filePath = Options.GenerateFilePath(frame);

        EnsureDirectoryExists();

        using var mat = FrameMatConverter.ToMat(frame);
        var encodingParams = GetEncodingParams();

        if (!Cv2.ImWrite(filePath, mat, encodingParams))
        {
            throw new IOException($"Failed to save frame to {filePath}");
        }

        Interlocked.Increment(ref _framesSaved);
        Log($"Saved: {filePath}");
        
        return filePath;
    }

    public async Task<string> SaveSnapshotAsync(Frame frame, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        
        var frameClone = frame.Clone();

        return await Task.Run(() =>
        {
            try
            {
                return SaveSnapshot(frameClone);
            }
            finally
            {
                frameClone.Dispose();
            }
        }, cancellationToken);
    }

    private void EnsureDirectoryExists()
    {
        lock (_lock)
        {
            if (!Directory.Exists(Options.OutputDirectory))
            {
                Directory.CreateDirectory(Options.OutputDirectory);
                Log($"Created directory: {Options.OutputDirectory}");
            }
        }
    }

    private ImageEncodingParam[] GetEncodingParams()
    {
        return Options.Format switch
        {
            ImageFormat.Jpeg => [new ImageEncodingParam(ImwriteFlags.JpegQuality, Options.JpegQuality)],
            ImageFormat.Png => [new ImageEncodingParam(ImwriteFlags.PngCompression, 3)],
            _ => []
        };
    }

    [Conditional("DEBUG")]
    private static void Log(string message)
    {
        Debug.WriteLine($"[FrameRecorder] {message}");
    }

    public void Dispose()
    {
        // Rien à disposer
        GC.SuppressFinalize(this);
    }
}
