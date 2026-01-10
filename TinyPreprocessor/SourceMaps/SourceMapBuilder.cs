using System;
using System.Collections.Generic;
using System.Linq;
using TinyPreprocessor.Core;

namespace TinyPreprocessor.SourceMaps;

/// <summary>
/// Builder for accumulating source mappings during merge operations.
/// </summary>
public sealed class SourceMapBuilder
{
    private readonly List<OffsetMappingSegment> _offsetSegments = [];

    /// <summary>
    /// Adds an exact offset-based mapping segment.
    /// </summary>
    /// <param name="resource">The original resource identifier.</param>
    /// <param name="generatedStartOffset">The inclusive start offset in generated output.</param>
    /// <param name="originalStartOffset">The inclusive start offset in original resource.</param>
    /// <param name="length">The length of the mapped segment (in content units).</param>
    public void AddOffsetSegment(ResourceId resource, int generatedStartOffset, int originalStartOffset, int length)
    {
        if (generatedStartOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(generatedStartOffset));
        }

        if (originalStartOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(originalStartOffset));
        }

        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        _offsetSegments.Add(new OffsetMappingSegment(
            new OffsetSpan(generatedStartOffset, generatedStartOffset + length),
            resource,
            new OffsetSpan(originalStartOffset, originalStartOffset + length)));
    }

    /// <summary>
    /// Builds an immutable <see cref="SourceMap"/> from the accumulated mappings.
    /// </summary>
    /// <returns>A new <see cref="SourceMap"/> with mappings sorted by generated position.</returns>
    public SourceMap Build()
    {
        var segments = _offsetSegments
            .OrderBy(s => s.GeneratedStart)
            .ToList()
            .AsReadOnly();

        return new SourceMap(segments);
    }

    /// <summary>
    /// Clears all accumulated mappings.
    /// </summary>
    public void Clear()
    {
        _offsetSegments.Clear();
    }
}
