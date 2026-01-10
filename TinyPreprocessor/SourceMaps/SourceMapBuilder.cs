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
    private readonly List<SourceMapping> _mappings = [];

    private readonly List<OffsetMappingSegment> _offsetSegments = [];

    private TextLineIndex? _generatedLineIndex;
    private readonly Dictionary<ResourceId, TextLineIndex> _originalLineIndexes = [];

    #region Content Registration

    /// <summary>
    /// Registers the final generated output so queries can convert <see cref="SourcePosition"/> to offsets.
    /// </summary>
    /// <param name="generatedContent">The merged output content.</param>
    public void SetGeneratedContent(ReadOnlyMemory<char> generatedContent)
    {
        _generatedLineIndex = TextLineIndex.Build(generatedContent.Span);
    }

    /// <summary>
    /// Registers original resources so queries can convert offsets to <see cref="SourcePosition"/>.
    /// </summary>
    /// <param name="resources">All resolved resources available during preprocessing.</param>
    public void SetOriginalResources(IReadOnlyDictionary<ResourceId, IResource> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        _originalLineIndexes.Clear();
        foreach (var (resourceId, resource) in resources)
        {
            _originalLineIndexes[resourceId] = TextLineIndex.Build(resource.Content.Span);
        }
    }

    #endregion

    /// <summary>
    /// Adds a mapping to the builder.
    /// </summary>
    /// <param name="mapping">The source mapping to add.</param>
    public void AddMapping(SourceMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        _mappings.Add(mapping);
    }

    /// <summary>
    /// Adds an exact offset-based mapping segment.
    /// </summary>
    /// <param name="resource">The original resource identifier.</param>
    /// <param name="generatedStartOffset">The inclusive start offset in generated output.</param>
    /// <param name="originalStartOffset">The inclusive start offset in original resource.</param>
    /// <param name="length">The length of the mapped segment (in characters).</param>
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
    /// Adds a mapping segment with the specified details.
    /// </summary>
    /// <param name="resource">The original resource identifier.</param>
    /// <param name="generatedSpan">The span in the generated output.</param>
    /// <param name="originalSpan">The span in the original resource.</param>
    public void AddSegment(ResourceId resource, SourceSpan generatedSpan, SourceSpan originalSpan)
    {
        _mappings.Add(new SourceMapping(generatedSpan, resource, originalSpan));
    }

    /// <summary>
    /// Adds a single-line mapping.
    /// </summary>
    /// <param name="resource">The original resource identifier.</param>
    /// <param name="generatedLine">The 0-based line number in the generated output.</param>
    /// <param name="originalLine">The 0-based line number in the original resource.</param>
    /// <param name="length">The length of the line (column count). Defaults to max value for full line coverage.</param>
    public void AddLine(ResourceId resource, int generatedLine, int originalLine, int length = int.MaxValue)
    {
        var generatedSpan = new SourceSpan(
            new SourcePosition(generatedLine, 0),
            new SourcePosition(generatedLine, length));

        var originalSpan = new SourceSpan(
            new SourcePosition(originalLine, 0),
            new SourcePosition(originalLine, length));

        _mappings.Add(new SourceMapping(generatedSpan, resource, originalSpan));
    }

    /// <summary>
    /// Builds an immutable <see cref="SourceMap"/> from the accumulated mappings.
    /// </summary>
    /// <returns>A new <see cref="SourceMap"/> with mappings sorted by generated position.</returns>
    public SourceMap Build()
    {
        var sortedMappings = _mappings
            .OrderBy(m => m.GeneratedSpan.Start)
            .ToList()
            .AsReadOnly();

        var segments = _offsetSegments.Count > 0
            ? _offsetSegments
                .OrderBy(s => s.GeneratedStart)
                .ToList()
                .AsReadOnly()
            : TryBuildOffsetSegments(sortedMappings);

        return new SourceMap(
            sortedMappings,
            segments,
            _generatedLineIndex,
            _originalLineIndexes.Count > 0 ? new Dictionary<ResourceId, TextLineIndex>(_originalLineIndexes) : null);
    }

    /// <summary>
    /// Clears all accumulated mappings.
    /// </summary>
    public void Clear()
    {
        _mappings.Clear();
        _offsetSegments.Clear();
        _generatedLineIndex = null;
        _originalLineIndexes.Clear();
    }

    #region Offset Segment Conversion

    private static IReadOnlyList<OffsetMappingSegment> EmptySegments { get; } = Array.Empty<OffsetMappingSegment>();

    private IReadOnlyList<OffsetMappingSegment> TryBuildOffsetSegments(IReadOnlyList<SourceMapping> mappings)
    {
        if (_generatedLineIndex is null || _originalLineIndexes.Count == 0 || mappings.Count == 0)
        {
            return EmptySegments;
        }

        var generatedIndex = _generatedLineIndex.Value;
        var segments = new List<OffsetMappingSegment>(capacity: mappings.Count);

        foreach (var mapping in mappings)
        {
            if (!_originalLineIndexes.TryGetValue(mapping.OriginalResource, out var originalIndex))
            {
                // Original resource index not registered; skip.
                continue;
            }

            if (!TryToOffsetSpan(generatedIndex, mapping.GeneratedSpan, out var generatedOffsetSpan) ||
                !TryToOffsetSpan(originalIndex, mapping.OriginalSpan, out var originalOffsetSpan))
            {
                // If spans cannot be converted, fall back to legacy SourceMapping queries.
                return EmptySegments;
            }

            // Require equal lengths for exact mapping segments.
            if (generatedOffsetSpan.Length != originalOffsetSpan.Length)
            {
                return EmptySegments;
            }

            segments.Add(new OffsetMappingSegment(generatedOffsetSpan, mapping.OriginalResource, originalOffsetSpan));
        }

        segments.Sort(static (a, b) => a.GeneratedStart.CompareTo(b.GeneratedStart));
        return segments.AsReadOnly();
    }

    private static bool TryToOffsetSpan(TextLineIndex index, SourceSpan span, out OffsetSpan offsetSpan)
    {
        offsetSpan = default;

        if (!TryToOffset(index, span.Start, isEnd: false, out var startOffset))
        {
            return false;
        }

        if (!TryToOffset(index, span.End, isEnd: true, out var endOffset))
        {
            return false;
        }

        offsetSpan = new OffsetSpan(startOffset, endOffset);
        return true;
    }

    private static bool TryToOffset(TextLineIndex index, SourcePosition position, bool isEnd, out int offset)
    {
        offset = 0;

        if (isEnd && position.Column == int.MaxValue)
        {
            if ((uint)position.Line >= (uint)index.LineCount)
            {
                return false;
            }

            position = new SourcePosition(position.Line, index.GetLineLength(position.Line));
        }

        return index.TryGetOffset(position, out offset);
    }

    #endregion
}
