using TheEyeOfCthulhu.Core;

namespace TheEyeOfCthulhu.Sources.File;

/// <summary>
/// Provider pour créer des sources File via la factory.
/// </summary>
public sealed class FileSourceProvider : IFrameSourceProvider
{
    public string SourceType => "File";

    public IFrameSource Create(SourceConfiguration configuration)
    {
        if (configuration is not FileSourceConfiguration fileConfig)
        {
            throw new ArgumentException(
                $"Expected {nameof(FileSourceConfiguration)}, got {configuration.GetType().Name}",
                nameof(configuration));
        }

        return new FileSource(fileConfig);
    }

    public bool CanHandle(SourceConfiguration configuration)
    {
        return configuration is FileSourceConfiguration;
    }
}
