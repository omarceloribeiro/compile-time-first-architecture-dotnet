# ADR 0005 — Uniform query terminals and paged UI reads

## Status

Accepted for v0.3.

## Context

EF Core and browser OData providers compose queries through `IQueryable<T>`, but they do not share a
provider-neutral asynchronous terminal API. Binding a live query directly to a visual component also
extends the lifetime of its DbContext/provider and permits synchronous or concurrent enumeration.
Materializing an entire data set before a grid applies paging is not suitable for production.

## Decision

All incidental UI reads terminate through `IReadQueryExecutor`, regardless of render mode.
`IQueryable<T>` and the read scope remain local to the load operation and are materialized before it
returns. Visual components receive only materialized data.

The feature specification selects the UI control and therefore the terminal:

- lookup by identifier → `FirstOrDefaultAsync`;
- dropdown → `ToListAsync`;
- data grid, data table, result list, autocomplete or history → `ToPageAsync`;
- export → read/export use case.

The executor implementation is selected by dependency injection. EF Core and OData/HTTP may optimize
the same terminal differently. Count and page operations execute sequentially when they share one
DbContext.

## Consequences

- feature code does not depend on its current Server, WebAssembly or Auto render mode;
- queries and DbContexts cannot become long-lived component state;
- data components page before materialization;
- the architecture gains one small terminal abstraction instead of separate read rules per runtime;
- portable queries remain limited to the LINQ subset supported by every enabled provider;
- specifications, not agents, decide between dropdown, autocomplete and adaptive controls.
