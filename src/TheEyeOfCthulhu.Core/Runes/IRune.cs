namespace TheEyeOfCthulhu.Core.Runes;

/// <summary>
/// Interface de base pour toutes les Runes (opérations de vision).
/// Une Rune est une opération élémentaire qui analyse une image et produit une Vision.
/// </summary>
public interface IRune : IDisposable
{
    /// <summary>
    /// Nom unique de la Rune dans le Ritual.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Type de Rune (ex: "ElderSign", "Whisper", "Measure").
    /// </summary>
    string RuneType { get; }
    
    /// <summary>
    /// Description de ce que fait cette Rune.
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Indique si cette Rune est activée.
    /// </summary>
    bool IsEnabled { get; set; }
    
    /// <summary>
    /// Seuil minimum de Resonance pour considérer la Vision comme Awakened.
    /// </summary>
    double ResonanceThreshold { get; set; }
    
    /// <summary>
    /// Exécute la Rune et retourne une Vision.
    /// </summary>
    /// <param name="context">Contexte partagé contenant la frame et les résultats précédents.</param>
    /// <returns>Vision résultante de l'exécution.</returns>
    Vision Execute(RuneContext context);
    
    /// <summary>
    /// Valide la configuration de la Rune.
    /// </summary>
    /// <returns>True si la configuration est valide.</returns>
    bool Validate(out string? errorMessage);
}

/// <summary>
/// Classe de base abstraite pour les Runes.
/// Fournit l'implémentation commune.
/// </summary>
public abstract class RuneBase : IRune
{
    public abstract string Name { get; }
    public abstract string RuneType { get; }
    public virtual string Description => $"{RuneType} Rune";
    
    public bool IsEnabled { get; set; } = true;
    public double ResonanceThreshold { get; set; } = 0.7;
    
    public Vision Execute(RuneContext context)
    {
        if (!IsEnabled)
        {
            return Vision.Dormant(Name, "Rune is disabled");
        }
        
        if (context.CurrentFrame == null)
        {
            return Vision.Void(Name, "No frame in context");
        }
        
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var vision = ExecuteCore(context);
            sw.Stop();
            
            // Enrichir la Vision avec le temps d'exécution
            return vision with { ExecutionTimeMs = sw.Elapsed.TotalMilliseconds };
        }
        catch (Exception ex)
        {
            return Vision.Void(Name, $"Error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Implémentation spécifique de la Rune.
    /// </summary>
    protected abstract Vision ExecuteCore(RuneContext context);
    
    public virtual bool Validate(out string? errorMessage)
    {
        errorMessage = null;
        
        if (string.IsNullOrWhiteSpace(Name))
        {
            errorMessage = "Rune name cannot be empty";
            return false;
        }
        
        if (ResonanceThreshold < 0 || ResonanceThreshold > 1)
        {
            errorMessage = "ResonanceThreshold must be between 0 and 1";
            return false;
        }
        
        return ValidateCore(out errorMessage);
    }
    
    /// <summary>
    /// Validation spécifique à la Rune.
    /// </summary>
    protected virtual bool ValidateCore(out string? errorMessage)
    {
        errorMessage = null;
        return true;
    }
    
    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
