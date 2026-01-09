using Moq;
using TinyPreprocessor.Core;
using TinyPreprocessor.Diagnostics;
using TinyPreprocessor.Merging;
using Xunit;

namespace TinyPreprocessor.Tests.Integration;

/// <summary>
/// Integration tests for the full preprocessing pipeline.
/// </summary>
public sealed class PreprocessorIntegrationTests
{
    #region Full Pipeline Tests

    [Fact]
    public async Task ProcessAsync_SingleFileNoIncludes_ReturnsContent()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();
        var root = new Resource("main.txt", "Hello, World!".AsMemory());

        var result = await preprocessor.ProcessAsync(root, new object());

        Assert.True(result.Success);
        Assert.Equal("Hello, World!", result.Content.ToString());
    }

    [Fact]
    public async Task ProcessAsync_WithSimpleInclude_MergesCorrectly()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();
        var header = new Resource("header.txt", "Header content".AsMemory());
        var main = new Resource("main.txt", "#include header.txt\nMain content".AsMemory());

        resolver.Setup(r => r.ResolveAsync("header.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(header, null));

        var result = await preprocessor.ProcessAsync(main, new object());

        Assert.True(result.Success);
        Assert.Contains("Header content", result.Content.ToString());
        Assert.Contains("Main content", result.Content.ToString());
    }

    [Fact]
    public async Task ProcessAsync_MultiLevelIncludes_ProcessesAll()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();
        var level2 = new Resource("level2.txt", "Level 2".AsMemory());
        var level1 = new Resource("level1.txt", "#include level2.txt\nLevel 1".AsMemory());
        var main = new Resource("main.txt", "#include level1.txt\nMain".AsMemory());

        resolver.Setup(r => r.ResolveAsync("level1.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(level1, null));
        resolver.Setup(r => r.ResolveAsync("level2.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(level2, null));

        var result = await preprocessor.ProcessAsync(main, new object());

        Assert.True(result.Success);
        Assert.Contains("Level 2", result.Content.ToString());
        Assert.Contains("Level 1", result.Content.ToString());
        Assert.Contains("Main", result.Content.ToString());
    }

    [Fact]
    public async Task ProcessAsync_BuildsSourceMap()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();
        var root = new Resource("source.txt", "Line 1\nLine 2".AsMemory());

        var result = await preprocessor.ProcessAsync(root, new object());

        Assert.NotNull(result.SourceMap);
        Assert.NotEmpty(result.SourceMap.Mappings);
    }

    [Fact]
    public async Task ProcessAsync_PopulatesDiagnostics()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();
        var root = new Resource("main.txt", "#include missing.txt\nContent".AsMemory());

        resolver.Setup(r => r.ResolveAsync("missing.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(null, 
                new ResolutionFailedDiagnostic("missing.txt", "File not found")));

        var result = await preprocessor.ProcessAsync(root, new object());

        Assert.False(result.Success);
        Assert.True(result.Diagnostics.HasErrors);
    }

    [Fact]
    public async Task ProcessAsync_TracksProcessedResources()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();
        var dep = new Resource("dep.txt", "Dependency".AsMemory());
        var main = new Resource("main.txt", "#include dep.txt\nMain".AsMemory());

        resolver.Setup(r => r.ResolveAsync("dep.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(dep, null));

        var result = await preprocessor.ProcessAsync(main, new object());

        Assert.Equal(2, result.ProcessedResources.Count);
        Assert.Contains(new ResourceId("main.txt"), result.ProcessedResources);
        Assert.Contains(new ResourceId("dep.txt"), result.ProcessedResources);
    }

    #endregion

    #region Circular Dependency Handling Tests

    [Fact]
    public async Task ProcessAsync_CircularDependency_ReportsDiagnostic()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();
        var fileA = new Resource("a.txt", "#include b.txt\nFile A".AsMemory());
        var fileB = new Resource("b.txt", "#include a.txt\nFile B".AsMemory());

        resolver.Setup(r => r.ResolveAsync("b.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(fileB, null));
        resolver.Setup(r => r.ResolveAsync("a.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(fileA, null));

        var result = await preprocessor.ProcessAsync(fileA, new object());

        // With deduplication, circular deps might not recurse infinitely
        // but cycles should still be detected
        var cycleDiagnostics = result.Diagnostics
            .OfType<CircularDependencyDiagnostic>()
            .ToList();

        Assert.NotEmpty(cycleDiagnostics);
    }

    [Fact]
    public async Task ProcessAsync_SelfReference_ReportsDiagnostic()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();
        var file = new Resource("self.txt", "#include self.txt\nContent".AsMemory());

        resolver.Setup(r => r.ResolveAsync("self.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(file, null));

        var result = await preprocessor.ProcessAsync(file, new object());

        var cycleDiagnostics = result.Diagnostics
            .OfType<CircularDependencyDiagnostic>()
            .ToList();

        Assert.NotEmpty(cycleDiagnostics);
    }

    [Fact]
    public async Task ProcessAsync_CircularDependency_ContinuesProcessing()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();
        var fileA = new Resource("a.txt", "#include b.txt\nContent A".AsMemory());
        var fileB = new Resource("b.txt", "#include a.txt\nContent B".AsMemory());

        resolver.Setup(r => r.ResolveAsync("b.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(fileB, null));
        resolver.Setup(r => r.ResolveAsync("a.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(fileA, null));

        var result = await preprocessor.ProcessAsync(fileA, new object());

        // Processing should continue despite cycle (collect all diagnostics pattern)
        Assert.NotNull(result.Content);
    }

    #endregion

    #region Deduplication Behavior Tests

    [Fact]
    public async Task ProcessAsync_DeduplicationEnabled_IncludesOnlyOnce()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();
        var shared = new Resource("shared.txt", "SHARED".AsMemory());
        var libA = new Resource("libA.txt", "#include shared.txt\nLibA".AsMemory());
        var libB = new Resource("libB.txt", "#include shared.txt\nLibB".AsMemory());
        var main = new Resource("main.txt", "#include libA.txt\n#include libB.txt\nMain".AsMemory());

        resolver.Setup(r => r.ResolveAsync("shared.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(shared, null));
        resolver.Setup(r => r.ResolveAsync("libA.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(libA, null));
        resolver.Setup(r => r.ResolveAsync("libB.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(libB, null));

        var options = new PreprocessorOptions(DeduplicateIncludes: true);
        var result = await preprocessor.ProcessAsync(main, new object(), options);

        // SHARED should appear only once
        var content = result.Content.ToString();
        var sharedCount = CountOccurrences(content, "SHARED");
        Assert.Equal(1, sharedCount);
    }

    [Fact]
    public async Task ProcessAsync_DeduplicationDisabled_RevisitsForDependencies()
    {
        // When deduplication is disabled, we still only include content once,
        // but we do revisit resources for dependency tracking purposes
        var (preprocessor, resolver, _) = CreatePreprocessor();
        var shared = new Resource("shared.txt", "SHARED".AsMemory());
        var main = new Resource("main.txt", "#include shared.txt\n#include shared.txt\nMain".AsMemory());

        resolver.Setup(r => r.ResolveAsync("shared.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(shared, null));

        var options = new PreprocessorOptions(DeduplicateIncludes: false);
        var result = await preprocessor.ProcessAsync(main, new object(), options);

        // Content is included only once even with deduplication disabled
        // (deduplication affects dependency resolution, not content inclusion)
        var content = result.Content.ToString();
        var sharedCount = CountOccurrences(content, "SHARED");
        Assert.Equal(1, sharedCount);
        
        // Both resources should be processed
        Assert.Equal(2, result.ProcessedResources.Count);
    }

    #endregion

    #region MaxIncludeDepth Enforcement Tests

    [Fact]
    public async Task ProcessAsync_ExceedsMaxDepth_ReportsDiagnostic()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();

        // Create a chain of includes that exceeds depth 3
        var level3 = new Resource("level3.txt", "#include level4.txt\nL3".AsMemory());
        var level2 = new Resource("level2.txt", "#include level3.txt\nL2".AsMemory());
        var level1 = new Resource("level1.txt", "#include level2.txt\nL1".AsMemory());
        var level0 = new Resource("level0.txt", "#include level1.txt\nL0".AsMemory());
        var level4 = new Resource("level4.txt", "L4".AsMemory());

        resolver.Setup(r => r.ResolveAsync("level1.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(level1, null));
        resolver.Setup(r => r.ResolveAsync("level2.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(level2, null));
        resolver.Setup(r => r.ResolveAsync("level3.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(level3, null));
        resolver.Setup(r => r.ResolveAsync("level4.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(level4, null));

        var options = new PreprocessorOptions(MaxIncludeDepth: 3);
        var result = await preprocessor.ProcessAsync(level0, new object(), options);

        var depthDiagnostics = result.Diagnostics
            .OfType<MaxDepthExceededDiagnostic>()
            .ToList();

        Assert.NotEmpty(depthDiagnostics);
    }

    [Fact]
    public async Task ProcessAsync_AtMaxDepth_NoError()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();

        var level2 = new Resource("level2.txt", "L2".AsMemory());
        var level1 = new Resource("level1.txt", "#include level2.txt\nL1".AsMemory());
        var level0 = new Resource("level0.txt", "#include level1.txt\nL0".AsMemory());

        resolver.Setup(r => r.ResolveAsync("level1.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(level1, null));
        resolver.Setup(r => r.ResolveAsync("level2.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(level2, null));

        var options = new PreprocessorOptions(MaxIncludeDepth: 5);
        var result = await preprocessor.ProcessAsync(level0, new object(), options);

        var depthDiagnostics = result.Diagnostics
            .OfType<MaxDepthExceededDiagnostic>()
            .ToList();

        Assert.Empty(depthDiagnostics);
    }

    [Fact]
    public async Task ProcessAsync_MaxDepthZero_FailsOnFirstInclude()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();
        var child = new Resource("child.txt", "Child".AsMemory());
        var main = new Resource("main.txt", "#include child.txt\nMain".AsMemory());

        resolver.Setup(r => r.ResolveAsync("child.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(child, null));

        var options = new PreprocessorOptions(MaxIncludeDepth: 0);
        var result = await preprocessor.ProcessAsync(main, new object(), options);

        var depthDiagnostics = result.Diagnostics
            .OfType<MaxDepthExceededDiagnostic>()
            .ToList();

        Assert.NotEmpty(depthDiagnostics);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task ProcessAsync_Cancelled_ThrowsOperationCancelledException()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();
        var root = new Resource("main.txt", "#include slow.txt\nContent".AsMemory());

        resolver.Setup(r => r.ResolveAsync("slow.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, IResource? _, CancellationToken ct) =>
            {
                await Task.Delay(1000, ct);
                return new ResourceResolutionResult(new Resource("slow.txt", "".AsMemory()), null);
            });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await preprocessor.ProcessAsync(root, new object(), ct: cts.Token));
    }

    #endregion

    #region Null Handling Tests

    [Fact]
    public async Task ProcessAsync_NullRoot_ThrowsArgumentNullException()
    {
        var (preprocessor, _, _) = CreatePreprocessor();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await preprocessor.ProcessAsync(null!, new object()));
    }

    #endregion

    #region Test Infrastructure

    private static (
        Preprocessor<TestIncludeDirective, object> Preprocessor,
        Mock<IResourceResolver> Resolver,
        Mock<IDirectiveParser<TestIncludeDirective>> Parser)
        CreatePreprocessor()
    {
        var parser = new Mock<IDirectiveParser<TestIncludeDirective>>();
        var resolver = new Mock<IResourceResolver>();
        var mergeStrategy = new ConcatenatingMergeStrategy<object>();

        // Setup parser to find #include directives
        parser.Setup(p => p.Parse(It.IsAny<ReadOnlyMemory<char>>(), It.IsAny<ResourceId>()))
            .Returns((ReadOnlyMemory<char> content, ResourceId _) =>
            {
                var text = content.ToString();
                var directives = new List<TestIncludeDirective>();

                var lines = text.Split('\n');
                var offset = 0;
                foreach (var line in lines)
                {
                    if (line.StartsWith("#include "))
                    {
                        var reference = line[9..].Trim();
                        directives.Add(new TestIncludeDirective(reference, offset..(offset + line.Length)));
                    }
                    offset += line.Length + 1; // +1 for newline
                }

                return directives;
            });

        var preprocessor = new Preprocessor<TestIncludeDirective, object>(
            parser.Object,
            resolver.Object,
            mergeStrategy);

        return (preprocessor, resolver, parser);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    #endregion
}

/// <summary>
/// Test implementation of IIncludeDirective for integration tests.
/// </summary>
public sealed record TestIncludeDirective(string Reference, System.Range Location) : IIncludeDirective;
