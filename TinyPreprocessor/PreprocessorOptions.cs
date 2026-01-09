namespace TinyPreprocessor;

/// <summary>
/// Configuration options for preprocessing behavior.
/// </summary>
/// <param name="DeduplicateIncludes">
/// When <see langword="true"/>, each resource is included only once in the output (like #pragma once).
/// Defaults to <see langword="true"/>.
/// </param>
/// <param name="MaxIncludeDepth">
/// Safety limit against infinite recursion. Defaults to 100.
/// </param>
/// <param name="ContinueOnError">
/// When <see langword="true"/>, processing continues after errors, collecting all diagnostics.
/// Defaults to <see langword="true"/>.
/// </param>
public sealed record PreprocessorOptions(
    bool DeduplicateIncludes = true,
    int MaxIncludeDepth = 100,
    bool ContinueOnError = true)
{
    /// <summary>
    /// Gets the default preprocessor options instance.
    /// </summary>
    public static PreprocessorOptions Default { get; } = new();
}
