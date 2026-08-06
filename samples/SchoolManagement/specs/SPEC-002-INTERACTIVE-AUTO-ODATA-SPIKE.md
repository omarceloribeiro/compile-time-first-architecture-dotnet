# SPEC-002 — Interactive Auto OData spike

## Actor

Developer evaluating provider-independent reads.

## Objective

Prove that one Blazor page and ViewModel can execute the same portable LINQ query through EF Core during Interactive Server execution and through Microsoft.OData.Client after Interactive Auto switches to WebAssembly.

## Flow

1. Open `/auto-subjects`.
2. The server render resolves the EF read provider and loads active subjects.
3. After the WebAssembly bundle is available, revisit the page without a full reload.
4. The client resolves the OData provider and executes the same `Where`, `OrderBy` and `Select` expression.
5. The page displays both the rows and the active provider name.

## Constraints

- OData exposes read-only DTOs, never EF entities.
- Only `$filter`, `$select`, `$orderby`, `$top`, `$skip` and `$count` are enabled.
- Server-driven page size and `MaxTop` are 100.
- `$expand` is not enabled.
- The client composes LINQ; it does not assemble OData query strings.

## Acceptance criteria

- [ ] The same component and ViewModel run in Server and WebAssembly.
- [ ] The browser calls `/odata/Subjects` through Microsoft.OData.Client.
- [ ] Filtering and ordering are visible in the emitted OData request.
- [ ] The server and client executors pass the same contract tests for supported terminal operations.
- [ ] Build, DI validation and tests pass.

## Outside the spike

Authentication, tenant filters, generated clients, offline support, production AOT guarantees and general exposure of every read surface.
