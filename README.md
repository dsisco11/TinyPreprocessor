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

TinyPreprocessor is a small pipeline that:

1. Parses directives from each resource.
2. Uses `IDirectiveModel<TDirective>` to decide which directives represent dependencies (and where they are).
3. Resolves dependencies via `IResourceResolver<TContent>` (building a dependency graph).
4. Topologically orders resources (dependencies first).
5. Merges them via `IMergeStrategy<TContent, TDirective, TContext>` while building a source map and collecting diagnostics.

TinyPreprocessor requires five components:

1. **`IDirectiveParser<TContent, TDirective>`** – Parses directives from resource content
2. **`IDirectiveModel<TDirective>`** – Interprets directive locations and dependency references
3. **`IResourceResolver<TContent>`** – Resolves references to actual resources
4. **`IMergeStrategy<TContent, TDirective, TContext>`** – Combines resources into final output
5. **`IContentModel<TContent>`** – Defines how offsets and slicing work for your `TContent`

### Example: Minimal In-Memory Includes

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TinyPreprocessor;
using TinyPreprocessor.Core;
using TinyPreprocessor.Diagnostics;
using TinyPreprocessor.Text;

// 1) Define your directive type.
public sealed record IncludeDirective(string Reference, Range Location);

// 2) Provide directive semantics to the pipeline.
public sealed class IncludeDirectiveModel : IDirectiveModel<IncludeDirective>
{
    public Range GetLocation(IncludeDirective directive) => directive.Location;

    public bool TryGetReference(IncludeDirective directive, out string reference)
    {
        reference = directive.Reference;
        return true;
    }
}

// 3) Implement a tiny directive parser for lines like: #include other.txt
public sealed class IncludeParser : IDirectiveParser<ReadOnlyMemory<char>, IncludeDirective>
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

// 4) Implement an in-memory resolver.
public sealed class InMemoryResolver : IResourceResolver<ReadOnlyMemory<char>>
{
    private readonly IReadOnlyDictionary<ResourceId, string> _files;

    public InMemoryResolver(IReadOnlyDictionary<ResourceId, string> files) => _files = files;

    public ValueTask<ResourceResolutionResult<ReadOnlyMemory<char>>> ResolveAsync(
        string reference,
        IResource<ReadOnlyMemory<char>>? context,
        CancellationToken ct)
    {
        if (!_files.TryGetValue(new ResourceId(reference), out var content))
        {
            return ValueTask.FromResult(new ResourceResolutionResult<ReadOnlyMemory<char>>(
                null,
                new ResolutionFailedDiagnostic(reference, $"Not found: {reference}")));
        }

        var resource = new Resource<ReadOnlyMemory<char>>(reference, content.AsMemory());
        return ValueTask.FromResult(new ResourceResolutionResult<ReadOnlyMemory<char>>(resource, null));
    }
}

// 5) Wire everything together.
var files = new Dictionary<ResourceId, string>
{
    ["main.txt"] = "#include a.txt\nMAIN\n",
    ["a.txt"] = "A\n#include b.txt\n",
    ["b.txt"] = "B\n"
};

var parser = new IncludeParser();
var directiveModel = new IncludeDirectiveModel();
var resolver = new InMemoryResolver(files);
var merger = new ConcatenatingMergeStrategy<IncludeDirective, object>();
var contentModel = new ReadOnlyMemoryCharContentModel();
var context = new object();
var config = new PreprocessorConfiguration<ReadOnlyMemory<char>, IncludeDirective, object>(
    parser,
    directiveModel,
    resolver,
    merger,
    contentModel);
var preprocessor = new Preprocessor<ReadOnlyMemory<char>, IncludeDirective, object>(config);
var root = new Resource<ReadOnlyMemory<char>>("main.txt", files["main.txt"].AsMemory());

var result = await preprocessor.ProcessAsync(root, context);

if (!result.Diagnostics.HasErrors)
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

// Find where generated offset 0 in output came from
var location = result.SourceMap.Query(generatedOffset: 0);

if (location is not null)
{
    Console.WriteLine($"Originated from {location.Resource.Path} at original offset {location.OriginalOffset}");
}

// For precise diagnostic spans, query a range.
// The range may map to multiple original resources (e.g., it crosses file boundaries).
var ranges = result.SourceMap.QueryRangeByLength(generatedStartOffset: 0, length: 20);

foreach (var range in ranges)
{
    Console.WriteLine(
        $"Generated [{range.GeneratedStartOffset} - {range.GeneratedEndOffset}) -> {range.Resource.Path} [{range.OriginalStartOffset} - {range.OriginalEndOffset})");
}
```

## Custom Merge Strategy

Implement `IMergeStrategy<TContent, TDirective, TContext>` for custom output formatting:

```csharp
public sealed record JsonMergeOptions;

public sealed class JsonMergeStrategy : IMergeStrategy<ReadOnlyMemory<char>, IncludeDirective, JsonMergeOptions>
{
    public ReadOnlyMemory<char> Merge(
        IReadOnlyList<ResolvedResource<ReadOnlyMemory<char>, IncludeDirective>> orderedResources,
        JsonMergeOptions userContext,
        MergeContext<ReadOnlyMemory<char>, IncludeDirective> mergeContext)
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
│  ┌──────────────┐  ┌────────────────────┐  ┌──────────────────┐  │
│  │ Directive    │  │ IResourceResolver  │  │ IMergeStrategy   │  │
│  │ Parser/Model │  │                   │  │                  │  │
│  └──────────────┘  └────────────────────┘  └──────────────────┘  │
│  ┌────────────────────┐                                         │
│  │ IContentModel       │                                         │
│  └────────────────────┘                                         │
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
