using TinyPreprocessor.Core;

namespace TinyPreprocessor.Diagnostics;

/// <summary>
/// Diagnostic reported when a cycle is detected in the resource dependency graph.
/// </summary>
/// <param name="Cycle">The resources forming the circular dependency.</param>
/// <param name="Resource">The resource where the cycle was detected, if applicable.</param>
/// <param name="Location">The location within the resource, if applicable.</param>
public sealed record CircularDependencyDiagnostic(
    IReadOnlyList<ResourceId> Cycle,
    ResourceId? Resource = null,
    Range? Location = null) : IPreprocessorDiagnostic
{
    /// <inheritdoc />
    public DiagnosticSeverity Severity => DiagnosticSeverity.Error;

    /// <inheritdoc />
    public string Code => "TPP0001";

    /// <inheritdoc />
    public string Message => $"Circular dependency detected: {string.Join(" → ", Cycle.Select(r => r.Path))}";
}
