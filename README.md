# TinyPreprocessor

A lightweight, extensible preprocessing library for .NET that resolves dependencies, merges resources, and generates source maps.

[![NuGet](https://img.shields.io/nuget/v/TinyPreprocessor.svg)](https://www.nuget.org/packages/TinyPreprocessor/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

## Features

- **Dependency Resolution** – Recursively resolves resource dependencies with cycle detection
- **Source Maps** – Generates mappings from output positions back to original sources
- **Extensible** – Bring your own directive parser, resource resolver, and merge strategy
- **Diagnostics** – Comprehensive error collection with "continue on error" support
- **Thread-Safe** – Concurrent `ProcessAsync` calls with isolated state

## Installation

```bash
dotnet add package TinyPreprocessor
```

## Requirements

- .NET 8+ (TinyPreprocessor targets `net8.0`)

## Quick Start

TinyPreprocessor requires three components:

1. **`IDirectiveParser<TDirective>`** – Parses directives from resource content
2. **`IResourceResolver`** – Resolves references to actual resources
3. **`IMergeStrategy<TContext>`** – Combines resources into final output

### Example: Simple Include Directive

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TinyPreprocessor;
using TinyPreprocessor.Core;
using TinyPreprocessor.Diagnostics;
using TinyPreprocessor.Merging;

// 1. Define your directive type
public sealed record IncludeDirective(string Path, Range Location) : IIncludeDirective
{
    public string Reference => Path;
}

// 2. Implement directive parser
public sealed class IncludeParser : IDirectiveParser<IncludeDirective>
{
    public IEnumerable<IncludeDirective> Parse(ReadOnlyMemory<char> content, ResourceId resourceId)
    {
        var text = content.ToString();
        var lines = text.Split('\n');
        var offset = 0;

        foreach (var line in lines)
        {
            if (line.StartsWith("#include "))
            {
                var path = line[9..].Trim().Trim('"');
                yield return new IncludeDirective(path, offset..(offset + line.Length));
            }
            offset += line.Length + 1;
        }
    }
}

// 3. Implement resource resolver
public sealed class FileResolver : IResourceResolver
{
    private readonly string _basePath;

    public FileResolver(string basePath) => _basePath = basePath;

    public ValueTask<ResourceResolutionResult> ResolveAsync(
        string reference,
        IResource? context,
        CancellationToken ct)
    {
        var fullPath = Path.Combine(_basePath, reference);

        if (!File.Exists(fullPath))
        {
            return ValueTask.FromResult(new ResourceResolutionResult(
                null,
                new ResolutionFailedDiagnostic(reference, $"File not found: {fullPath}")));
        }

        var content = File.ReadAllText(fullPath);
        var resource = new Resource(reference, content.AsMemory());
        return ValueTask.FromResult(new ResourceResolutionResult(resource, null));
    }
}

// 4. Use the preprocessor
var parser = new IncludeParser();
var resolver = new FileResolver(@"C:\MyProject\src");
var merger = new ConcatenatingMergeStrategy<object>();

var context = new object();

var preprocessor = new Preprocessor<IncludeDirective, object>(parser, resolver, merger);

var rootContent = File.ReadAllText(@"C:\MyProject\src\main.txt");
var root = new Resource("main.txt", rootContent.AsMemory());

var result = await preprocessor.ProcessAsync(root, context);

if (result.Success)
{
    Console.WriteLine(result.Content.ToString());
}
else
{
    foreach (var diagnostic in result.Diagnostics)
    {
        Console.WriteLine($"[{diagnostic.Code}] {diagnostic.Message}");
    }
}
```

## Configuration

```csharp
var options = new PreprocessorOptions(
    DeduplicateIncludes: true,  // Currently informational (resources are processed once per call)
    MaxIncludeDepth: 100,       // Safety limit for recursion
    ContinueOnError: true       // Collect all diagnostics instead of stopping early
);

// Note: The current implementation processes each resource at most once per call,
// so `DeduplicateIncludes` does not currently change output.
var result = await preprocessor.ProcessAsync(root, context, options);
```

## Source Map Usage

Query the source map to trace output positions back to original files:

```csharp
var result = await preprocessor.ProcessAsync(root, context);

// Find where line 50, column 10 in output came from
var location = result.SourceMap.Query(new SourcePosition(line: 49, column: 9));

if (location is not null)
{
    var (line, col) = location.OriginalPosition.ToOneBased();
    Console.WriteLine($"Originated from {location.Resource.Path} at line {line}, column {col}");
}

// For precise diagnostic spans, query a range.
// The range may map to multiple original resources (e.g., it crosses file boundaries).
var ranges = result.SourceMap.Query(new SourcePosition(line: 49, column: 9), length: 20);

foreach (var range in ranges)
{
    Console.WriteLine(
        $"Generated [{range.GeneratedStart} - {range.GeneratedEnd}) -> {range.Resource.Path} [{range.OriginalStart} - {range.OriginalEnd})");
}
```

## Custom Merge Strategy

Implement `IMergeStrategy<TContext>` for custom output formatting:

```csharp
public sealed class JsonMergeStrategy : IMergeStrategy<JsonMergeOptions>
{
    public ReadOnlyMemory<char> Merge(
        IReadOnlyList<ResolvedResource> orderedResources,
        JsonMergeOptions context,
        MergeContext mergeContext)
    {
        // Custom merge logic here
        // Use mergeContext.SourceMapBuilder to record mappings.
        // Use offset-based segments for precise mappings:
        //   mergeContext.SourceMapBuilder.AddOffsetSegment(resourceId, generatedStartOffset, originalStartOffset, length)
        // Use mergeContext.Diagnostics to report issues

        return ReadOnlyMemory<char>.Empty;
    }
}
```

## Docs

- [Core Abstractions](docs/01-core-abstractions.md)
- [Diagnostics System](docs/02-diagnostics-system.md)
- [Dependency Graph](docs/03-dependency-graph.md)
- [Source Mapping](docs/04-source-mapping.md)
- [Merge System](docs/05-merge-system.md)
- [Preprocessor Orchestrator](docs/06-preprocessor-orchestrator.md)

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Preprocessor                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐  │
│  │ IDirective   │  │ IResource    │  │ IMergeStrategy   │  │
│  │ Parser       │  │ Resolver     │  │                  │  │
│  └──────────────┘  └──────────────┘  └──────────────────┘  │
│          │                │                   │             │
│          ▼                ▼                   ▼             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │              ResourceDependencyGraph                │   │
│  │         (cycle detection, topological sort)         │   │
│  └─────────────────────────────────────────────────────┘   │
│                           │                                 │
│                           ▼                                 │
│  ┌─────────────────────────────────────────────────────┐   │
│  │                  PreprocessResult                   │   │
│  │    Content + SourceMap + Diagnostics + Graph        │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

## License

MIT License - see [LICENSE.txt](LICENSE.txt)
