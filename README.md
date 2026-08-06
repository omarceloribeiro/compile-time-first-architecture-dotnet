# Compile-Time First Architecture for .NET

An experimental, AI-friendly architecture for building strongly typed .NET systems with fewer accidental abstractions.

The central principle is simple:

> If an inconsistency can be detected at compile time, it should not wait until runtime.

This repository is a **v0 reference**, not a framework. It combines established .NET mechanisms into a predictable development model for humans and coding agents.

## Core model

### Writes

```text
View / ViewModel / Endpoint
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
ViewModel or endpoint
        ↓
IReadDbFactory
        ↓
IQueryable<T>
        ↓
EF Core locally or OData remotely
```

### Business reads

```text
ViewModel or endpoint
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
- a small number of predictable implementation paths;
- one cohesive file per use case;
- direct, typed LINQ for simple reads;
- build and test loops as part of agent execution.

## Principles

1. **Simple First** — abstractions must solve a current problem.
2. **Compile-Time First** — prefer strong types, generators and static validation.
3. **Feature First** — organize by business capability and actor, not technical type.
4. **Write Through Use Cases** — all state changes pass through explicit contracts.
5. **Read Directly When Incidental** — screen-specific reads may use the read store directly.
6. **Read Use Cases for Business Views** — dashboards, indicators, reports and exports are explicit use cases.
7. **Provider-Independent Reads** — the same portable LINQ may target EF Core on the server and OData from WebAssembly.
8. **AI Predictability** — minimize hidden conventions, reflection and one-use indirection.

## Repository structure

```text
AGENTS.md
Architecture.md
docs/
  HISTORY-AND-ANALYSIS.pt-BR.md
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
- a direct ViewModel read;
- a business read use case for a dashboard;
- an export use case whose formats share one typed report model.

The sample targets `.NET 10` and uses EF Core InMemory only for demonstration.

```bash
dotnet restore samples/SchoolManagement/CompileTimeFirst.Sample.sln
dotnet build samples/SchoolManagement/CompileTimeFirst.Sample.sln -c Release
dotnet run --project samples/SchoolManagement/src/CompileTimeFirst.Sample.Console
```

## What this architecture intentionally avoids

- generic repositories over EF Core;
- mandatory MediatR;
- command/query handlers for every trivial operation;
- mapping frameworks by default;
- assembly scanning as a default DI strategy;
- reflection-driven business rules;
- screen-specific query classes used only once;
- direct writes from UI, endpoints or ViewModels.

It does **not** reject DDD, CQRS, repositories or messaging categorically. It applies them only when their benefits exceed their cost.

## Status

`v0.1-draft` — suitable for discussion, experiments and a small pilot. Validate the patterns in a real application before treating them as settled guidance.

## License

MIT.
