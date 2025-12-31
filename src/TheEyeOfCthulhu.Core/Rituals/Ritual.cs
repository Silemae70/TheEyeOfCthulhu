using TheEyeOfCthulhu.Core.Runes;

namespace TheEyeOfCthulhu.Core.Rituals;

/// <summary>
/// Stratégie pour déterminer l'état global d'un Ritual.
/// </summary>
public enum ProphecyStrategy
{
    /// <summary>
    /// Awakened seulement si TOUTES les Runes sont Awakened.
    /// </summary>
    AllMustAwaken,
    
    /// <summary>
    /// Awakened si AU MOINS UNE Rune est Awakened.
    /// </summary>
    AnyAwakened,
    
    /// <summary>
    /// Awakened si la MAJORITÉ des Runes sont Awakened.
    /// </summary>
    MajorityAwakened,
    
    /// <summary>
    /// Utiliser une logique personnalisée.
    /// </summary>
    Custom
}

/// <summary>
/// Interface pour un Ritual (programme de vision).
/// </summary>
public interface IRitual : IDisposable
{
    /// <summary>
    /// Nom unique du Ritual.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Description du Ritual.
    /// </summary>
    string Description { get; set; }
    
    /// <summary>
    /// Liste des Runes à exécuter dans l'ordre.
    /// </summary>
    IReadOnlyList<IRune> Runes { get; }
    
    /// <summary>
    /// Stratégie pour déterminer le résultat global.
    /// </summary>
    ProphecyStrategy Strategy { get; set; }
    
    /// <summary>
    /// Arrêter l'exécution dès qu'une Rune est Dormant.
    /// </summary>
    bool StopOnFirstDormant { get; set; }
    
    /// <summary>
    /// Exécute le Ritual complet sur une frame.
    /// </summary>
    Prophecy Execute(Frame frame);
    
    /// <summary>
    /// Ajoute une Rune au Ritual.
    /// </summary>
    IRitual AddRune(IRune rune);
    
    /// <summary>
    /// Retire une Rune du Ritual.
    /// </summary>
    bool RemoveRune(string runeName);
    
    /// <summary>
    /// Valide la configuration du Ritual.
    /// </summary>
    bool Validate(out List<string> errors);
}

/// <summary>
/// Implémentation standard d'un Ritual.
/// </summary>
public class Ritual : IRitual
{
    private readonly List<IRune> _runes = new();
    
    public string Name { get; }
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<IRune> Runes => _runes.AsReadOnly();
    public ProphecyStrategy Strategy { get; set; } = ProphecyStrategy.AllMustAwaken;
    public bool StopOnFirstDormant { get; set; } = false;
    
    public Ritual(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Ritual name cannot be empty", nameof(name));
            
        Name = name;
    }
    
    public Prophecy Execute(Frame frame)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        if (_runes.Count == 0)
        {
            return Prophecy.Void(Name, "No runes configured");
        }
        
        var context = new RuneContext { CurrentFrame = frame };
        var visions = new List<Vision>();
        
        foreach (var rune in _runes)
        {
            if (!rune.IsEnabled)
                continue;
                
            var vision = rune.Execute(context);
            visions.Add(vision);
            context.AddVision(vision);
            
            // Arrêter si demandé et Dormant
            if (StopOnFirstDormant && vision.State == VisionState.Dormant)
            {
                break;
            }
            
            // Arrêter si erreur
            if (vision.State == VisionState.Void)
            {
                break;
            }
        }
        
        sw.Stop();
        
        // Déterminer l'état global selon la stratégie
        var globalState = DetermineGlobalState(visions);
        
        return globalState == VisionState.Awakened
            ? Prophecy.Awakened(Name, visions, sw.Elapsed.TotalMilliseconds)
            : Prophecy.Dormant(Name, visions, sw.Elapsed.TotalMilliseconds);
    }
    
    private VisionState DetermineGlobalState(List<Vision> visions)
    {
        if (visions.Count == 0)
            return VisionState.Void;
            
        if (visions.Any(v => v.State == VisionState.Void))
            return VisionState.Void;
        
        return Strategy switch
        {
            ProphecyStrategy.AllMustAwaken => 
                visions.All(v => v.State == VisionState.Awakened) 
                    ? VisionState.Awakened 
                    : VisionState.Dormant,
                    
            ProphecyStrategy.AnyAwakened => 
                visions.Any(v => v.State == VisionState.Awakened) 
                    ? VisionState.Awakened 
                    : VisionState.Dormant,
                    
            ProphecyStrategy.MajorityAwakened => 
                visions.Count(v => v.State == VisionState.Awakened) > visions.Count / 2 
                    ? VisionState.Awakened 
                    : VisionState.Dormant,
                    
            _ => VisionState.Dormant
        };
    }
    
    public IRitual AddRune(IRune rune)
    {
        if (_runes.Any(r => r.Name == rune.Name))
            throw new ArgumentException($"Rune '{rune.Name}' already exists in this Ritual");
            
        _runes.Add(rune);
        return this;
    }
    
    public bool RemoveRune(string runeName)
    {
        var rune = _runes.FirstOrDefault(r => r.Name == runeName);
        if (rune == null) return false;
        
        _runes.Remove(rune);
        return true;
    }
    
    public bool Validate(out List<string> errors)
    {
        errors = new List<string>();
        
        if (_runes.Count == 0)
        {
            errors.Add("Ritual has no runes");
        }
        
        foreach (var rune in _runes)
        {
            if (!rune.Validate(out var runeError) && runeError != null)
            {
                errors.Add($"Rune '{rune.Name}': {runeError}");
            }
        }
        
        return errors.Count == 0;
    }
    
    public void Dispose()
    {
        foreach (var rune in _runes)
        {
            rune.Dispose();
        }
        _runes.Clear();
        GC.SuppressFinalize(this);
    }
}
