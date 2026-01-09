namespace TinyPreprocessor.Core;

/// <summary>
/// Extracts directives from resource content.
/// </summary>
/// <typeparam name="TDirective">The type of directive this parser produces.</typeparam>
/// <remarks>
/// Parsing is CPU-bound, so this interface uses synchronous methods.
/// The <see cref="IEnumerable{T}"/> return type allows lazy evaluation for streaming large files.
/// </remarks>
public interface IDirectiveParser<out TDirective> where TDirective : IDirective
{
    /// <summary>
    /// Parses the content and extracts all directives.
    /// </summary>
    /// <param name="content">The content to parse.</param>
    /// <param name="resourceId">The identifier of the resource being parsed, for context-aware parsing.</param>
    /// <returns>An enumerable of parsed directives.</returns>
    IEnumerable<TDirective> Parse(ReadOnlyMemory<char> content, ResourceId resourceId);
}
