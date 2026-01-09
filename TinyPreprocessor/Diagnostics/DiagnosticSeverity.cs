using TinyPreprocessor.Core;

namespace TinyPreprocessor.Diagnostics;

/// <summary>
/// Categorizes the importance of diagnostics.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>
    /// Informational messages (e.g., "Resource loaded from cache").
    /// </summary>
    Info = 0,

    /// <summary>
    /// Non-fatal issues that may indicate problems (e.g., "Deprecated directive syntax").
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Fatal issues that prevent correct output (e.g., "Circular dependency detected").
    /// </summary>
    Error = 2
}
