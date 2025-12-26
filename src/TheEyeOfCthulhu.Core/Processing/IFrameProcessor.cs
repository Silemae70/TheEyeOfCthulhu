namespace TheEyeOfCthulhu.Core.Processing;

/// <summary>
/// Interface pour un processeur de frame.
/// Chaque processeur effectue une opération sur la frame (filtre, détection, etc.)
/// </summary>
public interface IFrameProcessor : IDisposable
{
    /// <summary>
    /// Nom du processeur.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Le processeur est-il activé ?
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Traite une frame et retourne le résultat.
    /// </summary>
    ProcessingResult Process(Frame input);
}

/// <summary>
/// Classe de base abstraite pour les processeurs.
/// Fournit des fonctionnalités communes.
/// </summary>
public abstract class FrameProcessorBase : IFrameProcessor
{
    public abstract string Name { get; }
    public bool IsEnabled { get; set; } = true;

    public ProcessingResult Process(Frame input)
    {
        if (!IsEnabled)
        {
            return ProcessingResult.Ok(input);
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = ProcessCore(input);
            sw.Stop();
            return ProcessingResult.Ok(result.Frame, result.Metadata, sw.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ProcessingResult.Fail(input, ex.Message, sw.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Implémentation du traitement à fournir par les classes dérivées.
    /// </summary>
    protected abstract (Frame Frame, Dictionary<string, object>? Metadata) ProcessCore(Frame input);

    public virtual void Dispose()
    {
    }
}
