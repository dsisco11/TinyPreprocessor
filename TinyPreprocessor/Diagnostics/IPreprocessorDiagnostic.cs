using TinyPreprocessor.Core;

namespace TinyPreprocessor.Diagnostics;

/// <summary>
/// Interface for all diagnostic messages in the preprocessing system.
/// </summary>
/// <remarks>
/// <para>
/// Code Convention: TPP#### (TinyPreProcessor + 4-digit number)
/// </para>
/// <list type="bullet">
///   <item><description>TPP0001-TPP0099: Dependency/graph errors</description></item>
///   <item><description>TPP0100-TPP0199: Resolution errors</description></item>
///   <item><description>TPP0200-TPP0299: Parse errors</description></item>
///   <item><description>TPP0300-TPP0399: Merge errors</description></item>
///   <item><description>TPP0400-TPP0499: Configuration/options errors</description></item>
/// </list>
/// </remarks>
public interface IPreprocessorDiagnostic
{
    /// <summary>
    /// Gets the severity level of this diagnostic.
    /// </summary>
    DiagnosticSeverity Severity { get; }

    /// <summary>
    /// Gets the machine-readable diagnostic code (e.g., "TPP0001").
    /// </summary>
    string Code { get; }

    /// <summary>
    /// Gets the human-readable diagnostic message.
    /// </summary>
    string Message { get; }

    /// <summary>
    /// Gets the resource where this diagnostic occurred, if applicable.
    /// </summary>
    ResourceId? Resource { get; }

    /// <summary>
    /// Gets the location within the resource where this diagnostic occurred, if applicable.
    /// </summary>
    Range? Location { get; }
}
