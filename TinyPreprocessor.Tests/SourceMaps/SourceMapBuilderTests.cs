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

        var generated = "0123456789";
        builder.SetGeneratedContent(generated.AsMemory());

        var r1 = new Resource("a.txt", "xxxxx".AsMemory());
        var r2 = new Resource("b.txt", "yyyyy".AsMemory());
        builder.SetOriginalResources(new Dictionary<ResourceId, IResource>
        {
            [r1.Id] = r1,
            [r2.Id] = r2
        });

        builder.AddOffsetSegment(r2.Id, generatedStartOffset: 5, originalStartOffset: 0, length: 5);
        builder.AddOffsetSegment(r1.Id, generatedStartOffset: 0, originalStartOffset: 0, length: 5);

        var map = builder.Build();

        var first = map.Query(new SourcePosition(0, 0));
        var second = map.Query(new SourcePosition(0, 7));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(r1.Id, first.Resource);
        Assert.Equal(r2.Id, second.Resource);
    }

    [Fact]
    public void Clear_RemovesAllSegmentsAndIndexes()
    {
        var builder = new SourceMapBuilder();
        builder.SetGeneratedContent("abc".AsMemory());
        builder.SetOriginalResources(new Dictionary<ResourceId, IResource>
        {
            [new ResourceId("x.txt")] = new Resource("x.txt", "abc".AsMemory())
        });
        builder.AddOffsetSegment("x.txt", generatedStartOffset: 0, originalStartOffset: 0, length: 3);

        builder.Clear();
        var map = builder.Build();

        Assert.Null(map.Query(new SourcePosition(0, 0)));
    }
}
