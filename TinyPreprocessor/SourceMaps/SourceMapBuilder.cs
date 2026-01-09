using TinyPreprocessor.Core;

namespace TinyPreprocessor.SourceMaps;

/// <summary>
/// Builder for accumulating source mappings during merge operations.
/// </summary>
public sealed class SourceMapBuilder
{
    private readonly List<SourceMapping> _mappings = [];

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

        return new SourceMap(sortedMappings);
    }

    /// <summary>
    /// Clears all accumulated mappings.
    /// </summary>
    public void Clear() => _mappings.Clear();
}
