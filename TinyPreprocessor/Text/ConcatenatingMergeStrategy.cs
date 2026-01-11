using System.Buffers;
using TinyPreprocessor.Core;
using TinyPreprocessor.Diagnostics;
using TinyPreprocessor.Merging;
using TinyPreprocessor.SourceMaps;

namespace TinyPreprocessor.Text;

/// <summary>
/// Default merge strategy that concatenates resources and strips directives (text/char specialization).
/// </summary>
/// <typeparam name="TDirective">The directive type associated with resources.</typeparam>
/// <typeparam name="TContext">User-defined context type (unused by this strategy).</typeparam>
public sealed class ConcatenatingMergeStrategy<TDirective, TContext> : IMergeStrategy<ReadOnlyMemory<char>, TDirective, TContext>
{
    private readonly ConcatenatingMergeOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="ConcatenatingMergeStrategy{TDirective,TContext}"/> with default options.
    /// </summary>
    public ConcatenatingMergeStrategy() : this(new ConcatenatingMergeOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ConcatenatingMergeStrategy{TDirective,TContext}"/> with the specified options.
    /// </summary>
    /// <param name="options">The merge options.</param>
    public ConcatenatingMergeStrategy(ConcatenatingMergeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    public ReadOnlyMemory<char> Merge(
        IReadOnlyList<ResolvedResource<ReadOnlyMemory<char>, TDirective>> orderedResources,
        TContext userContext,
        MergeContext<ReadOnlyMemory<char>, TDirective> context)
    {
        ArgumentNullException.ThrowIfNull(orderedResources);
        ArgumentNullException.ThrowIfNull(context);

        if (orderedResources.Count == 0)
        {
            return ReadOnlyMemory<char>.Empty;
        }

        var output = new ArrayBufferWriter<char>();

        for (var i = 0; i < orderedResources.Count; i++)
        {
            var resource = orderedResources[i];

            if (_options.IncludeResourceMarkers)
            {
                var marker = string.Format(_options.MarkerFormat, resource.Id.Path);
                Append(output, marker.AsSpan());
            }

            StripDirectivesAndEmitSegments(resource, output, context);

            if (i < orderedResources.Count - 1)
            {
                Append(output, _options.Separator.AsSpan());
            }
        }

        var merged = new string(output.WrittenSpan);
        return merged.AsMemory();
    }

    private static void StripDirectivesAndEmitSegments(
        ResolvedResource<ReadOnlyMemory<char>, TDirective> resource,
        ArrayBufferWriter<char> output,
        MergeContext<ReadOnlyMemory<char>, TDirective> context)
    {
        var content = resource.Content.Span;
        if (content.Length == 0)
        {
            return;
        }

        if (resource.Directives.Count == 0)
        {
            var generatedStart = output.WrittenCount;
            Append(output, content);
            context.SourceMapBuilder.AddOffsetSegment(resource.Id, generatedStart, originalStartOffset: 0, length: content.Length);
            return;
        }

        var excludedRanges = BuildExcludedRanges(resource, content, context);
        if (excludedRanges.Count == 0)
        {
            var generatedStart = output.WrittenCount;
            Append(output, content);
            context.SourceMapBuilder.AddOffsetSegment(resource.Id, generatedStart, originalStartOffset: 0, length: content.Length);
            return;
        }

        var current = 0;
        foreach (var range in excludedRanges)
        {
            if (range.Start > current)
            {
                var length = range.Start - current;
                var generatedStart = output.WrittenCount;
                Append(output, content.Slice(current, length));
                context.SourceMapBuilder.AddOffsetSegment(resource.Id, generatedStart, originalStartOffset: current, length: length);
            }

            current = Math.Max(current, range.End);
            if (current >= content.Length)
            {
                break;
            }
        }

        if (current < content.Length)
        {
            var length = content.Length - current;
            var generatedStart = output.WrittenCount;
            Append(output, content.Slice(current, length));
            context.SourceMapBuilder.AddOffsetSegment(resource.Id, generatedStart, originalStartOffset: current, length: length);
        }
    }

    private static List<(int Start, int End)> BuildExcludedRanges(
        ResolvedResource<ReadOnlyMemory<char>, TDirective> resource,
        ReadOnlySpan<char> content,
        MergeContext<ReadOnlyMemory<char>, TDirective> context)
    {
        var ranges = new List<(int Start, int End)>(capacity: resource.Directives.Count);

        foreach (var directive in resource.Directives)
        {
            var location = context.DirectiveModel.GetLocation(directive);

            var start = location.Start.GetOffset(content.Length);
            var end = location.End.GetOffset(content.Length);

            start = Math.Clamp(start, 0, content.Length);
            end = Math.Clamp(end, 0, content.Length);

            if (end < start)
            {
                (start, end) = (end, start);
            }

            if (!IsWholeLineDirective(content, start, end, context, resource.Id, resource.Content))
            {
                context.Diagnostics.Add(new NonWholeLineDirectiveDiagnostic(resource.Id, location));
            }

            if (end > start)
            {
                ranges.Add((start, end));
            }
        }

        if (ranges.Count == 0)
        {
            return ranges;
        }

        ranges.Sort(static (a, b) => a.Start.CompareTo(b.Start));

        var coalesced = new List<(int Start, int End)>(capacity: ranges.Count);
        var current = ranges[0];
        for (var i = 1; i < ranges.Count; i++)
        {
            var next = ranges[i];
            if (next.Start <= current.End)
            {
                current = (current.Start, Math.Max(current.End, next.End));
                continue;
            }

            coalesced.Add(current);
            current = next;
        }

        coalesced.Add(current);
        return coalesced;
    }

    private static bool IsWholeLineDirective(
        ReadOnlySpan<char> content,
        int start,
        int end,
        MergeContext<ReadOnlyMemory<char>, TDirective> context,
        ResourceId resourceId,
        ReadOnlyMemory<char> contentMemory)
    {
        if (context.ContentBoundaryResolverProvider.TryGet<ReadOnlyMemory<char>, LineBoundary>(out var boundaryResolver))
        {
            return IsWholeLineDirectiveByLineBoundaries(content, start, end, boundaryResolver, resourceId, contentMemory);
        }

        return IsWholeLineDirectiveLegacyLf(content, start, end);
    }

    private static bool IsWholeLineDirectiveByLineBoundaries(
        ReadOnlySpan<char> content,
        int start,
        int end,
        IContentBoundaryResolver<ReadOnlyMemory<char>, LineBoundary> boundaryResolver,
        ResourceId resourceId,
        ReadOnlyMemory<char> contentMemory)
    {
        if ((uint)start > (uint)content.Length || (uint)end > (uint)content.Length)
        {
            return false;
        }

        var lineStart = 0;
        foreach (var boundaryStart in boundaryResolver.ResolveOffsets(contentMemory, resourceId, startOffset: 0, endOffset: start + 1))
        {
            lineStart = boundaryStart;
        }

        var lineEnd = content.Length;
        foreach (var boundaryStart in boundaryResolver.ResolveOffsets(contentMemory, resourceId, startOffset: start + 1, endOffset: content.Length))
        {
            lineEnd = boundaryStart;
            break;
        }

        // Directive must not extend into the next line.
        if (end > lineEnd)
        {
            return false;
        }

        for (var i = lineStart; i < start; i++)
        {
            if (!char.IsWhiteSpace(content[i]))
            {
                return false;
            }
        }

        for (var i = end; i < lineEnd; i++)
        {
            if (!char.IsWhiteSpace(content[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsWholeLineDirectiveLegacyLf(ReadOnlySpan<char> content, int start, int end)
    {
        if ((uint)start > (uint)content.Length || (uint)end > (uint)content.Length)
        {
            return false;
        }

        var lineStart = 0;
        if (start > 0)
        {
            var prevNewline = content.Slice(0, start).LastIndexOf('\n');
            lineStart = prevNewline >= 0 ? prevNewline + 1 : 0;
        }

        var lineEnd = content.Length;
        var nextNewline = content.Slice(start).IndexOf('\n');
        if (nextNewline >= 0)
        {
            lineEnd = start + nextNewline;
        }

        if (end > lineEnd + 1)
        {
            return false;
        }

        for (var i = lineStart; i < start; i++)
        {
            if (!char.IsWhiteSpace(content[i]))
            {
                return false;
            }
        }

        if (end <= lineEnd)
        {
            for (var i = end; i < lineEnd; i++)
            {
                if (!char.IsWhiteSpace(content[i]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void Append(ArrayBufferWriter<char> writer, ReadOnlySpan<char> value)
    {
        if (value.Length == 0)
        {
            return;
        }

        var dest = writer.GetSpan(value.Length);
        value.CopyTo(dest);
        writer.Advance(value.Length);
    }
}
