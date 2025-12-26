using TheEyeOfCthulhu.Core;

namespace TheEyeOfCthulhu.Sources.Webcam;

/// <summary>
/// Configuration pour une webcam USB ou virtuelle (DroidCam driver).
/// </summary>
public sealed class WebcamConfiguration : SourceConfiguration
{
    public override string SourceType => "Webcam";

    /// <summary>
    /// Index de la caméra (0 = première, 1 = deuxième, etc.)
    /// </summary>
    public int DeviceIndex { get; set; } = 0;

    /// <summary>
    /// Backend préféré (DirectShow sur Windows).
    /// </summary>
    public WebcamBackend Backend { get; set; } = WebcamBackend.Auto;

    /// <summary>
    /// Crée une configuration pour un index donné.
    /// </summary>
    public static WebcamConfiguration Create(int deviceIndex = 0)
    {
        return new WebcamConfiguration
        {
            DeviceIndex = deviceIndex
        };
    }
}

/// <summary>
/// Backend de capture vidéo.
/// </summary>
public enum WebcamBackend
{
    /// <summary>Choix automatique.</summary>
    Auto,
    
    /// <summary>DirectShow (Windows).</summary>
    DirectShow,
    
    /// <summary>Media Foundation (Windows).</summary>
    MediaFoundation,
    
    /// <summary>V4L2 (Linux).</summary>
    V4L2
}
