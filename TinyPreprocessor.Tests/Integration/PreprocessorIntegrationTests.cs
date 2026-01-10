using Moq;
using TinyPreprocessor.Core;
using TinyPreprocessor.Diagnostics;
using TinyPreprocessor.Merging;
using TinyPreprocessor.SourceMaps;
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
    public async Task ProcessAsync_WithSimpleInclude_ProducesExactFlattenedOutputAndSourceMap()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();
        var header = new Resource("header.txt", "H1\nH2".AsMemory());
        var main = new Resource("main.txt", "#include header.txt\nM1".AsMemory());

        resolver.Setup(r => r.ResolveAsync("header.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(header, null));

        var result = await preprocessor.ProcessAsync(main, new object());

        Assert.True(result.Success);
        Assert.Equal("H1\nH2\n\nM1", result.Content.ToString());

        AssertMapped(result.SourceMap, generatedLine: 0, generatedColumn: 0, expectedResource: "header.txt", expectedOriginalLine: 0, expectedOriginalColumn: 0);
        AssertMapped(result.SourceMap, generatedLine: 1, generatedColumn: 1, expectedResource: "header.txt", expectedOriginalLine: 1, expectedOriginalColumn: 1);
        AssertMapped(result.SourceMap, generatedLine: 2, generatedColumn: 0, expectedResource: "main.txt", expectedOriginalLine: 0, expectedOriginalColumn: 0);
        AssertMapped(result.SourceMap, generatedLine: 3, generatedColumn: 0, expectedResource: "main.txt", expectedOriginalLine: 1, expectedOriginalColumn: 0);
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
    public async Task ProcessAsync_MultiLevelIncludes_FlattensAndMapsNestedIncludesCorrectly()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();
        var level2 = new Resource("level2.txt", "L2a\nL2b".AsMemory());
        var level1 = new Resource("level1.txt", "#include level2.txt\nL1".AsMemory());
        var main = new Resource("main.txt", "#include level1.txt\nM".AsMemory());

        resolver.Setup(r => r.ResolveAsync("level1.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(level1, null));
        resolver.Setup(r => r.ResolveAsync("level2.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(level2, null));

        var result = await preprocessor.ProcessAsync(main, new object());

        Assert.True(result.Success);
        Assert.Equal("L2a\nL2b\n\nL1\n\nM", result.Content.ToString());

        AssertMapped(result.SourceMap, generatedLine: 0, generatedColumn: 1, expectedResource: "level2.txt", expectedOriginalLine: 0, expectedOriginalColumn: 1);
        AssertMapped(result.SourceMap, generatedLine: 1, generatedColumn: 0, expectedResource: "level2.txt", expectedOriginalLine: 1, expectedOriginalColumn: 0);
        AssertMapped(result.SourceMap, generatedLine: 2, generatedColumn: 0, expectedResource: "level1.txt", expectedOriginalLine: 0, expectedOriginalColumn: 0);
        AssertMapped(result.SourceMap, generatedLine: 3, generatedColumn: 0, expectedResource: "level1.txt", expectedOriginalLine: 1, expectedOriginalColumn: 0);
        AssertMapped(result.SourceMap, generatedLine: 4, generatedColumn: 0, expectedResource: "main.txt", expectedOriginalLine: 0, expectedOriginalColumn: 0);
        AssertMapped(result.SourceMap, generatedLine: 5, generatedColumn: 0, expectedResource: "main.txt", expectedOriginalLine: 1, expectedOriginalColumn: 0);
    }

    [Fact]
    public async Task ProcessAsync_BranchingIncludes_FlattensAllAndMapsEachOriginWithoutAssumingOrder()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();

        var c = new Resource("c.txt", "C1".AsMemory());
        var a = new Resource("a.txt", "#include c.txt\nA1".AsMemory());
        var d = new Resource("d.txt", "D1".AsMemory());
        var b = new Resource("b.txt", "#include d.txt\nB1".AsMemory());
        var main = new Resource("main.txt", "#include a.txt\n#include b.txt\nM1".AsMemory());

        resolver.Setup(r => r.ResolveAsync("a.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(a, null));
        resolver.Setup(r => r.ResolveAsync("b.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(b, null));
        resolver.Setup(r => r.ResolveAsync("c.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(c, null));
        resolver.Setup(r => r.ResolveAsync("d.txt", It.IsAny<IResource?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult(d, null));

        var result = await preprocessor.ProcessAsync(main, new object());

        Assert.True(result.Success);

        var content = result.Content.ToString();

        Assert.DoesNotContain("#include", content);
        Assert.Contains("C1", content);
        Assert.Contains("A1", content);
        Assert.Contains("D1", content);
        Assert.Contains("B1", content);
        Assert.Contains("M1", content);

        // Within each branch, the transitive dependency should appear before its parent.
        Assert.True(content.IndexOf("C1", StringComparison.Ordinal) < content.IndexOf("A1", StringComparison.Ordinal));
        Assert.True(content.IndexOf("D1", StringComparison.Ordinal) < content.IndexOf("B1", StringComparison.Ordinal));

        // Validate source mapping for each unique token without assuming a/b branch ordering.
        AssertMappedAtToken(result.SourceMap, content, token: "C1", expectedResource: "c.txt", expectedOriginalLine: 0, expectedOriginalColumn: 0);
        AssertMappedAtToken(result.SourceMap, content, token: "A1", expectedResource: "a.txt", expectedOriginalLine: 1, expectedOriginalColumn: 0);
        AssertMappedAtToken(result.SourceMap, content, token: "D1", expectedResource: "d.txt", expectedOriginalLine: 0, expectedOriginalColumn: 0);
        AssertMappedAtToken(result.SourceMap, content, token: "B1", expectedResource: "b.txt", expectedOriginalLine: 1, expectedOriginalColumn: 0);
        AssertMappedAtToken(result.SourceMap, content, token: "M1", expectedResource: "main.txt", expectedOriginalLine: 2, expectedOriginalColumn: 0);
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
        Assert.False(result.Content.IsEmpty);
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

    private static void AssertMapped(
        SourceMap sourceMap,
        int generatedLine,
        int generatedColumn,
        string expectedResource,
        int expectedOriginalLine,
        int expectedOriginalColumn)
    {
        var mapped = sourceMap.Query(new SourcePosition(generatedLine, generatedColumn));

        Assert.NotNull(mapped);
        Assert.Equal(new ResourceId(expectedResource), mapped.Resource);
        Assert.Equal(expectedOriginalLine, mapped.OriginalPosition.Line);
        Assert.Equal(expectedOriginalColumn, mapped.OriginalPosition.Column);
    }

    private static void AssertMappedAtToken(
        SourceMap sourceMap,
        string generatedContent,
        string token,
        string expectedResource,
        int expectedOriginalLine,
        int expectedOriginalColumn)
    {
        var position = FindPosition(generatedContent, token);
        var mapped = sourceMap.Query(position);

        Assert.NotNull(mapped);
        Assert.Equal(new ResourceId(expectedResource), mapped.Resource);
        Assert.Equal(expectedOriginalLine, mapped.OriginalPosition.Line);
        Assert.Equal(expectedOriginalColumn, mapped.OriginalPosition.Column);
    }

    private static SourcePosition FindPosition(string text, string token)
    {
        var index = text.IndexOf(token, StringComparison.Ordinal);
        Assert.True(index >= 0, $"Token '{token}' not found in generated output.");

        var line = 0;
        var lastNewLineIndex = -1;
        for (var i = 0; i < index; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lastNewLineIndex = i;
            }
        }

        var column = index - (lastNewLineIndex + 1);
        return new SourcePosition(line, column);
    }

    #endregion
}

/// <summary>
/// Test implementation of IIncludeDirective for integration tests.
/// </summary>
public sealed record TestIncludeDirective(string Reference, System.Range Location) : IIncludeDirective;
