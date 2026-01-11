using System;
using System.Collections.Generic;
using TinyPreprocessor.Core;
using TinyPreprocessor.SourceMaps;
using Xunit;

namespace TinyPreprocessor.Tests.SourceMaps;

public sealed class SourceMapBoundaryExtensionsTests
{
    private readonly struct TestBoundary;

    private sealed class FixedBoundaryResolver : IContentBoundaryResolver<ReadOnlyMemory<char>, TestBoundary>
    {
        private readonly int[] _boundaries;

        public FixedBoundaryResolver(params int[] boundaries) => _boundaries = boundaries;

        public IEnumerable<int> ResolveOffsets(ReadOnlyMemory<char> content, ResourceId resourceId, int startOffset, int endOffset)
        {
            for (var i = 0; i < _boundaries.Length; i++)
            {
                var offset = _boundaries[i];
                if (offset >= startOffset && offset < endOffset)
                {
                    yield return offset;
                }
            }
        }
    }

    [Fact]
    public void ResolveBoundaryIndex_CountsBoundariesBeforeOffset()
    {
        var resolver = new FixedBoundaryResolver(2, 5);
        var content = "abcdef".AsMemory();

        Assert.Equal(0, resolver.ResolveBoundaryIndex(content, "file.txt", offset: 0));
        Assert.Equal(0, resolver.ResolveBoundaryIndex(content, "file.txt", offset: 2));
        Assert.Equal(1, resolver.ResolveBoundaryIndex(content, "file.txt", offset: 3));
        Assert.Equal(2, resolver.ResolveBoundaryIndex(content, "file.txt", offset: 6));
    }

    [Fact]
    public void ResolveOriginalBoundaryLocation_MappedOffset_ReturnsOriginalLocationAndBoundaryIndex()
    {
        var builder = new SourceMapBuilder();
        builder.AddOffsetSegment("a.txt", generatedStartOffset: 0, originalStartOffset: 0, length: 10);
        var sourceMap = builder.Build();

        var resolver = new FixedBoundaryResolver(2, 5);
        var contentByResource = new Dictionary<ResourceId, ReadOnlyMemory<char>>
        {
            ["a.txt"] = "0123456789".AsMemory(),
        };

        var result = sourceMap.ResolveOriginalBoundaryLocation(
            generatedOffset: 3,
            contentProvider: id => contentByResource[id],
            boundaryResolver: resolver);

        Assert.NotNull(result);
        Assert.Equal(new ResourceId("a.txt"), result!.Resource);
        Assert.Equal(3, result.OriginalOffset);
        Assert.Equal(1, result.BoundaryIndex);
    }

    [Fact]
    public void ResolveOriginalBoundaryLocation_UnmappedOffset_ReturnsNull()
    {
        var builder = new SourceMapBuilder();
        builder.AddOffsetSegment("a.txt", generatedStartOffset: 10, originalStartOffset: 0, length: 10);
        var sourceMap = builder.Build();

        var resolver = new FixedBoundaryResolver(2, 5);

        var result = sourceMap.ResolveOriginalBoundaryLocation(
            generatedOffset: 0,
            contentProvider: _ => ReadOnlyMemory<char>.Empty,
            boundaryResolver: resolver);

        Assert.Null(result);
    }
}
