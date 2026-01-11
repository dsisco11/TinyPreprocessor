using Moq;
using TinyPreprocessor.Core;
using TinyPreprocessor.Diagnostics;
using TinyPreprocessor.Merging;
using TinyPreprocessor.SourceMaps;
using TinyPreprocessor.Text;
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
        var root = new Resource<ReadOnlyMemory<char>>("main.txt", "Hello, World!".AsMemory());

        var result = await preprocessor.ProcessAsync(root, new object());

        Assert.True(result.Success);
        Assert.Equal("Hello, World!", result.Content.ToString());
    }

    [Fact]
    public async Task ProcessAsync_WithSimpleInclude_MergesCorrectly()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();
        var header = new Resource<ReadOnlyMemory<char>>("header.txt", "Header content".AsMemory());
        var main = new Resource<ReadOnlyMemory<char>>("main.txt", "#include header.txt\nMain content".AsMemory());

        resolver.Setup(r => r.ResolveAsync("header.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(header, null));

        var result = await preprocessor.ProcessAsync(main, new object());

        Assert.True(result.Success);
        Assert.Contains("Header content", result.Content.ToString());
        Assert.Contains("Main content", result.Content.ToString());
    }

    [Fact]
    public async Task ProcessAsync_WithSimpleInclude_ProducesExactFlattenedOutputAndSourceMap()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();
        const string headerText = "H1\nH2";
        var header = new Resource<ReadOnlyMemory<char>>("header.txt", headerText.AsMemory());
        var main = new Resource<ReadOnlyMemory<char>>("main.txt", "#include header.txt\nM1".AsMemory());

        resolver.Setup(r => r.ResolveAsync("header.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(header, null));

        var result = await preprocessor.ProcessAsync(main, new object());

        Assert.True(result.Success);
        var content = result.Content.ToString();
        Assert.Equal("H1\nH2\n\nM1", content);

        AssertMapped(result.SourceMap, content, generatedLine: 0, generatedColumn: 0, expectedResource: "header.txt", expectedOriginalOffset: 0);
        AssertMapped(result.SourceMap, content, generatedLine: 1, generatedColumn: 1, expectedResource: "header.txt", expectedOriginalOffset: OffsetOfLineColumn(headerText, line: 1, column: 1));
        // This blank line is formed by the original newline after the include directive.
        AssertMapped(result.SourceMap, content, generatedLine: 2, generatedColumn: 0, expectedResource: "main.txt", expectedOriginalOffset: "#include header.txt".Length);
        AssertMapped(result.SourceMap, content, generatedLine: 3, generatedColumn: 0, expectedResource: "main.txt", expectedOriginalOffset: "#include header.txt\n".Length);
    }

    [Fact]
    public async Task ProcessAsync_CustomMappedImport_UsesResolvedReferencesForInlining()
    {
        var parser = new Mock<IDirectiveParser<ReadOnlyMemory<char>, TestImportDirective>>();
        var resolver = new Mock<IResourceResolver<ReadOnlyMemory<char>>>();
        var mergeStrategy = new ResolvedReferenceInliningMergeStrategy();

        parser
            .Setup(p => p.Parse(It.IsAny<ReadOnlyMemory<char>>(), It.IsAny<ResourceId>()))
            .Returns((ReadOnlyMemory<char> content, ResourceId _) =>
            {
                var directives = new List<TestImportDirective>();
                var text = content.ToString();
                var lines = text.Split('\n');
                var offset = 0;

                foreach (var line in lines)
                {
                    if (line.StartsWith("@import ", StringComparison.Ordinal))
                    {
                        var firstQuote = line.IndexOf('"', StringComparison.Ordinal);
                        var lastQuote = line.LastIndexOf('"');
                        if (firstQuote >= 0 && lastQuote > firstQuote)
                        {
                            var reference = line[(firstQuote + 1)..lastQuote];
                            directives.Add(new TestImportDirective(reference, offset..(offset + line.Length)));
                        }
                    }

                    offset += line.Length + 1; // +1 for newline
                }

                return directives;
            });

        var config = new PreprocessorConfiguration<ReadOnlyMemory<char>, TestImportDirective, object>(
            parser.Object,
            new TestImportDirectiveModel(),
            resolver.Object,
            mergeStrategy,
            new ReadOnlyMemoryCharContentModel());

        var preprocessor = new Preprocessor<ReadOnlyMemory<char>, TestImportDirective, object>(config);

        var includedId = new ResourceId("domain:shaderincludes/shared.glsl");
        var included = new Resource<ReadOnlyMemory<char>>(includedId.Path, "/* shared */".AsMemory());
        var rootId = new ResourceId("domain:shaders/test.fsh");
        var root = new Resource<ReadOnlyMemory<char>>(rootId.Path, "@import \"shared.glsl\"\nvoid main(){}".AsMemory());

        resolver
            .Setup(r => r.ResolveAsync("shared.glsl", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(included, null));

        var result = await preprocessor.ProcessAsync(root, new object());

        Assert.True(result.Success);

        var content = result.Content.ToString();
        Assert.Contains("/* shared */", content);
        Assert.Contains("void main(){}", content);
        Assert.DoesNotContain("@import", content);

        var deps = result.DependencyGraph.GetDependencies(rootId);
        Assert.Contains(includedId, deps);

        Assert.Contains(rootId, result.ProcessedResources);
        Assert.Contains(includedId, result.ProcessedResources);

        var processed = result.ProcessedResources.ToList();
        Assert.True(processed.IndexOf(includedId) < processed.IndexOf(rootId));
    }

    [Fact]
    public async Task ProcessAsync_MultiLevelIncludes_ProcessesAll()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();
        var level2 = new Resource<ReadOnlyMemory<char>>("level2.txt", "Level 2".AsMemory());
        var level1 = new Resource<ReadOnlyMemory<char>>("level1.txt", "#include level2.txt\nLevel 1".AsMemory());
        var main = new Resource<ReadOnlyMemory<char>>("main.txt", "#include level1.txt\nMain".AsMemory());

        resolver.Setup(r => r.ResolveAsync("level1.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(level1, null));
        resolver.Setup(r => r.ResolveAsync("level2.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(level2, null));

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
        const string level2Text = "L2a\nL2b";
        const string level1Text = "#include level2.txt\nL1";
        const string mainText = "#include level1.txt\nM";

        var level2 = new Resource<ReadOnlyMemory<char>>("level2.txt", level2Text.AsMemory());
        var level1 = new Resource<ReadOnlyMemory<char>>("level1.txt", level1Text.AsMemory());
        var main = new Resource<ReadOnlyMemory<char>>("main.txt", mainText.AsMemory());

        resolver.Setup(r => r.ResolveAsync("level1.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(level1, null));
        resolver.Setup(r => r.ResolveAsync("level2.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(level2, null));

        var result = await preprocessor.ProcessAsync(main, new object());

        Assert.True(result.Success);
        var content = result.Content.ToString();
        Assert.Equal("L2a\nL2b\n\nL1\n\nM", content);

        AssertMapped(result.SourceMap, content, generatedLine: 0, generatedColumn: 1, expectedResource: "level2.txt", expectedOriginalOffset: OffsetOfLineColumn(level2Text, line: 0, column: 1));
        AssertMapped(result.SourceMap, content, generatedLine: 1, generatedColumn: 0, expectedResource: "level2.txt", expectedOriginalOffset: OffsetOfLineColumn(level2Text, line: 1, column: 0));
        // Blank line after level2 comes from the original newline after the include directive in level1.
        AssertMapped(result.SourceMap, content, generatedLine: 2, generatedColumn: 0, expectedResource: "level1.txt", expectedOriginalOffset: "#include level2.txt".Length);
        AssertMapped(result.SourceMap, content, generatedLine: 3, generatedColumn: 0, expectedResource: "level1.txt", expectedOriginalOffset: "#include level2.txt\n".Length);
        // Blank line after level1 comes from the original newline after the include directive in main.
        AssertMapped(result.SourceMap, content, generatedLine: 4, generatedColumn: 0, expectedResource: "main.txt", expectedOriginalOffset: "#include level1.txt".Length);
        AssertMapped(result.SourceMap, content, generatedLine: 5, generatedColumn: 0, expectedResource: "main.txt", expectedOriginalOffset: "#include level1.txt\n".Length);
    }

    [Fact]
    public async Task ProcessAsync_BranchingIncludes_FlattensAllAndMapsEachOriginWithoutAssumingOrder()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();

        var c = new Resource<ReadOnlyMemory<char>>("c.txt", "C1".AsMemory());
        var a = new Resource<ReadOnlyMemory<char>>("a.txt", "#include c.txt\nA1".AsMemory());
        var d = new Resource<ReadOnlyMemory<char>>("d.txt", "D1".AsMemory());
        var b = new Resource<ReadOnlyMemory<char>>("b.txt", "#include d.txt\nB1".AsMemory());
        var main = new Resource<ReadOnlyMemory<char>>("main.txt", "#include a.txt\n#include b.txt\nM1".AsMemory());

        resolver.Setup(r => r.ResolveAsync("a.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(a, null));
        resolver.Setup(r => r.ResolveAsync("b.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(b, null));
        resolver.Setup(r => r.ResolveAsync("c.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(c, null));
        resolver.Setup(r => r.ResolveAsync("d.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(d, null));

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
        AssertMappedAtToken(result.SourceMap, content, token: "C1", expectedResource: "c.txt", expectedOriginalOffset: 0);
        AssertMappedAtToken(result.SourceMap, content, token: "A1", expectedResource: "a.txt", expectedOriginalOffset: "#include c.txt\n".Length);
        AssertMappedAtToken(result.SourceMap, content, token: "D1", expectedResource: "d.txt", expectedOriginalOffset: 0);
        AssertMappedAtToken(result.SourceMap, content, token: "B1", expectedResource: "b.txt", expectedOriginalOffset: "#include d.txt\n".Length);
        AssertMappedAtToken(result.SourceMap, content, token: "M1", expectedResource: "main.txt", expectedOriginalOffset: "#include a.txt\n#include b.txt\n".Length);
    }

    [Fact]
    public async Task ProcessAsync_BuildsSourceMap()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();
        var root = new Resource<ReadOnlyMemory<char>>("source.txt", "Line 1\nLine 2".AsMemory());

        var result = await preprocessor.ProcessAsync(root, new object());

        Assert.NotNull(result.SourceMap);
        Assert.NotNull(result.SourceMap.Query(generatedOffset: 0));
    }

    [Fact]
    public async Task ProcessAsync_PopulatesDiagnostics()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();
        var root = new Resource<ReadOnlyMemory<char>>("main.txt", "#include missing.txt\nContent".AsMemory());

        resolver.Setup(r => r.ResolveAsync("missing.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(null, 
                new ResolutionFailedDiagnostic("missing.txt", "File not found")));

        var result = await preprocessor.ProcessAsync(root, new object());

        Assert.False(result.Success);
        Assert.True(result.Diagnostics.HasErrors);
    }

    [Fact]
    public async Task ProcessAsync_TracksProcessedResources()
    {
        var (preprocessor, resolver, _) = CreatePreprocessor();
        var dep = new Resource<ReadOnlyMemory<char>>("dep.txt", "Dependency".AsMemory());
        var main = new Resource<ReadOnlyMemory<char>>("main.txt", "#include dep.txt\nMain".AsMemory());

        resolver.Setup(r => r.ResolveAsync("dep.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(dep, null));

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
        var fileA = new Resource<ReadOnlyMemory<char>>("a.txt", "#include b.txt\nFile A".AsMemory());
        var fileB = new Resource<ReadOnlyMemory<char>>("b.txt", "#include a.txt\nFile B".AsMemory());

        resolver.Setup(r => r.ResolveAsync("b.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(fileB, null));
        resolver.Setup(r => r.ResolveAsync("a.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(fileA, null));

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
        var file = new Resource<ReadOnlyMemory<char>>("self.txt", "#include self.txt\nContent".AsMemory());

        resolver.Setup(r => r.ResolveAsync("self.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(file, null));

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
        var fileA = new Resource<ReadOnlyMemory<char>>("a.txt", "#include b.txt\nContent A".AsMemory());
        var fileB = new Resource<ReadOnlyMemory<char>>("b.txt", "#include a.txt\nContent B".AsMemory());

        resolver.Setup(r => r.ResolveAsync("b.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(fileB, null));
        resolver.Setup(r => r.ResolveAsync("a.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(fileA, null));

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
        var shared = new Resource<ReadOnlyMemory<char>>("shared.txt", "SHARED".AsMemory());
        var libA = new Resource<ReadOnlyMemory<char>>("libA.txt", "#include shared.txt\nLibA".AsMemory());
        var libB = new Resource<ReadOnlyMemory<char>>("libB.txt", "#include shared.txt\nLibB".AsMemory());
        var main = new Resource<ReadOnlyMemory<char>>("main.txt", "#include libA.txt\n#include libB.txt\nMain".AsMemory());

        resolver.Setup(r => r.ResolveAsync("shared.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(shared, null));
        resolver.Setup(r => r.ResolveAsync("libA.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(libA, null));
        resolver.Setup(r => r.ResolveAsync("libB.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(libB, null));

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
        var shared = new Resource<ReadOnlyMemory<char>>("shared.txt", "SHARED".AsMemory());
        var main = new Resource<ReadOnlyMemory<char>>("main.txt", "#include shared.txt\n#include shared.txt\nMain".AsMemory());

        resolver.Setup(r => r.ResolveAsync("shared.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(shared, null));

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
        var level3 = new Resource<ReadOnlyMemory<char>>("level3.txt", "#include level4.txt\nL3".AsMemory());
        var level2 = new Resource<ReadOnlyMemory<char>>("level2.txt", "#include level3.txt\nL2".AsMemory());
        var level1 = new Resource<ReadOnlyMemory<char>>("level1.txt", "#include level2.txt\nL1".AsMemory());
        var level0 = new Resource<ReadOnlyMemory<char>>("level0.txt", "#include level1.txt\nL0".AsMemory());
        var level4 = new Resource<ReadOnlyMemory<char>>("level4.txt", "L4".AsMemory());

        resolver.Setup(r => r.ResolveAsync("level1.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(level1, null));
        resolver.Setup(r => r.ResolveAsync("level2.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(level2, null));
        resolver.Setup(r => r.ResolveAsync("level3.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(level3, null));
        resolver.Setup(r => r.ResolveAsync("level4.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(level4, null));

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

        var level2 = new Resource<ReadOnlyMemory<char>>("level2.txt", "L2".AsMemory());
        var level1 = new Resource<ReadOnlyMemory<char>>("level1.txt", "#include level2.txt\nL1".AsMemory());
        var level0 = new Resource<ReadOnlyMemory<char>>("level0.txt", "#include level1.txt\nL0".AsMemory());

        resolver.Setup(r => r.ResolveAsync("level1.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(level1, null));
        resolver.Setup(r => r.ResolveAsync("level2.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(level2, null));

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
        var child = new Resource<ReadOnlyMemory<char>>("child.txt", "Child".AsMemory());
        var main = new Resource<ReadOnlyMemory<char>>("main.txt", "#include child.txt\nMain".AsMemory());

        resolver.Setup(r => r.ResolveAsync("child.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResolutionResult<ReadOnlyMemory<char>>(child, null));

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
        var root = new Resource<ReadOnlyMemory<char>>("main.txt", "#include slow.txt\nContent".AsMemory());

        resolver.Setup(r => r.ResolveAsync("slow.txt", It.IsAny<IResource<ReadOnlyMemory<char>>?>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, IResource<ReadOnlyMemory<char>>? _, CancellationToken ct) =>
            {
                await Task.Delay(1000, ct);
                return new ResourceResolutionResult<ReadOnlyMemory<char>>(new Resource<ReadOnlyMemory<char>>("slow.txt", "".AsMemory()), null);
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
        Preprocessor<ReadOnlyMemory<char>, TestIncludeDirective, object> Preprocessor,
        Mock<IResourceResolver<ReadOnlyMemory<char>>> Resolver,
        Mock<IDirectiveParser<ReadOnlyMemory<char>, TestIncludeDirective>> Parser)
        CreatePreprocessor()
    {
        var parser = new Mock<IDirectiveParser<ReadOnlyMemory<char>, TestIncludeDirective>>();
        var resolver = new Mock<IResourceResolver<ReadOnlyMemory<char>>>();
        var mergeStrategy = new ConcatenatingMergeStrategy<TestIncludeDirective, object>();

        parser
            .Setup(p => p.Parse(It.IsAny<ReadOnlyMemory<char>>(), It.IsAny<ResourceId>()))
            .Returns((ReadOnlyMemory<char> content, ResourceId _) =>
            {
                var directives = new List<TestIncludeDirective>();
                var text = content.ToString();
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

        var directiveModel = new TestIncludeDirectiveModel();
        var contentModel = new ReadOnlyMemoryCharContentModel();

        var config = new PreprocessorConfiguration<ReadOnlyMemory<char>, TestIncludeDirective, object>(
            parser.Object,
            directiveModel,
            resolver.Object,
            mergeStrategy,
            contentModel);

        var preprocessor = new Preprocessor<ReadOnlyMemory<char>, TestIncludeDirective, object>(config);

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
        string generatedContent,
        int generatedLine,
        int generatedColumn,
        string expectedResource,
        int expectedOriginalOffset)
    {
        var generatedOffset = OffsetOfLineColumn(generatedContent, generatedLine, generatedColumn);
        var mapped = sourceMap.Query(generatedOffset);

        Assert.NotNull(mapped);
        Assert.Equal(new ResourceId(expectedResource), mapped.Resource);
        Assert.Equal(expectedOriginalOffset, mapped.OriginalOffset);
    }

    private static void AssertMappedAtToken(
        SourceMap sourceMap,
        string generatedContent,
        string token,
        string expectedResource,
        int expectedOriginalOffset)
    {
        var generatedOffset = FindOffset(generatedContent, token);
        var mapped = sourceMap.Query(generatedOffset);

        Assert.NotNull(mapped);
        Assert.Equal(new ResourceId(expectedResource), mapped.Resource);
        Assert.Equal(expectedOriginalOffset, mapped.OriginalOffset);
    }

    private static int FindOffset(string text, string token)
    {
        var index = text.IndexOf(token, StringComparison.Ordinal);
        Assert.True(index >= 0, $"Token '{token}' not found in generated output.");

        return index;
    }

    private static int OffsetOfLineColumn(string text, int line, int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(line);
        ArgumentOutOfRangeException.ThrowIfNegative(column);

        var offset = 0;
        var currentLine = 0;
        var currentColumn = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (currentLine == line && currentColumn == column)
            {
                return offset;
            }

            if (text[i] == '\n')
            {
                currentLine++;
                currentColumn = 0;
            }
            else
            {
                currentColumn++;
            }

            offset++;
        }

        if (currentLine == line && currentColumn == column)
        {
            return offset;
        }

        throw new ArgumentOutOfRangeException(nameof(line), "Line/column is outside the provided text.");
    }

    #endregion
}

/// <summary>
/// Test include-like directive for integration tests.
/// </summary>
public sealed record TestIncludeDirective(string Reference, System.Range Location);

file sealed class TestIncludeDirectiveModel : IDirectiveModel<TestIncludeDirective>
{
    public System.Range GetLocation(TestIncludeDirective directive) => directive.Location;

    public bool TryGetReference(TestIncludeDirective directive, out string reference)
    {
        reference = directive.Reference;
        return true;
    }
}

public sealed record TestImportDirective(string Reference, System.Range Location);

file sealed class TestImportDirectiveModel : IDirectiveModel<TestImportDirective>
{
    public System.Range GetLocation(TestImportDirective directive) => directive.Location;

    public bool TryGetReference(TestImportDirective directive, out string reference)
    {
        reference = directive.Reference;
        return true;
    }
}

file sealed class ResolvedReferenceInliningMergeStrategy : IMergeStrategy<ReadOnlyMemory<char>, TestImportDirective, object>
{
    public ReadOnlyMemory<char> Merge(
        IReadOnlyList<ResolvedResource<ReadOnlyMemory<char>, TestImportDirective>> orderedResources,
        object userContext,
        MergeContext<ReadOnlyMemory<char>, TestImportDirective> context)
    {
        ArgumentNullException.ThrowIfNull(orderedResources);
        ArgumentNullException.ThrowIfNull(context);

        if (orderedResources.Count == 0)
        {
            return ReadOnlyMemory<char>.Empty;
        }

        var root = orderedResources[^1];
        var merged = root.Content.ToString();

        for (var i = root.Directives.Count - 1; i >= 0; i--)
        {
            var key = new MergeContext<ReadOnlyMemory<char>, TestImportDirective>.ResolvedReferenceKey(root.Id, i);
            if (!context.ResolvedReferences.TryGetValue(key, out var resolvedId))
            {
                throw new InvalidOperationException($"Resolved id missing for directive key {key}.");
            }

            if (!context.ResolvedCache.TryGetValue(resolvedId, out var resolvedResource))
            {
                throw new InvalidOperationException($"Resolved resource missing for id '{resolvedId.Path}'.");
            }

            var directive = root.Directives[i];
            var location = context.DirectiveModel.GetLocation(directive);

            var start = location.Start.GetOffset(merged.Length);
            var end = location.End.GetOffset(merged.Length);

            start = Math.Clamp(start, 0, merged.Length);
            end = Math.Clamp(end, 0, merged.Length);
            if (end < start)
            {
                (start, end) = (end, start);
            }

            var includeText = resolvedResource.Content.ToString();

            merged = string.Concat(
                merged.AsSpan(0, start),
                includeText,
                merged.AsSpan(end));
        }

        return merged.AsMemory();
    }
}
