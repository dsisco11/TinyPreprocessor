using TinyPreprocessor.Core;
using TinyPreprocessor.Graph;
using Xunit;

namespace TinyPreprocessor.Tests.Graph;

/// <summary>
/// Unit tests for <see cref="ResourceDependencyGraph"/>.
/// </summary>
public sealed class ResourceDependencyGraphTests
{
    #region Cycle Detection Tests

    [Fact]
    public void DetectCycles_NoCycles_ReturnsEmpty()
    {
        var graph = new ResourceDependencyGraph();
        graph.AddDependency("A", "B");
        graph.AddDependency("B", "C");
        graph.AddDependency("A", "C");

        var cycles = graph.DetectCycles();

        Assert.Empty(cycles);
    }

    [Fact]
    public void DetectCycles_SimpleCycle_DetectsCycle()
    {
        var graph = new ResourceDependencyGraph();
        graph.AddDependency("A", "B");
        graph.AddDependency("B", "A");

        var cycles = graph.DetectCycles();

        Assert.NotEmpty(cycles);
        Assert.Contains(cycles, cycle =>
            cycle.Contains(new ResourceId("A")) && cycle.Contains(new ResourceId("B")));
    }

    [Fact]
    public void DetectCycles_SelfReference_DetectsCycle()
    {
        var graph = new ResourceDependencyGraph();
        graph.AddDependency("A", "A");

        var cycles = graph.DetectCycles();

        Assert.NotEmpty(cycles);
    }

    [Fact]
    public void DetectCycles_TriangleCycle_DetectsCycle()
    {
        var graph = new ResourceDependencyGraph();
        graph.AddDependency("A", "B");
        graph.AddDependency("B", "C");
        graph.AddDependency("C", "A");

        var cycles = graph.DetectCycles();

        Assert.NotEmpty(cycles);
    }

    [Fact]
    public void DetectCycles_MultipleCycles_DetectsAll()
    {
        var graph = new ResourceDependencyGraph();
        // Cycle 1: A -> B -> A
        graph.AddDependency("A", "B");
        graph.AddDependency("B", "A");
        // Cycle 2: C -> D -> C
        graph.AddDependency("C", "D");
        graph.AddDependency("D", "C");

        var cycles = graph.DetectCycles();

        Assert.True(cycles.Count >= 2);
    }

    [Fact]
    public void HasCycles_WithCycle_ReturnsTrue()
    {
        var graph = new ResourceDependencyGraph();
        graph.AddDependency("A", "B");
        graph.AddDependency("B", "A");

        Assert.True(graph.HasCycles());
    }

    [Fact]
    public void HasCycles_WithoutCycle_ReturnsFalse()
    {
        var graph = new ResourceDependencyGraph();
        graph.AddDependency("A", "B");
        graph.AddDependency("B", "C");

        Assert.False(graph.HasCycles());
    }

    #endregion

    #region Topological Sort Tests

    [Fact]
    public void GetProcessingOrder_LinearChain_ReturnsDependenciesFirst()
    {
        var graph = new ResourceDependencyGraph();
        graph.AddDependency("main.txt", "helper.txt");
        graph.AddDependency("helper.txt", "utils.txt");

        var order = graph.GetProcessingOrder();

        var mainIndex = order.IndexOf(new ResourceId("main.txt"));
        var helperIndex = order.IndexOf(new ResourceId("helper.txt"));
        var utilsIndex = order.IndexOf(new ResourceId("utils.txt"));

        // Dependencies should appear before dependents
        Assert.True(utilsIndex < helperIndex);
        Assert.True(helperIndex < mainIndex);
    }

    [Fact]
    public void GetProcessingOrder_DiamondDependency_AllDependenciesBeforeDependent()
    {
        var graph = new ResourceDependencyGraph();
        // Diamond: A depends on B and C, both depend on D
        graph.AddDependency("A", "B");
        graph.AddDependency("A", "C");
        graph.AddDependency("B", "D");
        graph.AddDependency("C", "D");

        var order = graph.GetProcessingOrder();

        var aIndex = order.IndexOf(new ResourceId("A"));
        var bIndex = order.IndexOf(new ResourceId("B"));
        var cIndex = order.IndexOf(new ResourceId("C"));
        var dIndex = order.IndexOf(new ResourceId("D"));

        // D must come before B and C
        Assert.True(dIndex < bIndex);
        Assert.True(dIndex < cIndex);
        // B and C must come before A
        Assert.True(bIndex < aIndex);
        Assert.True(cIndex < aIndex);
    }

    [Fact]
    public void GetProcessingOrder_MultipleTrees_AllResourcesIncluded()
    {
        var graph = new ResourceDependencyGraph();
        // Tree 1
        graph.AddDependency("A", "B");
        // Tree 2 (disconnected)
        graph.AddDependency("C", "D");

        var order = graph.GetProcessingOrder();

        Assert.Equal(4, order.Count);
        Assert.Contains(new ResourceId("A"), order);
        Assert.Contains(new ResourceId("B"), order);
        Assert.Contains(new ResourceId("C"), order);
        Assert.Contains(new ResourceId("D"), order);
    }

    [Fact]
    public void AddResource_IsolatedResources_TrackedInAllResources()
    {
        var graph = new ResourceDependencyGraph();
        graph.AddResource("isolated.txt");
        graph.AddDependency("A", "B");

        var allResources = graph.GetAllResources();

        Assert.Contains(new ResourceId("isolated.txt"), allResources);
        Assert.Contains(new ResourceId("A"), allResources);
        Assert.Contains(new ResourceId("B"), allResources);
    }

    #endregion

    #region Empty Graph Edge Cases

    [Fact]
    public void DetectCycles_EmptyGraph_ReturnsEmpty()
    {
        var graph = new ResourceDependencyGraph();

        var cycles = graph.DetectCycles();

        Assert.Empty(cycles);
    }

    [Fact]
    public void HasCycles_EmptyGraph_ReturnsFalse()
    {
        var graph = new ResourceDependencyGraph();

        Assert.False(graph.HasCycles());
    }

    [Fact]
    public void GetProcessingOrder_EmptyGraph_ReturnsEmpty()
    {
        var graph = new ResourceDependencyGraph();

        var order = graph.GetProcessingOrder();

        Assert.Empty(order);
    }

    [Fact]
    public void GetAllResources_SingleResource_ReturnsSingleItem()
    {
        var graph = new ResourceDependencyGraph();
        graph.AddResource("single.txt");

        var allResources = graph.GetAllResources();

        Assert.Single(allResources);
        Assert.Contains(new ResourceId("single.txt"), allResources);
    }

    [Fact]
    public void GetDependencies_NonExistentResource_ReturnsEmpty()
    {
        var graph = new ResourceDependencyGraph();
        graph.AddDependency("A", "B");

        var deps = graph.GetDependencies("NonExistent");

        Assert.Empty(deps);
    }

    #endregion

    #region AddResource / AddDependency Tests

    [Fact]
    public void AddResource_MultipleTimes_NoDuplicates()
    {
        var graph = new ResourceDependencyGraph();
        graph.AddResource("file.txt");
        graph.AddResource("file.txt");
        graph.AddResource("file.txt");

        var allResources = graph.GetAllResources();

        Assert.Single(allResources);
    }

    [Fact]
    public void AddDependency_AutomaticallyAddsResources()
    {
        var graph = new ResourceDependencyGraph();
        graph.AddDependency("A", "B");

        var order = graph.GetProcessingOrder();

        Assert.Contains(new ResourceId("A"), order);
        Assert.Contains(new ResourceId("B"), order);
    }

    [Fact]
    public void GetDependencies_ReturnsDirectDependencies()
    {
        var graph = new ResourceDependencyGraph();
        graph.AddDependency("main", "lib1");
        graph.AddDependency("main", "lib2");
        graph.AddDependency("lib1", "common");

        var mainDeps = graph.GetDependencies("main");

        Assert.Equal(2, mainDeps.Count);
        Assert.Contains(new ResourceId("lib1"), mainDeps);
        Assert.Contains(new ResourceId("lib2"), mainDeps);
        // common is transitive, not direct
        Assert.DoesNotContain(new ResourceId("common"), mainDeps);
    }

    #endregion

    #region Complex Graph Scenarios

    [Fact]
    public void ComplexGraph_MixedCyclesAndChains()
    {
        var graph = new ResourceDependencyGraph();
        // Linear chain: root -> a -> b -> c
        graph.AddDependency("root", "a");
        graph.AddDependency("a", "b");
        graph.AddDependency("b", "c");
        // Cycle branch: root -> x -> y -> x
        graph.AddDependency("root", "x");
        graph.AddDependency("x", "y");
        graph.AddDependency("y", "x");

        var cycles = graph.DetectCycles();

        Assert.NotEmpty(cycles);
        Assert.Contains(cycles, cycle =>
            cycle.Contains(new ResourceId("x")) && cycle.Contains(new ResourceId("y")));
    }

    #endregion
}

file static class ListExtensions
{
    public static int IndexOf<T>(this IReadOnlyList<T> list, T item)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(list[i], item))
            {
                return i;
            }
        }
        return -1;
    }
}
