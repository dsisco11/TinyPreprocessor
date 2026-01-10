using TinyPreprocessor.Core;

namespace TinyPreprocessor.SourceMaps;

/// <summary>
/// Represents an exact mapping between a generated range and an original range.
/// </summary>
/// <param name="Resource">The identifier of the original resource.</param>
/// <param name="OriginalStartOffset">Inclusive start offset in the original resource.</param>
/// <param name="OriginalEndOffset">Exclusive end offset in the original resource.</param>
/// <param name="GeneratedStartOffset">Inclusive start offset in the generated output.</param>
/// <param name="GeneratedEndOffset">Exclusive end offset in the generated output.</param>
public sealed record SourceRangeLocation(
    ResourceId Resource,
    int OriginalStartOffset,
    int OriginalEndOffset,
    int GeneratedStartOffset,
    int GeneratedEndOffset);
