# Interactive Auto + OData Pattern

## Goal
Allow a shared ViewModel to compose the same portable LINQ query in Interactive Server and Interactive WebAssembly.

## Providers

```text
Server implementation: EF Core IQueryable
Client query provider: Microsoft.OData.Client IQueryable
Client transport/materialization: browser HttpClient + System.Text.Json
Terminal contract: IReadQueryExecutor
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

`IReadQueryExecutor` is the standard incidental-read terminal in every render mode. The client OData
provider remains optional and must be enabled only after the required spike.

## Validated sample boundary

The School Management sample includes `/auto-subjects`, a shared Interactive Auto component and
ViewModel. The server resolves an EF Core read provider and the WebAssembly client resolves a
Microsoft.OData.Client provider. Both execute the same portable `Where` and `OrderBy` query through
`IReadQueryExecutor`. In WebAssembly, Microsoft.OData.Client translates LINQ into the OData URI and
the browser `HttpClient` asynchronously downloads and materializes the JSON response. This avoids
the synchronous response-enumeration path that is incompatible with the single-threaded browser runtime.

The spike validates provider switching, typed LINQ translation, async execution, query-option limits
and DI hydration. Authentication, tenant filtering, generated-client metadata lifecycle and AOT
publishing remain explicitly outside the spike.

Paged controls use `ToPageAsync`. The client sends `$count`, `$skip` and `$top`, while the server
executor uses EF Core `CountAsync`, `Skip`, `Take` and `ToListAsync`. In either runtime, the query and
read scope are disposed before the load operation returns.
