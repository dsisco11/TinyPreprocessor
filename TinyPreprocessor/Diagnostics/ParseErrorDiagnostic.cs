using TinyPreprocessor.Core;

namespace TinyPreprocessor.Diagnostics;

/// <summary>
/// Diagnostic reported when directive parsing fails.
/// </summary>
/// <param name="ErrorMessage">The parse error description.</param>
/// <param name="Resource">The resource where the parse error occurred.</param>
/// <param name="Location">The location of the parse error within the resource.</param>
/// <param name="Severity">The severity level (defaults to Error).</param>
public sealed record ParseErrorDiagnostic(
    string ErrorMessage,
    ResourceId? Resource = null,
    Range? Location = null,
    DiagnosticSeverity Severity = DiagnosticSeverity.Error) : IPreprocessorDiagnostic
{
    /// <inheritdoc />
    public string Code => "TPP0200";

    /// <inheritdoc />
    public string Message => ErrorMessage;
}
