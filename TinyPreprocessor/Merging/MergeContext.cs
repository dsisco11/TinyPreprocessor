using TinyPreprocessor.Core;
using TinyPreprocessor.Diagnostics;
using TinyPreprocessor.SourceMaps;

namespace TinyPreprocessor.Merging;

/// <summary>
/// Shared context provided to merge strategies for source map building and diagnostics.
/// </summary>
public sealed class MergeContext
{
    /// <summary>
    /// Initializes a new instance of <see cref="MergeContext"/>.
    /// </summary>
    /// <param name="sourceMapBuilder">The builder for recording source mappings.</param>
    /// <param name="diagnostics">The collection for reporting diagnostics.</param>
    /// <param name="resolvedCache">The cache of resolved resources.</param>
    public MergeContext(
        SourceMapBuilder sourceMapBuilder,
        DiagnosticCollection diagnostics,
        IReadOnlyDictionary<ResourceId, IResource> resolvedCache)
    {
        ArgumentNullException.ThrowIfNull(sourceMapBuilder);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(resolvedCache);

        SourceMapBuilder = sourceMapBuilder;
        Diagnostics = diagnostics;
        ResolvedCache = resolvedCache;
    }

    /// <summary>
    /// Gets the source map builder for recording mappings.
    /// </summary>
    public SourceMapBuilder SourceMapBuilder { get; }

    /// <summary>
    /// Gets the diagnostic collection for reporting issues.
    /// </summary>
    public DiagnosticCollection Diagnostics { get; }

    /// <summary>
    /// Gets the cache of resolved resources for cross-referencing.
    /// </summary>
    public IReadOnlyDictionary<ResourceId, IResource> ResolvedCache { get; }
}
