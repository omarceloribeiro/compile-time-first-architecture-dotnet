# ADR 0008 — Blazor code-behind lives in the `.razor` file by default

## Status

Accepted for v0.5. Explicitly provisional.

## Context

With the ViewModel layer removed (ADR 0007), screen logic needs a home. Two conventions are
available: the `@code` block of the `.razor` file, or a `.razor.cs` partial class.

`.razor.cs` gives better C# editor tooling — navigation, refactoring and analyzer surfacing behave
as they do in ordinary C# files. `@code` gives one file per screen: one thing to open, one diff to
review, one artifact to regenerate.

Neither is clearly better. What is clearly worse is having no stated default: agents and humans then
choose case by case, and the same repository ends up carrying both conventions, so a reader must
first discover which one a given screen used.

## Decision

The `@code` block of the `.razor` file is the default for every page and component.

A `.razor.cs` partial is created only when a feature specification asks for it explicitly, naming
the file and the reason. Absence of instruction means single file.

## Consequences

- one file per screen; predictable agent behaviour; no mixed convention inside one repository;
- degraded C# editor experience inside large `@code` blocks;
- a long `@code` block is a signal to extract a use case or a child component, not a signal to split
  the file — splitting would hide the size without reducing it.

This ADR records a default chosen for consistency, not a resolved trade-off. It should be revisited
through a superseding ADR when there is evidence — a measured size threshold, a tooling change, or
recorded friction — and not through case-by-case deviation.
