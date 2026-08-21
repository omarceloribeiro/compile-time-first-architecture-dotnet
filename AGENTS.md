# AGENTS.md

These rules apply to every coding agent working in this repository.

## Mission

Deliver working, compiled and tested software with the smallest coherent architecture that preserves correctness, performance and evolvability.

## Mandatory workflow

1. Read this file.
2. Read `Architecture.md`.
3. Read the feature spec and related data specs.
4. Inspect nearby code and follow the existing pattern.
5. Implement the smallest complete change.
6. Restore, build and test.
7. Fix all introduced errors and relevant warnings.
8. Summarize changed files, decisions and validation commands.

Do not claim success without running the available build and tests.

## Core principles

- Simple First.
- Compile-Time First.
- Feature First.
- Strongly typed contracts.
- Explicit dependencies.
- No hidden persistence.
- One DbContext per operation.
- Writes are controlled; reads are flexible.

## Use cases

A use case represents one executable actor intention, not one database operation.

Each use-case production file normally contains:

- specific interface;
- request;
- result;
- small auxiliary contracts;
- implementation;
- private validation and helper methods.

All use-case interfaces implement `IUseCase`.
All implementations inherit `UseCaseBase<TRequest,TResult>`.
Implement `ExecuteCoreAsync`; do not replace the public pipeline.

Do not use MediatR unless an approved ADR explicitly introduces it.
Do not create `Command`, `Handler`, `Validator`, `Mapper` and `Repository` files mechanically.

## DbContext

Never inject a write `DbContext` directly into Blazor components, ViewModels or endpoints.

Write use cases inject:

```csharp
IDbContextFactory<SchoolDbContext>
```

Read code uses a read-only factory or approved read abstraction.

Each operation creates and disposes its own context.

## Reads

Use direct `IReadDb` queries for incidental UI needs:

- dropdowns;
- autocompletes;
- screen-specific lists;
- editor loading;
- simple grids;
- one-use projections.

Every incidental read terminates through `IReadQueryExecutor`. Do not call EF Core, OData or another
provider's terminal extensions from a ViewModel or component.

Use the terminal dictated by the control in the feature specification:

- lookup by identifier → `FirstOrDefaultAsync`;
- dropdown → `ToListAsync`;
- data grid, data table, result list, autocomplete or history → `ToPageAsync`;
- export → read/export use case.

The specification chooses the control. Do not replace a dropdown with an autocomplete, invent row
thresholds or add adaptive behavior unless the specification requires it.

Keep `IQueryable<T>` and the read scope in local variables. Compose and materialize the query before
the operation returns. Never store `IQueryable<T>`, `IReadDb`, a read scope or a DbContext as UI state,
and never bind a live query provider directly to a visual component. Execute count and page queries
sequentially when they share one DbContext.

Do not create one-use classes named `GetXFormUseCase`, `XDropdownService`, `XPageQuery` or `XReadModel` unless there is a demonstrated reuse or business reason.

Use a read use case for:

- dashboards;
- indicators;
- reports;
- exports;
- progress summaries;
- reusable or auditable business views.

## ViewModels

ViewModels may:

- query the read store;
- compose data for the screen;
- hold UI state;
- map UI models to use-case requests;
- invoke use cases.

ViewModels must not write through the read store or a write DbContext.

## Data modeling

Do not silently introduce or change:

- foreign keys;
- cardinalities;
- aggregate ownership;
- tenant boundaries;
- historical deletion rules;
- idempotency constraints;
- versioning rules.

Read related data specs first. When a structural decision is missing, document the options and request a decision before creating migrations.

## Portable LINQ

Queries shared by Interactive Server and WASM/OData must use the portable subset:

- `Where`;
- `Select`;
- `OrderBy` / `ThenBy`;
- `Skip` / `Take`;
- simple comparisons;
- explicitly supported string operations;
- approved navigation expressions.

Avoid provider-specific functions, local methods inside expression trees and complex grouping unless verified on both providers.

## Exports

Exports are business read use cases. Build a typed report model once and pass it to format-specific exporters. Exporters never query the database.

## Dependency injection

Prefer explicit registration. Do not introduce assembly scanning merely to avoid a short list of registrations.

Every executable composition root must enable `ValidateOnBuild` and `ValidateScopes`, expose the
repository's `--validate-di` mode, and opt in to `ValidateDependencyInjectionOnBuild`. Validate all
implementations marked by `IUseCase` or the composition root's `IViewModel`, plus Blazor constructor,
`@inject` and keyed-service dependencies. Do not use `SkipDependencyInjectionValidation` when
validating work. A build is not successful when the DI gate was bypassed or failed.

## Compile-time and AOT

Prefer:

- records and enums;
- nullable reference types;
- strongly typed IDs and requests;
- source-generated JSON/logging/regex where useful;
- explicit code over reflection.

Investigate trimming and AOT warnings. Do not suppress them without documenting why.

## Forbidden by default

- generic repository over EF Core;
- direct persistence from UI;
- service locator;
- dynamic dictionaries as application contracts;
- SQL/OData/GraphQL query strings assembled manually when a typed provider exists;
- business rules hidden in endpoints;
- business rules hidden in exporters;
- broad refactors unrelated to the current spec.
