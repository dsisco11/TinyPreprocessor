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
    private readonly TextLineIndex? _generatedLineIndex;
    private readonly IReadOnlyDictionary<ResourceId, TextLineIndex>? _originalLineIndexes;

    /// <summary>
    /// Initializes a new instance of <see cref="SourceMap"/> with the specified offset segments.
    /// </summary>
    /// <param name="segments">The sorted list of offset mapping segments.</param>
    /// <param name="generatedLineIndex">Line index for converting generated positions to offsets.</param>
    /// <param name="originalLineIndexes">Line indexes for converting original offsets to positions.</param>
    internal SourceMap(
        IReadOnlyList<OffsetMappingSegment> segments,
        TextLineIndex? generatedLineIndex,
        IReadOnlyDictionary<ResourceId, TextLineIndex>? originalLineIndexes)
    {
        _segments = segments;
        _generatedLineIndex = generatedLineIndex;
        _originalLineIndexes = originalLineIndexes;
    }

    /// <summary>
    /// Gets the offset-based mapping segments used for exact range mapping.
    /// </summary>
    /// <remarks>
    /// This is an implementation detail exposed for debugging and advanced scenarios.
    /// The primary API surface is the set of <see cref="Query(SourcePosition)"/> and range query overloads.
    /// </remarks>
    internal IReadOnlyList<OffsetMappingSegment> Segments => _segments;

    /// <summary>
    /// Queries the source map for the original location corresponding to a generated position.
    /// </summary>
    /// <param name="generatedPosition">The position in the generated output.</param>
    /// <returns>
    /// The original source location, or <see langword="null"/> if no mapping contains the position.
    /// </returns>
    public SourceLocation? Query(SourcePosition generatedPosition)
    {
        var ranges = Query(generatedPosition, length: 1);
        return ranges.Count == 0
            ? null
            : new SourceLocation(ranges[0].Resource, ranges[0].OriginalStart);
    }

    /// <summary>
    /// Queries the source map for all original ranges corresponding to a generated range.
    /// </summary>
    /// <param name="generatedStart">Inclusive start position in the generated output.</param>
    /// <param name="length">Length of the generated range (in characters).</param>
    /// <returns>Zero or more exact mappings. Unmapped gaps are omitted.</returns>
    public IReadOnlyList<SourceRangeLocation> Query(SourcePosition generatedStart, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (length == 0)
        {
            return Array.Empty<SourceRangeLocation>();
        }

        if (_generatedLineIndex is null || _originalLineIndexes is null || _segments.Count == 0)
        {
            return Array.Empty<SourceRangeLocation>();
        }

        var generatedIndex = _generatedLineIndex.Value;
        if (!generatedIndex.TryGetOffset(generatedStart, out var startOffset))
        {
            return Array.Empty<SourceRangeLocation>();
        }

        var endOffset = Math.Min(startOffset + length, generatedIndex.TextLength);
        return QueryOffsetRange(new OffsetSpan(startOffset, endOffset));
    }

    /// <summary>
    /// Queries the source map for all original ranges corresponding to a generated range.
    /// </summary>
    /// <param name="generatedStart">Inclusive start position in the generated output.</param>
    /// <param name="generatedEnd">Exclusive end position in the generated output.</param>
    /// <returns>Zero or more exact mappings. Unmapped gaps are omitted.</returns>
    public IReadOnlyList<SourceRangeLocation> Query(SourcePosition generatedStart, SourcePosition generatedEnd)
    {
        if (generatedEnd < generatedStart)
        {
            throw new ArgumentException("End position must be greater than or equal to start position.", nameof(generatedEnd));
        }

        if (_generatedLineIndex is null || _originalLineIndexes is null || _segments.Count == 0)
        {
            return Array.Empty<SourceRangeLocation>();
        }

        var generatedIndex = _generatedLineIndex.Value;
        if (!generatedIndex.TryGetOffset(generatedStart, out var startOffset) ||
            !generatedIndex.TryGetOffset(generatedEnd, out var endOffset))
        {
            return Array.Empty<SourceRangeLocation>();
        }

        endOffset = Math.Min(endOffset, generatedIndex.TextLength);
        return QueryOffsetRange(new OffsetSpan(startOffset, endOffset));
    }

    #region Offset-Based Query Core

    private IReadOnlyList<SourceRangeLocation> QueryOffsetRange(OffsetSpan generatedRange)
    {
        if (_generatedLineIndex is null || _originalLineIndexes is null || generatedRange.Length <= 0)
        {
            return Array.Empty<SourceRangeLocation>();
        }

        var generatedIndex = _generatedLineIndex.Value;
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

            if (!_originalLineIndexes.TryGetValue(segment.OriginalResource, out var originalIndex))
            {
                continue;
            }

            var delta = overlap.Start - segment.Generated.Start;
            var originalStartOffset = segment.Original.Start + delta;
            var originalEndOffset = originalStartOffset + overlap.Length;

            var mappedGeneratedStart = generatedIndex.GetPosition(overlap.Start);
            var mappedGeneratedEnd = generatedIndex.GetPosition(overlap.End);
            var mappedOriginalStart = originalIndex.GetPosition(originalStartOffset);
            var mappedOriginalEnd = originalIndex.GetPosition(originalEndOffset);

            results.Add(new SourceRangeLocation(
                segment.OriginalResource,
                mappedOriginalStart,
                mappedOriginalEnd,
                mappedGeneratedStart,
                mappedGeneratedEnd));
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
