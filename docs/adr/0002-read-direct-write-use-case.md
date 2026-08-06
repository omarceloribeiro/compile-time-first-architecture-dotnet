# ADR 0002 — Read directly, write through use cases

## Status
Accepted for v0.

## Decision
Screen-specific incidental reads may query a read-only `IReadDb` from the ViewModel or endpoint. All writes pass through use cases. Business reads use read use cases.

## Consequences
The UI remains free to change its projections. The application preserves stable write contracts. Persistence safety is enforced at the write boundary.
