using System.Buffers;
using System.Text;
using TinyPreprocessor.Diagnostics;
using TinyPreprocessor.Core;
using TinyPreprocessor.SourceMaps;

namespace TinyPreprocessor.Merging;

/// <summary>
/// Options for the concatenating merge strategy.
/// </summary>
/// <param name="Separator">The separator between resources. Defaults to newline.</param>
/// <param name="IncludeResourceMarkers">Whether to include debug markers for resource boundaries.</param>
/// <param name="MarkerFormat">The format string for resource markers. {0} is replaced with the resource ID.</param>
public sealed record ConcatenatingMergeOptions(
    string Separator = "\n",
    bool IncludeResourceMarkers = false,
    string MarkerFormat = "/* === {0} === */\n");

/// <summary>
/// Default merge strategy that concatenates resources and strips directives.
/// </summary>
/// <typeparam name="TContext">User-defined context type (unused by this strategy).</typeparam>
public sealed class ConcatenatingMergeStrategy<TContext> : IMergeStrategy<TContext>
{
    private readonly ConcatenatingMergeOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="ConcatenatingMergeStrategy{TContext}"/> with default options.
    /// </summary>
    public ConcatenatingMergeStrategy() : this(new ConcatenatingMergeOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ConcatenatingMergeStrategy{TContext}"/> with the specified options.
    /// </summary>
    /// <param name="options">The merge options.</param>
    public ConcatenatingMergeStrategy(ConcatenatingMergeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    public ReadOnlyMemory<char> Merge(
        IReadOnlyList<ResolvedResource> orderedResources,
        TContext userContext,
        MergeContext context)
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

            // Ensure original resources are registered (when available) so offset-based queries can resolve.
            // The pipeline already does this in Preprocessor, but merge unit tests may construct a context manually.
            if (context.ResolvedCache.Count > 0)
            {
                context.SourceMapBuilder.SetOriginalResources(context.ResolvedCache);
            }

            // Add resource marker if enabled
            if (_options.IncludeResourceMarkers)
            {
                var marker = string.Format(_options.MarkerFormat, resource.Id.Path);
                Append(output, marker.AsSpan());
            }

            StripDirectivesAndEmitSegments(resource, output, context.Diagnostics, context.SourceMapBuilder);

            // Add separator between resources (but not after the last one)
            if (i < orderedResources.Count - 1)
            {
                Append(output, _options.Separator.AsSpan());
            }
        }

        var merged = new string(output.WrittenSpan);

        // Register generated output so offset-based queries can convert SourcePosition -> offset.
        // The pipeline already does this in Preprocessor, but merge unit tests may construct a context manually.
        context.SourceMapBuilder.SetGeneratedContent(merged.AsMemory());

        return merged.AsMemory();
    }

    /// <summary>
    /// Strips directive ranges from the resource content.
    /// </summary>
    /// <param name="resource">The resource to process.</param>
    /// <returns>
    /// A tuple containing the stripped content and a list mapping output line indices to original line indices.
    /// </returns>
    private static void StripDirectivesAndEmitSegments(
        ResolvedResource resource,
        ArrayBufferWriter<char> output,
        DiagnosticCollection diagnostics,
        SourceMapBuilder builder)
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
            builder.AddOffsetSegment(resource.Id, generatedStart, originalStartOffset: 0, length: content.Length);
            return;
        }

        var excludedRanges = BuildExcludedRanges(resource, content, diagnostics);
        if (excludedRanges.Count == 0)
        {
            var generatedStart = output.WrittenCount;
            Append(output, content);
            builder.AddOffsetSegment(resource.Id, generatedStart, originalStartOffset: 0, length: content.Length);
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
                builder.AddOffsetSegment(resource.Id, generatedStart, originalStartOffset: current, length: length);
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
            builder.AddOffsetSegment(resource.Id, generatedStart, originalStartOffset: current, length: length);
        }
    }

    private static List<(int Start, int End)> BuildExcludedRanges(
        ResolvedResource resource,
        ReadOnlySpan<char> content,
        DiagnosticCollection diagnostics)
    {
        var ranges = new List<(int Start, int End)>(capacity: resource.Directives.Count);

        foreach (var directive in resource.Directives)
        {
            var start = directive.Location.Start.GetOffset(content.Length);
            var end = directive.Location.End.GetOffset(content.Length);

            start = Math.Clamp(start, 0, content.Length);
            end = Math.Clamp(end, 0, content.Length);

            if (end < start)
            {
                (start, end) = (end, start);
            }

            // Whole-line validation (diagnostic-only).
            if (!IsWholeLineDirective(content, start, end))
            {
                diagnostics.Add(new NonWholeLineDirectiveDiagnostic(resource.Id, directive.Location));
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

        // Coalesce overlaps/adjacency.
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

    private static bool IsWholeLineDirective(ReadOnlySpan<char> content, int start, int end)
    {
        if ((uint)start > (uint)content.Length || (uint)end > (uint)content.Length)
        {
            return false;
        }

        // Find start-of-line.
        var lineStart = 0;
        if (start > 0)
        {
            var prevNewline = content.Slice(0, start).LastIndexOf('\n');
            lineStart = prevNewline >= 0 ? prevNewline + 1 : 0;
        }

        // Find end-of-line for the line containing 'start'.
        var lineEnd = content.Length;
        var nextNewline = content.Slice(start).IndexOf('\n');
        if (nextNewline >= 0)
        {
            lineEnd = start + nextNewline;
        }

        // Disallow spans that extend beyond the line (except optionally including the newline itself).
        if (end > lineEnd + 1)
        {
            return false;
        }

        // Only whitespace allowed before directive start.
        for (var i = lineStart; i < start; i++)
        {
            if (!char.IsWhiteSpace(content[i]))
            {
                return false;
            }
        }

        // Only whitespace allowed after directive end until end-of-line.
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
