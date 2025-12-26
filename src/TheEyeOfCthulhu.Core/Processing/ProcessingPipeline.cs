namespace TheEyeOfCthulhu.Core.Processing;

/// <summary>
/// Résultat complet d'un pipeline.
/// </summary>
public sealed class PipelineResult
{
    /// <summary>
    /// Frame finale après tous les traitements.
    /// </summary>
    public Frame FinalFrame { get; }

    /// <summary>
    /// Résultats individuels de chaque processeur.
    /// </summary>
    public IReadOnlyList<(string ProcessorName, ProcessingResult Result)> StepResults { get; }

    /// <summary>
    /// Temps total de traitement (ms).
    /// </summary>
    public double TotalProcessingTimeMs { get; }

    /// <summary>
    /// Le pipeline a-t-il réussi entièrement ?
    /// </summary>
    public bool Success => StepResults.All(r => r.Result.Success);

    /// <summary>
    /// Toutes les métadonnées agrégées (clé = ProcessorName.MetadataKey).
    /// </summary>
    public Dictionary<string, object> AllMetadata { get; }

    public PipelineResult(Frame finalFrame, List<(string, ProcessingResult)> stepResults)
    {
        FinalFrame = finalFrame;
        StepResults = stepResults;
        TotalProcessingTimeMs = stepResults.Sum(r => r.Item2.ProcessingTimeMs);

        AllMetadata = new Dictionary<string, object>();
        foreach (var (name, result) in stepResults)
        {
            foreach (var (key, value) in result.Metadata)
            {
                AllMetadata[$"{name}.{key}"] = value;
            }
        }
    }

    /// <summary>
    /// Récupère une métadonnée d'un processeur spécifique.
    /// </summary>
    public T? GetMetadata<T>(string processorName, string key)
    {
        var fullKey = $"{processorName}.{key}";
        if (AllMetadata.TryGetValue(fullKey, out var value) && value is T typed)
        {
            return typed;
        }
        return default;
    }
}

/// <summary>
/// Pipeline de traitement d'images.
/// Chaîne plusieurs processeurs en séquence.
/// </summary>
public sealed class ProcessingPipeline : IDisposable
{
    private readonly List<IFrameProcessor> _processors = new();
    private readonly object _lock = new();

    /// <summary>
    /// Nom du pipeline.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Processeurs dans le pipeline.
    /// </summary>
    public IReadOnlyList<IFrameProcessor> Processors => _processors;

    public ProcessingPipeline(string name = "Default Pipeline")
    {
        Name = name;
    }

    /// <summary>
    /// Ajoute un processeur à la fin du pipeline.
    /// </summary>
    public ProcessingPipeline Add(IFrameProcessor processor)
    {
        lock (_lock)
        {
            _processors.Add(processor);
        }
        return this; // Fluent API
    }

    /// <summary>
    /// Insère un processeur à une position spécifique.
    /// </summary>
    public ProcessingPipeline Insert(int index, IFrameProcessor processor)
    {
        lock (_lock)
        {
            _processors.Insert(index, processor);
        }
        return this;
    }

    /// <summary>
    /// Retire un processeur.
    /// </summary>
    public bool Remove(IFrameProcessor processor)
    {
        lock (_lock)
        {
            return _processors.Remove(processor);
        }
    }

    /// <summary>
    /// Retire un processeur par nom.
    /// </summary>
    public bool Remove(string processorName)
    {
        lock (_lock)
        {
            var processor = _processors.FirstOrDefault(p => p.Name == processorName);
            if (processor != null)
            {
                return _processors.Remove(processor);
            }
            return false;
        }
    }

    /// <summary>
    /// Vide le pipeline.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            foreach (var processor in _processors)
            {
                processor.Dispose();
            }
            _processors.Clear();
        }
    }

    /// <summary>
    /// Exécute le pipeline sur une frame.
    /// </summary>
    public PipelineResult Process(Frame input)
    {
        var results = new List<(string, ProcessingResult)>();
        var currentFrame = input;

        List<IFrameProcessor> processorsCopy;
        lock (_lock)
        {
            processorsCopy = _processors.ToList();
        }

        foreach (var processor in processorsCopy)
        {
            var result = processor.Process(currentFrame);
            results.Add((processor.Name, result));

            if (!result.Success)
            {
                // On continue quand même avec la frame d'origine si erreur
                continue;
            }

            // La frame résultante devient l'entrée du prochain processeur
            if (result.Frame != currentFrame && currentFrame != input)
            {
                // Dispose l'ancienne frame intermédiaire
                currentFrame.Dispose();
            }
            currentFrame = result.Frame;
        }

        return new PipelineResult(currentFrame, results);
    }

    /// <summary>
    /// Récupère un processeur par son nom.
    /// </summary>
    public IFrameProcessor? GetProcessor(string name)
    {
        lock (_lock)
        {
            return _processors.FirstOrDefault(p => p.Name == name);
        }
    }

    /// <summary>
    /// Récupère un processeur typé par son nom.
    /// </summary>
    public T? GetProcessor<T>(string name) where T : class, IFrameProcessor
    {
        return GetProcessor(name) as T;
    }

    public void Dispose()
    {
        Clear();
    }
}
