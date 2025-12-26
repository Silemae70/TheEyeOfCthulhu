namespace TheEyeOfCthulhu.Core;

/// <summary>
/// État d'une source de frames.
/// </summary>
public enum SourceState
{
    /// <summary>Source créée mais pas encore initialisée.</summary>
    Created,
    
    /// <summary>Source initialisée et prête à capturer.</summary>
    Ready,
    
    /// <summary>Capture en cours.</summary>
    Running,
    
    /// <summary>Capture en pause.</summary>
    Paused,
    
    /// <summary>Source arrêtée.</summary>
    Stopped,
    
    /// <summary>Erreur.</summary>
    Error
}

/// <summary>
/// Arguments pour l'événement de nouvelle frame.
/// </summary>
public class FrameEventArgs : EventArgs
{
    public Frame Frame { get; }

    public FrameEventArgs(Frame frame)
    {
        Frame = frame;
    }
}

/// <summary>
/// Arguments pour les événements d'erreur.
/// </summary>
public class SourceErrorEventArgs : EventArgs
{
    public Exception Exception { get; }
    public string Message { get; }

    public SourceErrorEventArgs(Exception exception, string? message = null)
    {
        Exception = exception;
        Message = message ?? exception.Message;
    }
}

/// <summary>
/// Interface abstraite pour une source de frames.
/// Implémentée par DroidCam, Basler, AlliedVision, fichiers, etc.
/// </summary>
public interface IFrameSource : IDisposable
{
    /// <summary>
    /// Nom unique de la source (ex: "DroidCam_192.168.1.57", "Basler_acA1920-40gc").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Type de source (ex: "DroidCam", "Basler", "File").
    /// </summary>
    string SourceType { get; }

    /// <summary>
    /// État actuel de la source.
    /// </summary>
    SourceState State { get; }

    /// <summary>
    /// Largeur actuelle des frames (0 si pas encore initialisé).
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Hauteur actuelle des frames (0 si pas encore initialisé).
    /// </summary>
    int Height { get; }

    /// <summary>
    /// FPS cible (0 si non applicable ou inconnu).
    /// </summary>
    double TargetFps { get; }

    /// <summary>
    /// FPS réel mesuré.
    /// </summary>
    double ActualFps { get; }

    /// <summary>
    /// Nombre total de frames capturées depuis le démarrage.
    /// </summary>
    long TotalFramesCaptured { get; }

    /// <summary>
    /// Événement déclenché à chaque nouvelle frame.
    /// </summary>
    event EventHandler<FrameEventArgs>? FrameReceived;

    /// <summary>
    /// Événement déclenché en cas d'erreur.
    /// </summary>
    event EventHandler<SourceErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// Événement déclenché quand l'état change.
    /// </summary>
    event EventHandler<SourceState>? StateChanged;

    /// <summary>
    /// Initialise la source (connexion, ouverture, etc.).
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Démarre la capture continue.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Arrête la capture.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Capture une seule frame (mode snapshot).
    /// </summary>
    Task<Frame?> CaptureFrameAsync(CancellationToken cancellationToken = default);
}
