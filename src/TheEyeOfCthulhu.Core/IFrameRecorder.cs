namespace TheEyeOfCthulhu.Core;

/// <summary>
/// Format de sauvegarde des images.
/// </summary>
public enum ImageFormat
{
    Png,
    Jpeg,
    Bmp,
    Tiff
}

/// <summary>
/// Options pour l'enregistrement de frames.
/// </summary>
public class RecordingOptions
{
    /// <summary>
    /// Dossier de destination.
    /// </summary>
    public string OutputDirectory { get; set; } = "captures";

    /// <summary>
    /// Préfixe des noms de fichiers.
    /// </summary>
    public string FilePrefix { get; set; } = "frame";

    /// <summary>
    /// Format d'image.
    /// </summary>
    public ImageFormat Format { get; set; } = ImageFormat.Png;

    /// <summary>
    /// Qualité JPEG (1-100, ignoré pour les autres formats).
    /// </summary>
    public int JpegQuality { get; set; } = 95;

    /// <summary>
    /// Inclure le timestamp dans le nom du fichier.
    /// </summary>
    public bool IncludeTimestamp { get; set; } = true;

    /// <summary>
    /// Inclure le numéro de frame dans le nom du fichier.
    /// </summary>
    public bool IncludeFrameNumber { get; set; } = true;

    /// <summary>
    /// Génère le chemin complet pour une frame.
    /// </summary>
    public string GenerateFilePath(Frame frame)
    {
        var extension = Format switch
        {
            ImageFormat.Png => ".png",
            ImageFormat.Jpeg => ".jpg",
            ImageFormat.Bmp => ".bmp",
            ImageFormat.Tiff => ".tiff",
            _ => ".png"
        };

        var parts = new List<string> { FilePrefix };

        if (IncludeTimestamp)
        {
            parts.Add(DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
        }

        if (IncludeFrameNumber)
        {
            parts.Add($"f{frame.FrameNumber:D6}");
        }

        var fileName = string.Join("_", parts) + extension;
        return Path.Combine(OutputDirectory, fileName);
    }
}

/// <summary>
/// Interface pour l'enregistrement de frames.
/// </summary>
public interface IFrameRecorder : IDisposable
{
    /// <summary>
    /// Options d'enregistrement.
    /// </summary>
    RecordingOptions Options { get; }

    /// <summary>
    /// Nombre de frames sauvegardées.
    /// </summary>
    long FramesSaved { get; }

    /// <summary>
    /// Sauvegarde une frame unique (snapshot).
    /// </summary>
    /// <returns>Chemin du fichier sauvegardé.</returns>
    string SaveSnapshot(Frame frame);

    /// <summary>
    /// Sauvegarde une frame de manière asynchrone.
    /// </summary>
    Task<string> SaveSnapshotAsync(Frame frame, CancellationToken cancellationToken = default);
}
