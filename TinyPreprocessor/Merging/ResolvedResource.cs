using TinyPreprocessor.Core;

namespace TinyPreprocessor.Merging;

/// <summary>
/// A resource paired with its parsed directives, ready for merging.
/// </summary>
/// <param name="Resource">The resolved resource.</param>
/// <param name="Directives">The parsed directives found in the resource.</param>
public sealed record ResolvedResource<TSymbol, TDirective>(
    IResource<TSymbol> Resource,
    IReadOnlyList<TDirective> Directives)
{
    /// <summary>
    /// Gets the resource identifier.
    /// </summary>
    public ResourceId Id => Resource.Id;

    /// <summary>
    /// Gets the resource content.
    /// </summary>
    public ReadOnlyMemory<TSymbol> Content => Resource.Content;
}
