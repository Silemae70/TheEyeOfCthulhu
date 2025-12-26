namespace TheEyeOfCthulhu.Core.Processing;

/// <summary>
/// Résultat d'un processeur de frame.
/// Contient la frame traitée et des métadonnées optionnelles.
/// </summary>
public sealed class ProcessingResult
{
    /// <summary>
    /// Frame résultante (peut être la même que l'entrée ou une nouvelle).
    /// </summary>
    public Frame Frame { get; }

    /// <summary>
    /// Métadonnées extraites par le processeur (ex: positions détectées, scores, etc.)
    /// </summary>
    public Dictionary<string, object> Metadata { get; }

    /// <summary>
    /// Temps de traitement en millisecondes.
    /// </summary>
    public double ProcessingTimeMs { get; }

    /// <summary>
    /// Le traitement a-t-il réussi ?
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Message d'erreur si échec.
    /// </summary>
    public string? ErrorMessage { get; }

    private ProcessingResult(Frame frame, Dictionary<string, object>? metadata, double processingTimeMs, bool success, string? errorMessage)
    {
        Frame = frame;
        Metadata = metadata ?? new Dictionary<string, object>();
        ProcessingTimeMs = processingTimeMs;
        Success = success;
        ErrorMessage = errorMessage;
    }

    public static ProcessingResult Ok(Frame frame, Dictionary<string, object>? metadata = null, double processingTimeMs = 0)
    {
        return new ProcessingResult(frame, metadata, processingTimeMs, true, null);
    }

    public static ProcessingResult Fail(Frame frame, string errorMessage, double processingTimeMs = 0)
    {
        return new ProcessingResult(frame, null, processingTimeMs, false, errorMessage);
    }

    /// <summary>
    /// Récupère une métadonnée typée.
    /// </summary>
    public T? GetMetadata<T>(string key)
    {
        if (Metadata.TryGetValue(key, out var value) && value is T typed)
        {
            return typed;
        }
        return default;
    }
}
