namespace TinyPreprocessor.Merging;

/// <summary>
/// Interface for custom merge implementations.
/// </summary>
/// <typeparam name="TContext">User-defined context type for strategy-specific options.</typeparam>
public interface IMergeStrategy<in TContext>
{
    /// <summary>
    /// Merges resolved resources into a single output.
    /// </summary>
    /// <param name="orderedResources">Resources sorted by dependency order (dependencies first).</param>
    /// <param name="userContext">User-provided context for strategy customization.</param>
    /// <param name="context">Merge context with source map builder and diagnostics.</param>
    /// <returns>The merged content.</returns>
    ReadOnlyMemory<char> Merge(
        IReadOnlyList<ResolvedResource> orderedResources,
        TContext userContext,
        MergeContext context);
}
