using TinyPreprocessor.Diagnostics;

namespace TinyPreprocessor.Core;

/// <summary>
/// Represents the result of a resource resolution operation.
/// </summary>
/// <typeparam name="TSymbol">The symbol type of the resolved resource content.</typeparam>
/// <param name="Resource">The resolved resource, or null if resolution failed.</param>
/// <param name="Error">The error diagnostic if resolution failed, or null on success.</param>
public sealed record ResourceResolutionResult<TSymbol>(
    IResource<TSymbol>? Resource,
    IPreprocessorDiagnostic? Error)
{
    /// <summary>
    /// Gets a value indicating whether the resolution was successful.
    /// </summary>
    public bool IsSuccess => Resource is not null;

    /// <summary>
    /// Creates a successful resolution result.
    /// </summary>
    /// <param name="resource">The resolved resource.</param>
    /// <returns>A successful <see cref="ResourceResolutionResult{TSymbol}"/>.</returns>
    public static ResourceResolutionResult<TSymbol> Success(IResource<TSymbol> resource)
        => new(resource, null);

    /// <summary>
    /// Creates a failed resolution result.
    /// </summary>
    /// <param name="error">The error diagnostic describing the failure.</param>
    /// <returns>A failed <see cref="ResourceResolutionResult{TSymbol}"/>.</returns>
    public static ResourceResolutionResult<TSymbol> Failure(IPreprocessorDiagnostic error)
        => new(null, error);
}
