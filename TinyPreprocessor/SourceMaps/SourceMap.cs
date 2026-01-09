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
    private readonly IReadOnlyList<SourceMapping> _mappings;

    /// <summary>
    /// Initializes a new instance of <see cref="SourceMap"/> with the specified mappings.
    /// </summary>
    /// <param name="mappings">The sorted list of mappings.</param>
    internal SourceMap(IReadOnlyList<SourceMapping> mappings)
    {
        _mappings = mappings;
    }

    /// <summary>
    /// Gets the source mappings sorted by generated position.
    /// </summary>
    public IReadOnlyList<SourceMapping> Mappings => _mappings;

    /// <summary>
    /// Queries the source map for the original location corresponding to a generated position.
    /// </summary>
    /// <param name="generatedPosition">The position in the generated output.</param>
    /// <returns>
    /// The original source location, or <see langword="null"/> if no mapping contains the position.
    /// </returns>
    public SourceLocation? Query(SourcePosition generatedPosition)
    {
        // Binary search to find the mapping containing the generated position
        var mapping = FindMappingForPosition(generatedPosition);
        if (mapping is null)
        {
            return null;
        }

        var originalPosition = mapping.MapPosition(generatedPosition);
        return originalPosition.HasValue
            ? new SourceLocation(mapping.OriginalResource, originalPosition.Value)
            : null;
    }

    /// <summary>
    /// Gets all mappings for a specific original resource.
    /// </summary>
    /// <param name="resourceId">The resource identifier to filter by.</param>
    /// <returns>An enumerable of mappings from the specified resource.</returns>
    public IEnumerable<SourceMapping> GetMappingsForResource(ResourceId resourceId)
    {
        return _mappings.Where(m => m.OriginalResource == resourceId);
    }

    /// <summary>
    /// Finds the mapping that contains the specified generated position using binary search.
    /// </summary>
    private SourceMapping? FindMappingForPosition(SourcePosition generatedPosition)
    {
        if (_mappings.Count == 0)
        {
            return null;
        }

        // Binary search for the rightmost mapping where Start <= generatedPosition
        var left = 0;
        var right = _mappings.Count - 1;
        var result = -1;

        while (left <= right)
        {
            var mid = left + (right - left) / 2;
            var mapping = _mappings[mid];

            if (mapping.GeneratedSpan.Start <= generatedPosition)
            {
                result = mid;
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }

        // Check if the found mapping actually contains the position
        if (result >= 0 && _mappings[result].GeneratedSpan.Contains(generatedPosition))
        {
            return _mappings[result];
        }

        return null;
    }
}
