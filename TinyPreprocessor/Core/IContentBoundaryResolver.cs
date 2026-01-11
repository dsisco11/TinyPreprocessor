namespace TinyPreprocessor.Core;

/// <summary>
/// Resolves boundary offsets within a range of offsets in a specific content instance.
/// </summary>
/// <typeparam name="TContent">The content representation type.</typeparam>
/// <typeparam name="TBoundary">
/// A marker type identifying a particular boundary kind (e.g., line boundaries, record boundaries).
/// Downstream users are expected to define their own marker types.
/// </typeparam>
public interface IContentBoundaryResolver<in TContent, TBoundary>
{
    /// <summary>
    /// Returns boundary offsets within the half-open range <c>[startOffset, endOffset)</c>.
    /// </summary>
    /// <param name="content">The content instance to query.</param>
    /// <param name="resourceId">The identifier of the resource the content belongs to.</param>
    /// <param name="startOffset">The start offset (inclusive).</param>
    /// <param name="endOffset">The end offset (exclusive).</param>
    /// <returns>
    /// An ordered sequence of offsets in ascending order.
    /// Each returned offset MUST be within <c>[startOffset, endOffset)</c>.
    /// </returns>
    IEnumerable<int> ResolveOffsets(TContent content, ResourceId resourceId, int startOffset, int endOffset);
}
