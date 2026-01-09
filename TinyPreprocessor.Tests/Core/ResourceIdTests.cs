using TinyPreprocessor.Core;
using Xunit;

namespace TinyPreprocessor.Tests.Core;

/// <summary>
/// Unit tests for <see cref="ResourceId"/>.
/// </summary>
public sealed class ResourceIdTests
{
    #region Equality Tests

    [Fact]
    public void Equals_SamePath_ReturnsTrue()
    {
        var id1 = new ResourceId("test/file.txt");
        var id2 = new ResourceId("test/file.txt");

        Assert.Equal(id1, id2);
        Assert.True(id1 == id2);
        Assert.False(id1 != id2);
    }

    [Fact]
    public void Equals_DifferentPath_ReturnsFalse()
    {
        var id1 = new ResourceId("test/file1.txt");
        var id2 = new ResourceId("test/file2.txt");

        Assert.NotEqual(id1, id2);
        Assert.False(id1 == id2);
        Assert.True(id1 != id2);
    }

    [Fact]
    public void Equals_CaseSensitive_ReturnsFalseForDifferentCase()
    {
        var id1 = new ResourceId("Test/File.txt");
        var id2 = new ResourceId("test/file.txt");

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void Equals_NullPath_HandledCorrectly()
    {
        var id1 = new ResourceId(null!);
        var id2 = new ResourceId(null!);

        Assert.Equal(id1, id2);
    }

    [Fact]
    public void Equals_EmptyPath_ReturnsTrue()
    {
        var id1 = new ResourceId(string.Empty);
        var id2 = new ResourceId(string.Empty);

        Assert.Equal(id1, id2);
    }

    #endregion

    #region GetHashCode Tests

    [Fact]
    public void GetHashCode_SamePath_ReturnsSameHash()
    {
        var id1 = new ResourceId("test/file.txt");
        var id2 = new ResourceId("test/file.txt");

        Assert.Equal(id1.GetHashCode(), id2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentPath_ReturnsDifferentHash()
    {
        var id1 = new ResourceId("test/file1.txt");
        var id2 = new ResourceId("test/file2.txt");

        // Note: Different paths might theoretically have same hash (collision),
        // but these specific paths should have different hashes
        Assert.NotEqual(id1.GetHashCode(), id2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_UsableInHashSet()
    {
        var id1 = new ResourceId("file1.txt");
        var id2 = new ResourceId("file2.txt");
        var id3 = new ResourceId("file1.txt"); // Duplicate of id1

        var set = new HashSet<ResourceId> { id1, id2, id3 };

        Assert.Equal(2, set.Count);
        Assert.Contains(id1, set);
        Assert.Contains(id2, set);
    }

    [Fact]
    public void GetHashCode_UsableAsDictionaryKey()
    {
        var id1 = new ResourceId("key1");
        var id2 = new ResourceId("key2");

        var dict = new Dictionary<ResourceId, string>
        {
            [id1] = "value1",
            [id2] = "value2"
        };

        Assert.Equal("value1", dict[new ResourceId("key1")]);
        Assert.Equal("value2", dict[new ResourceId("key2")]);
    }

    #endregion

    #region Conversion Tests

    [Fact]
    public void ImplicitConversion_FromString_CreatesResourceId()
    {
        ResourceId id = "test/path.txt";

        Assert.Equal("test/path.txt", id.Path);
    }

    [Fact]
    public void ExplicitConversion_ToString_ReturnsPath()
    {
        var id = new ResourceId("test/path.txt");

        string path = (string)id;

        Assert.Equal("test/path.txt", path);
    }

    [Fact]
    public void Path_ReturnsConstructorValue()
    {
        const string path = "some/nested/path/file.cs";
        var id = new ResourceId(path);

        Assert.Equal(path, id.Path);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        var id = new ResourceId("test.txt");

        var result = id.ToString();

        Assert.Contains("test.txt", result);
    }

    #endregion
}
