namespace TheEyeOfCthulhu.Core;

/// <summary>
/// Configuration de base pour une source de frames.
/// Chaque type de source aura sa propre classe dérivée.
/// </summary>
public abstract class SourceConfiguration
{
    /// <summary>
    /// Nom personnalisé pour identifier cette source.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Type de source (doit correspondre à une factory enregistrée).
    /// </summary>
    public abstract string SourceType { get; }

    /// <summary>
    /// Timeout pour l'initialisation (ms).
    /// </summary>
    public int InitializationTimeoutMs { get; set; } = 10000;

    /// <summary>
    /// Timeout pour la capture d'une frame (ms).
    /// </summary>
    public int CaptureTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// FPS cible (0 = aussi vite que possible).
    /// </summary>
    public double TargetFps { get; set; } = 30;

    /// <summary>
    /// Largeur souhaitée (0 = par défaut de la source).
    /// </summary>
    public int RequestedWidth { get; set; } = 0;

    /// <summary>
    /// Hauteur souhaitée (0 = par défaut de la source).
    /// </summary>
    public int RequestedHeight { get; set; } = 0;
}
