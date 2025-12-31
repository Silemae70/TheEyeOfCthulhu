namespace TheEyeOfCthulhu.Core.Runes;

/// <summary>
/// Contexte partagé entre les Runes d'un même Ritual.
/// Permet de passer des informations d'une Rune à l'autre.
/// </summary>
public class RuneContext
{
    /// <summary>
    /// Frame actuelle à analyser.
    /// </summary>
    public Frame? CurrentFrame { get; set; }
    
    /// <summary>
    /// Position de référence trouvée par une Rune précédente (ex: SummonRune).
    /// Les Runes suivantes peuvent utiliser cette position comme origine.
    /// </summary>
    public PointF? ReferencePosition { get; set; }
    
    /// <summary>
    /// Angle de référence trouvé par une Rune précédente.
    /// Permet d'ajuster les recherches suivantes.
    /// </summary>
    public double? ReferenceAngle { get; set; }
    
    /// <summary>
    /// Échelle de référence trouvée par une Rune précédente.
    /// </summary>
    public double? ReferenceScale { get; set; }
    
    /// <summary>
    /// Glyph (ROI) dynamique défini par une Rune précédente.
    /// </summary>
    public Rectangle? DynamicGlyph { get; set; }
    
    /// <summary>
    /// Résultats des Runes précédentes, indexés par nom.
    /// </summary>
    public Dictionary<string, Vision> PreviousVisions { get; } = new();
    
    /// <summary>
    /// Données partagées entre Runes (clé-valeur libre).
    /// </summary>
    public Dictionary<string, object> SharedData { get; } = new();
    
    /// <summary>
    /// Ajoute une Vision au contexte.
    /// </summary>
    public void AddVision(Vision vision)
    {
        PreviousVisions[vision.RuneName] = vision;
        
        // Mettre à jour les références si la Vision contient des infos de position
        if (vision.State == VisionState.Awakened)
        {
            if (vision.Position.HasValue)
                ReferencePosition = vision.Position;
            
            if (vision.Angle.HasValue)
                ReferenceAngle = vision.Angle;
            
            if (vision.Scale.HasValue)
                ReferenceScale = vision.Scale;
        }
    }
    
    /// <summary>
    /// Récupère la Vision d'une Rune précédente.
    /// </summary>
    public Vision? GetVision(string runeName)
    {
        return PreviousVisions.TryGetValue(runeName, out var vision) ? vision : null;
    }
    
    /// <summary>
    /// Vérifie si toutes les Runes précédentes sont Awakened.
    /// </summary>
    public bool AllPreviousAwakened()
    {
        return PreviousVisions.Values.All(v => v.State == VisionState.Awakened);
    }
    
    /// <summary>
    /// Réinitialise le contexte pour une nouvelle exécution.
    /// </summary>
    public void Reset()
    {
        CurrentFrame = null;
        ReferencePosition = null;
        ReferenceAngle = null;
        ReferenceScale = null;
        DynamicGlyph = null;
        PreviousVisions.Clear();
        SharedData.Clear();
    }
}
