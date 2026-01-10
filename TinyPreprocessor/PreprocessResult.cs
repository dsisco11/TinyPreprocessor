using TinyPreprocessor.Core;
using TinyPreprocessor.Diagnostics;
using TinyPreprocessor.Graph;
using TinyPreprocessor.SourceMaps;

namespace TinyPreprocessor;

/// <summary>
/// The result of preprocessing, containing merged output and metadata.
/// </summary>
/// <param name="Content">The merged output content.</param>
/// <param name="SourceMap">The source map for position mapping from generated to original.</param>
/// <param name="Diagnostics">All collected diagnostics during processing.</param>
/// <param name="ProcessedResources">The resources in topological order (dependencies first).</param>
/// <param name="DependencyGraph">The dependency graph for downstream analysis.</param>
public sealed record PreprocessResult<TSymbol>(
    ReadOnlyMemory<TSymbol> Content,
    SourceMap SourceMap,
    DiagnosticCollection Diagnostics,
    IReadOnlyList<ResourceId> ProcessedResources,
    ResourceDependencyGraph DependencyGraph)
{
    /// <summary>
    /// Gets a value indicating whether preprocessing completed without errors.
    /// </summary>
    public bool Success => !Diagnostics.HasErrors;
}
