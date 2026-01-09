using System.Text;
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

        var output = new StringBuilder();
        var currentOutputLine = 0;

        for (var i = 0; i < orderedResources.Count; i++)
        {
            var resource = orderedResources[i];

            // Add resource marker if enabled
            if (_options.IncludeResourceMarkers)
            {
                var marker = string.Format(_options.MarkerFormat, resource.Id.Path);
                output.Append(marker);
                currentOutputLine += CountLines(marker);
            }

            // Strip directives and get the processed content with line mapping info
            var (strippedContent, lineMappings) = StripDirectives(resource);

            // Record source mappings
            RecordSourceMappings(
                resource.Id,
                strippedContent,
                lineMappings,
                currentOutputLine,
                context.SourceMapBuilder);

            output.Append(strippedContent);
            currentOutputLine += CountLines(strippedContent);

            // Add separator between resources (but not after the last one)
            if (i < orderedResources.Count - 1)
            {
                output.Append(_options.Separator);
                currentOutputLine += CountLines(_options.Separator);
            }
        }

        return output.ToString().AsMemory();
    }

    /// <summary>
    /// Strips directive ranges from the resource content.
    /// </summary>
    /// <param name="resource">The resource to process.</param>
    /// <returns>
    /// A tuple containing the stripped content and a list mapping output line indices to original line indices.
    /// </returns>
    private static (string Content, List<int> LineMappings) StripDirectives(ResolvedResource resource)
    {
        var content = resource.Content;
        var directives = resource.Directives;

        if (directives.Count == 0)
        {
            // No directives, return content as-is with identity line mapping
            var contentString = content.ToString();
            var lineCount = CountLines(contentString);
            var identityMapping = Enumerable.Range(0, lineCount).ToList();
            return (contentString, identityMapping);
        }

        // Sort directives by location (start index) in descending order for safe removal
        var sortedDirectives = directives
            .OrderBy(d => d.Location.Start.GetOffset(content.Length))
            .ToList();

        // Build a set of character ranges to exclude
        var excludedRanges = sortedDirectives
            .Select(d => (
                Start: d.Location.Start.GetOffset(content.Length),
                End: d.Location.End.GetOffset(content.Length)))
            .ToList();

        // Build the stripped content and track line mappings
        var output = new StringBuilder();
        var lineMappings = new List<int>();
        var span = content.Span;

        var currentOriginalLine = 0;
        var currentCharIndex = 0;
        var excludeIndex = 0;

        while (currentCharIndex < span.Length)
        {
            // Check if we're entering an excluded range
            while (excludeIndex < excludedRanges.Count &&
                   excludedRanges[excludeIndex].End <= currentCharIndex)
            {
                excludeIndex++;
            }

            var inExcludedRange = excludeIndex < excludedRanges.Count &&
                                  currentCharIndex >= excludedRanges[excludeIndex].Start &&
                                  currentCharIndex < excludedRanges[excludeIndex].End;

            if (inExcludedRange)
            {
                // Skip this character, but track line changes
                if (span[currentCharIndex] == '\n')
                {
                    currentOriginalLine++;
                }
                currentCharIndex++;
            }
            else
            {
                // Record line mapping at start of each output line
                if (output.Length == 0 || (output.Length > 0 && output[^1] == '\n'))
                {
                    lineMappings.Add(currentOriginalLine);
                }

                output.Append(span[currentCharIndex]);

                if (span[currentCharIndex] == '\n')
                {
                    currentOriginalLine++;
                }
                currentCharIndex++;
            }
        }

        // Handle case where content doesn't end with newline but we have content
        if (output.Length > 0 && lineMappings.Count == 0)
        {
            lineMappings.Add(0);
        }

        return (output.ToString(), lineMappings);
    }

    /// <summary>
    /// Records source mappings for the stripped content.
    /// </summary>
    private static void RecordSourceMappings(
        ResourceId resourceId,
        string strippedContent,
        List<int> lineMappings,
        int outputLineOffset,
        SourceMapBuilder builder)
    {
        if (string.IsNullOrEmpty(strippedContent) || lineMappings.Count == 0)
        {
            return;
        }

        // Split content into lines and record mappings
        var lines = strippedContent.Split('\n');

        for (var outputLine = 0; outputLine < lineMappings.Count && outputLine < lines.Length; outputLine++)
        {
            var originalLine = lineMappings[outputLine];
            var lineLength = lines[outputLine].Length;

            // Skip empty lines that are just artifacts of splitting
            if (outputLine == lines.Length - 1 && lineLength == 0 && strippedContent.EndsWith('\n'))
            {
                continue;
            }

            builder.AddLine(
                resourceId,
                generatedLine: outputLineOffset + outputLine,
                originalLine: originalLine,
                length: lineLength > 0 ? lineLength : int.MaxValue);
        }
    }

    /// <summary>
    /// Counts the number of lines in a string.
    /// </summary>
    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var count = 0;
        foreach (var c in text)
        {
            if (c == '\n')
            {
                count++;
            }
        }

        // If text doesn't end with newline, count the last line
        if (text.Length > 0 && text[^1] != '\n')
        {
            count++;
        }

        return count;
    }
}
