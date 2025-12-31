using TheEyeOfCthulhu.Core;

namespace TheEyeOfCthulhu.Core.Matching;

/// <summary>
/// Résultat d'une recherche d'ElderSign dans une image.
/// </summary>
public class ElderSignMatch
{
    /// <summary>
    /// L'ElderSign qui a été trouvé.
    /// </summary>
    public ElderSign ElderSign { get; }

    /// <summary>
    /// Position trouvée (coin supérieur gauche du match).
    /// </summary>
    public PointF Position { get; }

    /// <summary>
    /// Position du point d'ancrage dans l'image.
    /// </summary>
    public PointF AnchorPosition => new(
        Position.X + ElderSign.Anchor.X * (float)Scale,
        Position.Y + ElderSign.Anchor.Y * (float)Scale);

    /// <summary>
    /// Score de confiance (0.0 - 1.0).
    /// </summary>
    public double Score { get; }

    /// <summary>
    /// Le match est-il valide (score >= MinScore) ?
    /// </summary>
    public bool IsValid => Score >= ElderSign.MinScore;

    /// <summary>
    /// Angle de rotation détecté (en degrés, 0 si non supporté).
    /// </summary>
    public double Angle { get; init; } = 0;

    /// <summary>
    /// Facteur d'échelle détecté (1.0 = même taille, > 1 = plus grand).
    /// </summary>
    public double Scale { get; init; } = 1.0;

    /// <summary>
    /// Les 4 coins du quadrilatère transformé (TopLeft, TopRight, BottomRight, BottomLeft).
    /// Null si le matcher ne supporte pas cette info (ex: template matching).
    /// </summary>
    public PointF[]? TransformedCorners { get; init; }

    /// <summary>
    /// Rectangle englobant le match (axis-aligned bounding box).
    /// </summary>
    public Rectangle BoundingBox
    {
        get
        {
            if (TransformedCorners != null && TransformedCorners.Length == 4)
            {
                var minX = (int)TransformedCorners.Min(p => p.X);
                var minY = (int)TransformedCorners.Min(p => p.Y);
                var maxX = (int)TransformedCorners.Max(p => p.X);
                var maxY = (int)TransformedCorners.Max(p => p.Y);
                return new Rectangle(minX, minY, maxX - minX, maxY - minY);
            }
            return new Rectangle(
                (int)Position.X,
                (int)Position.Y,
                (int)(ElderSign.Width * Scale),
                (int)(ElderSign.Height * Scale));
        }
    }

    public ElderSignMatch(ElderSign elderSign, PointF position, double score)
    {
        ElderSign = elderSign ?? throw new ArgumentNullException(nameof(elderSign));
        Position = position;
        Score = Math.Clamp(score, 0.0, 1.0);
    }

    public override string ToString() =>
        $"Match '{ElderSign.Name}' at {AnchorPosition} (score: {Score:P1})";
}

/// <summary>
/// Résultat d'une recherche avec potentiellement plusieurs matches.
/// </summary>
public class ElderSignSearchResult
{
    /// <summary>
    /// Liste des matches trouvés, triés par score décroissant.
    /// </summary>
    public IReadOnlyList<ElderSignMatch> Matches { get; }

    /// <summary>
    /// Le meilleur match (ou null si aucun).
    /// </summary>
    public ElderSignMatch? BestMatch => Matches.Count > 0 ? Matches[0] : null;

    /// <summary>
    /// Au moins un match valide a été trouvé.
    /// </summary>
    public bool Found => BestMatch?.IsValid == true;

    /// <summary>
    /// Nombre de matches trouvés.
    /// </summary>
    public int Count => Matches.Count;

    /// <summary>
    /// Temps de recherche en millisecondes.
    /// </summary>
    public double SearchTimeMs { get; }

    public ElderSignSearchResult(IEnumerable<ElderSignMatch> matches, double searchTimeMs)
    {
        Matches = matches
            .OrderByDescending(m => m.Score)
            .ToList();
        SearchTimeMs = searchTimeMs;
    }

    public static ElderSignSearchResult Empty(double searchTimeMs = 0) =>
        new(Enumerable.Empty<ElderSignMatch>(), searchTimeMs);
}
