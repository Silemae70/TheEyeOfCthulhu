using TheEyeOfCthulhu.Core;

namespace TheEyeOfCthulhu.Sources.DroidCam;

/// <summary>
/// Provider pour créer des sources DroidCam via la factory.
/// </summary>
public sealed class DroidCamSourceProvider : IFrameSourceProvider
{
    public string SourceType => "DroidCam";

    public IFrameSource Create(SourceConfiguration configuration)
    {
        if (configuration is not DroidCamConfiguration droidCamConfig)
        {
            throw new ArgumentException(
                $"Expected {nameof(DroidCamConfiguration)}, got {configuration.GetType().Name}",
                nameof(configuration));
        }

        return new DroidCamSource(droidCamConfig);
    }

    public bool CanHandle(SourceConfiguration configuration)
    {
        return configuration is DroidCamConfiguration;
    }
}
