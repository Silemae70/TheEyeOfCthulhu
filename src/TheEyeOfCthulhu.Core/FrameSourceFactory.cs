namespace TheEyeOfCthulhu.Core;

/// <summary>
/// Interface pour un provider de source.
/// Permet d'enregistrer différents types de sources dans la factory.
/// </summary>
public interface IFrameSourceProvider
{
    /// <summary>
    /// Type de source géré par ce provider (ex: "DroidCam", "Basler").
    /// </summary>
    string SourceType { get; }

    /// <summary>
    /// Crée une instance de source à partir de la configuration.
    /// </summary>
    IFrameSource Create(SourceConfiguration configuration);

    /// <summary>
    /// Vérifie si ce provider peut gérer cette configuration.
    /// </summary>
    bool CanHandle(SourceConfiguration configuration);
}

/// <summary>
/// Factory pour créer des sources de frames.
/// Pattern Registry : les providers s'enregistrent et la factory dispatch.
/// </summary>
public sealed class FrameSourceFactory
{
    private readonly Dictionary<string, IFrameSourceProvider> _providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    /// <summary>
    /// Instance singleton (optionnel, on peut aussi instancier directement).
    /// </summary>
    public static FrameSourceFactory Default { get; } = new();

    /// <summary>
    /// Enregistre un provider pour un type de source.
    /// </summary>
    public void RegisterProvider(IFrameSourceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        lock (_lock)
        {
            if (_providers.ContainsKey(provider.SourceType))
            {
                throw new InvalidOperationException(
                    $"A provider for source type '{provider.SourceType}' is already registered.");
            }
            _providers[provider.SourceType] = provider;
        }
    }

    /// <summary>
    /// Désenregistre un provider.
    /// </summary>
    public bool UnregisterProvider(string sourceType)
    {
        lock (_lock)
        {
            return _providers.Remove(sourceType);
        }
    }

    /// <summary>
    /// Liste les types de sources disponibles.
    /// </summary>
    public IReadOnlyList<string> GetAvailableSourceTypes()
    {
        lock (_lock)
        {
            return _providers.Keys.ToList();
        }
    }

    /// <summary>
    /// Vérifie si un type de source est supporté.
    /// </summary>
    public bool IsSourceTypeSupported(string sourceType)
    {
        lock (_lock)
        {
            return _providers.ContainsKey(sourceType);
        }
    }

    /// <summary>
    /// Crée une source à partir d'une configuration.
    /// </summary>
    public IFrameSource Create(SourceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        IFrameSourceProvider? provider;
        lock (_lock)
        {
            if (!_providers.TryGetValue(configuration.SourceType, out provider))
            {
                throw new NotSupportedException(
                    $"No provider registered for source type '{configuration.SourceType}'. " +
                    $"Available types: {string.Join(", ", _providers.Keys)}");
            }
        }

        if (!provider.CanHandle(configuration))
        {
            throw new ArgumentException(
                $"Provider '{provider.SourceType}' cannot handle the provided configuration.",
                nameof(configuration));
        }

        return provider.Create(configuration);
    }

    /// <summary>
    /// Tente de créer une source, retourne null si impossible.
    /// </summary>
    public IFrameSource? TryCreate(SourceConfiguration configuration)
    {
        try
        {
            return Create(configuration);
        }
        catch
        {
            return null;
        }
    }
}
