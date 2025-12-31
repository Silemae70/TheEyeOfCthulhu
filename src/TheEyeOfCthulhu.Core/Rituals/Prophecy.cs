using TheEyeOfCthulhu.Core.Runes;

namespace TheEyeOfCthulhu.Core.Rituals;

/// <summary>
/// Résultat global d'un Ritual (Prophecy).
/// Contient le verdict final et toutes les Visions individuelles.
/// </summary>
public class Prophecy
{
    /// <summary>
    /// Nom du Ritual qui a produit cette Prophecy.
    /// </summary>
    public string RitualName { get; init; } = string.Empty;
    
    /// <summary>
    /// État global du Ritual.
    /// </summary>
    public VisionState State { get; init; } = VisionState.Void;
    
    /// <summary>
    /// Horodatage de l'exécution.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;
    
    /// <summary>
    /// Temps total d'exécution en millisecondes.
    /// </summary>
    public double TotalExecutionTimeMs { get; init; }
    
    /// <summary>
    /// Resonance globale (moyenne ou minimum selon la stratégie).
    /// </summary>
    public double GlobalResonance { get; init; }
    
    /// <summary>
    /// Toutes les Visions produites par les Runes.
    /// </summary>
    public IReadOnlyList<Vision> Visions { get; init; } = Array.Empty<Vision>();
    
    /// <summary>
    /// Message descriptif du résultat global.
    /// </summary>
    public string Message { get; init; } = string.Empty;
    
    /// <summary>
    /// Nombre de Runes exécutées.
    /// </summary>
    public int RunesExecuted => Visions.Count;
    
    /// <summary>
    /// Nombre de Runes Awakened.
    /// </summary>
    public int RunesAwakened => Visions.Count(v => v.State == VisionState.Awakened);
    
    /// <summary>
    /// Nombre de Runes Dormant.
    /// </summary>
    public int RunesDormant => Visions.Count(v => v.State == VisionState.Dormant);
    
    /// <summary>
    /// Le Ritual est-il globalement Awakened ?
    /// </summary>
    public bool IsAwakened => State == VisionState.Awakened;
    
    /// <summary>
    /// Récupère la Vision d'une Rune par son nom.
    /// </summary>
    public Vision? GetVision(string runeName)
    {
        return Visions.FirstOrDefault(v => v.RuneName == runeName);
    }
    
    /// <summary>
    /// Crée une Prophecy réussie.
    /// </summary>
    public static Prophecy Awakened(string ritualName, IReadOnlyList<Vision> visions, double executionTimeMs)
    {
        var globalResonance = visions.Count > 0 
            ? visions.Average(v => v.Resonance) 
            : 0;
            
        return new Prophecy
        {
            RitualName = ritualName,
            State = VisionState.Awakened,
            Visions = visions,
            GlobalResonance = globalResonance,
            TotalExecutionTimeMs = executionTimeMs,
            Message = $"All {visions.Count} runes awakened"
        };
    }
    
    /// <summary>
    /// Crée une Prophecy échouée.
    /// </summary>
    public static Prophecy Dormant(string ritualName, IReadOnlyList<Vision> visions, double executionTimeMs, string? reason = null)
    {
        var dormantRunes = visions.Where(v => v.State == VisionState.Dormant).Select(v => v.RuneName);
        
        return new Prophecy
        {
            RitualName = ritualName,
            State = VisionState.Dormant,
            Visions = visions,
            GlobalResonance = 0,
            TotalExecutionTimeMs = executionTimeMs,
            Message = reason ?? $"Dormant runes: {string.Join(", ", dormantRunes)}"
        };
    }
    
    /// <summary>
    /// Crée une Prophecy en erreur.
    /// </summary>
    public static Prophecy Void(string ritualName, string errorMessage)
    {
        return new Prophecy
        {
            RitualName = ritualName,
            State = VisionState.Void,
            Message = errorMessage
        };
    }
}
