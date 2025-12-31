using TheEyeOfCthulhu.Core;

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
    /// Masque binaire définissant la forme réelle de l'objet (null = rectangle complet).
    /// Format: Gray8, mêmes dimensions que Template. 255 = objet, 0 = fond.
    /// </summary>
    public Frame? Mask { get; private set; }

    /// <summary>
    /// Points du contour de l'objet (null = pas de contour défini).
    /// Utilisé pour dessiner la vraie forme au lieu d'un rectangle.
    /// </summary>
    public PointF[]? ContourPoints { get; private set; }

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
    /// Indique si cet ElderSign a un masque/contour défini.
    /// </summary>
    public bool HasMask => Mask != null || ContourPoints != null;

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

    /// <summary>
    /// Définit le masque binaire de l'objet.
    /// </summary>
    public void SetMask(Frame mask)
    {
        if (mask.Width != Width || mask.Height != Height)
            throw new ArgumentException("Mask dimensions must match template dimensions");
        
        Mask?.Dispose();
        Mask = mask.Clone();
    }

    /// <summary>
    /// Définit les points du contour de l'objet.
    /// </summary>
    public void SetContour(PointF[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length < 3)
            throw new ArgumentException("Contour must have at least 3 points");
        
        ContourPoints = points.ToArray(); // Copie
    }

    /// <summary>
    /// Définit les points du contour à partir de points entiers.
    /// </summary>
    public void SetContour(Point[] points)
    {
        SetContour(points.Select(p => new PointF(p.X, p.Y)).ToArray());
    }

    public void Dispose()
    {
        Template.Dispose();
        Mask?.Dispose();
        GC.SuppressFinalize(this);
    }
}
