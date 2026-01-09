using TinyPreprocessor.Diagnostics;

namespace TinyPreprocessor.Core;

/// <summary>
/// Represents the result of a resource resolution operation.
/// </summary>
/// <param name="Resource">The resolved resource, or null if resolution failed.</param>
/// <param name="Error">The error diagnostic if resolution failed, or null on success.</param>
public sealed record ResourceResolutionResult(
    IResource? Resource,
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
    /// <returns>A successful <see cref="ResourceResolutionResult"/>.</returns>
    public static ResourceResolutionResult Success(IResource resource)
        => new(resource, null);

    /// <summary>
    /// Creates a failed resolution result.
    /// </summary>
    /// <param name="error">The error diagnostic describing the failure.</param>
    /// <returns>A failed <see cref="ResourceResolutionResult"/>.</returns>
    public static ResourceResolutionResult Failure(IPreprocessorDiagnostic error)
        => new(null, error);
}
