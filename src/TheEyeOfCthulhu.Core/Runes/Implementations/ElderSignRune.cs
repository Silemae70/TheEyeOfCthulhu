using TheEyeOfCthulhu.Core.Matching;

namespace TheEyeOfCthulhu.Core.Runes.Implementations;

/// <summary>
/// Configuration pour une ElderSignRune.
/// </summary>
public class ElderSignRuneConfig
{
    /// <summary>
    /// ElderSign à rechercher.
    /// </summary>
    public ElderSign? ElderSign { get; set; }
    
    /// <summary>
    /// Type de matcher à utiliser.
    /// </summary>
    public ElderSignMatcherType MatcherType { get; set; } = ElderSignMatcherType.AKAZE;
    
    /// <summary>
    /// Zone de recherche (null = toute l'image).
    /// </summary>
    public Rectangle? SearchGlyph { get; set; }
    
    /// <summary>
    /// Utiliser le Glyph dynamique du contexte si disponible.
    /// </summary>
    public bool UseDynamicGlyph { get; set; } = true;
    
    /// <summary>
    /// Mettre à jour la position de référence du contexte si trouvé.
    /// </summary>
    public bool UpdateContextPosition { get; set; } = true;
}

/// <summary>
/// Rune de détection d'ElderSign (pattern matching).
/// Recherche un modèle dans l'image et retourne sa position/orientation.
/// </summary>
public class ElderSignRune : RuneBase
{
    private readonly string _name;
    private readonly ElderSignRuneConfig _config;
    private IElderSignMatcher? _matcher;
    
    public override string Name => _name;
    public override string RuneType => "ElderSign";
    public override string Description => $"Search for ElderSign '{_config.ElderSign?.Name ?? "undefined"}'";
    
    /// <summary>
    /// Configuration de la Rune.
    /// </summary>
    public ElderSignRuneConfig Config => _config;
    
    /// <summary>
    /// Crée une nouvelle ElderSignRune.
    /// </summary>
    /// <param name="name">Nom unique de la Rune.</param>
    /// <param name="config">Configuration (optionnel, peut être défini après).</param>
    public ElderSignRune(string name, ElderSignRuneConfig? config = null)
    {
        _name = name;
        _config = config ?? new ElderSignRuneConfig();
    }
    
    /// <summary>
    /// Crée une ElderSignRune avec un ElderSign directement.
    /// </summary>
    public ElderSignRune(string name, ElderSign elderSign, ElderSignMatcherType matcherType = ElderSignMatcherType.AKAZE)
        : this(name, new ElderSignRuneConfig { ElderSign = elderSign, MatcherType = matcherType })
    {
    }
    
    /// <summary>
    /// Définit ou change le matcher utilisé.
    /// </summary>
    public void SetMatcher(IElderSignMatcher matcher)
    {
        _matcher?.Dispose();
        _matcher = matcher;
    }
    
    protected override Vision ExecuteCore(RuneContext context)
    {
        // Vérifier qu'on a un ElderSign
        if (_config.ElderSign == null)
        {
            return Vision.Void(Name, "No ElderSign configured");
        }
        
        // Vérifier qu'on a un matcher
        if (_matcher == null)
        {
            return Vision.Void(Name, "No matcher configured - call SetMatcher() first");
        }
        
        // Déterminer la zone de recherche
        var searchOptions = CreateMatcherOptions(context);
        
        // Exécuter la recherche
        var result = _matcher.Search(context.CurrentFrame!, _config.ElderSign);
        
        if (result.Matches.Count == 0)
        {
            return Vision.Dormant(Name, $"ElderSign '{_config.ElderSign.Name}' not found");
        }
        
        var bestMatch = result.Matches[0];
        
        // Vérifier le seuil de Resonance
        if (bestMatch.Score < ResonanceThreshold)
        {
            return new Vision
            {
                RuneName = Name,
                State = VisionState.Dormant,
                Resonance = bestMatch.Score,
                Message = $"Score {bestMatch.Score:P0} below threshold {ResonanceThreshold:P0}",
                Position = bestMatch.Position,
                Angle = bestMatch.Angle,
                Scale = bestMatch.Scale
            };
        }
        
        // Succès !
        var vision = new Vision
        {
            RuneName = Name,
            State = VisionState.Awakened,
            Resonance = bestMatch.Score,
            Message = $"Found '{_config.ElderSign.Name}' with {bestMatch.Score:P0} confidence",
            Position = bestMatch.Position,
            Angle = bestMatch.Angle,
            Scale = bestMatch.Scale,
            Metadata = new Dictionary<string, object>
            {
                ["ElderSignName"] = _config.ElderSign.Name,
                ["MatcherType"] = _config.MatcherType.ToString(),
                ["SearchTimeMs"] = result.SearchTimeMs
            }
        };
        
        // Ajouter les coins transformés si disponibles
        if (bestMatch.TransformedCorners != null)
        {
            vision.Metadata["TransformedCorners"] = bestMatch.TransformedCorners;
        }
        
        return vision;
    }
    
    private MatcherOptions CreateMatcherOptions(RuneContext context)
    {
        var options = new MatcherOptions
        {
            MinScore = ResonanceThreshold
        };
        
        // Priorité : Glyph dynamique > Config SearchGlyph
        if (_config.UseDynamicGlyph && context.DynamicGlyph.HasValue)
        {
            options.RegionOfInterest = context.DynamicGlyph;
        }
        else if (_config.SearchGlyph.HasValue)
        {
            options.RegionOfInterest = _config.SearchGlyph;
        }
        
        return options;
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
