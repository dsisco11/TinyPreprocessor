# Content Boundaries

TinyPreprocessor’s core pipeline is offset-based and content-agnostic.
When downstream users want higher-level concepts like “line numbers”, they can supply boundary resolvers.

## Core idea

A boundary is a consumer-defined “split point” in content.
A boundary resolver answers the question:

- “Which boundary offsets exist within the offset range `[startOffset, endOffset)` of this content instance?”

This is exposed via:

- `IContentBoundaryResolver<TContent, TBoundary>`
- `IContentBoundaryResolverProvider`

`TBoundary` is a marker type so downstream users can define their own boundary kinds.

## Line boundaries

The built-in text merge strategy uses the boundary kind `TinyPreprocessor.Text.LineBoundary` when it is available.
This allows users to define what a “line break” means for their content (LF, CRLF, custom tokens, etc.).

### Expected `LineBoundary` convention

For `LineBoundary`, a typical convention is:

- Each boundary offset represents the start offset of a line after the first line.
- In other words, boundary offsets are the offsets _immediately after_ a line break sequence.
- Offsets are 0-based and must be returned in ascending order.

Examples:

- LF (`\n`) text: boundary offsets are `indexOf('\n') + 1` for each line break.
- CRLF (`\r\n`) text: boundary offsets are `indexOf("\r\n") + 2` for each line break.
- Custom line breaks: boundary offsets are whatever “start of next line” means to you.

## Composing boundaries with SourceMap

`SourceMap` stays offset-only. To compute a “line number” (boundary index):

- Map generated offset → original `(ResourceId, OriginalOffset)` via `SourceMap.Query(...)`.
- Count how many `LineBoundary` offsets occur before that original offset.

See `SourceMapBoundaryExtensions.ResolveOriginalBoundaryLocation(...)` for a helper that performs this composition.
