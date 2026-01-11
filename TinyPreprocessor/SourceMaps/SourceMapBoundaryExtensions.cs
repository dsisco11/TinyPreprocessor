using System;
using TinyPreprocessor.Core;

namespace TinyPreprocessor.SourceMaps;

public static class SourceMapBoundaryExtensions
{
    public sealed record SourceBoundaryLocation(ResourceId Resource, int OriginalOffset, int BoundaryIndex);

    /// <summary>
    /// Resolves the original location for a generated offset and computes the boundary index at that original offset.
    /// </summary>
    /// <remarks>
    /// This composes <see cref="SourceMap.Query(int)"/> with a caller-provided <paramref name="contentProvider"/>
    /// and an <see cref="IContentBoundaryResolver{TContent, TBoundary}"/>.
    /// </remarks>
    /// <returns>
    /// A <see cref="SourceBoundaryLocation"/> if the generated offset is mapped; otherwise <see langword="null"/>.
    /// </returns>
    public static SourceBoundaryLocation? ResolveOriginalBoundaryLocation<TContent, TBoundary>(
        this SourceMap sourceMap,
        int generatedOffset,
        Func<ResourceId, TContent> contentProvider,
        IContentBoundaryResolver<TContent, TBoundary> boundaryResolver)
    {
        ArgumentNullException.ThrowIfNull(sourceMap);
        ArgumentNullException.ThrowIfNull(contentProvider);
        ArgumentNullException.ThrowIfNull(boundaryResolver);

        var location = sourceMap.Query(generatedOffset);
        if (location is null)
        {
            return null;
        }

        var content = contentProvider(location.Resource);
        var boundaryIndex = boundaryResolver.ResolveBoundaryIndex(content, location.Resource, location.OriginalOffset);

        return new SourceBoundaryLocation(location.Resource, location.OriginalOffset, boundaryIndex);
    }
}
