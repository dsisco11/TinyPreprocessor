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

        var generated = "0123456789";
        builder.SetGeneratedContent(generated.AsMemory());

        var originalContent = "AAAAAAAAAA";
        var original = new Resource(resource, originalContent.AsMemory());
        builder.SetOriginalResources(new Dictionary<ResourceId, IResource> { [resource] = original });

        // Generated: offsets [0..10) -> Original: offsets [0..10)
        builder.AddOffsetSegment(resource, generatedStartOffset: 0, originalStartOffset: 0, length: 10);

        var sourceMap = builder.Build();
        var result = sourceMap.Query(new SourcePosition(0, 5));

        Assert.NotNull(result);
        Assert.Equal(resource, result.Resource);
        Assert.Equal(0, result.OriginalPosition.Line);
        Assert.Equal(5, result.OriginalPosition.Column);
    }

    [Fact]
    public void Query_PositionAtMappingStart_ReturnsOriginalLocation()
    {
        var builder = new SourceMapBuilder();

        var generated = string.Join("\n", Enumerable.Range(0, 11).Select(i => i == 10 ? new string('G', 50) : ""));
        builder.SetGeneratedContent(generated.AsMemory());

        var original = new Resource("file.txt", new string('O', 50).AsMemory());
        builder.SetOriginalResources(new Dictionary<ResourceId, IResource> { [original.Id] = original });

        // generated line 10, col 0 is offset of the start of that line.
        var generatedStartOffset = GetOffset(generated, new SourcePosition(10, 0));
        builder.AddOffsetSegment("file.txt", generatedStartOffset, originalStartOffset: 0, length: 50);

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

        var generated = string.Join("\n", Enumerable.Range(0, 6).Select(i => i == 5 ? new string('G', 20) : ""));
        builder.SetGeneratedContent(generated.AsMemory());

        var original = new Resource("file.txt", new string('O', 20).AsMemory());
        builder.SetOriginalResources(new Dictionary<ResourceId, IResource> { [original.Id] = original });

        var start = GetOffset(generated, new SourcePosition(5, 0));
        builder.AddOffsetSegment("file.txt", start, originalStartOffset: 0, length: 20);

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
        builder.SetGeneratedContent("".AsMemory());
        builder.SetOriginalResources(new Dictionary<ResourceId, IResource>());
        var sourceMap = builder.Build();

        var result = sourceMap.Query(new SourcePosition(0, 0));

        Assert.Null(result);
    }

    [Fact]
    public void Query_MultipleMappings_FindsCorrectOne()
    {
        var builder = new SourceMapBuilder();

        var generated = "XXXXXXXXXX\nYYYYYYYYYY\nZZZZZZZZZZ";
        builder.SetGeneratedContent(generated.AsMemory());

        var r1 = new Resource("file1.txt", "XXXXXXXXXX".AsMemory());
        var r2 = new Resource("file2.txt", "YYYYYYYYYY".AsMemory());
        var r3 = new Resource("file3.txt", "ZZZZZZZZZZ".AsMemory());
        builder.SetOriginalResources(new Dictionary<ResourceId, IResource>
        {
            [r1.Id] = r1,
            [r2.Id] = r2,
            [r3.Id] = r3
        });

        builder.AddOffsetSegment("file1.txt", generatedStartOffset: GetOffset(generated, new SourcePosition(0, 0)), originalStartOffset: 0, length: 10);
        builder.AddOffsetSegment("file2.txt", generatedStartOffset: GetOffset(generated, new SourcePosition(1, 0)), originalStartOffset: 0, length: 10);
        builder.AddOffsetSegment("file3.txt", generatedStartOffset: GetOffset(generated, new SourcePosition(2, 0)), originalStartOffset: 0, length: 10);

        var sourceMap = builder.Build();

        var result = sourceMap.Query(new SourcePosition(1, 5));

        Assert.NotNull(result);
        Assert.Equal(new ResourceId("file2.txt"), result.Resource);
        Assert.Equal(0, result.OriginalPosition.Line);
    }

    [Fact]
    public void Query_BinarySearchWithManyMappings_FindsCorrectMapping()
    {
        var builder = new SourceMapBuilder();

        // Generated is 100 lines of single char plus newline.
        var generatedLines = Enumerable.Range(0, 100).Select(_ => "XXXXXXXXXX");
        var generated = string.Join("\n", generatedLines);
        builder.SetGeneratedContent(generated.AsMemory());

        var originals = new Dictionary<ResourceId, IResource>();
        for (var i = 0; i < 100; i++)
        {
            var r = new Resource($"file{i}.txt", "XXXXXXXXXX".AsMemory());
            originals[r.Id] = r;
        }
        builder.SetOriginalResources(originals);

        // Add 100 offset segments.
        for (var i = 0; i < 100; i++)
        {
            var offset = GetOffset(generated, new SourcePosition(i, 0));
            builder.AddOffsetSegment($"file{i}.txt", offset, originalStartOffset: 0, length: 10);
        }

        var sourceMap = builder.Build();

        // Query various positions
        var result50 = sourceMap.Query(new SourcePosition(50, 5));
        Assert.NotNull(result50);
        Assert.Equal(new ResourceId("file50.txt"), result50.Resource);
        Assert.Equal(0, result50.OriginalPosition.Line);

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

        var generated = string.Join("\n", Enumerable.Range(0, 6).Select(i => i == 0 || i == 5 ? new string('X', 10) : ""));
        builder.SetGeneratedContent(generated.AsMemory());

        var original = new Resource("file.txt", new string('O', 100).AsMemory());
        builder.SetOriginalResources(new Dictionary<ResourceId, IResource> { [original.Id] = original });

        builder.AddOffsetSegment("file.txt", GetOffset(generated, new SourcePosition(0, 0)), originalStartOffset: 0, length: 10);
        builder.AddOffsetSegment("file.txt", GetOffset(generated, new SourcePosition(5, 0)), originalStartOffset: 50, length: 10);

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
        builder.AddOffsetSegment(resource1.Id, generatedStartOffset: 0, originalStartOffset: 0, length: 5);

        // Segment 2: generated [10..15) -> file2 [0..5)
        builder.AddOffsetSegment(resource2.Id, generatedStartOffset: 10, originalStartOffset: 0, length: 5);

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

        builder.AddOffsetSegment(resource.Id, generatedStartOffset: 3, originalStartOffset: 7, length: 5);

        var sourceMap = builder.Build();

        var byLength = sourceMap.Query(new SourcePosition(0, 0), length: 10);
        var byStartEnd = sourceMap.Query(new SourcePosition(0, 0), new SourcePosition(0, 10));

        Assert.Equal(byLength, byStartEnd);
    }

    #endregion

    private static int GetOffset(string text, SourcePosition position)
    {
        var offset = 0;
        var line = 0;
        var column = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (line == position.Line && column == position.Column)
            {
                return offset;
            }

            if (text[i] == '\n')
            {
                line++;
                column = 0;
            }
            else
            {
                column++;
            }

            offset++;
        }

        // Allow querying at end of text.
        if (line == position.Line && column == position.Column)
        {
            return offset;
        }

        throw new ArgumentOutOfRangeException(nameof(position));
    }
}
