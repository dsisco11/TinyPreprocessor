using Graffs;
using Graffs.Algorithms;
using Graffs.Builders;
using TinyPreprocessor.Core;

namespace TinyPreprocessor.Graph;

/// <summary>
/// Manages the dependency relationships between resources.
/// </summary>
/// <remarks>
/// <para>
/// This class wraps the Graffs library to provide TinyPreprocessor-specific API
/// for dependency tracking, cycle detection, and topological ordering.
/// </para>
/// <para>
/// This class is not thread-safe. It's designed for single-threaded use during
/// a preprocessing operation.
/// </para>
/// </remarks>
public sealed class ResourceDependencyGraph
{
    private readonly HashSet<ResourceId> _resources = [];
    private readonly DependencyGraphBuilder<ResourceId> _builder = new();

    /// <summary>
    /// Registers an isolated resource node in the graph.
    /// </summary>
    /// <param name="id">The resource identifier to add.</param>
    public void AddResource(ResourceId id)
    {
        _resources.Add(id);
    }

    /// <summary>
    /// Records a dependency relationship: <paramref name="dependent"/> depends on <paramref name="dependency"/>.
    /// </summary>
    /// <param name="dependent">The resource that has the dependency (e.g., the file containing an include).</param>
    /// <param name="dependency">The resource being depended upon (e.g., the included file).</param>
    /// <remarks>
    /// Both resources are automatically registered in the graph.
    /// </remarks>
    public void AddDependency(ResourceId dependent, ResourceId dependency)
    {
        _resources.Add(dependent);
        _resources.Add(dependency);
        _builder.DependsOn(dependent, dependency);
    }

    /// <summary>
    /// Detects all cycles in the dependency graph.
    /// </summary>
    /// <returns>
    /// A list of cycles, where each cycle is represented as an ordered list of resource identifiers
    /// forming the circular dependency.
    /// </returns>
    public IReadOnlyList<IReadOnlyList<ResourceId>> DetectCycles()
    {
        var graph = BuildGraph();
        var result = CycleDetection.FindAllCycles(graph);
        return result.Cycles
            .Select(cycle => (IReadOnlyList<ResourceId>)cycle.Nodes.ToList())
            .ToList();
    }

    /// <summary>
    /// Quickly checks whether the graph contains any cycles.
    /// </summary>
    /// <returns><c>true</c> if cycles exist; otherwise, <c>false</c>.</returns>
    public bool HasCycles()
    {
        var graph = BuildGraph();
        return CycleDetection.HasCycles(graph);
    }

    /// <summary>
    /// Gets the resources in topological order (dependencies first).
    /// </summary>
    /// <returns>
    /// A list of resource identifiers in processing order, where dependencies appear before dependents.
    /// </returns>
    /// <remarks>
    /// If the graph contains cycles, the result may be incomplete or empty.
    /// Check for cycles using <see cref="DetectCycles"/> or <see cref="HasCycles"/> first.
    /// </remarks>
    public IReadOnlyList<ResourceId> GetProcessingOrder()
    {
        var graph = BuildGraph();
        var result = TopologicalSort.KahnSort(graph);
        return result.SortedNodes.ToList();
    }

    /// <summary>
    /// Gets all resources that the specified resource depends on.
    /// </summary>
    /// <param name="id">The resource identifier to query.</param>
    /// <returns>A set of resource identifiers that <paramref name="id"/> depends on.</returns>
    public IReadOnlySet<ResourceId> GetDependencies(ResourceId id)
    {
        var graph = BuildGraph();
        if (!graph.Nodes.Contains(id))
        {
            return new HashSet<ResourceId>();
        }

        return graph.GetDependencies(id).ToHashSet();
    }

    /// <summary>
    /// Gets all resources that depend on the specified resource.
    /// </summary>
    /// <param name="id">The resource identifier to query.</param>
    /// <returns>A set of resource identifiers that depend on <paramref name="id"/>.</returns>
    public IReadOnlySet<ResourceId> GetDependents(ResourceId id)
    {
        var graph = BuildGraph();
        if (!graph.Nodes.Contains(id))
        {
            return new HashSet<ResourceId>();
        }

        return graph.GetDependents(id).ToHashSet();
    }

    /// <summary>
    /// Gets all registered resources in the graph.
    /// </summary>
    /// <returns>A set of all resource identifiers.</returns>
    public IReadOnlySet<ResourceId> GetAllResources() => _resources;

    #region Private Methods

    private IDirectedGraph<ResourceId> BuildGraph() => _builder.Build();

    #endregion
}
