using TheEyeOfCthulhu.Core.Matching;

namespace TheEyeOfCthulhu.Core.Runes.Implementations;

/// <summary>
/// Configuration pour une SummonRune.
/// </summary>
public class SummonRuneConfig
{
    /// <summary>
    /// ElderSign servant de référence pour localiser la pièce.
    /// </summary>
    public ElderSign? ReferenceSign { get; set; }
    
    /// <summary>
    /// Type de matcher à utiliser.
    /// </summary>
    public ElderSignMatcherType MatcherType { get; set; } = ElderSignMatcherType.AKAZE;
    
    /// <summary>
    /// Zone de recherche initiale (null = toute l'image).
    /// </summary>
    public Rectangle? SearchGlyph { get; set; }
    
    /// <summary>
    /// Définir un Glyph dynamique autour de la position trouvée.
    /// Les Runes suivantes pourront l'utiliser.
    /// </summary>
    public bool CreateDynamicGlyph { get; set; } = true;
    
    /// <summary>
    /// Marge autour de la position trouvée pour le Glyph dynamique (en pixels).
    /// </summary>
    public int DynamicGlyphMargin { get; set; } = 50;
    
    /// <summary>
    /// Taille fixe du Glyph dynamique (si null, basé sur la taille du match + marge).
    /// </summary>
    public Size? DynamicGlyphSize { get; set; }
}

/// <summary>
/// Rune de localisation (Summon).
/// Trouve une pièce/référence et établit le contexte pour les Runes suivantes.
/// C'est typiquement la première Rune d'un Ritual.
/// </summary>
public class SummonRune : RuneBase
{
    private readonly string _name;
    private readonly SummonRuneConfig _config;
    private IElderSignMatcher? _matcher;
    
    public override string Name => _name;
    public override string RuneType => "Summon";
    public override string Description => $"Locate reference '{_config.ReferenceSign?.Name ?? "undefined"}' and establish context";
    
    /// <summary>
    /// Configuration de la Rune.
    /// </summary>
    public SummonRuneConfig Config => _config;
    
    public SummonRune(string name, SummonRuneConfig? config = null)
    {
        _name = name;
        _config = config ?? new SummonRuneConfig();
    }
    
    public SummonRune(string name, ElderSign referenceSign, ElderSignMatcherType matcherType = ElderSignMatcherType.AKAZE)
        : this(name, new SummonRuneConfig { ReferenceSign = referenceSign, MatcherType = matcherType })
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
        if (_config.ReferenceSign == null)
        {
            return Vision.Void(Name, "No reference ElderSign configured");
        }
        
        if (_matcher == null)
        {
            return Vision.Void(Name, "No matcher configured");
        }
        
        // Rechercher la référence
        var result = _matcher.Search(context.CurrentFrame!, _config.ReferenceSign);
        
        if (result.Matches.Count == 0)
        {
            return Vision.Dormant(Name, "Reference not found - piece may be absent");
        }
        
        var match = result.Matches[0];
        
        if (match.Score < ResonanceThreshold)
        {
            return new Vision
            {
                RuneName = Name,
                State = VisionState.Dormant,
                Resonance = match.Score,
                Message = $"Reference found but score {match.Score:P0} below threshold",
                Position = match.Position
            };
        }
        
        // Mettre à jour le contexte avec la position/angle/scale
        context.ReferencePosition = match.Position;
        context.ReferenceAngle = match.Angle;
        context.ReferenceScale = match.Scale;
        
        // Créer le Glyph dynamique si demandé
        if (_config.CreateDynamicGlyph)
        {
            context.DynamicGlyph = CalculateDynamicGlyph(match, context.CurrentFrame!);
        }
        
        return new Vision
        {
            RuneName = Name,
            State = VisionState.Awakened,
            Resonance = match.Score,
            Message = $"Reference located at ({match.Position.X:F0}, {match.Position.Y:F0})",
            Position = match.Position,
            Angle = match.Angle,
            Scale = match.Scale,
            Metadata = new Dictionary<string, object>
            {
                ["SearchTimeMs"] = result.SearchTimeMs,
                ["DynamicGlyphCreated"] = _config.CreateDynamicGlyph,
                ["DynamicGlyph"] = context.DynamicGlyph?.ToString() ?? "none"
            }
        };
    }
    
    private Rectangle CalculateDynamicGlyph(ElderSignMatch match, Frame frame)
    {
        int x, y, width, height;
        
        if (_config.DynamicGlyphSize.HasValue)
        {
            // Taille fixe centrée sur la position
            width = _config.DynamicGlyphSize.Value.Width;
            height = _config.DynamicGlyphSize.Value.Height;
            x = (int)(match.Position.X - width / 2);
            y = (int)(match.Position.Y - height / 2);
        }
        else if (match.TransformedCorners != null && match.TransformedCorners.Length == 4)
        {
            // Basé sur les coins transformés + marge
            var minX = match.TransformedCorners.Min(c => c.X);
            var maxX = match.TransformedCorners.Max(c => c.X);
            var minY = match.TransformedCorners.Min(c => c.Y);
            var maxY = match.TransformedCorners.Max(c => c.Y);
            
            x = (int)minX - _config.DynamicGlyphMargin;
            y = (int)minY - _config.DynamicGlyphMargin;
            width = (int)(maxX - minX) + _config.DynamicGlyphMargin * 2;
            height = (int)(maxY - minY) + _config.DynamicGlyphMargin * 2;
        }
        else
        {
            // Fallback : zone carrée autour de la position
            var size = 200 + _config.DynamicGlyphMargin * 2;
            x = (int)(match.Position.X - size / 2);
            y = (int)(match.Position.Y - size / 2);
            width = size;
            height = size;
        }
        
        // Clamp aux limites de l'image
        x = Math.Max(0, x);
        y = Math.Max(0, y);
        width = Math.Min(width, frame.Width - x);
        height = Math.Min(height, frame.Height - y);
        
        return new Rectangle(x, y, width, height);
    }
    
    protected override bool ValidateCore(out string? errorMessage)
    {
        if (_config.ReferenceSign == null)
        {
            errorMessage = "Reference ElderSign is not configured";
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
