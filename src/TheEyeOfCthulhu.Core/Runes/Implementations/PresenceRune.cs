using TheEyeOfCthulhu.Core.Matching;

namespace TheEyeOfCthulhu.Core.Runes.Implementations;

/// <summary>
/// Mode de vérification de présence.
/// </summary>
public enum PresenceMode
{
    /// <summary>
    /// Awakened si le pattern EST trouvé.
    /// </summary>
    MustBePresent,
    
    /// <summary>
    /// Awakened si le pattern N'EST PAS trouvé.
    /// </summary>
    MustBeAbsent
}

/// <summary>
/// Configuration pour une PresenceRune.
/// </summary>
public class PresenceRuneConfig
{
    /// <summary>
    /// ElderSign à vérifier.
    /// </summary>
    public ElderSign? ElderSign { get; set; }
    
    /// <summary>
    /// Mode de vérification.
    /// </summary>
    public PresenceMode Mode { get; set; } = PresenceMode.MustBePresent;
    
    /// <summary>
    /// Type de matcher à utiliser.
    /// </summary>
    public ElderSignMatcherType MatcherType { get; set; } = ElderSignMatcherType.AKAZE;
    
    /// <summary>
    /// Zone de recherche (null = utiliser le Glyph dynamique ou toute l'image).
    /// </summary>
    public Rectangle? SearchGlyph { get; set; }
    
    /// <summary>
    /// Utiliser le Glyph dynamique du contexte si disponible.
    /// </summary>
    public bool UseDynamicGlyph { get; set; } = true;
}

/// <summary>
/// Rune de vérification de présence/absence.
/// Vérifie simplement si un élément est présent ou absent.
/// Plus simple que ElderSignRune - juste un contrôle binaire.
/// </summary>
public class PresenceRune : RuneBase
{
    private readonly string _name;
    private readonly PresenceRuneConfig _config;
    private IElderSignMatcher? _matcher;
    
    public override string Name => _name;
    public override string RuneType => "Presence";
    public override string Description => _config.Mode == PresenceMode.MustBePresent
        ? $"Verify '{_config.ElderSign?.Name ?? "?"}' is PRESENT"
        : $"Verify '{_config.ElderSign?.Name ?? "?"}' is ABSENT";
    
    public PresenceRuneConfig Config => _config;
    
    public PresenceRune(string name, PresenceRuneConfig? config = null)
    {
        _name = name;
        _config = config ?? new PresenceRuneConfig();
    }
    
    public PresenceRune(string name, ElderSign elderSign, PresenceMode mode = PresenceMode.MustBePresent)
        : this(name, new PresenceRuneConfig { ElderSign = elderSign, Mode = mode })
    {
    }
    
    public void SetMatcher(IElderSignMatcher matcher)
    {
        _matcher?.Dispose();
        _matcher = matcher;
    }
    
    protected override Vision ExecuteCore(RuneContext context)
    {
        if (_config.ElderSign == null)
        {
            return Vision.Void(Name, "No ElderSign configured");
        }
        
        if (_matcher == null)
        {
            return Vision.Void(Name, "No matcher configured");
        }
        
        // Rechercher
        var result = _matcher.Search(context.CurrentFrame!, _config.ElderSign);
        var isFound = result.Matches.Count > 0 && result.Matches[0].Score >= ResonanceThreshold;
        
        // Évaluer selon le mode
        bool isAwakened;
        string message;
        
        if (_config.Mode == PresenceMode.MustBePresent)
        {
            isAwakened = isFound;
            message = isFound 
                ? $"✓ '{_config.ElderSign.Name}' is PRESENT (score: {result.Matches[0].Score:P0})"
                : $"✗ '{_config.ElderSign.Name}' is MISSING";
        }
        else // MustBeAbsent
        {
            isAwakened = !isFound;
            message = isFound
                ? $"✗ '{_config.ElderSign.Name}' was found but should be ABSENT"
                : $"✓ '{_config.ElderSign.Name}' is correctly ABSENT";
        }
        
        var resonance = isFound ? result.Matches[0].Score : 0;
        
        return new Vision
        {
            RuneName = Name,
            State = isAwakened ? VisionState.Awakened : VisionState.Dormant,
            Resonance = isAwakened ? (isFound ? resonance : 1.0) : 0,
            Message = message,
            Position = isFound ? result.Matches[0].Position : null,
            Metadata = new Dictionary<string, object>
            {
                ["Mode"] = _config.Mode.ToString(),
                ["PatternFound"] = isFound,
                ["SearchTimeMs"] = result.SearchTimeMs
            }
        };
    }
    
    protected override bool ValidateCore(out string? errorMessage)
    {
        if (_config.ElderSign == null)
        {
            errorMessage = "ElderSign is not configured";
            return false;
        }
        
        if (_matcher == null)
        {
            errorMessage = "Matcher is not configured";
            return false;
        }
        
        errorMessage = null;
        return true;
    }
    
    public override void Dispose()
    {
        _matcher?.Dispose();
        _matcher = null;
        base.Dispose();
    }
}
