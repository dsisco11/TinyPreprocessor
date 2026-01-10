using System.Collections.Generic;
using TinyPreprocessor.Core;
using TinyPreprocessor.SourceMaps;
using Xunit;

namespace TinyPreprocessor.Tests.SourceMaps;

/// <summary>
/// Unit tests for <see cref="SourceMap"/> query functionality.
/// </summary>
public sealed class SourceMapQueryTests
{
    #region Query Binary Search Tests

    [Fact]
    public void Query_PositionWithinMapping_ReturnsOriginalLocation()
    {
        var builder = new SourceMapBuilder();
        ResourceId resource = "original.txt";
        // Generated: line 0, columns 0-10 -> Original: line 5, columns 0-10
        builder.AddSegment(
            resource,
            new SourceSpan(new SourcePosition(0, 0), new SourcePosition(0, 10)),
            new SourceSpan(new SourcePosition(5, 0), new SourcePosition(5, 10)));

        var sourceMap = builder.Build();
        var result = sourceMap.Query(new SourcePosition(0, 5));

        Assert.NotNull(result);
        Assert.Equal(resource, result.Resource);
        Assert.Equal(5, result.OriginalPosition.Line);
        Assert.Equal(5, result.OriginalPosition.Column);
    }

    [Fact]
    public void Query_PositionAtMappingStart_ReturnsOriginalLocation()
    {
        var builder = new SourceMapBuilder();
        builder.AddSegment(
            "file.txt",
            new SourceSpan(new SourcePosition(10, 0), new SourcePosition(10, 50)),
            new SourceSpan(new SourcePosition(0, 0), new SourcePosition(0, 50)));

        var sourceMap = builder.Build();
        var result = sourceMap.Query(new SourcePosition(10, 0));

        Assert.NotNull(result);
        Assert.Equal(0, result.OriginalPosition.Line);
        Assert.Equal(0, result.OriginalPosition.Column);
    }

    [Fact]
    public void Query_PositionOutsideAllMappings_ReturnsNull()
    {
        var builder = new SourceMapBuilder();
        builder.AddSegment(
            "file.txt",
            new SourceSpan(new SourcePosition(5, 0), new SourcePosition(5, 20)),
            new SourceSpan(new SourcePosition(0, 0), new SourcePosition(0, 20)));

        var sourceMap = builder.Build();

        // Query position before any mapping
        var result1 = sourceMap.Query(new SourcePosition(0, 0));
        Assert.Null(result1);

        // Query position after all mappings
        var result2 = sourceMap.Query(new SourcePosition(100, 0));
        Assert.Null(result2);
    }

    [Fact]
    public void Query_EmptySourceMap_ReturnsNull()
    {
        var builder = new SourceMapBuilder();
        var sourceMap = builder.Build();

        var result = sourceMap.Query(new SourcePosition(0, 0));

        Assert.Null(result);
    }

    [Fact]
    public void Query_MultipleMappings_FindsCorrectOne()
    {
        var builder = new SourceMapBuilder();
        builder.AddLine("file1.txt", generatedLine: 0, originalLine: 10);
        builder.AddLine("file2.txt", generatedLine: 1, originalLine: 20);
        builder.AddLine("file3.txt", generatedLine: 2, originalLine: 30);

        var sourceMap = builder.Build();

        var result = sourceMap.Query(new SourcePosition(1, 5));

        Assert.NotNull(result);
        Assert.Equal(new ResourceId("file2.txt"), result.Resource);
        Assert.Equal(20, result.OriginalPosition.Line);
    }

    [Fact]
    public void Query_BinarySearchWithManyMappings_FindsCorrectMapping()
    {
        var builder = new SourceMapBuilder();

        // Add 100 mappings to test binary search
        for (var i = 0; i < 100; i++)
        {
            builder.AddLine($"file{i}.txt", generatedLine: i, originalLine: i * 10);
        }

        var sourceMap = builder.Build();

        // Query various positions
        var result50 = sourceMap.Query(new SourcePosition(50, 5));
        Assert.NotNull(result50);
        Assert.Equal(new ResourceId("file50.txt"), result50.Resource);
        Assert.Equal(500, result50.OriginalPosition.Line);

        var result0 = sourceMap.Query(new SourcePosition(0, 0));
        Assert.NotNull(result0);
        Assert.Equal(new ResourceId("file0.txt"), result0.Resource);

        var result99 = sourceMap.Query(new SourcePosition(99, 0));
        Assert.NotNull(result99);
        Assert.Equal(new ResourceId("file99.txt"), result99.Resource);
    }

    [Fact]
    public void Query_PositionBetweenMappings_ReturnsNull()
    {
        var builder = new SourceMapBuilder();
        // Mapping at line 0
        builder.AddSegment(
            "file.txt",
            new SourceSpan(new SourcePosition(0, 0), new SourcePosition(0, 10)),
            new SourceSpan(new SourcePosition(0, 0), new SourcePosition(0, 10)));
        // Mapping at line 5 (gap between line 1-4)
        builder.AddSegment(
            "file.txt",
            new SourceSpan(new SourcePosition(5, 0), new SourcePosition(5, 10)),
            new SourceSpan(new SourcePosition(10, 0), new SourcePosition(10, 10)));

        var sourceMap = builder.Build();

        // Query position in the gap
        var result = sourceMap.Query(new SourcePosition(2, 5));

        Assert.Null(result);
    }

    #endregion

    #region Exact Range Query Tests

    [Fact]
    public void Query_RangeOverTwoSegments_ReturnsTwoMappings()
    {
        var builder = new SourceMapBuilder();

        var generated = "AAAAABBBBBCCCCCDDDDDEEEEE";
        builder.SetGeneratedContent(generated.AsMemory());

        var resource1 = new Resource("file1.txt", "xxxxx".AsMemory());
        var resource2 = new Resource("file2.txt", "yyyyy".AsMemory());
        builder.SetOriginalResources(new Dictionary<ResourceId, IResource>
        {
            [resource1.Id] = resource1,
            [resource2.Id] = resource2
        });

        // Segment 1: generated [0..5) -> file1 [0..5)
        builder.AddSegment(
            resource1.Id,
            new SourceSpan(new SourcePosition(0, 0), new SourcePosition(0, 5)),
            new SourceSpan(new SourcePosition(0, 0), new SourcePosition(0, 5)));

        // Segment 2: generated [10..15) -> file2 [0..5)
        builder.AddSegment(
            resource2.Id,
            new SourceSpan(new SourcePosition(0, 10), new SourcePosition(0, 15)),
            new SourceSpan(new SourcePosition(0, 0), new SourcePosition(0, 5)));

        var sourceMap = builder.Build();

        var results = sourceMap.Query(new SourcePosition(0, 0), length: 15);

        Assert.Equal(2, results.Count);

        Assert.Equal(resource1.Id, results[0].Resource);
        Assert.Equal(new SourcePosition(0, 0), results[0].GeneratedStart);
        Assert.Equal(new SourcePosition(0, 5), results[0].GeneratedEnd);
        Assert.Equal(new SourcePosition(0, 0), results[0].OriginalStart);
        Assert.Equal(new SourcePosition(0, 5), results[0].OriginalEnd);

        Assert.Equal(resource2.Id, results[1].Resource);
        Assert.Equal(new SourcePosition(0, 10), results[1].GeneratedStart);
        Assert.Equal(new SourcePosition(0, 15), results[1].GeneratedEnd);
        Assert.Equal(new SourcePosition(0, 0), results[1].OriginalStart);
        Assert.Equal(new SourcePosition(0, 5), results[1].OriginalEnd);
    }

    [Fact]
    public void Query_RangeByStartEnd_ReturnsSameResultsAsLengthOverload()
    {
        var builder = new SourceMapBuilder();

        var generated = "0123456789ABCDEFGHIJ";
        builder.SetGeneratedContent(generated.AsMemory());

        var resource = new Resource("file.txt", "xxxxxxxxxxxxxxxxxxxx".AsMemory());
        builder.SetOriginalResources(new Dictionary<ResourceId, IResource>
        {
            [resource.Id] = resource
        });

        builder.AddSegment(
            resource.Id,
            new SourceSpan(new SourcePosition(0, 3), new SourcePosition(0, 8)),
            new SourceSpan(new SourcePosition(0, 7), new SourcePosition(0, 12)));

        var sourceMap = builder.Build();

        var byLength = sourceMap.Query(new SourcePosition(0, 0), length: 10);
        var byStartEnd = sourceMap.Query(new SourcePosition(0, 0), new SourcePosition(0, 10));

        Assert.Equal(byLength, byStartEnd);
    }

    #endregion

    #region GetMappingsForResource Tests

    [Fact]
    public void GetMappingsForResource_FiltersByResource()
    {
        var builder = new SourceMapBuilder();
        builder.AddLine("file1.txt", 0, 0);
        builder.AddLine("file2.txt", 1, 0);
        builder.AddLine("file1.txt", 2, 1);
        builder.AddLine("file2.txt", 3, 1);

        var sourceMap = builder.Build();

        var file1Mappings = sourceMap.GetMappingsForResource("file1.txt").ToList();

        Assert.Equal(2, file1Mappings.Count);
        Assert.All(file1Mappings, m => Assert.Equal(new ResourceId("file1.txt"), m.OriginalResource));
    }

    [Fact]
    public void GetMappingsForResource_NoMatchingResource_ReturnsEmpty()
    {
        var builder = new SourceMapBuilder();
        builder.AddLine("file1.txt", 0, 0);

        var sourceMap = builder.Build();

        var result = sourceMap.GetMappingsForResource("nonexistent.txt");

        Assert.Empty(result);
    }

    #endregion

    #region SourceMapping.MapPosition Tests

    [Fact]
    public void MapPosition_WithinSpan_MapsCorrectly()
    {
        var mapping = new SourceMapping(
            new SourceSpan(new SourcePosition(10, 5), new SourcePosition(10, 25)),
            "source.txt",
            new SourceSpan(new SourcePosition(0, 0), new SourcePosition(0, 20)));

        var result = mapping.MapPosition(new SourcePosition(10, 15));

        Assert.NotNull(result);
        // Column offset: 15 - 5 = 10, so original column = 0 + 10 = 10
        Assert.Equal(0, result.Value.Line);
        Assert.Equal(10, result.Value.Column);
    }

    [Fact]
    public void MapPosition_OutsideSpan_ReturnsNull()
    {
        var mapping = new SourceMapping(
            new SourceSpan(new SourcePosition(5, 0), new SourcePosition(5, 10)),
            "source.txt",
            new SourceSpan(new SourcePosition(0, 0), new SourcePosition(0, 10)));

        var result = mapping.MapPosition(new SourcePosition(10, 0));

        Assert.Null(result);
    }

    [Fact]
    public void MapPosition_MultiLineMapping_MapsCorrectly()
    {
        var mapping = new SourceMapping(
            new SourceSpan(new SourcePosition(0, 0), new SourcePosition(5, 0)),
            "source.txt",
            new SourceSpan(new SourcePosition(10, 0), new SourcePosition(15, 0)));

        // Query position on generated line 2
        var result = mapping.MapPosition(new SourcePosition(2, 5));

        Assert.NotNull(result);
        // Line delta = 2, so original line = 10 + 2 = 12
        Assert.Equal(12, result.Value.Line);
    }

    #endregion
}
