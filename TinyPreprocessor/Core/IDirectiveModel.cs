namespace TinyPreprocessor.Core;

/// <summary>
/// Provides directive semantics to the preprocessing pipeline without requiring directives
/// to implement any specific interface.
/// </summary>
/// <typeparam name="TDirective">The directive type.</typeparam>
public interface IDirectiveModel<in TDirective>
{
    /// <summary>
    /// Gets the location of the directive within its source content.
    /// </summary>
    /// <param name="directive">The directive instance.</param>
    /// <returns>The directive location.</returns>
    Range GetLocation(TDirective directive);

    /// <summary>
    /// Attempts to extract a dependency reference from the directive.
    /// </summary>
    /// <param name="directive">The directive instance.</param>
    /// <param name="reference">When true is returned, receives the reference to resolve.</param>
    /// <returns>
    /// <see langword="true"/> if this directive represents a dependency reference to resolve;
    /// otherwise <see langword="false"/>.
    /// </returns>
    bool TryGetReference(TDirective directive, out string reference);
}
