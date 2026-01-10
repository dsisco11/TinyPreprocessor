using TinyPreprocessor.Core;

namespace TinyPreprocessor.SourceMaps;

/// <summary>
/// Maps an end-exclusive span in generated output (offset space) to an end-exclusive span in an original resource.
/// </summary>
internal readonly record struct OffsetMappingSegment(
    OffsetSpan Generated,
    ResourceId OriginalResource,
    OffsetSpan Original)
{
    public int GeneratedStart => Generated.Start;

    public int GeneratedEnd => Generated.End;
}
