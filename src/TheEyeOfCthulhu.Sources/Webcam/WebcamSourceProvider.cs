using TheEyeOfCthulhu.Core;

namespace TheEyeOfCthulhu.Sources.Webcam;

/// <summary>
/// Provider pour créer des sources Webcam via la factory.
/// </summary>
public sealed class WebcamSourceProvider : IFrameSourceProvider
{
    public string SourceType => "Webcam";

    public IFrameSource Create(SourceConfiguration configuration)
    {
        if (configuration is not WebcamConfiguration webcamConfig)
        {
            throw new ArgumentException(
                $"Expected {nameof(WebcamConfiguration)}, got {configuration.GetType().Name}",
                nameof(configuration));
        }

        return new WebcamSource(webcamConfig);
    }

    public bool CanHandle(SourceConfiguration configuration)
    {
        return configuration is WebcamConfiguration;
    }
}
