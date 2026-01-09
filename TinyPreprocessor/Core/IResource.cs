namespace TinyPreprocessor.Core;

/// <summary>
/// Represents a single resource (file, module, or abstract content unit) in the preprocessing pipeline.
/// </summary>
public interface IResource
{
    /// <summary>
    /// Gets the unique identifier for this resource.
    /// </summary>
    ResourceId Id { get; }

    /// <summary>
    /// Gets the content of this resource.
    /// </summary>
    ReadOnlyMemory<char> Content { get; }

    /// <summary>
    /// Gets optional metadata associated with this resource.
    /// </summary>
    IReadOnlyDictionary<string, object>? Metadata { get; }
}
