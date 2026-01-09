using TinyPreprocessor.Core;
using TinyPreprocessor.Diagnostics;

namespace TinyPreprocessor;

/// <summary>
/// Diagnostic reported when the maximum include depth is exceeded.
/// </summary>
/// <param name="Reference">The reference that exceeded the depth limit.</param>
/// <param name="Depth">The current depth when the limit was exceeded.</param>
/// <param name="MaxDepth">The configured maximum depth.</param>
/// <param name="Resource">The resource containing the directive that exceeded the limit.</param>
/// <param name="Location">The location of the directive.</param>
public sealed record MaxDepthExceededDiagnostic(
    string Reference,
    int Depth,
    int MaxDepth,
    ResourceId? Resource = null,
    Range? Location = null) : IPreprocessorDiagnostic
{
    /// <inheritdoc />
    public DiagnosticSeverity Severity => DiagnosticSeverity.Error;

    /// <inheritdoc />
    public string Code => "TPP0004";

    /// <inheritdoc />
    public string Message => $"Maximum include depth ({MaxDepth}) exceeded while resolving '{Reference}' at depth {Depth}";
}
