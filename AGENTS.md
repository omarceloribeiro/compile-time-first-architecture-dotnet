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
- Well-Known First.
- Compile-Time First.
- Feature First.
- Strongly typed contracts.
- Explicit dependencies.
- No hidden persistence.
- One DbContext per operation.
- Writes are controlled; reads are flexible.
- State lives in the component; there is no ViewModel layer.
- Page before materialization.

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

Never inject a write `DbContext` directly into Blazor pages, components or endpoints.

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

`IReadDb` and `IReadDbFactory` are intentional provider/lifetime boundaries. Preserve them even in
Server-only feature code; do not replace them with an EF-specific DbContext factory. They keep the
same feature compatible with operation-scoped EF and remote/OData read providers.

Every incidental read terminates through `IReadQueryExecutor`. Do not call EF Core, OData or another
provider's terminal extensions from a page or component.

Use the terminal dictated by the control in the feature specification:

- lookup by identifier → `FirstOrDefaultAsync`;
- dropdown → `ToListAsync`;
- data grid, data table, result list, autocomplete or history → `ToPageAsync`;
- export → read/export use case.

When an incidental read explicitly needs uniqueness, count or existence, use
`IReadQueryExecutor.SingleOrDefaultAsync`, `CountAsync` or `AnyAsync`. Never call the corresponding
EF Core terminal extensions directly from a page or component.

The specification chooses the control. Do not replace a dropdown with an autocomplete, invent row
thresholds or add adaptive behavior unless the specification requires it.

Every query passed to `ToPageAsync` must define deterministic ordering first. When the primary sort
key is not unique, add a stable unique tie-breaker such as `ThenBy(x => x.Id)`.

Keep `IQueryable<T>` and the read scope in local variables. Compose and materialize the query before
the operation returns. Never store `IQueryable<T>`, `IReadDb`, a read scope or a DbContext as component state,
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

## UI components

The component is the screen boundary. There is no ViewModel layer. Do not create a class named
`XViewModel`, and do not reintroduce the same layer under another name such as `XPageState`,
`XScreenModel` or `XPresenter`.

A page or component may:

- query the read store through `IReadDbFactory` and `IReadQueryExecutor`;
- hold screen state: filters, selection, paging position, loading flags, form models;
- map screen state to a use-case request;
- invoke use cases;
- translate expected application failures into user-visible messages.

A page or component must not:

- write through the read store or a write `DbContext`;
- hold `IQueryable<T>`, a read scope or a `DbContext` in a field or property;
- call an EF Core, OData or other provider terminal extension directly;
- own a rule that a second actor path also needs - that rule belongs in a use case.

When a screen stops fitting in one reader's head, extract a use case or a child component. Do not
extract a state class.

A support type used by a single screen - a row record, a select option, a form model - is declared
privately inside that screen. Promote it to the read model only when a second screen needs it.

### Code-behind placement

By default, **all code-behind for a page or component lives in the `@code` block of the same
`.razor` file**. Do not create a `.razor.cs` partial class.

Create a `.razor.cs` only when the feature specification asks for it explicitly, naming the file and
the reason. Absence of instruction means: single file.

This default is deliberate and reversible. It optimizes for one file per screen, so a human or an
agent loads exactly one file to understand a screen and a screen change is a single diff. It costs
C# editor tooling quality inside large `@code` blocks, and that trade-off is not considered settled.
If the cost becomes dominant, change it through a new ADR that supersedes ADR 0008 - not file by
file, and not by agent judgement.

## Multi-tenancy

Tenant isolation is a property of the model and the query filter. It is never a property of
remembering to filter.

- A tenant-owned entity carries `TenantId` and has a `(TenantId, Id)` alternate key.
- Every foreign key between tenant-owned entities is composite and includes `TenantId`, so a
  cross-tenant reference is not merely forbidden - it is unrepresentable in the database.
- Both contexts apply a named EF Core global query filter on `TenantId`, taken from the tenant the
  factory stamped onto the context for this operation.
- **Do not write a `TenantId` comparison in a page, component, endpoint or use-case query.** A
  hand-written tenant filter is a defect even when it is correct, because it is one more place that
  can be omitted with no compile error, no analyzer diagnostic and no failing test.
- An unresolved tenant reads nothing. Do not "fix" that by making the filter pass when the tenant is
  null - that turns a missing tenant into a cross-tenant read.
- `IgnoreQueryFilters([DomainModelConfiguration.TenantFilter])` is allowed only in a composition
  root, a seeder or a migration. Never in a component or a feature use case.
- A write use case never accepts `TenantId` in its request. The tenant comes from `ICurrentUser`;
  accepting it from the caller makes the caller the authority on isolation.
- The tenant accessor never throws when no tenant is resolved. Seeding, the dependency-injection
  gate and tests all run without one.

## Validation and errors

A request declares its own constraints with DataAnnotations. `UseCaseBase` validates the request
before `ExecuteCoreAsync` runs, so the contract and the validation are one artifact. Do not re-check
in the use case what the annotations already state; keep cross-field and asynchronous rules in
`ExecuteCoreAsync`.

Failures are typed application exceptions: a validation exception carrying the rejected rules, and a
not-found exception. A page catches them by type with a filtered `catch when` and renders the
message.

An infrastructure exception never becomes text on a screen. In the supported Blazor Server profile
it reaches the global interactive layout error boundary and is rendered generically. The
experimental Auto page owns a local interactive boundary because its host does not make the whole
layout interactive. If a condition deserves a specific message to the user, it deserves a typed
application exception.

## Time

Inject `TimeProvider` and read the current instant from it. `DateTime.Now`, `DateTime.UtcNow`,
`DateTimeOffset.Now` and `DateTimeOffset.UtcNow` are forbidden in domain, application and UI code.
Tests use `FakeTimeProvider`.

## Domain

An entity has a private parameterless constructor for materialization, private property setters and
public methods named after the behaviour they perform. A use case calls a behaviour method; a use
case that assigns properties one by one has moved a domain rule into the application layer.

A rule that both constructs and rejects - defaults an editor offers and constraints a write
enforces - lives once, in the application layer. The screen projects it. Keeping only the rejecting
half there would strand the constructing half in a disposable screen.

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

## Experimental render modes

Interactive Server is the default and the supported path.

Do not create, extend or wire any file, class, endpoint, controller, EDM registration, client
provider, project reference or render-mode attribute for Interactive Auto, WebAssembly or OData
unless the feature specification explicitly requests Interactive Auto and names what it needs.
Absence of instruction means Interactive Server only.

The supported Blazor profile lives in `CompileTimeFirst.Sample.BlazorServer`. It uses global
Interactive Server and must not reference the Auto host, WebAssembly client, OData packages or EDM.

The existing Auto/OData code is a reference to read, not a template to replicate. It stays in the
same solution so restore, analyzers, DI validation and tests cover it, but is physically isolated in
`CompileTimeFirst.Sample.BlazorAuto` and `CompileTimeFirst.Sample.BlazorAuto.Client`. The Auto host
exists because one component must exercise both Server and WebAssembly; that coverage does not
justify contaminating the supported Server composition root.

Known gaps in that path: generated-client metadata lifecycle, OData exposure beyond the sample's
small read surface, and trimming and AOT. The sample authenticates same-origin OData with the
Identity cookie and resolves its tenant from a server-issued claim, but the path remains
experimental and must not be presented as production-ready.

## Portable LINQ

The portable subset applies only to queries a specification has scoped for Interactive Auto. In
Interactive Server code, use the full provider surface the specification allows.

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
implementations marked by `IUseCase`, plus Blazor constructor, `@inject` and keyed-service
dependencies. There is no marker interface for UI services: with screen logic inside components,
`@inject` compiles to an `[Inject]` property and the gate already resolves it. Do not use `SkipDependencyInjectionValidation` when
validating work. A build is not successful when the DI gate was bypassed or failed.

## Compile-time and AOT

Prefer:

- records and enums;
- nullable reference types;
- strongly typed IDs and requests;
- source-generated JSON/logging/regex where useful;
- explicit code over reflection.

In analyzers, prefer Roslyn semantic symbols and `SymbolEqualityComparer` over syntax text,
display-name prefixes or other string matching whenever type or member identity is available.

Investigate trimming and AOT warnings. Do not suppress them without documenting why.

## Well-Known First

Prefer public, well-known APIs, protocols, types and conventions when they solve the current problem
adequately. They reduce the private context that humans and coding agents must load before changing
a feature.

Read `docs/WELL-KNOWN-FIRST.md` before introducing a cross-cutting infrastructure abstraction, UI
wrapper or private framework vocabulary.

Do not hide a suitable public abstraction behind a private wrapper unless the wrapper adds concrete
product semantics, enforces a real architectural boundary, isolates a provider-specific capability
that is needed now, or coordinates actual shared behavior.

Preferred public semantic surfaces include:

- `HttpClient`, HTTP, JSON, OpenAPI and OData;
- `IQueryable<T>`, `DbContext` and `IDbContextFactory<TContext>`;
- `ILogger<T>`, DataAnnotations, ASP.NET Core Identity, roles and policies;
- the selected UI library's public component APIs.

Before introducing a private abstraction, answer:

1. What concrete capability does it add?
2. What product meaning or architectural boundary does it express?
3. What current problem would exist if the public API were used directly?
4. Is that problem present now, rather than hypothetical?

Public documentation and the installed package version remain authoritative. Compile, analyze and
test the actual API usage; do not rely only on model familiarity or remembered examples.

Well-known does not mean automatically suitable. Evaluate security, maintenance, licensing,
compatibility, performance and API quality together with public familiarity.

Use ASP.NET Core Identity APIs directly for framework-owned sign-in, password, token, lockout,
claims/roles primitives and authentication state. Product operations such as invitation,
provisioning, tenant linkage, access activation/deactivation or eligibility remain application use
cases when they carry product semantics. Granting or revoking a product role is a product decision
even though its implementation uses public Identity APIs.

## Documentation style

The rules in this repository were validated by real production use. Describe that origin in neutral
terms only - "field use", "an application in production", "one real deployment". **Never name a
product, client, company, internal system or repository**, in code, documentation, ADRs, tests or
commit messages.

A learning is written as a rule plus the anti-pattern it prevents, never as a story about a specific
project. This is not enforced by CI on purpose: a denylist would have to contain the names it
forbids, so it would introduce the very reference it exists to prevent. It is a review rule.

## Forbidden by default

- a ViewModel class, or the same layer renamed;
- a `.razor.cs` partial that no specification asked for;
- unpaged terminals behind a grid, data table, result list, autocomplete or history;
- string matching in analyzers where a Roslyn symbol is available;
- generic repository over EF Core;
- private wrappers that only rename a suitable public API;
- direct persistence from UI;
- service locator;
- dynamic dictionaries as application contracts;
- SQL/OData/GraphQL query strings assembled manually when a typed provider exists;
- business rules hidden in endpoints;
- business rules hidden in exporters;
- broad refactors unrelated to the current spec.
