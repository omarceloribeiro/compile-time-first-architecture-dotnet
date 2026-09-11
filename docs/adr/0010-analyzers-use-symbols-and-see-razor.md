# ADR 0010 — Architecture analyzers use Roslyn symbols and analyze Razor-generated code

## Status

Accepted for v0.5. Completes an open item in ADR 0006.

## Context

Two defects were found by measurement, not by review.

First, the analyzer did not observe Razor-generated code. `ConfigureGeneratedCodeAnalysis` was set
to `None`, and Blazor components only exist as `*.razor.g.cs`, which Roslyn classifies as generated.
Every rule was therefore unenforced inside `.razor` files. The same violation — an `IQueryable`
stored as state — produced a build error in a `.cs` file and compiled with zero warnings inside an
`@code` block. With screen logic moving into components (ADR 0007), that gap would have covered
almost the entire read surface.

Second, rule scope was decided by `className.EndsWith("ViewModel")` plus a `ComponentBase` check.
String matching for type identity is forbidden by this repository's own rules, and removing the
ViewModel layer would have silently disabled the suffix half of that gate.

## Decision

A type is in analyzer scope when its base-type chain reaches
`Microsoft.AspNetCore.Components.ComponentBase`, compared with `SymbolEqualityComparer`. Class-name
suffixes do not determine scope.

Analyzers are configured with
`GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics`. Both flags are
required: `Analyze` alone runs the rules over generated code but still suppresses the diagnostics
they report there.

A new rule must be expressible through symbol comparison. A rule that can only be expressed by
matching a name, namespace or display string is not added; it is recorded as planned, with the
reason.

The rule set exists in three places that change together: the analyzer implementation with its
tests, `AnalyzerReleases.Unshipped.md`, and the catalogue in `docs/ANALYZER-RULES.md`.

## Consequences

- rules apply where the code actually is, and survive naming changes;
- analysis covers Razor-generated syntax trees, with diagnostics mapped back to the `.razor` source
  line;
- rules no longer cover non-Blazor classes that the suffix previously caught. That narrowing is
  deliberate and recorded, rather than discovered later;
- the regression is locked by tests that fail when either the flag or the symbol scoping is reverted.
