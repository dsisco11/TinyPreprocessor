using System.Collections;

namespace TinyPreprocessor.Diagnostics;

/// <summary>
/// Thread-safe collection for accumulating diagnostics during preprocessing.
/// </summary>
/// <remarks>
/// This collection follows the "collect all, decide later" pattern—processing continues 
/// on errors, and the caller decides how to handle the collected diagnostics.
/// </remarks>
public sealed class DiagnosticCollection : IReadOnlyCollection<IPreprocessorDiagnostic>
{
    private readonly object _lock = new();
    private readonly List<IPreprocessorDiagnostic> _diagnostics = [];

    /// <inheritdoc />
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _diagnostics.Count;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether any diagnostics with <see cref="DiagnosticSeverity.Error"/> severity exist.
    /// </summary>
    public bool HasErrors
    {
        get
        {
            lock (_lock)
            {
                return _diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether any diagnostics with <see cref="DiagnosticSeverity.Warning"/> severity exist.
    /// </summary>
    public bool HasWarnings
    {
        get
        {
            lock (_lock)
            {
                return _diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning);
            }
        }
    }

    /// <summary>
    /// Adds a diagnostic to the collection.
    /// </summary>
    /// <param name="diagnostic">The diagnostic to add.</param>
    public void Add(IPreprocessorDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        lock (_lock)
        {
            _diagnostics.Add(diagnostic);
        }
    }

    /// <summary>
    /// Adds multiple diagnostics to the collection.
    /// </summary>
    /// <param name="diagnostics">The diagnostics to add.</param>
    public void AddRange(IEnumerable<IPreprocessorDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        lock (_lock)
        {
            _diagnostics.AddRange(diagnostics);
        }
    }

    /// <summary>
    /// Gets all diagnostics for a specific resource.
    /// </summary>
    /// <param name="resourceId">The resource identifier to filter by.</param>
    /// <returns>A list of diagnostics for the specified resource.</returns>
    public IReadOnlyList<IPreprocessorDiagnostic> GetByResource(Core.ResourceId resourceId)
    {
        lock (_lock)
        {
            return _diagnostics
                .Where(d => d.Resource.HasValue && d.Resource.Value == resourceId)
                .ToList();
        }
    }

    /// <summary>
    /// Gets all diagnostics with a specific severity level.
    /// </summary>
    /// <param name="severity">The severity level to filter by.</param>
    /// <returns>A list of diagnostics with the specified severity.</returns>
    public IReadOnlyList<IPreprocessorDiagnostic> GetBySeverity(DiagnosticSeverity severity)
    {
        lock (_lock)
        {
            return _diagnostics
                .Where(d => d.Severity == severity)
                .ToList();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns a snapshot of the collection for thread-safe iteration.
    /// </remarks>
    public IEnumerator<IPreprocessorDiagnostic> GetEnumerator()
    {
        List<IPreprocessorDiagnostic> snapshot;
        lock (_lock)
        {
            snapshot = [.. _diagnostics];
        }
        return snapshot.GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
