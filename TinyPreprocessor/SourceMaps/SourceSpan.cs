namespace TinyPreprocessor.SourceMaps;

/// <summary>
/// Represents a range of text from a start to an end position.
/// </summary>
/// <remarks>
/// The start position is inclusive and the end position is exclusive,
/// consistent with <see cref="Range"/> and <see cref="Span{T}"/> conventions.
/// </remarks>
public readonly struct SourceSpan : IEquatable<SourceSpan>
{
    /// <summary>
    /// Gets the inclusive start position of this span.
    /// </summary>
    public SourcePosition Start { get; }

    /// <summary>
    /// Gets the exclusive end position of this span.
    /// </summary>
    public SourcePosition End { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="SourceSpan"/>.
    /// </summary>
    /// <param name="start">The inclusive start position.</param>
    /// <param name="end">The exclusive end position.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="end"/> is less than <paramref name="start"/>.
    /// </exception>
    public SourceSpan(SourcePosition start, SourcePosition end)
    {
        if (end < start)
        {
            throw new ArgumentException("End position must be greater than or equal to start position.", nameof(end));
        }

        Start = start;
        End = end;
    }

    /// <summary>
    /// Determines whether this span contains the specified position.
    /// </summary>
    /// <param name="position">The position to check.</param>
    /// <returns><see langword="true"/> if the position is within [Start, End); otherwise, <see langword="false"/>.</returns>
    public bool Contains(SourcePosition position) => position >= Start && position < End;

    /// <summary>
    /// Determines whether this span overlaps with another span.
    /// </summary>
    /// <param name="other">The other span to check.</param>
    /// <returns><see langword="true"/> if the spans intersect; otherwise, <see langword="false"/>.</returns>
    public bool Overlaps(SourceSpan other) => Start < other.End && other.Start < End;

    #region IEquatable<SourceSpan> Implementation

    /// <inheritdoc />
    public bool Equals(SourceSpan other) => Start.Equals(other.Start) && End.Equals(other.End);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SourceSpan other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Start, End);

    #endregion

    #region Operators

    /// <summary>
    /// Determines whether two <see cref="SourceSpan"/> instances are equal.
    /// </summary>
    public static bool operator ==(SourceSpan left, SourceSpan right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="SourceSpan"/> instances are not equal.
    /// </summary>
    public static bool operator !=(SourceSpan left, SourceSpan right) => !left.Equals(right);

    #endregion

    /// <inheritdoc />
    public override string ToString() => $"[{Start} - {End})";
}
