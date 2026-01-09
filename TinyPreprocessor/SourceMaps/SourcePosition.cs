namespace TinyPreprocessor.SourceMaps;

/// <summary>
/// Represents a position within text content using 0-based line and column numbers.
/// </summary>
/// <remarks>
/// This struct follows the standard source map convention of 0-based indexing.
/// Use <see cref="ToOneBased"/> for display purposes.
/// </remarks>
public readonly struct SourcePosition : IEquatable<SourcePosition>, IComparable<SourcePosition>
{
    /// <summary>
    /// Gets the 0-based line number.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets the 0-based column number.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="SourcePosition"/>.
    /// </summary>
    /// <param name="line">The 0-based line number.</param>
    /// <param name="column">The 0-based column number.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="line"/> or <paramref name="column"/> is negative.
    /// </exception>
    public SourcePosition(int line, int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(line);
        ArgumentOutOfRangeException.ThrowIfNegative(column);

        Line = line;
        Column = column;
    }

    /// <summary>
    /// Converts this position to 1-based line and column numbers for display purposes.
    /// </summary>
    /// <returns>A tuple containing 1-based line and column numbers.</returns>
    public (int Line, int Column) ToOneBased() => (Line + 1, Column + 1);

    #region IComparable<SourcePosition> Implementation

    /// <inheritdoc />
    public int CompareTo(SourcePosition other)
    {
        var lineComparison = Line.CompareTo(other.Line);
        return lineComparison != 0 ? lineComparison : Column.CompareTo(other.Column);
    }

    #endregion

    #region IEquatable<SourcePosition> Implementation

    /// <inheritdoc />
    public bool Equals(SourcePosition other) => Line == other.Line && Column == other.Column;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SourcePosition other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Line, Column);

    #endregion

    #region Operators

    /// <summary>
    /// Determines whether two <see cref="SourcePosition"/> instances are equal.
    /// </summary>
    public static bool operator ==(SourcePosition left, SourcePosition right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="SourcePosition"/> instances are not equal.
    /// </summary>
    public static bool operator !=(SourcePosition left, SourcePosition right) => !left.Equals(right);

    /// <summary>
    /// Determines whether the left <see cref="SourcePosition"/> is less than the right.
    /// </summary>
    public static bool operator <(SourcePosition left, SourcePosition right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Determines whether the left <see cref="SourcePosition"/> is less than or equal to the right.
    /// </summary>
    public static bool operator <=(SourcePosition left, SourcePosition right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Determines whether the left <see cref="SourcePosition"/> is greater than the right.
    /// </summary>
    public static bool operator >(SourcePosition left, SourcePosition right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Determines whether the left <see cref="SourcePosition"/> is greater than or equal to the right.
    /// </summary>
    public static bool operator >=(SourcePosition left, SourcePosition right) => left.CompareTo(right) >= 0;

    #endregion

    /// <inheritdoc />
    public override string ToString()
    {
        var (line, column) = ToOneBased();
        return $"({line}:{column})";
    }
}
