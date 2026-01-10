using System;
using System.Collections.Generic;
using TinyPreprocessor.Core;

namespace TinyPreprocessor.SourceMaps;

/// <summary>
/// Immutable collection of source mappings with efficient position lookup.
/// </summary>
/// <remarks>
/// Mappings are stored sorted by generated position for O(log n) lookup.
/// Use <see cref="SourceMapBuilder"/> to construct instances.
/// </remarks>
public sealed class SourceMap
{
    private readonly IReadOnlyList<OffsetMappingSegment> _segments;

    /// <summary>
    /// Initializes a new instance of <see cref="SourceMap"/> with the specified offset segments.
    /// </summary>
    /// <param name="segments">The sorted list of offset mapping segments.</param>
    internal SourceMap(
        IReadOnlyList<OffsetMappingSegment> segments)
    {
        _segments = segments;
    }

    /// <summary>
    /// Gets the offset-based mapping segments used for exact range mapping.
    /// </summary>
    /// <remarks>
    /// This is an implementation detail exposed for debugging and advanced scenarios.
    /// The primary API surface is the set of offset-based <see cref="Query(int)"/> and range query overloads.
    /// </remarks>
    internal IReadOnlyList<OffsetMappingSegment> Segments => _segments;

    /// <summary>
    /// Queries the source map for the original location corresponding to a generated offset.
    /// </summary>
    /// <param name="generatedOffset">The 0-based offset in the generated output.</param>
    /// <returns>
    /// The original source location, or <see langword="null"/> if no mapping contains the position.
    /// </returns>
    public SourceLocation? Query(int generatedOffset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(generatedOffset);

        var ranges = QueryRangeByLength(generatedOffset, length: 1);
        return ranges.Count == 0
            ? null
            : new SourceLocation(ranges[0].Resource, ranges[0].OriginalStartOffset);
    }

    /// <summary>
    /// Queries the source map for all original ranges corresponding to a generated range.
    /// </summary>
    /// <param name="generatedStartOffset">Inclusive start offset in the generated output.</param>
    /// <param name="length">Length of the generated range (in content units).</param>
    /// <returns>Zero or more exact mappings. Unmapped gaps are omitted.</returns>
    public IReadOnlyList<SourceRangeLocation> QueryRangeByLength(int generatedStartOffset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfNegative(generatedStartOffset);

        if (length == 0)
        {
            return Array.Empty<SourceRangeLocation>();
        }

        if (_segments.Count == 0)
        {
            return Array.Empty<SourceRangeLocation>();
        }

        return QueryOffsetRange(new OffsetSpan(generatedStartOffset, generatedStartOffset + length));
    }

    /// <summary>
    /// Queries the source map for all original ranges corresponding to a generated range.
    /// </summary>
    /// <param name="generatedStartOffset">Inclusive start offset in the generated output.</param>
    /// <param name="generatedEndOffset">Exclusive end offset in the generated output.</param>
    /// <returns>Zero or more exact mappings. Unmapped gaps are omitted.</returns>
    public IReadOnlyList<SourceRangeLocation> QueryRangeByEnd(int generatedStartOffset, int generatedEndOffset)
    {
        if (generatedEndOffset < generatedStartOffset)
        {
            throw new ArgumentException("End offset must be greater than or equal to start offset.", nameof(generatedEndOffset));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(generatedStartOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(generatedEndOffset);

        if (_segments.Count == 0)
        {
            return Array.Empty<SourceRangeLocation>();
        }

        return QueryOffsetRange(new OffsetSpan(generatedStartOffset, generatedEndOffset));
    }

    #region Offset-Based Query Core

    private IReadOnlyList<SourceRangeLocation> QueryOffsetRange(OffsetSpan generatedRange)
    {
        if (generatedRange.Length <= 0)
        {
            return Array.Empty<SourceRangeLocation>();
        }
        var results = new List<SourceRangeLocation>();

        var startIdx = FindFirstOverlappingSegmentIndex(generatedRange.Start);
        if (startIdx < 0)
        {
            return Array.Empty<SourceRangeLocation>();
        }

        for (var i = startIdx; i < _segments.Count; i++)
        {
            var segment = _segments[i];

            if (segment.GeneratedStart >= generatedRange.End)
            {
                break;
            }

            if (!segment.Generated.Overlaps(generatedRange))
            {
                continue;
            }

            var overlap = segment.Generated.Intersect(generatedRange);
            if (overlap.Length <= 0)
            {
                continue;
            }

            var delta = overlap.Start - segment.Generated.Start;
            var originalStartOffset = segment.Original.Start + delta;
            var originalEndOffset = originalStartOffset + overlap.Length;

            results.Add(new SourceRangeLocation(
                segment.OriginalResource,
                originalStartOffset,
                originalEndOffset,
                overlap.Start,
                overlap.End));
        }

        return results;
    }

    private int FindFirstOverlappingSegmentIndex(int generatedOffset)
    {
        if (_segments.Count == 0)
        {
            return -1;
        }

        // Find first segment whose start is >= generatedOffset.
        var left = 0;
        var right = _segments.Count - 1;
        var candidate = _segments.Count;

        while (left <= right)
        {
            var mid = left + (right - left) / 2;
            var start = _segments[mid].GeneratedStart;

            if (start >= generatedOffset)
            {
                candidate = mid;
                right = mid - 1;
            }
            else
            {
                left = mid + 1;
            }
        }

        // The overlapping segment could be the one right before candidate.
        var idx = candidate;
        if (idx > 0 && _segments[idx - 1].GeneratedEnd > generatedOffset)
        {
            idx--;
        }

        return idx >= _segments.Count ? -1 : idx;
    }

    #endregion

    // Legacy SourceMapping/SourceSpan-based mapping has been removed.
}
