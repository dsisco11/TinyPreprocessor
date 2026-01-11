using System;

namespace TinyPreprocessor.Core;

public static class ContentBoundaryResolverExtensions
{
    /// <summary>
    /// Computes a 0-based boundary index for <paramref name="offset"/>.
    /// </summary>
    /// <remarks>
    /// This method assumes the boundary offsets returned by <see cref="IContentBoundaryResolver{TContent, TBoundary}.ResolveOffsets"/>
    /// represent the start offsets of boundary regions (excluding the implicit first region starting at 0).
    /// Under that convention, the boundary index is the number of boundary offsets strictly less than <paramref name="offset"/>.
    /// </remarks>
    public static int ResolveBoundaryIndex<TContent, TBoundary>(
        this IContentBoundaryResolver<TContent, TBoundary> boundaryResolver,
        TContent content,
        ResourceId resourceId,
        int offset)
    {
        ArgumentNullException.ThrowIfNull(boundaryResolver);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        var count = 0;
        foreach (var _ in boundaryResolver.ResolveOffsets(content, resourceId, startOffset: 0, endOffset: offset))
        {
            count++;
        }
        return count;
    }

    /// <summary>
    /// Counts boundary offsets within the half-open range <c>[startOffset, endOffset)</c>.
    /// </summary>
    public static int CountBoundaries<TContent, TBoundary>(
        this IContentBoundaryResolver<TContent, TBoundary> boundaryResolver,
        TContent content,
        ResourceId resourceId,
        int startOffset,
        int endOffset)
    {
        ArgumentNullException.ThrowIfNull(boundaryResolver);
        ArgumentOutOfRangeException.ThrowIfNegative(startOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(endOffset);

        if (endOffset < startOffset)
        {
            throw new ArgumentException("End offset must be greater than or equal to start offset.", nameof(endOffset));
        }

        var count = 0;
        foreach (var _ in boundaryResolver.ResolveOffsets(content, resourceId, startOffset, endOffset))
        {
            count++;
        }
        return count;
    }
}
