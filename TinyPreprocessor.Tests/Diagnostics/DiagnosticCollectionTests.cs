using TinyPreprocessor.Core;
using TinyPreprocessor.Diagnostics;
using Xunit;

namespace TinyPreprocessor.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="DiagnosticCollection"/>.
/// </summary>
public sealed class DiagnosticCollectionTests
{
    #region Thread-Safety Tests

    [Fact]
    public void Add_ConcurrentAdds_AllDiagnosticsAreAdded()
    {
        var collection = new DiagnosticCollection();
        const int threadCount = 10;
        const int diagnosticsPerThread = 100;

        var tasks = Enumerable.Range(0, threadCount)
            .Select(threadIndex => Task.Run(() =>
            {
                for (var i = 0; i < diagnosticsPerThread; i++)
                {
                    collection.Add(new TestDiagnostic(
                        DiagnosticSeverity.Info,
                        $"TEST{threadIndex:D2}{i:D3}",
                        $"Thread {threadIndex} diagnostic {i}"));
                }
            }))
            .ToArray();

        Task.WaitAll(tasks);

        Assert.Equal(threadCount * diagnosticsPerThread, collection.Count);
    }

    [Fact]
    public void HasErrors_ConcurrentReadsAndWrites_NoExceptions()
    {
        var collection = new DiagnosticCollection();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var exceptions = new List<Exception>();

        var writerTask = Task.Run(() =>
        {
            var random = new Random();
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    var severity = random.Next(3) switch
                    {
                        0 => DiagnosticSeverity.Info,
                        1 => DiagnosticSeverity.Warning,
                        _ => DiagnosticSeverity.Error
                    };
                    collection.Add(new TestDiagnostic(severity, "TEST001", "Test"));
                }
                catch (Exception ex)
                {
                    lock (exceptions) { exceptions.Add(ex); }
                }
            }
        });

        var readerTasks = Enumerable.Range(0, 5)
            .Select(index => Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var hasErrors = collection.HasErrors;
                        var hasWarnings = collection.HasWarnings;
                        var count = collection.Count;
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions) { exceptions.Add(ex); }
                    }
                }
            }))
            .ToArray();

        Task.WaitAll([writerTask, .. readerTasks]);

        Assert.Empty(exceptions);
    }

    [Fact]
    public void AddRange_ConcurrentCalls_AllDiagnosticsAreAdded()
    {
        var collection = new DiagnosticCollection();
        const int batchCount = 50;
        const int diagnosticsPerBatch = 20;

        var tasks = Enumerable.Range(0, batchCount)
            .Select(batchIndex => Task.Run(() =>
            {
                var diagnostics = Enumerable.Range(0, diagnosticsPerBatch)
                    .Select(i => new TestDiagnostic(
                        DiagnosticSeverity.Info,
                        $"BATCH{batchIndex:D3}",
                        $"Diagnostic {i}"))
                    .ToList();

                collection.AddRange(diagnostics);
            }))
            .ToArray();

        Task.WaitAll(tasks);

        Assert.Equal(batchCount * diagnosticsPerBatch, collection.Count);
    }

    #endregion

    #region HasErrors Logic Tests

    [Fact]
    public void HasErrors_EmptyCollection_ReturnsFalse()
    {
        var collection = new DiagnosticCollection();

        Assert.False(collection.HasErrors);
    }

    [Fact]
    public void HasErrors_OnlyInfoDiagnostics_ReturnsFalse()
    {
        var collection = new DiagnosticCollection();
        collection.Add(new TestDiagnostic(DiagnosticSeverity.Info, "INFO001", "Info message"));
        collection.Add(new TestDiagnostic(DiagnosticSeverity.Info, "INFO002", "Another info"));

        Assert.False(collection.HasErrors);
    }

    [Fact]
    public void HasErrors_OnlyWarningDiagnostics_ReturnsFalse()
    {
        var collection = new DiagnosticCollection();
        collection.Add(new TestDiagnostic(DiagnosticSeverity.Warning, "WARN001", "Warning message"));
        collection.Add(new TestDiagnostic(DiagnosticSeverity.Warning, "WARN002", "Another warning"));

        Assert.False(collection.HasErrors);
    }

    [Fact]
    public void HasErrors_WithErrorDiagnostic_ReturnsTrue()
    {
        var collection = new DiagnosticCollection();
        collection.Add(new TestDiagnostic(DiagnosticSeverity.Error, "ERR001", "Error message"));

        Assert.True(collection.HasErrors);
    }

    [Fact]
    public void HasErrors_MixedDiagnosticsWithError_ReturnsTrue()
    {
        var collection = new DiagnosticCollection();
        collection.Add(new TestDiagnostic(DiagnosticSeverity.Info, "INFO001", "Info"));
        collection.Add(new TestDiagnostic(DiagnosticSeverity.Warning, "WARN001", "Warning"));
        collection.Add(new TestDiagnostic(DiagnosticSeverity.Error, "ERR001", "Error"));

        Assert.True(collection.HasErrors);
    }

    #endregion

    #region HasWarnings Logic Tests

    [Fact]
    public void HasWarnings_EmptyCollection_ReturnsFalse()
    {
        var collection = new DiagnosticCollection();

        Assert.False(collection.HasWarnings);
    }

    [Fact]
    public void HasWarnings_OnlyInfoDiagnostics_ReturnsFalse()
    {
        var collection = new DiagnosticCollection();
        collection.Add(new TestDiagnostic(DiagnosticSeverity.Info, "INFO001", "Info"));

        Assert.False(collection.HasWarnings);
    }

    [Fact]
    public void HasWarnings_WithWarningDiagnostic_ReturnsTrue()
    {
        var collection = new DiagnosticCollection();
        collection.Add(new TestDiagnostic(DiagnosticSeverity.Warning, "WARN001", "Warning"));

        Assert.True(collection.HasWarnings);
    }

    #endregion

    #region Count Tests

    [Fact]
    public void Count_EmptyCollection_ReturnsZero()
    {
        var collection = new DiagnosticCollection();

        Assert.Equal(0, collection.Count);
    }

    [Fact]
    public void Count_AfterAdds_ReturnsCorrectCount()
    {
        var collection = new DiagnosticCollection();
        collection.Add(new TestDiagnostic(DiagnosticSeverity.Info, "TEST001", "Test 1"));
        collection.Add(new TestDiagnostic(DiagnosticSeverity.Info, "TEST002", "Test 2"));
        collection.Add(new TestDiagnostic(DiagnosticSeverity.Info, "TEST003", "Test 3"));

        Assert.Equal(3, collection.Count);
    }

    #endregion

    #region Enumeration Tests

    [Fact]
    public void Enumeration_ReturnsAllDiagnostics()
    {
        var collection = new DiagnosticCollection();
        var diagnostic1 = new TestDiagnostic(DiagnosticSeverity.Info, "TEST001", "Test 1");
        var diagnostic2 = new TestDiagnostic(DiagnosticSeverity.Warning, "TEST002", "Test 2");
        var diagnostic3 = new TestDiagnostic(DiagnosticSeverity.Error, "TEST003", "Test 3");

        collection.Add(diagnostic1);
        collection.Add(diagnostic2);
        collection.Add(diagnostic3);

        var enumerated = collection.ToList();

        Assert.Equal(3, enumerated.Count);
        Assert.Contains(diagnostic1, enumerated);
        Assert.Contains(diagnostic2, enumerated);
        Assert.Contains(diagnostic3, enumerated);
    }

    [Fact]
    public void Enumeration_EmptyCollection_ReturnsEmpty()
    {
        var collection = new DiagnosticCollection();

        Assert.Empty(collection);
    }

    #endregion

    #region GetByResource Tests

    [Fact]
    public void GetByResource_FiltersByResourceId()
    {
        var collection = new DiagnosticCollection();
        ResourceId resource1 = "file1.txt";
        ResourceId resource2 = "file2.txt";

        collection.Add(new TestDiagnostic(DiagnosticSeverity.Error, "ERR001", "Error in file1", resource1));
        collection.Add(new TestDiagnostic(DiagnosticSeverity.Warning, "WARN001", "Warning in file2", resource2));
        collection.Add(new TestDiagnostic(DiagnosticSeverity.Info, "INFO001", "Info in file1", resource1));

        var file1Diagnostics = collection.GetByResource(resource1);

        Assert.Equal(2, file1Diagnostics.Count);
        Assert.All(file1Diagnostics, d => Assert.Equal(resource1, d.Resource));
    }

    [Fact]
    public void GetByResource_NoMatchingResource_ReturnsEmpty()
    {
        var collection = new DiagnosticCollection();
        collection.Add(new TestDiagnostic(DiagnosticSeverity.Error, "ERR001", "Error", "other.txt"));

        var result = collection.GetByResource("nonexistent.txt");

        Assert.Empty(result);
    }

    #endregion

    #region Null Handling Tests

    [Fact]
    public void Add_NullDiagnostic_ThrowsArgumentNullException()
    {
        var collection = new DiagnosticCollection();

        Assert.Throws<ArgumentNullException>(() => collection.Add(null!));
    }

    [Fact]
    public void AddRange_NullCollection_ThrowsArgumentNullException()
    {
        var collection = new DiagnosticCollection();

        Assert.Throws<ArgumentNullException>(() => collection.AddRange(null!));
    }

    #endregion

    #region Test Helpers

    private sealed record TestDiagnostic(
        DiagnosticSeverity Severity,
        string Code,
        string Message,
        ResourceId? Resource = null,
        Range? Location = null) : IPreprocessorDiagnostic;

    #endregion
}
