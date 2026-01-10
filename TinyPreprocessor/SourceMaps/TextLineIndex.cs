using System;
using System.Collections.Generic;

namespace TinyPreprocessor.SourceMaps;

/// <summary>
/// Precomputed line start offsets for fast offset &lt;-&gt; <see cref="SourcePosition"/> conversion.
/// </summary>
/// <remarks>
/// Newlines are detected using '\n'. Line and column numbers are 0-based.
/// </remarks>
internal readonly struct TextLineIndex
{
    private readonly int[] _lineStarts;
    private readonly int[] _lineLengths;
    private readonly int _textLength;

    private TextLineIndex(int[] lineStarts, int[] lineLengths, int textLength)
    {
        _lineStarts = lineStarts;
        _lineLengths = lineLengths;
        _textLength = textLength;
    }

    public int LineCount => _lineStarts.Length;

    public int TextLength => _textLength;

    public static TextLineIndex Build(ReadOnlySpan<char> text)
    {
        // Worst case (all '\n'): line count is text.Length + 1.
        var lineStarts = new List<int>(capacity: Math.Min(text.Length + 1, 1024));
        var lineLengths = new List<int>(capacity: Math.Min(text.Length + 1, 1024));

        var currentLineStart = 0;
        lineStarts.Add(0);

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n')
            {
                continue;
            }

            lineLengths.Add(i - currentLineStart);
            currentLineStart = i + 1;
            lineStarts.Add(currentLineStart);
        }

        // Final line (may be empty).
        lineLengths.Add(text.Length - currentLineStart);

        return new TextLineIndex(lineStarts.ToArray(), lineLengths.ToArray(), text.Length);
    }

    public bool TryGetOffset(SourcePosition position, out int offset)
    {
        offset = 0;

        if ((uint)position.Line >= (uint)_lineStarts.Length)
        {
            return false;
        }

        var lineStart = _lineStarts[position.Line];
        var lineLength = _lineLengths[position.Line];

        // Allow column at end-of-line for end-exclusive spans.
        if (position.Column > lineLength)
        {
            return false;
        }

        offset = lineStart + position.Column;

        // Defensive: should never exceed text length.
        return (uint)offset <= (uint)_textLength;
    }

    public SourcePosition GetPosition(int offset)
    {
        if (offset < 0 || offset > _textLength)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        // Find the rightmost line start <= offset.
        var idx = Array.BinarySearch(_lineStarts, offset);
        if (idx < 0)
        {
            idx = ~idx - 1;
        }

        if (idx < 0)
        {
            idx = 0;
        }

        var column = offset - _lineStarts[idx];
        return new SourcePosition(idx, column);
    }

    public int GetLineLength(int line)
    {
        if ((uint)line >= (uint)_lineLengths.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(line));
        }

        return _lineLengths[line];
    }
}
