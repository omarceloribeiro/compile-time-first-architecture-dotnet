# ADR 0002 — Read directly, write through use cases

## Status
Accepted for v0. **Amended by ADR 0007** - the ViewModel layer named in this decision no longer
exists. Read "from the ViewModel or endpoint" as "from the component or endpoint".

## Decision
Screen-specific incidental reads may query a read-only `IReadDb` from the ViewModel or endpoint. All writes pass through use cases. Business reads use read use cases.

## Consequences
The UI remains free to change its projections. The application preserves stable write contracts. Persistence safety is enforced at the write boundary.
