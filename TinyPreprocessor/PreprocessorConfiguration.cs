using TinyPreprocessor.Core;
using TinyPreprocessor.Merging;

namespace TinyPreprocessor;

public sealed record class PreprocessorConfiguration<TContent, TDirective, TContext>
{
    public IDirectiveParser<TContent, TDirective> DirectiveParser { get; }

    public IDirectiveModel<TDirective> DirectiveModel { get; }

    public IResourceResolver<TContent> ResourceResolver { get; }

    public IMergeStrategy<TContent, TDirective, TContext> MergeStrategy { get; }

    public IContentModel<TContent> ContentModel { get; }

    public PreprocessorConfiguration(
        IDirectiveParser<TContent, TDirective> directiveParser,
        IDirectiveModel<TDirective> directiveModel,
        IResourceResolver<TContent> resourceResolver,
        IMergeStrategy<TContent, TDirective, TContext> mergeStrategy,
        IContentModel<TContent> contentModel)
    {
        ArgumentNullException.ThrowIfNull(directiveParser);
        ArgumentNullException.ThrowIfNull(directiveModel);
        ArgumentNullException.ThrowIfNull(resourceResolver);
        ArgumentNullException.ThrowIfNull(mergeStrategy);
        ArgumentNullException.ThrowIfNull(contentModel);

        DirectiveParser = directiveParser;
        DirectiveModel = directiveModel;
        ResourceResolver = resourceResolver;
        MergeStrategy = mergeStrategy;
        ContentModel = contentModel;
    }
}
