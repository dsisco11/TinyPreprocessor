namespace TinyPreprocessor.Core;

/// <summary>
/// Provides boundary resolvers for multiple boundary marker types.
/// </summary>
public interface IContentBoundaryResolverProvider
{
    /// <summary>
    /// Tries to get a boundary resolver for the requested boundary marker type.
    /// </summary>
    bool TryGet<TContent, TBoundary>(out IContentBoundaryResolver<TContent, TBoundary> resolver);
}
