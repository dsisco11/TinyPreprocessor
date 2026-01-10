namespace TinyPreprocessor.Text;

/// <summary>
/// Options for the concatenating merge strategy.
/// </summary>
/// <param name="Separator">The separator between resources. Defaults to newline.</param>
/// <param name="IncludeResourceMarkers">Whether to include debug markers for resource boundaries.</param>
/// <param name="MarkerFormat">The format string for resource markers. {0} is replaced with the resource ID.</param>
public sealed record ConcatenatingMergeOptions(
    string Separator = "\n",
    bool IncludeResourceMarkers = false,
    string MarkerFormat = "/* === {0} === */\n");
