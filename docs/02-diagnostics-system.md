# Diagnostics System Architecture

This document describes the diagnostic collection and reporting system for TinyPreprocessor.

## Overview

The diagnostics system provides a structured way to collect, categorize, and report issues encountered during preprocessing. It follows the "collect all, decide later" pattern—processing continues on errors, and the caller decides how to handle the collected diagnostics.

## Types

### DiagnosticSeverity

Categorizes the importance of diagnostics.

```csharp
public enum DiagnosticSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}
```

**Severity Guidelines:**

- **Info**: Informational messages (e.g., "Resource loaded from cache")
- **Warning**: Non-fatal issues that may indicate problems (e.g., "Deprecated directive syntax")
- **Error**: Fatal issues that prevent correct output (e.g., "Circular dependency detected")

---

### IPreprocessorDiagnostic

Interface for all diagnostic messages.

```csharp
public interface IPreprocessorDiagnostic
{
    DiagnosticSeverity Severity { get; }
    string Code { get; }
    string Message { get; }
    ResourceId? Resource { get; }
    Range? Location { get; }
}
```

**Design Decisions:**

- **Code property**: Machine-readable identifier (e.g., "TPP0001") for filtering and documentation
- **Nullable Resource**: Some diagnostics are global (e.g., configuration errors)
- **Nullable Location**: Some diagnostics apply to entire resources, not specific ranges

**Code Convention:**

- Format: `TPP####` (TinyPreProcessor + 4-digit number)
- Ranges:
  - `TPP0001-TPP0099`: Dependency/graph errors
  - `TPP0100-TPP0199`: Resolution errors
  - `TPP0200-TPP0299`: Parse errors
  - `TPP0300-TPP0399`: Merge errors
  - `TPP0400-TPP0499`: Configuration/options errors

---

### Built-in Diagnostic Types

#### CircularDependencyDiagnostic

Reported when a cycle is detected in the resource dependency graph.

```
record CircularDependencyDiagnostic : IPreprocessorDiagnostic
    Severity = Error
    Code     = "TPP0001"
    Cycle    : IReadOnlyList<ResourceId>  // the cycle path
    Message  = "Circular dependency detected: A → B → C → A"
```

---

#### ResolutionFailedDiagnostic

Reported when a resource reference cannot be resolved.

```
record ResolutionFailedDiagnostic : IPreprocessorDiagnostic
    Severity  = Error
    Code      = "TPP0100"
    Reference : string         // the unresolved reference
    Reason    : string?        // optional failure reason
    Message   = "Failed to resolve reference: '{Reference}'"
```

---

#### ParseErrorDiagnostic

Reported when directive parsing fails.

```
record ParseErrorDiagnostic : IPreprocessorDiagnostic
    Severity = Error (configurable)
    Code     = "TPP0200"
    Message  : string  // the parse error description
```

---

#### MaxDepthExceededDiagnostic

Reported when include depth exceeds the configured maximum.

```
record MaxDepthExceededDiagnostic : IPreprocessorDiagnostic
    Severity     = Error
    Code         = "TPP0002"
    MaxDepth     : int  // configured limit
    CurrentDepth : int  // actual depth reached
    Message      = "Maximum include depth ({MaxDepth}) exceeded at depth {CurrentDepth}"
```

---

### DiagnosticCollection

Thread-safe collection for accumulating diagnostics during processing.

```
class DiagnosticCollection : IReadOnlyCollection<IPreprocessorDiagnostic>
    // Thread-safe storage with lock-based synchronization

    Properties:
        Count          → number of diagnostics
        HasErrors      → true if any Error severity exists
        HasWarnings    → true if any Warning severity exists

    Methods:
        Add(diagnostic)              → append single diagnostic
        AddRange(diagnostics)        → append multiple diagnostics
        GetByResource(resourceId)    → filter by source resource
        GetBySeverity(severity)      → filter by severity level
        GetEnumerator()              → returns snapshot for thread-safe iteration
```

**Design Decisions:**

- **Lock-based**: Simple and correct; preprocessing is not typically lock-contention-heavy
- **Snapshot on enumerate**: Prevents collection-modified exceptions
- **Query methods**: Convenience for filtering by resource or severity

---

## Usage Patterns

### Adding Diagnostics During Processing

```
diagnostics = new DiagnosticCollection()

// During resolution
result = await resolver.ResolveAsync(reference, currentResource, ct)
if not result.IsSuccess:
    diagnostics.Add(result.Error)
    // Continue processing other directives

// During cycle detection
for each cycle in graph.DetectCycles():
    diagnostics.Add(new CircularDependencyDiagnostic(cycle))
```

### Checking Results

```
result = await preprocessor.ProcessAsync(root, context, ct)

if result.Diagnostics.HasErrors:
    for each error in result.Diagnostics.GetBySeverity(Error):
        print "[{error.Code}] {error.Resource}: {error.Message}"
    return

// Use result.Content
```

---

## Relationships

```mermaid
flowchart TB
    DiagnosticCollection -->|contains| IPreprocessorDiagnostic
    IPreprocessorDiagnostic <|-- CircularDependencyDiagnostic
    IPreprocessorDiagnostic <|-- ResolutionFailedDiagnostic
    IPreprocessorDiagnostic <|-- ParseErrorDiagnostic
    IPreprocessorDiagnostic <|-- MaxDepthExceededDiagnostic
```

## Extension Points

1. **Custom Diagnostics**: Implement `IPreprocessorDiagnostic` for domain-specific issues
2. **Diagnostic Formatters**: Create formatters for different output styles (JSON, SARIF, etc.)
3. **Diagnostic Filters**: Build filter chains for suppressing specific codes or severities
