using TinyPreprocessor.Core;
using TinyPreprocessor.SourceMaps;
using Xunit;

namespace TinyPreprocessor.Tests.SourceMaps;

/// <summary>
/// Unit tests for <see cref="SourceMapBuilder"/>.
/// </summary>
public sealed class SourceMapBuilderTests
{
    #region AddMapping Tests

    [Fact]
    public void AddMapping_SingleMapping_AccumulatesCorrectly()
    {
        var builder = new SourceMapBuilder();
        var mapping = new SourceMapping(
            new SourceSpan(new SourcePosition(0, 0), new SourcePosition(0, 10)),
            "test.txt",
            new SourceSpan(new SourcePosition(0, 0), new SourcePosition(0, 10)));

        builder.AddMapping(mapping);
        var sourceMap = builder.Build();

        Assert.Single(sourceMap.Mappings);
        Assert.Equal(mapping, sourceMap.Mappings[0]);
    }

    [Fact]
    public void AddMapping_MultipleMappings_AccumulatesAll()
    {
        var builder = new SourceMapBuilder();
        var mapping1 = new SourceMapping(
            new SourceSpan(new SourcePosition(0, 0), new SourcePosition(0, 10)),
            "file1.txt",
            new SourceSpan(new SourcePosition(0, 0), new SourcePosition(0, 10)));
        var mapping2 = new SourceMapping(
            new SourceSpan(new SourcePosition(1, 0), new SourcePosition(1, 20)),
            "file2.txt",
            new SourceSpan(new SourcePosition(5, 0), new SourcePosition(5, 20)));

        builder.AddMapping(mapping1);
        builder.AddMapping(mapping2);
        var sourceMap = builder.Build();

        Assert.Equal(2, sourceMap.Mappings.Count);
    }

    [Fact]
    public void AddMapping_NullMapping_ThrowsArgumentNullException()
    {
        var builder = new SourceMapBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddMapping(null!));
    }

    #endregion

    #region AddSegment Tests

    [Fact]
    public void AddSegment_CreatesMapping()
    {
        var builder = new SourceMapBuilder();
        ResourceId resource = "source.txt";
        var generatedSpan = new SourceSpan(new SourcePosition(0, 0), new SourcePosition(0, 15));
        var originalSpan = new SourceSpan(new SourcePosition(10, 0), new SourcePosition(10, 15));

        builder.AddSegment(resource, generatedSpan, originalSpan);
        var sourceMap = builder.Build();

        Assert.Single(sourceMap.Mappings);
        var mapping = sourceMap.Mappings[0];
        Assert.Equal(resource, mapping.OriginalResource);
        Assert.Equal(generatedSpan, mapping.GeneratedSpan);
        Assert.Equal(originalSpan, mapping.OriginalSpan);
    }

    #endregion

    #region AddLine Tests

    [Fact]
    public void AddLine_CreatesSingleLineMapping()
    {
        var builder = new SourceMapBuilder();
        ResourceId resource = "code.txt";

        builder.AddLine(resource, generatedLine: 5, originalLine: 10, length: 50);
        var sourceMap = builder.Build();

        Assert.Single(sourceMap.Mappings);
        var mapping = sourceMap.Mappings[0];
        Assert.Equal(resource, mapping.OriginalResource);
        Assert.Equal(5, mapping.GeneratedSpan.Start.Line);
        Assert.Equal(10, mapping.OriginalSpan.Start.Line);
    }

    [Fact]
    public void AddLine_MultipleLines_AccumulatesAll()
    {
        var builder = new SourceMapBuilder();
        ResourceId resource = "multi.txt";

        for (var i = 0; i < 10; i++)
        {
            builder.AddLine(resource, generatedLine: i, originalLine: i * 2);
        }

        var sourceMap = builder.Build();

        Assert.Equal(10, sourceMap.Mappings.Count);
    }

    #endregion

    #region Build Tests

    [Fact]
    public void Build_SortsMappingsByGeneratedPosition()
    {
        var builder = new SourceMapBuilder();

        // Add mappings out of order
        builder.AddLine("file.txt", generatedLine: 5, originalLine: 5);
        builder.AddLine("file.txt", generatedLine: 1, originalLine: 1);
        builder.AddLine("file.txt", generatedLine: 10, originalLine: 10);
        builder.AddLine("file.txt", generatedLine: 3, originalLine: 3);

        var sourceMap = builder.Build();

        // Verify sorted order
        Assert.Equal(1, sourceMap.Mappings[0].GeneratedSpan.Start.Line);
        Assert.Equal(3, sourceMap.Mappings[1].GeneratedSpan.Start.Line);
        Assert.Equal(5, sourceMap.Mappings[2].GeneratedSpan.Start.Line);
        Assert.Equal(10, sourceMap.Mappings[3].GeneratedSpan.Start.Line);
    }

    [Fact]
    public void Build_EmptyBuilder_ReturnsEmptySourceMap()
    {
        var builder = new SourceMapBuilder();

        var sourceMap = builder.Build();

        Assert.Empty(sourceMap.Mappings);
    }

    [Fact]
    public void Build_ReturnsImmutableSourceMap()
    {
        var builder = new SourceMapBuilder();
        builder.AddLine("test.txt", 0, 0);

        var sourceMap = builder.Build();

        // Adding more mappings after build shouldn't affect the built source map
        builder.AddLine("test.txt", 1, 1);

        Assert.Single(sourceMap.Mappings);
    }

    #endregion

    #region Clear Tests

    [Fact]
    public void Clear_RemovesAllMappings()
    {
        var builder = new SourceMapBuilder();
        builder.AddLine("file1.txt", 0, 0);
        builder.AddLine("file2.txt", 1, 1);

        builder.Clear();
        var sourceMap = builder.Build();

        Assert.Empty(sourceMap.Mappings);
    }

    #endregion

    #region Accumulation Tests

    [Fact]
    public void Builder_AccumulatesFromMultipleSources()
    {
        var builder = new SourceMapBuilder();

        // Simulate merging multiple files
        builder.AddLine("header.h", generatedLine: 0, originalLine: 0);
        builder.AddLine("header.h", generatedLine: 1, originalLine: 1);
        builder.AddLine("utils.c", generatedLine: 2, originalLine: 0);
        builder.AddLine("utils.c", generatedLine: 3, originalLine: 1);
        builder.AddLine("main.c", generatedLine: 4, originalLine: 0);

        var sourceMap = builder.Build();

        Assert.Equal(5, sourceMap.Mappings.Count);

        // Verify different resources are tracked
        var resources = sourceMap.Mappings.Select(m => m.OriginalResource).Distinct().ToList();
        Assert.Equal(3, resources.Count);
    }

    #endregion
}
