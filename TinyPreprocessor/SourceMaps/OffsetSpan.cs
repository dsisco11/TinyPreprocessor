namespace TinyPreprocessor.SourceMaps;

/// <summary>
/// Represents an end-exclusive span in offset space.
/// </summary>
internal readonly record struct OffsetSpan
{
    public int Start { get; }

    public int End { get; }

    public int Length => End - Start;

    public OffsetSpan(int start, int end)
    {
        if (start < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if (end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(end));
        }

        Start = start;
        End = end;
    }

    public bool Overlaps(OffsetSpan other) => Start < other.End && other.Start < End;

    public OffsetSpan Intersect(OffsetSpan other)
    {
        var start = Math.Max(Start, other.Start);
        var end = Math.Min(End, other.End);
        return end <= start ? new OffsetSpan(start, start) : new OffsetSpan(start, end);
    }
}
