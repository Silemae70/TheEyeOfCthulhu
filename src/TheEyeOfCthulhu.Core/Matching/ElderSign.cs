namespace TheEyeOfCthulhu.Core.Matching;

/// <summary>
/// L'ElderSign représente un modèle de référence (template) à rechercher dans les images.
/// C'est le "golden sample" du monde de Cthulhu.
/// </summary>
public class ElderSign : IDisposable
{
    /// <summary>
    /// Nom unique de cet ElderSign.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Image du template.
    /// </summary>
    public Frame Template { get; }

    /// <summary>
    /// Largeur du template.
    /// </summary>
    public int Width => Template.Width;

    /// <summary>
    /// Hauteur du template.
    /// </summary>
    public int Height => Template.Height;

    /// <summary>
    /// Point d'ancrage (origine) dans le template.
    /// Par défaut au centre.
    /// </summary>
    public Point Anchor { get; set; }

    /// <summary>
    /// Score minimum pour considérer un match valide (0.0 - 1.0).
    /// </summary>
    public double MinScore { get; set; } = 0.7;

    /// <summary>
    /// Métadonnées additionnelles (ex: numéro de pièce, tolérance, etc.)
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = new();

    /// <summary>
    /// Date de création de l'ElderSign.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Crée un nouvel ElderSign à partir d'une frame.
    /// </summary>
    public ElderSign(string name, Frame template)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(template);

        Name = name;
        Template = template.Clone(); // Clone pour indépendance
        CreatedAt = DateTime.UtcNow;

        // Anchor au centre par défaut
        Anchor = new Point(Width / 2, Height / 2);
    }

    /// <summary>
    /// Crée un ElderSign avec un anchor personnalisé.
    /// </summary>
    public ElderSign(string name, Frame template, Point anchor) : this(name, template)
    {
        Anchor = anchor;
    }

    public void Dispose()
    {
        Template.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Point 2D simple (pour éviter dépendance à System.Drawing ou OpenCV dans Core).
/// </summary>
public readonly struct Point
{
    public int X { get; }
    public int Y { get; }

    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }

    public static Point Zero => new(0, 0);

    public override string ToString() => $"({X}, {Y})";

    public static Point operator +(Point a, Point b) => new(a.X + b.X, a.Y + b.Y);
    public static Point operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y);
}

/// <summary>
/// Point 2D en double précision.
/// </summary>
public readonly struct PointF
{
    public double X { get; }
    public double Y { get; }

    public PointF(double x, double y)
    {
        X = x;
        Y = y;
    }

    public static PointF Zero => new(0, 0);

    public override string ToString() => $"({X:F2}, {Y:F2})";

    public Point ToPoint() => new((int)X, (int)Y);
}
