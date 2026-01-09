using TinyPreprocessor.Core;

namespace TinyPreprocessor.SourceMaps;

/// <summary>
/// Represents a location in an original resource, returned as the result of a source map query.
/// </summary>
/// <param name="Resource">The identifier of the original resource.</param>
/// <param name="OriginalPosition">The position within the original resource.</param>
public sealed record SourceLocation(ResourceId Resource, SourcePosition OriginalPosition)
{
    /// <inheritdoc />
    public override string ToString()
    {
        var (line, column) = OriginalPosition.ToOneBased();
        return $"{Resource.Path}:{line}:{column}";
    }
}
