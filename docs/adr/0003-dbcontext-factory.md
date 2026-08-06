# ADR 0003 — One DbContext per operation

## Status
Accepted.

## Decision
Use `IDbContextFactory<TContext>` in use cases and long-lived Blazor scopes. Create and dispose one context per operation.

## Rationale
A Blazor Interactive Server scoped service can live for the circuit and must not share a non-thread-safe DbContext across concurrent operations.
