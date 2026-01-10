namespace TinyPreprocessor.Core;

/// <summary>
/// Provides operations needed by the preprocessing pipeline to interpret offsets within <typeparamref name="TContent"/>.
/// </summary>
/// <typeparam name="TContent">The content representation type.</typeparam>
public interface IContentModel<TContent>
{
    /// <summary>
    /// Gets the length of <paramref name="content"/> in content units.
    /// </summary>
    /// <param name="content">The content value.</param>
    /// <returns>The length of the content.</returns>
    int GetLength(TContent content);

    /// <summary>
    /// Returns a slice of <paramref name="content"/> starting at <paramref name="start"/> with the specified <paramref name="length"/>.
    /// </summary>
    /// <param name="content">The content value.</param>
    /// <param name="start">The 0-based start offset.</param>
    /// <param name="length">The length in content units.</param>
    /// <returns>A sliced content value.</returns>
    TContent Slice(TContent content, int start, int length);
}
