using System;
using System.Collections.Generic;
using TinyPreprocessor.Core;
using Xunit;

namespace TinyPreprocessor.Tests.Core;

public sealed class ContentBoundaryResolverExtensionsTests
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
    public void CountBoundaries_ReturnsCountInRange()
    {
        var resolver = new FixedBoundaryResolver(2, 5, 7);
        var content = "abcdefghi".AsMemory();

        Assert.Equal(0, resolver.CountBoundaries(content, "file.txt", startOffset: 0, endOffset: 2));
        Assert.Equal(1, resolver.CountBoundaries(content, "file.txt", startOffset: 0, endOffset: 3));
        Assert.Equal(2, resolver.CountBoundaries(content, "file.txt", startOffset: 2, endOffset: 7));
        Assert.Equal(3, resolver.CountBoundaries(content, "file.txt", startOffset: 0, endOffset: 100));
    }
}
