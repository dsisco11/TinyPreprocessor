using System.Collections.Generic;
using TinyPreprocessor.Core;
using TinyPreprocessor.SourceMaps;
using Xunit;

namespace TinyPreprocessor.Tests.SourceMaps;

/// <summary>
/// Unit tests for <see cref="SourceMapBuilder"/> using offset-based segments only.
/// </summary>
public sealed class SourceMapBuilderTests
{
    [Fact]
    public void Build_SortsOffsetSegmentsByGeneratedStart()
    {
        var builder = new SourceMapBuilder();

        var r1 = new ResourceId("a.txt");
        var r2 = new ResourceId("b.txt");

        builder.AddOffsetSegment(r2, generatedStartOffset: 5, originalStartOffset: 0, length: 5);
        builder.AddOffsetSegment(r1, generatedStartOffset: 0, originalStartOffset: 0, length: 5);

        var map = builder.Build();

        var first = map.Query(generatedOffset: 0);
        var second = map.Query(generatedOffset: 7);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(r1, first.Resource);
        Assert.Equal(r2, second.Resource);
    }

    [Fact]
    public void Clear_RemovesAllSegments()
    {
        var builder = new SourceMapBuilder();
        builder.AddOffsetSegment("x.txt", generatedStartOffset: 0, originalStartOffset: 0, length: 3);

        builder.Clear();
        var map = builder.Build();

        Assert.Null(map.Query(generatedOffset: 0));
    }
}
