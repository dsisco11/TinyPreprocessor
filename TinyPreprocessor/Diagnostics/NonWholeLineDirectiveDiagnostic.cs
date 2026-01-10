using TinyPreprocessor.Core;
using TinyPreprocessor.Diagnostics;

namespace TinyPreprocessor;

/// <summary>
/// Diagnostic reported when a directive does not occupy an entire line.
/// </summary>
/// <param name="Resource">The resource containing the directive.</param>
/// <param name="Location">The location of the directive.</param>
public sealed record NonWholeLineDirectiveDiagnostic(
    ResourceId? Resource = null,
    Range? Location = null) : IPreprocessorDiagnostic
{
    /// <inheritdoc />
    public DiagnosticSeverity Severity => DiagnosticSeverity.Error;

    /// <inheritdoc />
    public string Code => "TPP0300";

    /// <inheritdoc />
    public string Message => "Directive must occupy an entire line (only whitespace allowed before/after).";
}
