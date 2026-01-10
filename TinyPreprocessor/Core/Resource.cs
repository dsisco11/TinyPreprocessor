namespace TinyPreprocessor.Core;

/// <summary>
/// Default implementation of <see cref="IResource{TContent}"/> as an immutable record.
/// </summary>
/// <param name="Id">The unique identifier for this resource.</param>
/// <param name="Content">The content of this resource.</param>
/// <param name="Metadata">Optional metadata associated with this resource.</param>
public sealed record Resource<TContent>(
    ResourceId Id,
    TContent Content,
    IReadOnlyDictionary<string, object>? Metadata = null
) : IResource<TContent>;
