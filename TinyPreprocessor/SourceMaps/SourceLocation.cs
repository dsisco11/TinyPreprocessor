using TinyPreprocessor.Core;

namespace TinyPreprocessor.SourceMaps;

/// <summary>
/// Represents a location in an original resource, returned as the result of a source map query.
/// </summary>
/// <param name="Resource">The identifier of the original resource.</param>
/// <param name="OriginalOffset">The 0-based offset within the original resource.</param>
public sealed record SourceLocation(ResourceId Resource, int OriginalOffset)
{
    /// <inheritdoc />
    public override string ToString() => $"{Resource.Path}@{OriginalOffset}";
}
