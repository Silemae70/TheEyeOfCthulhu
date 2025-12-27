using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheEyeOfCthulhu.Core.Matching;

/// <summary>
/// Gère la sauvegarde et le chargement des ElderSigns sur disque.
/// Format : dossier avec {name}.png + {name}.json
/// </summary>
public class ElderSignStorage
{
    private readonly string _baseDirectory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Crée un storage dans le dossier spécifié.
    /// </summary>
    public ElderSignStorage(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
        
        if (!Directory.Exists(_baseDirectory))
        {
            Directory.CreateDirectory(_baseDirectory);
        }
    }

    /// <summary>
    /// Sauvegarde un ElderSign sur disque.
    /// </summary>
    public void Save(ElderSign elderSign)
    {
        ArgumentNullException.ThrowIfNull(elderSign);

        var safeName = SanitizeFileName(elderSign.Name);
        var imagePath = Path.Combine(_baseDirectory, $"{safeName}.png");
        var metaPath = Path.Combine(_baseDirectory, $"{safeName}.json");

        // Sauvegarder l'image (raw bytes pour l'instant, on convertira en PNG dans Sources)
        SaveRawImage(elderSign.Template, imagePath);

        // Sauvegarder les métadonnées
        var meta = new ElderSignMetadata
        {
            Name = elderSign.Name,
            Width = elderSign.Width,
            Height = elderSign.Height,
            AnchorX = elderSign.Anchor.X,
            AnchorY = elderSign.Anchor.Y,
            MinScore = elderSign.MinScore,
            Format = elderSign.Template.Format.ToString(),
            CreatedAt = elderSign.CreatedAt,
            Metadata = elderSign.Metadata
        };

        var json = JsonSerializer.Serialize(meta, JsonOptions);
        File.WriteAllText(metaPath, json);
    }

    /// <summary>
    /// Charge un ElderSign depuis le disque.
    /// </summary>
    public ElderSign? Load(string name)
    {
        var safeName = SanitizeFileName(name);
        var imagePath = Path.Combine(_baseDirectory, $"{safeName}.png");
        var metaPath = Path.Combine(_baseDirectory, $"{safeName}.json");

        if (!File.Exists(imagePath) || !File.Exists(metaPath))
        {
            return null;
        }

        // Charger les métadonnées
        var json = File.ReadAllText(metaPath);
        var meta = JsonSerializer.Deserialize<ElderSignMetadata>(json, JsonOptions);
        
        if (meta == null) return null;

        // Charger l'image
        var frame = LoadRawImage(imagePath, meta);
        if (frame == null) return null;

        var elderSign = new ElderSign(meta.Name, frame, new Point(meta.AnchorX, meta.AnchorY))
        {
            MinScore = meta.MinScore
        };

        // Restaurer les métadonnées custom
        foreach (var kvp in meta.Metadata)
        {
            elderSign.Metadata[kvp.Key] = kvp.Value;
        }

        frame.Dispose(); // Le constructeur clone

        return elderSign;
    }

    /// <summary>
    /// Liste tous les ElderSigns disponibles.
    /// </summary>
    public IEnumerable<string> ListAvailable()
    {
        if (!Directory.Exists(_baseDirectory))
        {
            yield break;
        }

        foreach (var jsonFile in Directory.GetFiles(_baseDirectory, "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(jsonFile);
            var imagePath = Path.Combine(_baseDirectory, $"{name}.png");
            
            if (File.Exists(imagePath))
            {
                yield return name;
            }
        }
    }

    /// <summary>
    /// Supprime un ElderSign du disque.
    /// </summary>
    public bool Delete(string name)
    {
        var safeName = SanitizeFileName(name);
        var imagePath = Path.Combine(_baseDirectory, $"{safeName}.png");
        var metaPath = Path.Combine(_baseDirectory, $"{safeName}.json");

        var deleted = false;

        if (File.Exists(imagePath))
        {
            File.Delete(imagePath);
            deleted = true;
        }

        if (File.Exists(metaPath))
        {
            File.Delete(metaPath);
            deleted = true;
        }

        return deleted;
    }

    /// <summary>
    /// Vérifie si un ElderSign existe.
    /// </summary>
    public bool Exists(string name)
    {
        var safeName = SanitizeFileName(name);
        var imagePath = Path.Combine(_baseDirectory, $"{safeName}.png");
        var metaPath = Path.Combine(_baseDirectory, $"{safeName}.json");

        return File.Exists(imagePath) && File.Exists(metaPath);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    private static void SaveRawImage(Frame frame, string path)
    {
        // Sauvegarde raw : header simple + pixels
        // Format: [4 bytes width][4 bytes height][4 bytes format][4 bytes stride][pixels...]
        using var fs = new FileStream(path, FileMode.Create);
        using var bw = new BinaryWriter(fs);
        
        bw.Write(frame.Width);
        bw.Write(frame.Height);
        bw.Write((int)frame.Format);
        bw.Write(frame.Stride);
        bw.Write(frame.RawBuffer);
    }

    private static Frame? LoadRawImage(string path, ElderSignMetadata meta)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open);
            using var br = new BinaryReader(fs);
            
            var width = br.ReadInt32();
            var height = br.ReadInt32();
            var format = (PixelFormat)br.ReadInt32();
            var stride = br.ReadInt32();
            
            var dataLength = stride * height;
            var data = br.ReadBytes(dataLength);

            return new Frame(data, width, height, format, 0, stride);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Métadonnées JSON d'un ElderSign.
/// </summary>
internal class ElderSignMetadata
{
    public string Name { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public int AnchorX { get; set; }
    public int AnchorY { get; set; }
    public double MinScore { get; set; }
    public string Format { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
