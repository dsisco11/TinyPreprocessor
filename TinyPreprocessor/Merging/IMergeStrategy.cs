namespace TinyPreprocessor.Merging;

/// <summary>
/// Interface for custom merge implementations.
/// </summary>
/// <typeparam name="TContent">The content representation type being merged.</typeparam>
/// <typeparam name="TDirective">The directive type associated with each resource.</typeparam>
/// <typeparam name="TContext">User-defined context type for strategy-specific options.</typeparam>
public interface IMergeStrategy<TContent, TDirective, in TContext>
{
    /// <summary>
    /// Merges resolved resources into a single output.
    /// </summary>
    /// <param name="orderedResources">Resources sorted by dependency order (dependencies first).</param>
    /// <param name="userContext">User-provided context for strategy customization.</param>
    /// <param name="context">Merge context with source map builder and diagnostics.</param>
    /// <returns>The merged content.</returns>
    TContent Merge(
        IReadOnlyList<ResolvedResource<TContent, TDirective>> orderedResources,
        TContext userContext,
        MergeContext<TContent, TDirective> context);
}
