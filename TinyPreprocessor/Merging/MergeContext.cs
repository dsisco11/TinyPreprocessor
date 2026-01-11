using TinyPreprocessor.Core;
using TinyPreprocessor.Diagnostics;
using TinyPreprocessor.SourceMaps;

namespace TinyPreprocessor.Merging;

/// <summary>
/// Shared context provided to merge strategies for source map building and diagnostics.
/// </summary>
public sealed class MergeContext<TContent, TDirective>
{
    /// <summary>
    /// A stable key that identifies a specific directive within a specific resource.
    /// </summary>
    /// <param name="RequestingResourceId">The id of the resource that contained the directive.</param>
    /// <param name="DirectiveIndex">The zero-based index of the directive in the parsed directive list.</param>
    public readonly record struct ResolvedReferenceKey(ResourceId RequestingResourceId, int DirectiveIndex);

    /// <summary>
    /// Initializes a new instance of <see cref="MergeContext{TContent, TDirective}"/>.
    /// </summary>
    /// <param name="sourceMapBuilder">The builder for recording source mappings.</param>
    /// <param name="diagnostics">The collection for reporting diagnostics.</param>
    /// <param name="resolvedCache">The cache of resolved resources.</param>
    /// <param name="resolvedReferences">
    /// A lookup from a directive occurrence (by <see cref="ResolvedReferenceKey"/>) to the authoritative
    /// resolved target <see cref="ResourceId"/> returned by the resource resolver.
    /// </param>
    /// <param name="directiveModel">The directive model for interpreting directive locations and references.</param>
    /// <param name="contentModel">The content model for interpreting offsets and slicing content.</param>
    /// <param name="contentBoundaryResolverProvider">The provider for resolving logical boundaries within content.</param>
    public MergeContext(
        SourceMapBuilder sourceMapBuilder,
        DiagnosticCollection diagnostics,
        IReadOnlyDictionary<ResourceId, IResource<TContent>> resolvedCache,
        IReadOnlyDictionary<ResolvedReferenceKey, ResourceId> resolvedReferences,
        IDirectiveModel<TDirective> directiveModel,
        IContentModel<TContent> contentModel,
        IContentBoundaryResolverProvider? contentBoundaryResolverProvider = null)
    {
        ArgumentNullException.ThrowIfNull(sourceMapBuilder);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(resolvedCache);
        ArgumentNullException.ThrowIfNull(resolvedReferences);
        ArgumentNullException.ThrowIfNull(directiveModel);
        ArgumentNullException.ThrowIfNull(contentModel);

        SourceMapBuilder = sourceMapBuilder;
        Diagnostics = diagnostics;
        ResolvedCache = resolvedCache;
        ResolvedReferences = resolvedReferences;
        DirectiveModel = directiveModel;
        ContentModel = contentModel;
        ContentBoundaryResolverProvider = contentBoundaryResolverProvider ?? Core.ContentBoundaryResolverProvider.Empty;
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
    public IReadOnlyDictionary<ResourceId, IResource<TContent>> ResolvedCache { get; }

    /// <summary>
    /// Gets a mapping from directive occurrences to their authoritative resolved target resource ids.
    /// </summary>
    /// <remarks>
    /// This is the source of truth for resolving directive references during merge. Merge must not
    /// re-derive resource ids from raw reference strings using path heuristics.
    /// </remarks>
    public IReadOnlyDictionary<ResolvedReferenceKey, ResourceId> ResolvedReferences { get; }

    /// <summary>
    /// Gets the directive model to interpret directive locations and references.
    /// </summary>
    public IDirectiveModel<TDirective> DirectiveModel { get; }

    /// <summary>
    /// Gets the content model used to interpret offsets and slice content.
    /// </summary>
    public IContentModel<TContent> ContentModel { get; }

    /// <summary>
    /// Gets the provider for resolving logical boundaries within content.
    /// </summary>
    public IContentBoundaryResolverProvider ContentBoundaryResolverProvider { get; }
}
