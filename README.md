# Compile-Time First Architecture for .NET

An experimental, AI-friendly architecture for building strongly typed .NET systems with fewer accidental abstractions.

The central principle is simple:

> If an inconsistency can be detected at compile time, it should not wait until runtime.

This repository is a **v0.5 reference**, not a framework. It combines established .NET mechanisms into a predictable development model for humans and coding agents.

## Core model

### Writes

```text
Component / Page / Endpoint
        ↓
Strongly typed Request
        ↓
Use Case
        ↓
IDbContextFactory<WriteDbContext>
        ↓
Database
```

### Incidental reads

```text
Component or endpoint
        ↓
IReadDbFactory
        ↓
IQueryable<T>
        ↓
IReadQueryExecutor
        ↓
EF Core locally or OData remotely
```

### Business reads

```text
Component or endpoint
        ↓
Read Use Case
        ↓
Read-only DbContext
        ↓
Typed result
```

## Why this exists

AI coding agents become substantially more reliable when the repository offers:

- strong types instead of dictionaries and loose strings;
- explicit contracts instead of hidden pipelines;
- compile-time feedback instead of runtime discovery;
- well-known public constructs instead of private mechanical wrappers;
- a small number of predictable implementation paths;
- one cohesive file per use case;
- direct, typed LINQ for simple reads;
- build and test loops as part of agent execution.

## Principles

1. **Simple First** — abstractions must solve a current problem.
2. **Well-Known First** — prefer suitable public semantic surfaces over private mechanical wrappers.
3. **Compile-Time First** — prefer strong types, generators and static validation.
4. **Feature First** — organize by business capability and actor, not technical type.
5. **Write Through Use Cases** — product and domain state changes pass through explicit contracts.
6. **Read Directly When Incidental** — screen-specific reads may use the read store directly.
7. **Read Use Cases for Business Views** — dashboards, indicators, reports and exports are explicit use cases.
8. **Provider-Independent Reads** — the same portable LINQ may target EF Core on the server and OData from WebAssembly.
9. **Tenant Isolation Is Structural** — the model and the query filter enforce it, never a predicate someone has to remember.
10. **State Lives in the Component** — screens own their state; there is no ViewModel layer.
11. **AI Predictability** — minimize hidden conventions, reflection and one-use indirection.
12. **Page Before Materialization** — grids, data tables, result lists, autocompletes and histories terminate through `ToPageAsync`.

## Well-Known First

Public, well-known APIs act as a shared semantic surface for humans, tools and coding agents. Use
constructs such as `HttpClient`, `IQueryable<T>`, `IDbContextFactory<TContext>`, `ILogger<T>`,
DataAnnotations, ASP.NET Core policies, HTTP/JSON/OpenAPI/OData and the selected UI library directly
when they solve the problem adequately.

Create a private abstraction only when it adds current product semantics, policy, a concrete
architecture/lifecycle boundary, real provider variation or necessary external isolation. Public
documentation and the installed version remain authoritative; compiler, analyzer and test feedback
validate the actual use.

`IReadQueryExecutor` is the canonical justified example: LINQ remains the public query-composition
surface, while the executor owns the real async terminal difference between EF Core and browser
OData. The detailed guide shows how the same principle makes explicit UI-vendor usage easier for an
agent to understand and migrate. See [Well-Known First and semantic transparency](docs/WELL-KNOWN-FIRST.md)
and [ADR 0006](docs/adr/0006-well-known-first.md).

## Repository structure

```text
AGENTS.md
Architecture.md
CHANGELOG.md
docs/
  WELL-KNOWN-FIRST.md
  ANALYZER-RULES.md
  DEPENDENCY-INJECTION-VALIDATION.md
  SPEC-TEMPLATE.md
  DATA-SPEC-TEMPLATE.md
  adr/
patterns/
  Read-Pattern.md
  Write-Pattern.md
  Business-Read-Pattern.md
  Export-Pattern.md
  Interactive-Auto-OData-Pattern.md
samples/
  SchoolManagement/
```

## Sample

The sample demonstrates:

- `UseCaseBase<TRequest,TResult>` using the Template Method pattern;
- one production file per use case containing interface, request, result and implementation;
- `IDbContextFactory<TContext>` for one context per operation;
- a read-only EF Core context;
- an `IReadSchoolDb` surface based on `IQueryable<T>`;
- a direct component read with screen state owned by the component;
- one shared EF Core model configuration for the write and read contexts;
- encapsulated domain entities with behaviour methods and no public setters;
- tenant isolation by composite keys and a named global query filter, with no hand-written tenant predicate anywhere;
- paged incidental reads through the same executor contract in EF Core and browser OData;
- a business read use case for a dashboard;
- an export use case whose formats share one typed report model.

The sample targets `.NET 10` and uses SQLite in-memory for demonstration. A relational provider is
used deliberately: the composite foreign keys that make a cross-tenant reference impossible are only
enforced by a database that enforces foreign keys.

```bash
dotnet restore samples/SchoolManagement/CompileTimeFirst.Sample.sln
dotnet build samples/SchoolManagement/CompileTimeFirst.Sample.sln -c Release
dotnet run --project samples/SchoolManagement/src/CompileTimeFirst.Sample.Console
```

## Experimental: Interactive Auto, WebAssembly and OData

**Interactive Server is the supported path.** The Interactive Auto, WebAssembly and browser OData
code in the sample is experimental and is not a production-ready path.

It stays in the same solution on purpose: Interactive Auto exercises both Server and WebAssembly
from a single component, so a separate sample would duplicate hosts and lose that coverage. The
cost of keeping it is controlled by a rule rather than by isolation — nothing new is generated for
this path unless a feature specification explicitly asks for Interactive Auto. See
[`AGENTS.md`](AGENTS.md), section "Experimental render modes".

Validate before enabling it in production:

- authentication and authorization across the OData boundary;
- OData query limits and exposure of the read surface;
- trimming and AOT compatibility of the browser provider;
- tenant propagation, which crosses an HTTP boundary instead of a Blazor circuit.

## What this architecture intentionally avoids

- generic repositories over EF Core;
- mandatory MediatR;
- command/query handlers for every trivial operation;
- mapping frameworks by default;
- assembly scanning as a default DI strategy;
- reflection-driven business rules;
- wrappers that only rename suitable public APIs;
- screen-specific query classes used only once;
- direct writes from UI, components or endpoints;
- ViewModel classes, or the same layer renamed.

It does **not** reject DDD, CQRS, repositories or messaging categorically. It applies them only when their benefits exceed their cost.

## Status

`v0.5` — removes the ViewModel layer and moves screen state into the component; makes the `.razor`
file the default home for code-behind; makes Interactive Auto, WebAssembly and OData an experimental
path that only a specification may extend; makes the architecture analyzers scope by Roslyn symbol
and analyze Razor-generated code, which was previously unchecked; shares one EF Core model
configuration between the write and read contexts; encapsulates domain entities.

Validate provider-specific LINQ, installed API versions and production security constraints in each
application.

## License

MIT.
