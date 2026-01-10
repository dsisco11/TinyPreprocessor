using TinyPreprocessor.Core;

namespace TinyPreprocessor.SourceMaps;

/// <summary>
/// Represents an exact mapping between a generated range and an original range.
/// </summary>
/// <param name="Resource">The identifier of the original resource.</param>
/// <param name="OriginalStart">Inclusive start position in the original resource.</param>
/// <param name="OriginalEnd">Exclusive end position in the original resource.</param>
/// <param name="GeneratedStart">Inclusive start position in the generated output.</param>
/// <param name="GeneratedEnd">Exclusive end position in the generated output.</param>
public sealed record SourceRangeLocation(
    ResourceId Resource,
    SourcePosition OriginalStart,
    SourcePosition OriginalEnd,
    SourcePosition GeneratedStart,
    SourcePosition GeneratedEnd);
