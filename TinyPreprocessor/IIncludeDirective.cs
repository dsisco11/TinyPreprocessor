using TinyPreprocessor.Core;

namespace TinyPreprocessor;

/// <summary>
/// Marker interface for directives that represent resource includes.
/// </summary>
/// <remarks>
/// Implement this interface on directive types that should trigger recursive resolution.
/// The <see cref="Reference"/> property provides the string used for resolution.
/// </remarks>
public interface IIncludeDirective : IDirective
{
    /// <summary>
    /// Gets the reference string to resolve (e.g., file path, module name).
    /// </summary>
    string Reference { get; }
}
