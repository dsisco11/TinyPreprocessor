namespace TinyPreprocessor.Text;

using TinyPreprocessor.Core;

/// <summary>
/// Content model for <see cref="ReadOnlyMemory{T}"/> of <see langword="char"/>.
/// </summary>
public sealed class ReadOnlyMemoryCharContentModel : IContentModel<ReadOnlyMemory<char>>
{
    /// <inheritdoc />
    public int GetLength(ReadOnlyMemory<char> content) => content.Length;

    /// <inheritdoc />
    public ReadOnlyMemory<char> Slice(ReadOnlyMemory<char> content, int start, int length) => content.Slice(start, length);
}
