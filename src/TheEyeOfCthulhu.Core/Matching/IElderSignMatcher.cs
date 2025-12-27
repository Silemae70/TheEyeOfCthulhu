namespace TheEyeOfCthulhu.Core.Matching;

/// <summary>
/// Interface pour les matchers d'ElderSign.
/// Chaque implémentation utilise une stratégie différente (template, features, shape...).
/// </summary>
public interface IElderSignMatcher : IDisposable
{
    /// <summary>
    /// Nom du matcher (ex: "TemplateMatch", "FeatureMatch").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Recherche un ElderSign dans une frame.
    /// </summary>
    /// <param name="frame">Image dans laquelle chercher.</param>
    /// <param name="elderSign">Le signe à trouver.</param>
    /// <returns>Résultat de la recherche.</returns>
    ElderSignSearchResult Search(Frame frame, ElderSign elderSign);

    /// <summary>
    /// Recherche plusieurs ElderSigns dans une frame.
    /// </summary>
    /// <param name="frame">Image dans laquelle chercher.</param>
    /// <param name="elderSigns">Les signes à trouver.</param>
    /// <returns>Résultats par ElderSign.</returns>
    Dictionary<string, ElderSignSearchResult> SearchAll(Frame frame, IEnumerable<ElderSign> elderSigns);

    /// <summary>
    /// Recherche toutes les occurrences d'un ElderSign (pas seulement la meilleure).
    /// </summary>
    /// <param name="frame">Image dans laquelle chercher.</param>
    /// <param name="elderSign">Le signe à trouver.</param>
    /// <param name="maxMatches">Nombre maximum de matches à retourner.</param>
    /// <returns>Résultat avec plusieurs matches potentiels.</returns>
    ElderSignSearchResult SearchMultiple(Frame frame, ElderSign elderSign, int maxMatches = 10);
}

/// <summary>
/// Options de recherche pour les matchers.
/// </summary>
public class MatcherOptions
{
    /// <summary>
    /// Score minimum pour considérer un match valide (0.0 - 1.0).
    /// Override le MinScore de l'ElderSign si défini.
    /// </summary>
    public double? MinScore { get; set; }

    /// <summary>
    /// Rechercher à plusieurs échelles.
    /// </summary>
    public bool MultiScale { get; set; } = false;

    /// <summary>
    /// Échelles à tester si MultiScale est activé.
    /// </summary>
    public double[] Scales { get; set; } = { 0.8, 0.9, 1.0, 1.1, 1.2 };

    /// <summary>
    /// Rechercher à plusieurs angles.
    /// </summary>
    public bool MultiAngle { get; set; } = false;

    /// <summary>
    /// Pas d'angle en degrés si MultiAngle est activé.
    /// </summary>
    public double AngleStep { get; set; } = 5.0;

    /// <summary>
    /// Plage d'angles à tester (ex: -30 à +30).
    /// </summary>
    public double AngleRange { get; set; } = 30.0;

    /// <summary>
    /// Région d'intérêt (null = toute l'image).
    /// </summary>
    public Rectangle? RegionOfInterest { get; set; }

    /// <summary>
    /// Options par défaut.
    /// </summary>
    public static MatcherOptions Default => new();
}
