namespace TinyPreprocessor.Core;

/// <summary>
/// Built-in boundary resolver providers.
/// </summary>
public static class ContentBoundaryResolverProvider
{
    public static IContentBoundaryResolverProvider Empty { get; } = new EmptyContentBoundaryResolverProvider();

    private sealed class EmptyContentBoundaryResolverProvider : IContentBoundaryResolverProvider
    {
        public bool TryGet<TContent, TBoundary>(out IContentBoundaryResolver<TContent, TBoundary> resolver)
        {
            resolver = default!;
            return false;
        }
    }
}
