namespace TheEyeOfCthulhu.Core.Matching;

/// <summary>
/// Type de matcher à utiliser pour la détection d'ElderSign.
/// </summary>
public enum ElderSignMatcherType
{
    /// <summary>
    /// Template matching classique (rapide, sensible rotation/scale).
    /// </summary>
    Template,
    
    /// <summary>
    /// ORB feature matching (rotation/scale invariant, rapide).
    /// </summary>
    ORB,
    
    /// <summary>
    /// AKAZE feature matching (rotation/scale invariant, robuste).
    /// </summary>
    AKAZE,
    
    /// <summary>
    /// Shape matching via moments de Hu (formes simples).
    /// </summary>
    Shape
}

/// <summary>
/// Interface pour créer des matchers.
/// Permet de découpler Core des implémentations OpenCV.
/// </summary>
public interface IMatcherFactory
{
    /// <summary>
    /// Crée un matcher du type spécifié.
    /// </summary>
    /// <param name="type">Type de matcher.</param>
    /// <param name="options">Options du matcher (optionnel).</param>
    /// <returns>Instance du matcher.</returns>
    IElderSignMatcher Create(ElderSignMatcherType type, MatcherOptions? options = null);
}
