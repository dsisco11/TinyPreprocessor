using System.Collections.Generic;
using System.Linq;
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

        // Generated: offsets [0..10) -> Original: offsets [0..10)
        builder.AddOffsetSegment(resource, generatedStartOffset: 0, originalStartOffset: 0, length: 10);

        var sourceMap = builder.Build();
        var result = sourceMap.Query(generatedOffset: 5);

        Assert.NotNull(result);
        Assert.Equal(resource, result.Resource);
        Assert.Equal(5, result.OriginalOffset);
    }

    [Fact]
    public void Query_PositionAtMappingStart_ReturnsOriginalLocation()
    {
        var builder = new SourceMapBuilder();

        // 11 lines joined with "\n" (10 separators). Line 10 begins at offset 10.
        var generatedStartOffset = 10;
        builder.AddOffsetSegment("file.txt", generatedStartOffset, originalStartOffset: 0, length: 50);

        var sourceMap = builder.Build();
        var result = sourceMap.Query(generatedStartOffset);

        Assert.NotNull(result);
        Assert.Equal(0, result.OriginalOffset);
    }

    [Fact]
    public void Query_PositionOutsideAllMappings_ReturnsNull()
    {
        var builder = new SourceMapBuilder();

        // 6 lines joined with "\n" (5 separators). Line 5 begins at offset 5.
        builder.AddOffsetSegment("file.txt", generatedStartOffset: 5, originalStartOffset: 0, length: 20);

        var sourceMap = builder.Build();

        // Query position before any mapping
        var result1 = sourceMap.Query(generatedOffset: 0);
        Assert.Null(result1);

        // Query position after all mappings
        var result2 = sourceMap.Query(generatedOffset: 100);
        Assert.Null(result2);
    }

    [Fact]
    public void Query_EmptySourceMap_ReturnsNull()
    {
        var builder = new SourceMapBuilder();
        var sourceMap = builder.Build();

        var result = sourceMap.Query(generatedOffset: 0);

        Assert.Null(result);
    }

    [Fact]
    public void Query_MultipleMappings_FindsCorrectOne()
    {
        var builder = new SourceMapBuilder();

        // Each line is 10 chars + 1 newline, so line starts are 0, 11, 22.
        builder.AddOffsetSegment("file1.txt", generatedStartOffset: 0, originalStartOffset: 0, length: 10);
        builder.AddOffsetSegment("file2.txt", generatedStartOffset: 11, originalStartOffset: 0, length: 10);
        builder.AddOffsetSegment("file3.txt", generatedStartOffset: 22, originalStartOffset: 0, length: 10);

        var sourceMap = builder.Build();

        var result = sourceMap.Query(generatedOffset: 11 + 5);

        Assert.NotNull(result);
        Assert.Equal(new ResourceId("file2.txt"), result.Resource);
        Assert.Equal(5, result.OriginalOffset);
    }

    [Fact]
    public void Query_BinarySearchWithManyMappings_FindsCorrectMapping()
    {
        var builder = new SourceMapBuilder();

        // Add 100 offset segments.
        for (var i = 0; i < 100; i++)
        {
            var offset = i * 11;
            builder.AddOffsetSegment($"file{i}.txt", offset, originalStartOffset: 0, length: 10);
        }

        var sourceMap = builder.Build();

        // Query various positions
        var result50 = sourceMap.Query(generatedOffset: 50 * 11 + 5);
        Assert.NotNull(result50);
        Assert.Equal(new ResourceId("file50.txt"), result50.Resource);
        Assert.Equal(5, result50.OriginalOffset);

        var result0 = sourceMap.Query(generatedOffset: 0);
        Assert.NotNull(result0);
        Assert.Equal(new ResourceId("file0.txt"), result0.Resource);

        var result99 = sourceMap.Query(generatedOffset: 99 * 11);
        Assert.NotNull(result99);
        Assert.Equal(new ResourceId("file99.txt"), result99.Resource);
    }

    [Fact]
    public void Query_PositionBetweenMappings_ReturnsNull()
    {
        var builder = new SourceMapBuilder();

        // 6 lines joined with "\n" (5 separators). Line 5 begins at offset 15.
        builder.AddOffsetSegment("file.txt", generatedStartOffset: 0, originalStartOffset: 0, length: 10);
        builder.AddOffsetSegment("file.txt", generatedStartOffset: 15, originalStartOffset: 50, length: 10);

        var sourceMap = builder.Build();

        // Query position in the gap
        var result = sourceMap.Query(generatedOffset: 12);

        Assert.Null(result);
    }

    #endregion

    #region Exact Range Query Tests

    [Fact]
    public void Query_RangeOverTwoSegments_ReturnsTwoMappings()
    {
        var builder = new SourceMapBuilder();

        var resource1 = new ResourceId("file1.txt");
        var resource2 = new ResourceId("file2.txt");

        // Segment 1: generated [0..5) -> file1 [0..5)
        builder.AddOffsetSegment(resource1, generatedStartOffset: 0, originalStartOffset: 0, length: 5);

        // Segment 2: generated [10..15) -> file2 [0..5)
        builder.AddOffsetSegment(resource2, generatedStartOffset: 10, originalStartOffset: 0, length: 5);

        var sourceMap = builder.Build();

        var results = sourceMap.QueryRangeByLength(generatedStartOffset: 0, length: 15);

        Assert.Equal(2, results.Count);

        Assert.Equal(resource1, results[0].Resource);
        Assert.Equal(0, results[0].GeneratedStartOffset);
        Assert.Equal(5, results[0].GeneratedEndOffset);
        Assert.Equal(0, results[0].OriginalStartOffset);
        Assert.Equal(5, results[0].OriginalEndOffset);

        Assert.Equal(resource2, results[1].Resource);
        Assert.Equal(10, results[1].GeneratedStartOffset);
        Assert.Equal(15, results[1].GeneratedEndOffset);
        Assert.Equal(0, results[1].OriginalStartOffset);
        Assert.Equal(5, results[1].OriginalEndOffset);
    }

    [Fact]
    public void Query_RangeByStartEnd_ReturnsSameResultsAsLengthOverload()
    {
        var builder = new SourceMapBuilder();

        var resource = new ResourceId("file.txt");

        builder.AddOffsetSegment(resource, generatedStartOffset: 3, originalStartOffset: 7, length: 5);

        var sourceMap = builder.Build();

        var byLength = sourceMap.QueryRangeByLength(generatedStartOffset: 0, length: 10);
        var byStartEnd = sourceMap.QueryRangeByEnd(generatedStartOffset: 0, generatedEndOffset: 10);

        Assert.Equal(byLength, byStartEnd);
    }

    #endregion

}
