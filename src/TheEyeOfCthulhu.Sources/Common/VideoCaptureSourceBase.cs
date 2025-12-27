using System.Diagnostics;
using OpenCvSharp;
using TheEyeOfCthulhu.Core;
using PixelFormat = TheEyeOfCthulhu.Core.PixelFormat;

namespace TheEyeOfCthulhu.Sources.Common;

/// <summary>
/// Classe de base pour les sources utilisant OpenCV VideoCapture.
/// Factorise le code commun entre DroidCam, Webcam, etc.
/// </summary>
public abstract class VideoCaptureSourceBase : IFrameSource
{
    protected VideoCapture? Capture;
    protected volatile bool IsRunning;
    protected Thread? CaptureThread;
    
    private readonly object _stateLock = new();
    private readonly Stopwatch _fpsStopwatch = new();
    private int _frameCountForFps;
    private double _actualFps;
    
    // Reconnexion automatique
    private int _consecutiveFailures;
    private const int MaxConsecutiveFailures = 30; // ~1 seconde de frames ratées
    private const int ReconnectDelayMs = 2000;
    private const int MaxReconnectAttempts = 10;

    #region IFrameSource Properties

    public abstract string Name { get; }
    public abstract string SourceType { get; }
    public SourceState State { get; private set; } = SourceState.Created;
    public int Width { get; protected set; }
    public int Height { get; protected set; }
    public abstract double TargetFps { get; }
    public double ActualFps => _actualFps;
    public long TotalFramesCaptured { get; private set; }

    public event EventHandler<FrameEventArgs>? FrameReceived;
    public event EventHandler<SourceErrorEventArgs>? ErrorOccurred;
    public event EventHandler<SourceState>? StateChanged;

    #endregion

    #region Abstract Methods

    /// <summary>
    /// Crée et configure le VideoCapture.
    /// </summary>
    protected abstract VideoCapture CreateCapture();

    /// <summary>
    /// Nom du thread de capture pour le debug.
    /// </summary>
    protected abstract string CaptureThreadName { get; }

    /// <summary>
    /// Tag pour les logs.
    /// </summary>
    protected abstract string LogTag { get; }

    #endregion

    #region IFrameSource Methods

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (State != SourceState.Created && State != SourceState.Stopped)
        {
            throw new InvalidOperationException($"Cannot initialize from state {State}");
        }

        try
        {
            await Task.Run(() =>
            {
                Capture = CreateCapture();

                if (!Capture.IsOpened())
                {
                    throw new InvalidOperationException($"[{LogTag}] Failed to open capture");
                }

                // Lire une frame de test pour obtenir les dimensions
                using var testFrame = new Mat();
                if (!Capture.Read(testFrame) || testFrame.Empty())
                {
                    throw new InvalidOperationException($"[{LogTag}] Failed to read test frame");
                }

                Width = testFrame.Width;
                Height = testFrame.Height;

            }, cancellationToken);

            SetState(SourceState.Ready);
            Log($"Initialized: {Width}x{Height}");
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

        IsRunning = true;
        SetState(SourceState.Running);

        _fpsStopwatch.Restart();
        _frameCountForFps = 0;

        CaptureThread = new Thread(CaptureLoop)
        {
            Name = CaptureThreadName,
            IsBackground = true
        };
        CaptureThread.Start();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (State != SourceState.Running && State != SourceState.Paused)
        {
            return Task.CompletedTask;
        }

        Log("StopAsync called");

        // 1. Signaler l'arrêt
        IsRunning = false;

        // 2. Attendre que le thread se termine AVANT de release
        if (CaptureThread != null && CaptureThread.IsAlive)
        {
            Log("Waiting for capture thread...");
            if (!CaptureThread.Join(TimeSpan.FromSeconds(3)))
            {
                Log("Warning: Capture thread did not stop gracefully");
            }
        }
        CaptureThread = null;

        // 3. Release le capture
        ReleaseCapture();

        SetState(SourceState.Stopped);
        Log("Stopped");

        return Task.CompletedTask;
    }

    public async Task<Frame?> CaptureFrameAsync(CancellationToken cancellationToken = default)
    {
        var capture = Capture;
        if (capture == null || !capture.IsOpened())
        {
            return null;
        }

        return await Task.Run(() =>
        {
            using var mat = new Mat();
            if (!capture.Read(mat) || mat.Empty())
            {
                return null;
            }
            return MatToFrame(mat);
        }, cancellationToken);
    }

    public void Dispose()
    {
        Log("Dispose called");

        IsRunning = false;

        if (CaptureThread != null && CaptureThread.IsAlive)
        {
            CaptureThread.Join(TimeSpan.FromSeconds(2));
        }
        CaptureThread = null;

        ReleaseCapture();
        SetState(SourceState.Stopped);
        
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Capture Loop

    private void CaptureLoop()
    {
        Log("Capture loop started");

        using var mat = new Mat();
        int reconnectAttempts = 0;

        while (IsRunning)
        {
            var capture = Capture;
            
            // Vérifier si capture est valide
            if (capture == null || !capture.IsOpened())
            {
                if (!TryReconnect(ref reconnectAttempts))
                {
                    break;
                }
                continue;
            }

            try
            {
                if (!capture.Read(mat) || mat.Empty())
                {
                    _consecutiveFailures++;
                    
                    if (_consecutiveFailures >= MaxConsecutiveFailures)
                    {
                        Log($"Too many consecutive failures ({_consecutiveFailures}), attempting reconnect...");
                        
                        if (!TryReconnect(ref reconnectAttempts))
                        {
                            break;
                        }
                        _consecutiveFailures = 0;
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                    continue;
                }

                // Succès ! Reset les compteurs
                _consecutiveFailures = 0;
                reconnectAttempts = 0;

                var frame = MatToFrame(mat);
                if (frame != null)
                {
                    TotalFramesCaptured++;
                    UpdateFps();

                    if (IsRunning)
                    {
                        OnFrameReceived(frame);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Frame error: {ex.Message}");
                _consecutiveFailures++;
                
                if (_consecutiveFailures >= MaxConsecutiveFailures)
                {
                    if (!TryReconnect(ref reconnectAttempts))
                    {
                        break;
                    }
                    _consecutiveFailures = 0;
                }
            }
        }

        Log("Capture loop ended");
    }

    /// <summary>
    /// Tente de se reconnecter à la source.
    /// </summary>
    private bool TryReconnect(ref int attempts)
    {
        if (!IsRunning) return false;
        
        attempts++;
        
        if (attempts > MaxReconnectAttempts)
        {
            Log($"Max reconnect attempts ({MaxReconnectAttempts}) reached, giving up.");
            OnError(new Exception("Connection lost"), "Failed to reconnect after multiple attempts");
            return false;
        }

        Log($"Reconnect attempt {attempts}/{MaxReconnectAttempts}...");
        SetState(SourceState.Reconnecting);

        // Libérer l'ancienne capture
        var oldCapture = Capture;
        Capture = null;
        if (oldCapture != null)
        {
            try
            {
                oldCapture.Release();
                oldCapture.Dispose();
            }
            catch { /* ignore */ }
        }

        // Attendre avant de retenter
        Thread.Sleep(ReconnectDelayMs);

        if (!IsRunning) return false;

        // Tenter de recréer la capture
        try
        {
            Capture = CreateCapture();
            
            if (Capture != null && Capture.IsOpened())
            {
                Log($"Reconnected successfully!");
                SetState(SourceState.Running);
                return true;
            }
        }
        catch (Exception ex)
        {
            Log($"Reconnect failed: {ex.Message}");
        }

        // Échec, on réessaiera
        return TryReconnect(ref attempts);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Convertit un Mat OpenCV en Frame.
    /// </summary>
    protected Frame? MatToFrame(Mat mat)
    {
        if (mat.Empty()) return null;

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

        return new Frame(data, mat.Width, mat.Height, format, TotalFramesCaptured, stride);
    }

    /// <summary>
    /// Met à jour le calcul des FPS.
    /// </summary>
    protected void UpdateFps()
    {
        _frameCountForFps++;

        if (_fpsStopwatch.ElapsedMilliseconds >= 1000)
        {
            _actualFps = _frameCountForFps * 1000.0 / _fpsStopwatch.ElapsedMilliseconds;
            _frameCountForFps = 0;
            _fpsStopwatch.Restart();
        }
    }

    /// <summary>
    /// Change l'état de la source.
    /// </summary>
    protected void SetState(SourceState newState)
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

    /// <summary>
    /// Déclenche l'événement FrameReceived.
    /// </summary>
    protected void OnFrameReceived(Frame frame)
    {
        FrameReceived?.Invoke(this, new FrameEventArgs(frame));
    }

    /// <summary>
    /// Déclenche l'événement ErrorOccurred.
    /// </summary>
    protected void OnError(Exception ex, string message)
    {
        ErrorOccurred?.Invoke(this, new SourceErrorEventArgs(ex, message));
    }

    /// <summary>
    /// Libère le VideoCapture.
    /// </summary>
    private void ReleaseCapture()
    {
        var capture = Capture;
        Capture = null;
        if (capture != null)
        {
            Log("Releasing capture...");
            capture.Release();
            capture.Dispose();
        }
    }

    /// <summary>
    /// Log conditionnel (Debug only).
    /// </summary>
    [Conditional("DEBUG")]
    protected void Log(string message)
    {
        Debug.WriteLine($"[{LogTag}] {message}");
    }

    #endregion
}
