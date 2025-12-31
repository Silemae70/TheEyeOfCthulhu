using TheEyeOfCthulhu.Core.Matching;

namespace TheEyeOfCthulhu.Core.Runes.Implementations;

/// <summary>
/// Factory pour créer des Runes avec leurs dépendances.
/// Nécessite une implémentation de IMatcherFactory pour créer les matchers.
/// </summary>
public class RuneFactory
{
    private readonly IMatcherFactory _matcherFactory;
    
    public RuneFactory(IMatcherFactory matcherFactory)
    {
        _matcherFactory = matcherFactory;
    }
    
    /// <summary>
    /// Crée une ElderSignRune configurée et prête à l'emploi.
    /// </summary>
    public ElderSignRune CreateElderSignRune(
        string name, 
        ElderSign elderSign, 
        ElderSignMatcherType matcherType = ElderSignMatcherType.AKAZE,
        double resonanceThreshold = 0.7)
    {
        var rune = new ElderSignRune(name, elderSign, matcherType)
        {
            ResonanceThreshold = resonanceThreshold
        };
        
        var matcher = _matcherFactory.Create(matcherType);
        rune.SetMatcher(matcher);
        
        return rune;
    }
    
    /// <summary>
    /// Crée une SummonRune configurée et prête à l'emploi.
    /// </summary>
    public SummonRune CreateSummonRune(
        string name,
        ElderSign referenceSign,
        ElderSignMatcherType matcherType = ElderSignMatcherType.AKAZE,
        double resonanceThreshold = 0.7,
        bool createDynamicGlyph = true,
        int glyphMargin = 50)
    {
        var config = new SummonRuneConfig
        {
            ReferenceSign = referenceSign,
            MatcherType = matcherType,
            CreateDynamicGlyph = createDynamicGlyph,
            DynamicGlyphMargin = glyphMargin
        };
        
        var rune = new SummonRune(name, config)
        {
            ResonanceThreshold = resonanceThreshold
        };
        
        var matcher = _matcherFactory.Create(matcherType);
        rune.SetMatcher(matcher);
        
        return rune;
    }
    
    /// <summary>
    /// Crée une PresenceRune configurée et prête à l'emploi.
    /// </summary>
    public PresenceRune CreatePresenceRune(
        string name,
        ElderSign elderSign,
        PresenceMode mode = PresenceMode.MustBePresent,
        ElderSignMatcherType matcherType = ElderSignMatcherType.AKAZE,
        double resonanceThreshold = 0.7)
    {
        var config = new PresenceRuneConfig
        {
            ElderSign = elderSign,
            Mode = mode,
            MatcherType = matcherType
        };
        
        var rune = new PresenceRune(name, config)
        {
            ResonanceThreshold = resonanceThreshold
        };
        
        var matcher = _matcherFactory.Create(matcherType);
        rune.SetMatcher(matcher);
        
        return rune;
    }
}
