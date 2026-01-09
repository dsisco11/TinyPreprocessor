using TinyPreprocessor.Core;

namespace TinyPreprocessor.SourceMaps;

/// <summary>
/// Maps a span in generated output to a span in an original resource.
/// </summary>
/// <param name="GeneratedSpan">The span in the generated output.</param>
/// <param name="OriginalResource">The identifier of the original resource.</param>
/// <param name="OriginalSpan">The span in the original resource.</param>
public sealed record SourceMapping(
    SourceSpan GeneratedSpan,
    ResourceId OriginalResource,
    SourceSpan OriginalSpan)
{
    /// <summary>
    /// Maps a position in the generated output to the corresponding position in the original resource.
    /// </summary>
    /// <param name="generatedPosition">The position in the generated output.</param>
    /// <returns>
    /// The corresponding position in the original resource, or <see langword="null"/>
    /// if <paramref name="generatedPosition"/> is outside <see cref="GeneratedSpan"/>.
    /// </returns>
    public SourcePosition? MapPosition(SourcePosition generatedPosition)
    {
        if (!GeneratedSpan.Contains(generatedPosition))
        {
            return null;
        }

        // Calculate delta from generated span start
        var lineDelta = generatedPosition.Line - GeneratedSpan.Start.Line;
        var columnDelta = generatedPosition.Column - GeneratedSpan.Start.Column;

        // Apply delta to original span start
        // For multi-line spans, column delta only applies when on the same relative line
        var originalLine = OriginalSpan.Start.Line + lineDelta;
        var originalColumn = lineDelta == 0
            ? OriginalSpan.Start.Column + columnDelta
            : generatedPosition.Column;

        return new SourcePosition(originalLine, originalColumn);
    }
}
