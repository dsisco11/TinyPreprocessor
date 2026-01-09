namespace TinyPreprocessor.Core;

/// <summary>
/// A lightweight, immutable identifier for resources in the preprocessing system.
/// </summary>
/// <param name="Path">The path or identifier string for this resource.</param>
public readonly record struct ResourceId(string Path)
{
    /// <summary>
    /// Implicitly converts a string to a <see cref="ResourceId"/>.
    /// </summary>
    /// <param name="path">The path string to convert.</param>
    public static implicit operator ResourceId(string path) => new(path);

    /// <summary>
    /// Explicitly converts a <see cref="ResourceId"/> to a string.
    /// </summary>
    /// <param name="resourceId">The resource identifier to convert.</param>
    public static explicit operator string(ResourceId resourceId) => resourceId.Path;
}

