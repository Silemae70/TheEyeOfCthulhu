using System.Diagnostics;
using OpenCvSharp;
using TheEyeOfCthulhu.Core;
using TheEyeOfCthulhu.Sources.Processors;

namespace TheEyeOfCthulhu.Sources.File;

/// <summary>
/// Source de frames depuis un fichier (image ou vidéo) ou une séquence d'images.
/// </summary>
public sealed class FileSource : IFrameSource
{
    private readonly FileSourceConfiguration _config;
    private VideoCapture? _videoCapture;
    private Mat? _staticImage;
    private List<string>? _imageSequence;
    private int _sequenceIndex;
    private CancellationTokenSource? _captureCts;
    private Task? _captureTask;
    
    private readonly object _stateLock = new();
    private readonly Stopwatch _fpsStopwatch = new();
    private int _frameCountForFps;
    private double _actualFps;
    private bool _isVideo;

    public string Name { get; }
    public string SourceType => "File";
    public SourceState State { get; private set; } = SourceState.Created;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public double TargetFps => _config.TargetFps;
    public double ActualFps => _actualFps;
    public long TotalFramesCaptured { get; private set; }

    public event EventHandler<FrameEventArgs>? FrameReceived;
    public event EventHandler<SourceErrorEventArgs>? ErrorOccurred;
    public event EventHandler<SourceState>? StateChanged;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif", ".gif", ".webp"
    };

    public FileSource(FileSourceConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        Name = config.DisplayName ?? Path.GetFileName(config.FilePath ?? "FileSource");
    }

    #region IFrameSource Implementation

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (State != SourceState.Created && State != SourceState.Stopped)
        {
            throw new InvalidOperationException($"Cannot initialize from state {State}");
        }

        try
        {
            await Task.Run(() => InitializeSource(), cancellationToken);
            SetState(SourceState.Ready);
        }
        catch (Exception ex)
        {
            SetState(SourceState.Error);
            OnError(ex, "Initialization failed");
            throw;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (State != SourceState.Ready && State != SourceState.Paused)
        {
            throw new InvalidOperationException($"Cannot start from state {State}");
        }

        _captureCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        SetState(SourceState.Running);

        _fpsStopwatch.Restart();
        _frameCountForFps = 0;

        _captureTask = RunCaptureLoopAsync(_captureCts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (State != SourceState.Running && State != SourceState.Paused)
        {
            return;
        }

        _captureCts?.Cancel();

        if (_captureTask != null)
        {
            try
            {
                await _captureTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (TimeoutException)
            {
                Log("Warning: Capture task did not stop gracefully");
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        SetState(SourceState.Stopped);
    }

    public async Task<Frame?> CaptureFrameAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(CaptureNextFrame, cancellationToken);
    }

    public void Dispose()
    {
        _captureCts?.Cancel();
        _captureCts?.Dispose();
        _videoCapture?.Dispose();
        _staticImage?.Dispose();
        SetState(SourceState.Stopped);
        
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Initialization

    private void InitializeSource()
    {
        if (_config.ImageSequence is { Count: > 0 })
        {
            InitializeImageSequence();
        }
        else if (!string.IsNullOrEmpty(_config.FilePath))
        {
            var extension = Path.GetExtension(_config.FilePath);
            
            if (ImageExtensions.Contains(extension))
            {
                InitializeStaticImage();
            }
            else
            {
                InitializeVideo();
            }
        }
        else
        {
            throw new ArgumentException("No file path or image sequence provided");
        }
    }

    private void InitializeImageSequence()
    {
        _imageSequence = _config.ImageSequence!;
        _isVideo = false;

        using var firstImage = Cv2.ImRead(_imageSequence[0], ImreadModes.Color);
        if (firstImage.Empty())
        {
            throw new InvalidOperationException($"Failed to load first image: {_imageSequence[0]}");
        }

        Width = firstImage.Width;
        Height = firstImage.Height;

        Log($"Initialized image sequence: {_imageSequence.Count} images, {Width}x{Height}");
    }

    private void InitializeStaticImage()
    {
        _staticImage = Cv2.ImRead(_config.FilePath!, ImreadModes.Color);
        if (_staticImage.Empty())
        {
            throw new InvalidOperationException($"Failed to load image: {_config.FilePath}");
        }

        Width = _staticImage.Width;
        Height = _staticImage.Height;
        _isVideo = false;

        Log($"Initialized static image: {Width}x{Height}");
    }

    private void InitializeVideo()
    {
        _videoCapture = new VideoCapture(_config.FilePath!);
        if (!_videoCapture.IsOpened())
        {
            throw new InvalidOperationException($"Failed to open video: {_config.FilePath}");
        }

        Width = (int)_videoCapture.Get(VideoCaptureProperties.FrameWidth);
        Height = (int)_videoCapture.Get(VideoCaptureProperties.FrameHeight);
        var fps = _videoCapture.Get(VideoCaptureProperties.Fps);
        _isVideo = true;

        Log($"Initialized video: {Width}x{Height} @ {fps:F1} FPS");
    }

    #endregion

    #region Capture Loop

    private async Task RunCaptureLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var frame = CaptureNextFrame();

                if (frame != null)
                {
                    TotalFramesCaptured++;
                    UpdateFps();
                    OnFrameReceived(frame);
                }
                else if (!_config.Loop)
                {
                    break;
                }

                var delayMs = CalculateFrameDelay();
                await Task.Delay(delayMs, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal stop
        }
        catch (Exception ex)
        {
            SetState(SourceState.Error);
            OnError(ex, "Capture loop failed");
        }
    }

    private int CalculateFrameDelay()
    {
        if (_isVideo && _videoCapture != null)
        {
            var videoFps = _videoCapture.Get(VideoCaptureProperties.Fps);
            return videoFps > 0 ? (int)(1000.0 / videoFps) : 33;
        }

        return _config.TargetFps > 0
            ? (int)(1000.0 / _config.TargetFps)
            : _config.ImageRepeatIntervalMs;
    }

    private Frame? CaptureNextFrame()
    {
        Mat? mat = null;

        try
        {
            mat = GetNextMat();

            if (mat == null || mat.Empty())
            {
                mat?.Dispose();
                return null;
            }

            var frame = FrameMatConverter.ToFrame(mat, TotalFramesCaptured);
            return frame;
        }
        finally
        {
            mat?.Dispose();
        }
    }

    private Mat? GetNextMat()
    {
        if (_staticImage != null)
        {
            return _staticImage.Clone();
        }

        if (_imageSequence != null)
        {
            return GetNextSequenceImage();
        }

        if (_videoCapture != null)
        {
            return GetNextVideoFrame();
        }

        return null;
    }

    private Mat? GetNextSequenceImage()
    {
        if (_sequenceIndex >= _imageSequence!.Count)
        {
            if (!_config.Loop) return null;
            _sequenceIndex = 0;
        }

        var mat = Cv2.ImRead(_imageSequence[_sequenceIndex], ImreadModes.Color);
        _sequenceIndex++;

        return mat.Empty() ? null : mat;
    }

    private Mat? GetNextVideoFrame()
    {
        var mat = new Mat();

        if (_videoCapture!.Read(mat) && !mat.Empty())
        {
            return mat;
        }

        // End of video
        if (!_config.Loop)
        {
            mat.Dispose();
            return null;
        }

        // Loop: restart from beginning
        _videoCapture.Set(VideoCaptureProperties.PosFrames, 0);
        
        if (_videoCapture.Read(mat) && !mat.Empty())
        {
            return mat;
        }

        mat.Dispose();
        return null;
    }

    #endregion

    #region Helper Methods

    private void UpdateFps()
    {
        _frameCountForFps++;

        if (_fpsStopwatch.ElapsedMilliseconds >= 1000)
        {
            _actualFps = _frameCountForFps * 1000.0 / _fpsStopwatch.ElapsedMilliseconds;
            _frameCountForFps = 0;
            _fpsStopwatch.Restart();
        }
    }

    private void SetState(SourceState newState)
    {
        lock (_stateLock)
        {
            if (State != newState)
            {
                State = newState;
                StateChanged?.Invoke(this, newState);
            }
        }
    }

    private void OnFrameReceived(Frame frame)
    {
        FrameReceived?.Invoke(this, new FrameEventArgs(frame));
    }

    private void OnError(Exception ex, string message)
    {
        ErrorOccurred?.Invoke(this, new SourceErrorEventArgs(ex, message));
    }

    [Conditional("DEBUG")]
    private static void Log(string message)
    {
        Debug.WriteLine($"[FileSource] {message}");
    }

    #endregion
}
