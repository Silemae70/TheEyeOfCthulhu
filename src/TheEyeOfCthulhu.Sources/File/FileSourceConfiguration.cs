using TheEyeOfCthulhu.Core;

namespace TheEyeOfCthulhu.Sources.File;

/// <summary>
/// Configuration pour une source fichier (image ou vidéo).
/// </summary>
public sealed class FileSourceConfiguration : SourceConfiguration
{
    public override string SourceType => "File";

    /// <summary>
    /// Chemin vers le fichier (image ou vidéo).
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Pour les images : répéter en boucle.
    /// Pour les vidéos : loop automatique à la fin.
    /// </summary>
    public bool Loop { get; set; } = true;

    /// <summary>
    /// Pour les images : intervalle entre chaque "frame" répétée (ms).
    /// </summary>
    public int ImageRepeatIntervalMs { get; set; } = 100;

    /// <summary>
    /// Liste de fichiers images pour un mode "dossier/séquence".
    /// Si défini, FilePath est ignoré.
    /// </summary>
    public List<string>? ImageSequence { get; set; }

    /// <summary>
    /// Crée une config pour une image unique.
    /// </summary>
    public static FileSourceConfiguration FromImage(string imagePath, bool loop = true)
    {
        return new FileSourceConfiguration
        {
            FilePath = imagePath,
            Loop = loop
        };
    }

    /// <summary>
    /// Crée une config pour une vidéo.
    /// </summary>
    public static FileSourceConfiguration FromVideo(string videoPath, bool loop = true)
    {
        return new FileSourceConfiguration
        {
            FilePath = videoPath,
            Loop = loop
        };
    }

    /// <summary>
    /// Crée une config pour un dossier d'images.
    /// </summary>
    public static FileSourceConfiguration FromFolder(string folderPath, string pattern = "*.png", bool loop = true)
    {
        var files = Directory.GetFiles(folderPath, pattern)
            .OrderBy(f => f)
            .ToList();

        return new FileSourceConfiguration
        {
            ImageSequence = files,
            Loop = loop
        };
    }
}
