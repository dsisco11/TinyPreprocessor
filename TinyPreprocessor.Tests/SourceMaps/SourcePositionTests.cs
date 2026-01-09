using TinyPreprocessor.Core;
using TinyPreprocessor.SourceMaps;
using Xunit;

namespace TinyPreprocessor.Tests.SourceMaps;

/// <summary>
/// Unit tests for <see cref="SourcePosition"/>.
/// </summary>
public sealed class SourcePositionTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ValidValues_SetsProperties()
    {
        var position = new SourcePosition(10, 20);

        Assert.Equal(10, position.Line);
        Assert.Equal(20, position.Column);
    }

    [Fact]
    public void Constructor_ZeroValues_Succeeds()
    {
        var position = new SourcePosition(0, 0);

        Assert.Equal(0, position.Line);
        Assert.Equal(0, position.Column);
    }

    [Fact]
    public void Constructor_NegativeLine_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SourcePosition(-1, 0));
    }

    [Fact]
    public void Constructor_NegativeColumn_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SourcePosition(0, -1));
    }

    #endregion

    #region Comparison Tests

    [Fact]
    public void CompareTo_SameLine_ComparesColumns()
    {
        var pos1 = new SourcePosition(5, 10);
        var pos2 = new SourcePosition(5, 20);

        Assert.True(pos1.CompareTo(pos2) < 0);
        Assert.True(pos2.CompareTo(pos1) > 0);
    }

    [Fact]
    public void CompareTo_DifferentLines_ComparesLines()
    {
        var pos1 = new SourcePosition(5, 100); // Line 5, high column
        var pos2 = new SourcePosition(10, 0);  // Line 10, low column

        Assert.True(pos1.CompareTo(pos2) < 0);
        Assert.True(pos2.CompareTo(pos1) > 0);
    }

    [Fact]
    public void CompareTo_SamePosition_ReturnsZero()
    {
        var pos1 = new SourcePosition(5, 10);
        var pos2 = new SourcePosition(5, 10);

        Assert.Equal(0, pos1.CompareTo(pos2));
    }

    [Fact]
    public void Operators_LessThan_Works()
    {
        var pos1 = new SourcePosition(1, 0);
        var pos2 = new SourcePosition(2, 0);

        Assert.True(pos1 < pos2);
        Assert.False(pos2 < pos1);
        Assert.False(pos1 < pos1);
    }

    [Fact]
    public void Operators_LessThanOrEqual_Works()
    {
        var pos1 = new SourcePosition(1, 0);
        var pos2 = new SourcePosition(2, 0);
        var pos3 = new SourcePosition(1, 0);

        Assert.True(pos1 <= pos2);
        Assert.True(pos1 <= pos3);
        Assert.False(pos2 <= pos1);
    }

    [Fact]
    public void Operators_GreaterThan_Works()
    {
        var pos1 = new SourcePosition(2, 5);
        var pos2 = new SourcePosition(1, 10);

        Assert.True(pos1 > pos2);
        Assert.False(pos2 > pos1);
    }

    [Fact]
    public void Operators_GreaterThanOrEqual_Works()
    {
        var pos1 = new SourcePosition(2, 5);
        var pos2 = new SourcePosition(2, 5);
        var pos3 = new SourcePosition(1, 100);

        Assert.True(pos1 >= pos2);
        Assert.True(pos1 >= pos3);
    }

    [Fact]
    public void Sorting_ListOfPositions_SortsCorrectly()
    {
        var positions = new List<SourcePosition>
        {
            new(5, 10),
            new(1, 0),
            new(5, 5),
            new(3, 100),
            new(1, 50)
        };

        var sorted = positions.Order().ToList();

        Assert.Equal(new SourcePosition(1, 0), sorted[0]);
        Assert.Equal(new SourcePosition(1, 50), sorted[1]);
        Assert.Equal(new SourcePosition(3, 100), sorted[2]);
        Assert.Equal(new SourcePosition(5, 5), sorted[3]);
        Assert.Equal(new SourcePosition(5, 10), sorted[4]);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_SamePosition_ReturnsTrue()
    {
        var pos1 = new SourcePosition(10, 20);
        var pos2 = new SourcePosition(10, 20);

        Assert.True(pos1.Equals(pos2));
        Assert.True(pos1 == pos2);
        Assert.False(pos1 != pos2);
    }

    [Fact]
    public void Equals_DifferentLine_ReturnsFalse()
    {
        var pos1 = new SourcePosition(10, 20);
        var pos2 = new SourcePosition(11, 20);

        Assert.False(pos1.Equals(pos2));
        Assert.False(pos1 == pos2);
        Assert.True(pos1 != pos2);
    }

    [Fact]
    public void Equals_DifferentColumn_ReturnsFalse()
    {
        var pos1 = new SourcePosition(10, 20);
        var pos2 = new SourcePosition(10, 21);

        Assert.False(pos1.Equals(pos2));
    }

    [Fact]
    public void Equals_ObjectOverload_Works()
    {
        var pos1 = new SourcePosition(10, 20);
        object pos2 = new SourcePosition(10, 20);
        object notPosition = "not a position";

        Assert.True(pos1.Equals(pos2));
        Assert.False(pos1.Equals(notPosition));
        Assert.False(pos1.Equals(null));
    }

    [Fact]
    public void GetHashCode_SamePosition_SameHash()
    {
        var pos1 = new SourcePosition(10, 20);
        var pos2 = new SourcePosition(10, 20);

        Assert.Equal(pos1.GetHashCode(), pos2.GetHashCode());
    }

    #endregion

    #region ToOneBased Tests

    [Fact]
    public void ToOneBased_ConvertsCorrectly()
    {
        var position = new SourcePosition(0, 0);

        var (line, column) = position.ToOneBased();

        Assert.Equal(1, line);
        Assert.Equal(1, column);
    }

    [Fact]
    public void ToOneBased_NonZeroValues_ConvertsCorrectly()
    {
        var position = new SourcePosition(9, 19);

        var (line, column) = position.ToOneBased();

        Assert.Equal(10, line);
        Assert.Equal(20, column);
    }

    #endregion
}
