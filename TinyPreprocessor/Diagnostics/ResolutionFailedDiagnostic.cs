using TinyPreprocessor.Core;

namespace TinyPreprocessor.Diagnostics;

/// <summary>
/// Diagnostic reported when a resource reference cannot be resolved.
/// </summary>
/// <param name="Reference">The unresolved reference string.</param>
/// <param name="Reason">Optional failure reason providing additional context.</param>
/// <param name="Resource">The resource containing the unresolved reference.</param>
/// <param name="Location">The location of the reference within the resource.</param>
public sealed record ResolutionFailedDiagnostic(
    string Reference,
    string? Reason = null,
    ResourceId? Resource = null,
    Range? Location = null) : IPreprocessorDiagnostic
{
    /// <inheritdoc />
    public DiagnosticSeverity Severity => DiagnosticSeverity.Error;

    /// <inheritdoc />
    public string Code => "TPP0100";

    /// <inheritdoc />
    public string Message => Reason is null
        ? $"Failed to resolve reference: '{Reference}'"
        : $"Failed to resolve reference: '{Reference}'. {Reason}";
}
