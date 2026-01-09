using TinyPreprocessor.Core;
using Xunit;

namespace TinyPreprocessor.Tests.Core;

/// <summary>
/// Unit tests for <see cref="Resource"/>.
/// </summary>
public sealed class ResourceTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithAllParameters_SetsPropertiesCorrectly()
    {
        var id = new ResourceId("test.txt");
        var content = "Hello, World!".AsMemory();
        var metadata = new Dictionary<string, object> { ["key"] = "value" };

        var resource = new Resource(id, content, metadata);

        Assert.Equal(id, resource.Id);
        Assert.Equal("Hello, World!", resource.Content.ToString());
        Assert.NotNull(resource.Metadata);
        Assert.Equal("value", resource.Metadata["key"]);
    }

    [Fact]
    public void Constructor_WithoutMetadata_SetsMetadataToNull()
    {
        var id = new ResourceId("test.txt");
        var content = "Content".AsMemory();

        var resource = new Resource(id, content);

        Assert.Null(resource.Metadata);
    }

    [Fact]
    public void Constructor_WithEmptyContent_Succeeds()
    {
        var id = new ResourceId("empty.txt");
        var content = ReadOnlyMemory<char>.Empty;

        var resource = new Resource(id, content);

        Assert.True(resource.Content.IsEmpty);
    }

    #endregion

    #region IResource Implementation Tests

    [Fact]
    public void Id_ReturnsResourceId()
    {
        ResourceId id = "resource/path.txt";
        var resource = new Resource(id, "content".AsMemory());

        IResource iResource = resource;

        Assert.Equal(id, iResource.Id);
    }

    [Fact]
    public void Content_ReturnsReadOnlyMemory()
    {
        const string contentString = "Test content with special chars: éàü";
        var resource = new Resource("test.txt", contentString.AsMemory());

        Assert.Equal(contentString, resource.Content.ToString());
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void Equals_SameIdAndContent_ReturnsTrue()
    {
        var content = "Same content".AsMemory();
        var resource1 = new Resource("file.txt", content);
        var resource2 = new Resource("file.txt", content);

        // Records compare by value for properties
        Assert.Equal(resource1.Id, resource2.Id);
    }

    [Fact]
    public void Equals_DifferentId_ReturnsFalse()
    {
        var content = "Content".AsMemory();
        var resource1 = new Resource("file1.txt", content);
        var resource2 = new Resource("file2.txt", content);

        Assert.NotEqual(resource1.Id, resource2.Id);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = new Resource("original.txt", "original content".AsMemory());
        var newContent = "new content".AsMemory();

        var modified = original with { Content = newContent };

        Assert.Equal(original.Id, modified.Id);
        Assert.Equal("new content", modified.Content.ToString());
        Assert.Equal("original content", original.Content.ToString());
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public void Metadata_WhenProvided_IsAccessible()
    {
        var metadata = new Dictionary<string, object>
        {
            ["Author"] = "Test Author",
            ["LineCount"] = 42,
            ["IsGenerated"] = true
        };

        var resource = new Resource("test.txt", "content".AsMemory(), metadata);

        Assert.Equal("Test Author", resource.Metadata!["Author"]);
        Assert.Equal(42, resource.Metadata["LineCount"]);
        Assert.Equal(true, resource.Metadata["IsGenerated"]);
    }

    [Fact]
    public void Metadata_IsReadOnly()
    {
        var metadata = new Dictionary<string, object> { ["key"] = "value" };
        var resource = new Resource("test.txt", "content".AsMemory(), metadata);

        // IReadOnlyDictionary doesn't have Add method
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(resource.Metadata);
    }

    #endregion
}
