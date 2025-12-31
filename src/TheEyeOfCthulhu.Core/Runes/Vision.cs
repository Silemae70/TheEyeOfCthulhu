namespace TheEyeOfCthulhu.Core.Runes;

/// <summary>
/// État résultant de l'exécution d'une Rune.
/// </summary>
public enum VisionState
{
    /// <summary>
    /// La Rune a réussi son objectif (PASS).
    /// </summary>
    Awakened,
    
    /// <summary>
    /// La Rune a échoué son objectif (FAIL).
    /// </summary>
    Dormant,
    
    /// <summary>
    /// Résultat incertain, proche du seuil (WARN).
    /// </summary>
    Uncertain,
    
    /// <summary>
    /// Erreur lors de l'exécution (ERROR).
    /// </summary>
    Void
}

/// <summary>
/// Résultat de l'exécution d'une Rune individuelle.
/// </summary>
public record Vision
{
    /// <summary>
    /// Nom de la Rune qui a produit cette Vision.
    /// </summary>
    public string RuneName { get; init; } = string.Empty;
    
    /// <summary>
    /// État résultant.
    /// </summary>
    public VisionState State { get; init; } = VisionState.Void;
    
    /// <summary>
    /// Niveau de correspondance/confiance (0.0 à 1.0).
    /// </summary>
    public double Resonance { get; init; }
    
    /// <summary>
    /// Temps d'exécution en millisecondes.
    /// </summary>
    public double ExecutionTimeMs { get; init; }
    
    /// <summary>
    /// Message descriptif du résultat.
    /// </summary>
    public string Message { get; init; } = string.Empty;
    
    /// <summary>
    /// Position trouvée (si applicable).
    /// </summary>
    public PointF? Position { get; init; }
    
    /// <summary>
    /// Angle détecté en degrés (si applicable).
    /// </summary>
    public double? Angle { get; init; }
    
    /// <summary>
    /// Échelle détectée (si applicable).
    /// </summary>
    public double? Scale { get; init; }
    
    /// <summary>
    /// Texte lu (pour WhisperRune/OCR).
    /// </summary>
    public string? Text { get; init; }
    
    /// <summary>
    /// Valeur mesurée (pour MeasureRune).
    /// </summary>
    public double? MeasuredValue { get; init; }
    
    /// <summary>
    /// Données additionnelles spécifiques à la Rune.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
    
    /// <summary>
    /// Crée une Vision réussie (Awakened).
    /// </summary>
    public static Vision Awakened(string runeName, double resonance, string? message = null)
    {
        return new Vision
        {
            RuneName = runeName,
            State = VisionState.Awakened,
            Resonance = resonance,
            Message = message ?? "Success"
        };
    }
    
    /// <summary>
    /// Crée une Vision échouée (Dormant).
    /// </summary>
    public static Vision Dormant(string runeName, string? message = null)
    {
        return new Vision
        {
            RuneName = runeName,
            State = VisionState.Dormant,
            Resonance = 0,
            Message = message ?? "Not found"
        };
    }
    
    /// <summary>
    /// Crée une Vision en erreur (Void).
    /// </summary>
    public static Vision Void(string runeName, string errorMessage)
    {
        return new Vision
        {
            RuneName = runeName,
            State = VisionState.Void,
            Resonance = 0,
            Message = errorMessage
        };
    }
}
