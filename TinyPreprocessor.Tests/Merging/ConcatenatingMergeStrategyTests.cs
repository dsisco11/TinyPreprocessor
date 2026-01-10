using TinyPreprocessor.Core;
using TinyPreprocessor.Diagnostics;
using TinyPreprocessor.Merging;
using TinyPreprocessor.SourceMaps;
using TinyPreprocessor.Text;
using Xunit;

namespace TinyPreprocessor.Tests.Merging;

/// <summary>
/// Unit tests for <see cref="ConcatenatingMergeStrategy{TDirective,TContext}"/>.
/// </summary>
public sealed class ConcatenatingMergeStrategyTests
{
    #region Output Verification Tests

    [Fact]
    public void Merge_SingleResource_ReturnsContentUnmodified()
    {
        var strategy = new ConcatenatingMergeStrategy<TestDirective, object>();
        var content = "Hello, World!";
        var resources = CreateResolvedResources(("test.txt", content));
        var context = CreateMergeContext();

        var result = strategy.Merge(resources, new object(), context);

        Assert.Equal(content, result.ToString());
    }

    [Fact]
    public void Merge_MultipleResources_ConcatenatesInOrder()
    {
        var strategy = new ConcatenatingMergeStrategy<TestDirective, object>();
        var resources = CreateResolvedResources(
            ("first.txt", "First\n"),
            ("second.txt", "Second\n"),
            ("third.txt", "Third"));
        var context = CreateMergeContext();

        var result = strategy.Merge(resources, new object(), context);

        // Default separator is newline
        Assert.Equal("First\n\nSecond\n\nThird", result.ToString());
    }

    [Fact]
    public void Merge_EmptyResources_ReturnsEmpty()
    {
        var strategy = new ConcatenatingMergeStrategy<TestDirective, object>();
        var resources = new List<ResolvedResource<ReadOnlyMemory<char>, TestDirective>>();
        var context = CreateMergeContext();

        var result = strategy.Merge(resources, new object(), context);

        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Merge_CustomSeparator_UsesSeparator()
    {
        var options = new ConcatenatingMergeOptions(Separator: "\n\n---\n\n");
        var strategy = new ConcatenatingMergeStrategy<TestDirective, object>(options);
        var resources = CreateResolvedResources(
            ("a.txt", "Part A"),
            ("b.txt", "Part B"));
        var context = CreateMergeContext();

        var result = strategy.Merge(resources, new object(), context);

        Assert.Contains("---", result.ToString());
        Assert.Equal("Part A\n\n---\n\nPart B", result.ToString());
    }

    [Fact]
    public void Merge_WithResourceMarkers_IncludesMarkers()
    {
        var options = new ConcatenatingMergeOptions(
            IncludeResourceMarkers: true,
            MarkerFormat: "// File: {0}\n");
        var strategy = new ConcatenatingMergeStrategy<TestDirective, object>(options);
        var resources = CreateResolvedResources(("code.cs", "var x = 1;"));
        var context = CreateMergeContext();

        var result = strategy.Merge(resources, new object(), context);

        Assert.StartsWith("// File: code.cs", result.ToString());
    }

    #endregion

    #region Directive Stripping Tests

    [Fact]
    public void Merge_WithDirectives_StripsDirectiveRanges()
    {
        var strategy = new ConcatenatingMergeStrategy<TestDirective, object>();
        var content = "Line 1\n#include <header.h>\nLine 3";
        var directive = new TestDirective(7..26); // The #include directive
        var resource = new ResolvedResource<ReadOnlyMemory<char>, TestDirective>(
            new Resource<ReadOnlyMemory<char>>("test.txt", content.AsMemory()),
            new[] { directive });
        var context = CreateMergeContext();

        var result = strategy.Merge([resource], new object(), context);

        Assert.DoesNotContain("#include", result.ToString());
        Assert.Contains("Line 1", result.ToString());
        Assert.Contains("Line 3", result.ToString());
    }

    [Fact]
    public void Merge_MultipleDirectives_StripsAll()
    {
        var strategy = new ConcatenatingMergeStrategy<TestDirective, object>();
        // Content with directives at known positions
        var content = "#include <a>\ncode\n#include <b>";
        var directive1 = new TestDirective(0..12);
        var directive2 = new TestDirective(18..30);
        var resource = new ResolvedResource<ReadOnlyMemory<char>, TestDirective>(
            new Resource<ReadOnlyMemory<char>>("test.txt", content.AsMemory()),
            new[] { directive1, directive2 });
        var context = CreateMergeContext();

        var result = strategy.Merge([resource], new object(), context);

        Assert.DoesNotContain("#include", result.ToString());
        Assert.Contains("code", result.ToString());
    }

    [Fact]
    public void Merge_NoDirectives_ContentUnchanged()
    {
        var strategy = new ConcatenatingMergeStrategy<TestDirective, object>();
        var content = "Pure content without directives";
        var resource = new ResolvedResource<ReadOnlyMemory<char>, TestDirective>(
            new Resource<ReadOnlyMemory<char>>("clean.txt", content.AsMemory()),
            Array.Empty<TestDirective>());
        var context = CreateMergeContext();

        var result = strategy.Merge([resource], new object(), context);

        Assert.Equal(content, result.ToString());
    }

    [Fact]
    public void Merge_DirectiveAtStart_StripsCorrectly()
    {
        var strategy = new ConcatenatingMergeStrategy<TestDirective, object>();
        var content = "#pragma once\nActual content";
        var directive = new TestDirective(0..13);
        var resource = new ResolvedResource<ReadOnlyMemory<char>, TestDirective>(
            new Resource<ReadOnlyMemory<char>>("header.h", content.AsMemory()),
            new[] { directive });
        var context = CreateMergeContext();

        var result = strategy.Merge([resource], new object(), context);

        Assert.DoesNotContain("#pragma", result.ToString());
        Assert.Contains("Actual content", result.ToString());
    }

    [Fact]
    public void Merge_DirectiveAtEnd_StripsCorrectly()
    {
        var strategy = new ConcatenatingMergeStrategy<TestDirective, object>();
        var content = "Content\n#endif";
        var directive = new TestDirective(8..14);
        var resource = new ResolvedResource<ReadOnlyMemory<char>, TestDirective>(
            new Resource<ReadOnlyMemory<char>>("guarded.h", content.AsMemory()),
            new[] { directive });
        var context = CreateMergeContext();

        var result = strategy.Merge([resource], new object(), context);

        Assert.DoesNotContain("#endif", result.ToString());
        Assert.Contains("Content", result.ToString());
    }

    #endregion

    #region Source Mapping Accuracy Tests

    [Fact]
    public void Merge_RecordsMappingsToSourceMap()
    {
        var strategy = new ConcatenatingMergeStrategy<TestDirective, object>();
        var resources = CreateResolvedResources(("test.txt", "Line 1\nLine 2"));
        var context = CreateMergeContext(resources);

        var merged = strategy.Merge(resources, new object(), context);
        var sourceMap = context.SourceMapBuilder.Build();

        Assert.NotNull(sourceMap.Query(generatedOffset: 0));
    }

    [Fact]
    public void Merge_MappingsPointToCorrectResource()
    {
        var strategy = new ConcatenatingMergeStrategy<TestDirective, object>();
        ResourceId resourceId = "source.txt";
        var resource = new ResolvedResource<ReadOnlyMemory<char>, TestDirective>(
            new Resource<ReadOnlyMemory<char>>(resourceId, "content".AsMemory()),
            Array.Empty<TestDirective>());
        var context = CreateMergeContext([resource]);

        var merged = strategy.Merge([resource], new object(), context);
        var sourceMap = context.SourceMapBuilder.Build();

        var mapped = sourceMap.Query(generatedOffset: 0);
        Assert.NotNull(mapped);
        Assert.Equal(resourceId, mapped.Resource);
    }

    [Fact]
    public void Merge_MultipleFiles_MappingsTrackOrigin()
    {
        var strategy = new ConcatenatingMergeStrategy<TestDirective, object>();
        var resources = new List<ResolvedResource<ReadOnlyMemory<char>, TestDirective>>
        {
            new(new Resource<ReadOnlyMemory<char>>("file1.txt", "Content 1".AsMemory()), Array.Empty<TestDirective>()),
            new(new Resource<ReadOnlyMemory<char>>("file2.txt", "Content 2".AsMemory()), Array.Empty<TestDirective>())
        };
        var context = CreateMergeContext(resources);

        var merged = strategy.Merge(resources, new object(), context);
        var sourceMap = context.SourceMapBuilder.Build();

        var mapped1 = sourceMap.Query(generatedOffset: 0);
        var mapped2 = sourceMap.Query(generatedOffset: "Content 1".Length + "\n".Length);

        Assert.NotNull(mapped1);
        Assert.NotNull(mapped2);
        Assert.Equal(new ResourceId("file1.txt"), mapped1.Resource);
        Assert.Equal(new ResourceId("file2.txt"), mapped2.Resource);
    }

    [Fact]
    public void Merge_AfterDirectiveStrip_MappingsAreAccurate()
    {
        var strategy = new ConcatenatingMergeStrategy<TestDirective, object>();
        // Line 0: #include
        // Line 1: keep this
        var content = "#include\nkeep this";
        var directive = new TestDirective(0..9);
        var resource = new ResolvedResource<ReadOnlyMemory<char>, TestDirective>(
            new Resource<ReadOnlyMemory<char>>("test.txt", content.AsMemory()),
            new[] { directive });
        var context = CreateMergeContext([resource]);

        var merged = strategy.Merge([resource], new object(), context);
        var sourceMap = context.SourceMapBuilder.Build();

        var mapped = sourceMap.Query(generatedOffset: 0);
        Assert.NotNull(mapped);
        Assert.Equal(new ResourceId("test.txt"), mapped.Resource);
        Assert.Equal("#include\n".Length, mapped.OriginalOffset);
    }

    #endregion

    #region Null Handling Tests

    [Fact]
    public void Merge_NullResources_ThrowsArgumentNullException()
    {
        var strategy = new ConcatenatingMergeStrategy<TestDirective, object>();
        var context = CreateMergeContext();

        Assert.Throws<ArgumentNullException>(() =>
            strategy.Merge(null!, new object(), context));
    }

    [Fact]
    public void Merge_NullContext_ThrowsArgumentNullException()
    {
        var strategy = new ConcatenatingMergeStrategy<TestDirective, object>();
        var resources = CreateResolvedResources(("test.txt", "content"));

        Assert.Throws<ArgumentNullException>(() =>
            strategy.Merge(resources, new object(), null!));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ConcatenatingMergeStrategy<TestDirective, object>(null!));
    }

    #endregion

    #region Test Helpers

    private static List<ResolvedResource<ReadOnlyMemory<char>, TestDirective>> CreateResolvedResources(
        params (string Path, string Content)[] items)
    {
        return items
            .Select(item => new ResolvedResource<ReadOnlyMemory<char>, TestDirective>(
                new Resource<ReadOnlyMemory<char>>(item.Path, item.Content.AsMemory()),
                Array.Empty<TestDirective>()))
            .ToList();
    }

    private static MergeContext<ReadOnlyMemory<char>, TestDirective> CreateMergeContext(IReadOnlyList<ResolvedResource<ReadOnlyMemory<char>, TestDirective>>? resources = null)
    {
        var resolvedCache = resources is null
            ? new Dictionary<ResourceId, IResource<ReadOnlyMemory<char>>>()
            : resources.ToDictionary(r => r.Id, r => r.Resource);

        return new MergeContext<ReadOnlyMemory<char>, TestDirective>(
            new SourceMapBuilder(),
            new DiagnosticCollection(),
            resolvedCache,
            new TestDirectiveModel(),
            new ReadOnlyMemoryCharContentModel());
    }

    private sealed record TestDirective(Range Location);

    private sealed class TestDirectiveModel : IDirectiveModel<TestDirective>
    {
        public Range GetLocation(TestDirective directive) => directive.Location;

        public bool TryGetReference(TestDirective directive, out string reference)
        {
            reference = string.Empty;
            return false;
        }
    }

    #endregion
}
