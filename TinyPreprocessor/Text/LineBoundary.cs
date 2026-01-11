namespace TinyPreprocessor.Text;

/// <summary>
/// Marker type representing logical line boundaries for text content.
/// </summary>
/// <remarks>
/// The boundary offsets for this marker are defined by downstream users.
/// A typical convention is that boundary offsets represent the start offsets of lines after the first line
/// (i.e., offsets immediately after a line break sequence).
/// </remarks>
public readonly struct LineBoundary;
