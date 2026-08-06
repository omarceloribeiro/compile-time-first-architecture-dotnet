# Interactive Auto + OData Pattern

## Goal
Allow a shared ViewModel to compose the same portable LINQ query in Interactive Server and Interactive WebAssembly.

## Providers

```text
Server implementation: EF Core IQueryable
Client implementation: Microsoft.OData.Client IQueryable
```

## Required spike

Before production use, verify:

- generated OData client and metadata lifecycle;
- async query terminal operations;
- authentication and same-origin proxying;
- tenant filters applied before client query options;
- allowed query options and complexity limits;
- no `$expand` by default;
- server-driven pagination;
- portable LINQ subset;
- AOT/trimming behavior;
- Interactive Auto service registration and hydration.

This pattern is intentionally optional in v0.

## Validated sample boundary

The School Management sample includes `/auto-subjects`, a shared Interactive Auto component and
ViewModel. The server resolves an EF Core read provider and the WebAssembly client resolves a
Microsoft.OData.Client provider. Both execute the same portable `Where` and `OrderBy` query through
`IReadQueryExecutor`.

The spike validates provider switching, typed LINQ translation, async execution, query-option limits
and DI hydration. Authentication, tenant filtering, generated-client metadata lifecycle and AOT
publishing remain explicitly outside the spike.
