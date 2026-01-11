using TinyPreprocessor.Core;
using TinyPreprocessor.Diagnostics;
using TinyPreprocessor.Graph;
using TinyPreprocessor.Merging;
using TinyPreprocessor.SourceMaps;

namespace TinyPreprocessor;

/// <summary>
/// The main orchestrator that coordinates the entire preprocessing pipeline.
/// </summary>
/// <typeparam name="TContent">The content representation type of resource content.</typeparam>
/// <typeparam name="TDirective">The type of directive parsed from resources.</typeparam>
/// <typeparam name="TContext">User-defined context type for merge strategy customization.</typeparam>
/// <remarks>
/// <para>
/// This class is thread-safe for concurrent <see cref="ProcessAsync"/> calls,
/// as each call maintains isolated state.
/// </para>
/// <para>
/// The dependencies (parser, resolver, merger) should be thread-safe or documented otherwise.
/// </para>
/// </remarks>
public sealed class Preprocessor<TContent, TDirective, TContext>
{
    private readonly IDirectiveParser<TContent, TDirective> _parser;
    private readonly IDirectiveModel<TDirective> _directiveModel;
    private readonly IResourceResolver<TContent> _resolver;
    private readonly IMergeStrategy<TContent, TDirective, TContext> _mergeStrategy;
    private readonly IContentModel<TContent> _contentModel;

    public Preprocessor(PreprocessorConfiguration<TContent, TDirective, TContext> config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _parser = config.DirectiveParser;
        _directiveModel = config.DirectiveModel;
        _resolver = config.ResourceResolver;
        _mergeStrategy = config.MergeStrategy;
        _contentModel = config.ContentModel;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="Preprocessor{TContent, TDirective, TContext}"/>.
    /// </summary>
    /// <param name="parser">The directive parser for extracting directives from resources.</param>
    /// <param name="directiveModel">The directive model for interpreting directive locations and dependency references.</param>
    /// <param name="resolver">The resource resolver for resolving references.</param>
    /// <param name="mergeStrategy">The merge strategy for combining resources.</param>
    /// <param name="contentModel">The content model for interpreting offsets and slicing content.</param>
    public Preprocessor(
        IDirectiveParser<TContent, TDirective> parser,
        IDirectiveModel<TDirective> directiveModel,
        IResourceResolver<TContent> resolver,
        IMergeStrategy<TContent, TDirective, TContext> mergeStrategy,
        IContentModel<TContent> contentModel)
        : this(new PreprocessorConfiguration<TContent, TDirective, TContext>(
            parser,
            directiveModel,
            resolver,
            mergeStrategy,
            contentModel))
    {
    }

    /// <summary>
    /// Processes a root resource and all its dependencies, producing merged output.
    /// </summary>
    /// <param name="root">The root resource to process.</param>
    /// <param name="context">User-provided context for merge strategy customization.</param>
    /// <param name="options">Processing options, or <see langword="null"/> for defaults.</param>
    /// <param name="ct">A cancellation token to cancel the operation.</param>
    /// <returns>The preprocessing result containing merged content, source map, and diagnostics.</returns>
    public async ValueTask<PreprocessResult<TContent>> ProcessAsync(
        IResource<TContent> root,
        TContext context,
        PreprocessorOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(root);

        options ??= PreprocessorOptions.Default;

        // Initialize per-call state
        var diagnostics = new DiagnosticCollection();
        var graph = new ResourceDependencyGraph();
        var cache = new Dictionary<ResourceId, ResolvedResource<TContent, TDirective>>();
        var sourceMapBuilder = new SourceMapBuilder();

        // Phase 1: Recursive resolution
        await ResolveRecursiveAsync(root, depth: 0, options, diagnostics, graph, cache, ct);

        // Phase 2: Cycle detection
        DetectAndReportCycles(graph, diagnostics);

        // Phase 3: Topological ordering
        var processingOrder = GetProcessingOrder(graph, cache);

        // Phase 4: Merge
        var resolvedCache = cache.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Resource);

        var mergeContext = new MergeContext<TContent, TDirective>(
            sourceMapBuilder,
            diagnostics,
            resolvedCache,
            _directiveModel,
            _contentModel);
        var orderedResources = processingOrder
            .Where(id => cache.ContainsKey(id))
            .Select(id => cache[id])
            .ToList();

        var mergedContent = _mergeStrategy.Merge(orderedResources, context, mergeContext);

        // Phase 5: Build result
        return new PreprocessResult<TContent>(
            mergedContent,
            sourceMapBuilder.Build(),
            diagnostics,
            processingOrder,
            graph);
    }

    #region Phase 1: Recursive Resolution

    /// <summary>
    /// Recursively resolves a resource and its dependencies.
    /// </summary>
    private async ValueTask ResolveRecursiveAsync(
        IResource<TContent> resource,
        int depth,
        PreprocessorOptions options,
        DiagnosticCollection diagnostics,
        ResourceDependencyGraph graph,
        Dictionary<ResourceId, ResolvedResource<TContent, TDirective>> cache,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Check if already processed (deduplication)
        if (cache.ContainsKey(resource.Id))
        {
            return;
        }

        // Add resource to graph
        graph.AddResource(resource.Id);

        // Parse directives
        var directives = _parser.Parse(resource.Content, resource.Id).ToList();

        // Cache the resolved resource
        cache[resource.Id] = new ResolvedResource<TContent, TDirective>(resource, directives);

        // Process include directives
        foreach (var directive in directives)
        {
            if (!_directiveModel.TryGetReference(directive, out var reference))
            {
                continue;
            }

            var directiveLocation = _directiveModel.GetLocation(directive);

            // Check depth limit
            if (depth >= options.MaxIncludeDepth)
            {
                diagnostics.Add(new MaxDepthExceededDiagnostic(
                    reference,
                    depth,
                    options.MaxIncludeDepth,
                    resource.Id,
                    directiveLocation));

                if (!options.ContinueOnError)
                {
                    return;
                }

                continue;
            }

            // Resolve the reference
            var result = await _resolver.ResolveAsync(reference, resource, ct);

            if (result.Error is not null)
            {
                diagnostics.Add(result.Error);

                if (!options.ContinueOnError)
                {
                    return;
                }

                continue;
            }

            if (result.Resource is null)
            {
                // Should not happen if Error is null, but handle defensively
                diagnostics.Add(new ResolutionFailedDiagnostic(
                    reference,
                    Reason: null,
                    resource.Id,
                    directiveLocation));

                if (!options.ContinueOnError)
                {
                    return;
                }

                continue;
            }

            // Add dependency to graph
            graph.AddDependency(resource.Id, result.Resource.Id);

            // Check deduplication
            if (options.DeduplicateIncludes && cache.ContainsKey(result.Resource.Id))
            {
                continue;
            }

            // Recurse
            await ResolveRecursiveAsync(
                result.Resource,
                depth + 1,
                options,
                diagnostics,
                graph,
                cache,
                ct);
        }
    }

    #endregion

    #region Phase 2: Cycle Detection

    /// <summary>
    /// Detects cycles in the dependency graph and adds diagnostics.
    /// </summary>
    private static void DetectAndReportCycles(
        ResourceDependencyGraph graph,
        DiagnosticCollection diagnostics)
    {
        var cycles = graph.DetectCycles();

        foreach (var cycle in cycles)
        {
            diagnostics.Add(new CircularDependencyDiagnostic(cycle));
        }
    }

    #endregion

    #region Phase 3: Topological Ordering

    /// <summary>
    /// Gets the processing order from the dependency graph.
    /// </summary>
    private static IReadOnlyList<ResourceId> GetProcessingOrder(
        ResourceDependencyGraph graph,
        Dictionary<ResourceId, ResolvedResource<TContent, TDirective>> cache)
    {
        var order = graph.GetProcessingOrder();

        // If topological sort returned empty (due to cycles), fall back to cache order
        if (order.Count == 0 && cache.Count > 0)
        {
            return cache.Keys.ToList();
        }

        return order;
    }

    #endregion
}
