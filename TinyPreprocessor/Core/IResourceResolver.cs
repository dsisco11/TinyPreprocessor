namespace TinyPreprocessor.Core;

/// <summary>
/// Resolves string references (from directives) into actual resources.
/// </summary>
/// <remarks>
/// Implementations may involve I/O operations (file system, network, database),
/// hence the async-first design with <see cref="ValueTask{TResult}"/>.
/// </remarks>
public interface IResourceResolver<TContent>
{
    /// <summary>
    /// Resolves a reference string to an actual resource.
    /// </summary>
    /// <param name="reference">The reference string to resolve (e.g., file path, module name).</param>
    /// <param name="relativeTo">The resource from which the reference is made, enabling relative resolution.</param>
    /// <param name="ct">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="ResourceResolutionResult{TContent}"/> containing either the resolved resource or an error diagnostic.
    /// </returns>
    ValueTask<ResourceResolutionResult<TContent>> ResolveAsync(
        string reference,
        IResource<TContent>? relativeTo,
        CancellationToken ct);
}
