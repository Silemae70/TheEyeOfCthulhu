using TheEyeOfCthulhu.Core.Matching;

namespace TheEyeOfCthulhu.Sources.Matching;

/// <summary>
/// Factory pour créer des matchers OpenCV.
/// </summary>
public class MatcherFactory : IMatcherFactory
{
    public IElderSignMatcher Create(ElderSignMatcherType type, MatcherOptions? options = null)
    {
        return type switch
        {
            ElderSignMatcherType.Template => new TemplateSignMatcher(options),
            ElderSignMatcherType.ORB => new FeatureSignMatcher(FeatureDetectorType.ORB, options),
            ElderSignMatcherType.AKAZE => new FeatureSignMatcher(FeatureDetectorType.AKAZE, options),
            ElderSignMatcherType.Shape => new ShapeSignMatcher(options),
            _ => throw new ArgumentOutOfRangeException(nameof(type), $"Unknown matcher type: {type}")
        };
    }
}
