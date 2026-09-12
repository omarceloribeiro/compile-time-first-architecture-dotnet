# Full Blazor WebAssembly — future direction

> **Status: future design note.** This document preserves an exploration to resume after the
> Interactive Server architecture is complete. It is not an ADR, does not make WebAssembly a
> supported path, and does not change the experimental status of Interactive Auto/OData.

## Strategic placement

The intended paths have different purposes:

| Path | Intended role |
| --- | --- |
| Full Blazor Server / global Interactive Server | Primary architecture for new Blazor applications |
| Interactive Auto/OData | Experimental laboratory for runtime portability and remote `IQueryable` |
| Full Blazor WebAssembly | Future client-centric architecture built from the validated Auto/OData lessons |
| Full HTTP API + React or Angular | Future non-Blazor presentation over the same application use cases |

Interactive Auto is not the foundation that every application must adopt. It is the experiment that
proves which contracts and queries can cross an HTTP boundary. Full WebAssembly can later specialize
those findings without carrying the complexity of switching render modes.

## Single runtime model

Unlike Interactive Auto, a Full WebAssembly application has one UI runtime:

```text
Full Blazor WebAssembly
├── UI always runs in the browser
├── reads always cross HTTP through OData or typed API endpoints
├── writes always cross HTTP through use-case endpoints
├── authentication is established on HTTP requests
└── the application has one interactive component tree
```

There is no Server-to-WebAssembly transition, interactive circuit, provider switch during the
component lifetime, or prerendered-state handoff to coordinate. If prerendering is introduced later,
it must be treated as an explicit extension rather than assumed by this baseline.

## Architectural shape

The HTTP API is another presentation layer for the existing application use cases:

```text
Blazor WebAssembly ── HTTP/JSON ──► API presentation
                                         │
                                         ▼
                                   Application use case
                                         │
                                         ▼
                              IDbContextFactory<SchoolDbContext>
                                         │
                                         ▼
                                      Database
```

The WebAssembly project does not reference EF Core, a database context or server use-case
implementations. It uses typed HTTP/OData clients. Controllers or Minimal API handlers authenticate,
bind transport input, invoke one use case and translate its result into HTTP. They do not own business
rules or write directly through a `DbContext`.

The same Application and Data layers can serve a future React or Angular client. Only the client-side
presentation and generated HTTP client change.

## Reads and writes

Writes remain controlled. Each endpoint represents an actor intention and invokes one write use case.
The use case creates and disposes one context through `IDbContextFactory<SchoolDbContext>`, preserving
its explicit unit-of-work and tenant boundary.

Reads remain flexible within an approved surface:

- incidental screen reads compose the portable `IQueryable` subset over typed OData read models;
- every list, grid, autocomplete and history is paged and materialized through `IReadQueryExecutor`;
- dashboards, indicators, reports, exports and reusable or auditable views remain read use cases;
- the API exposes approved read DTOs, never EF entities or an unrestricted database model;
- the server applies authentication, tenant isolation and query limits before executing client query
  options.

This allows a human or coding agent to create new screens without adding one server endpoint per
screen when the required data is already present in the approved read model. Missing data or new
business meaning requires an explicit server-side design change.

## Error model

A single interactive tree makes client error handling more stable than the current per-page Auto
spike. A future baseline should define:

```text
Expected use-case rejection  ──► typed ProblemDetails ──► specific screen message
401 or 403                   ──► global authentication/authorization flow
Timeout, offline or retry    ──► centralized transport state
Unexpected API failure       ──► generic ProblemDetails + correlation identifier
Unexpected component failure ──► global interactive ErrorBoundary + logging
```

The global boundary must never render exception messages or stack traces. API logs retain diagnostic
details; the client receives a stable public error contract. Expected failures remain typed and are
not converted into infrastructure errors.

## Authentication and tenancy

The server remains the authority for identity and tenant scope. A same-origin deployment may use an
Identity cookie. A separately hosted client may require an OIDC/OAuth flow or a BFF. In either case,
the API resolves a server-issued tenant claim into `ICurrentUser`; the client never chooses or sends
an authoritative `TenantId` in a use-case request.

## Questions to resolve when work resumes

Before this direction becomes supported, decide and validate:

- standalone versus hosted deployment and whether prerendering is required;
- cookie/BFF versus OIDC/OAuth authentication;
- generated OpenAPI and OData client metadata lifecycle;
- the approved OData entity sets, properties, functions and query-complexity limits;
- global error handling, correlation and observability contracts;
- offline, retry, cancellation and stale-client-version behavior;
- trimming and AOT publishing;
- analyzer and test coverage for the portable LINQ subset;
- equivalent tenant-isolation and authorization tests across every HTTP read and write path.

Until those decisions are made, Interactive Server remains the supported implementation path and the
existing Interactive Auto/OData code remains an experimental reference rather than a template to
replicate automatically.
