using TheEyeOfCthulhu.Core;

namespace TheEyeOfCthulhu.Sources.DroidCam;

/// <summary>
/// Configuration pour une source DroidCam.
/// </summary>
public sealed class DroidCamConfiguration : SourceConfiguration
{
    public override string SourceType => "DroidCam";

    /// <summary>
    /// Adresse IP du téléphone.
    /// </summary>
    public string IpAddress { get; set; } = "192.168.1.1";

    /// <summary>
    /// Port DroidCam (4747 par défaut).
    /// </summary>
    public int Port { get; set; } = 4747;

    /// <summary>
    /// Utiliser HTTPS (rarement nécessaire).
    /// </summary>
    public bool UseHttps { get; set; } = false;

    /// <summary>
    /// Mode de capture.
    /// </summary>
    public DroidCamMode Mode { get; set; } = DroidCamMode.VideoStream;

    /// <summary>
    /// Intervalle entre les snapshots en mode Snapshot (ms).
    /// </summary>
    public int SnapshotIntervalMs { get; set; } = 100;

    /// <summary>
    /// URL complète du flux vidéo.
    /// </summary>
    public string VideoUrl => $"{(UseHttps ? "https" : "http")}://{IpAddress}:{Port}/video";

    /// <summary>
    /// URL pour les snapshots.
    /// </summary>
    public string SnapshotUrl => $"{(UseHttps ? "https" : "http")}://{IpAddress}:{Port}/shot.jpg";

    /// <summary>
    /// Crée une configuration avec les paramètres par défaut.
    /// </summary>
    public static DroidCamConfiguration Create(string ipAddress, int port = 4747)
    {
        return new DroidCamConfiguration
        {
            IpAddress = ipAddress,
            Port = port
        };
    }
}

/// <summary>
/// Mode de capture DroidCam.
/// </summary>
public enum DroidCamMode
{
    /// <summary>
    /// Flux vidéo MJPEG continu (meilleur FPS).
    /// </summary>
    VideoStream,

    /// <summary>
    /// Snapshots répétés (plus stable mais plus lent).
    /// </summary>
    Snapshot
}
