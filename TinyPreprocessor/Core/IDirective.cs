namespace TinyPreprocessor.Core;

/// <summary>
/// Marker interface for parsed directives found within resource content.
/// </summary>
/// <remarks>
/// Downstream users define their own directive types (IncludeDirective, ImportDirective, etc.)
/// by implementing this interface. The <see cref="Location"/> property indicates where 
/// the directive appears in the source content, enabling directive stripping during merge.
/// </remarks>
public interface IDirective
{
    /// <summary>
    /// Gets the location of this directive within the source content.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="Range"/> for efficient content slicing.
    /// </remarks>
    Range Location { get; }
}
