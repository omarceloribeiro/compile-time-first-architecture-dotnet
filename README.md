# Compile-Time First Architecture for .NET

An experimental, AI-friendly architecture for building strongly typed .NET systems with fewer accidental abstractions.

The central principle is simple:

> If an inconsistency can be detected at compile time, it should not wait until runtime.

This repository is a **v0.5 reference**, not a framework. It combines established .NET mechanisms into a predictable development model for humans and coding agents.

## What this repository proves

Three guarantees, each enforced by a mechanism and locked by a test that fails when the mechanism is
reverted. Clone and run them:

```bash
dotnet test samples/SchoolManagement/CompileTimeFirst.Sample.sln -c Release
```

### The rules apply inside `.razor`, not only inside `.cs`

Screen state and reads live in components, so `.razor` is where nearly all read code is. Until
`v0.5` the analyzers never reached it: the same violation was an `error` in a `.cs` file and
compiled with zero warnings inside an `@code` block, because Roslyn classifies Razor-generated
`*.razor.g.cs` as generated code. The rules existed and enforced nothing where it mattered.

`CTFA004 — A component cannot store IQueryable, a read scope or a DbContext as state` now fires in
both places. Two things make that true:

- `GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics`. Both flags
  are required — `Analyze` on its own runs the rules over generated code but still suppresses the
  diagnostics they report there;
- scope is the `ComponentBase` symbol, never a class-name suffix, so renaming a type cannot move it
  out of enforcement.

Locked by `Razor_generated_component_is_analyzed` and `Component_marked_as_generated_code_is_analyzed`;
the second marks the class as generated code and still demands the diagnostic. A rule that holds in
`.cs` and not in `.razor` is a rule that does not hold.

See [analyzer rules](docs/ANALYZER-RULES.md) and [ADR 0010](docs/adr/0010-analyzers-use-symbols-and-see-razor.md).

### A cross-tenant reference is unrepresentable, not merely forbidden

Reads are covered by a named global query filter declared once in a shared model configuration, so
no feature code carries a hand-written tenant predicate — there is not one in the sample. Writes are
covered by composite foreign keys carrying `TenantId` and pointing at a `(TenantId, Id)` alternate
key, which makes the invalid reference impossible to store rather than merely against the rules.

Four tests carry the guarantee:

| Test | What it proves |
|---|---|
| `Cross_tenant_reference_is_rejected_by_the_database` | linking a question to another tenant's subject throws `DbUpdateException` |
| `Query_without_a_tenant_predicate_cannot_see_another_tenant` | a query with no tenant predicate of its own still cannot read across tenants |
| `Unresolved_tenant_reads_nothing` | an unresolved tenant denies everything rather than revealing everything |
| `Model_cache_does_not_freeze_the_first_tenant` | the filter reads the tenant per query; EF caches the model per context type, so a captured value would serve the first tenant's rows to everyone afterwards |

The sample runs on a relational provider for this reason: composite foreign keys are only enforced
by a database that enforces foreign keys, so the in-memory provider could not demonstrate the
guarantee the architecture claims.

The sample closes the tenant-propagation gap for its experimental same-origin OData path: ASP.NET
Core Identity emits the account's tenant as a server-owned claim, and the OData request carries the
same authentication cookie. The browser never supplies a tenant identifier. See
[ADR 0012](docs/adr/0012-tenant-isolation-with-named-query-filters.md).

### The build is the gate, so an agent cannot finish while violating the architecture

The feedback loop of a coding agent is the compiler. Every rule above fails the build rather than
waiting for a review comment:

- `TreatWarningsAsErrors` for every project in the repository;
- the five `CTFA` rules pinned to `error` severity in `.editorconfig`;
- the dependency-injection gate — after each composition root builds, the freshly compiled
  executable is *run* with `--validate-di`, failing the build when the graph cannot be constructed,
  so a missing registration is a build failure instead of a first-request one;
- a CI job that fails if the removed ViewModel layer reappears, or if any documented path stops
  existing.

See [the dependency-injection build gate](docs/DEPENDENCY-INJECTION-VALIDATION.md).

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
- a supported Blazor Server host with global Interactive Server and one interactive layout error
  boundary;
- an experimental Auto/OData spike isolated in its own host and WebAssembly client projects.

The sample targets `.NET 10` and uses SQLite in-memory for demonstration.

```bash
dotnet restore samples/SchoolManagement/CompileTimeFirst.Sample.sln
dotnet build samples/SchoolManagement/CompileTimeFirst.Sample.sln -c Release
dotnet test samples/SchoolManagement/CompileTimeFirst.Sample.sln -c Release --no-build
dotnet run --project samples/SchoolManagement/src/CompileTimeFirst.Sample.BlazorServer
dotnet run --project samples/SchoolManagement/src/CompileTimeFirst.Sample.Console
```

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
LICENSE
README.pt-BR.md
Directory.Build.props
Directory.Build.targets
.editorconfig
.github/
  workflows/
docs/
  WELL-KNOWN-FIRST.md
  ANALYZER-RULES.md
  DEPENDENCY-INJECTION-VALIDATION.md
  SPEC-TEMPLATE.md
  DATA-SPEC-TEMPLATE.md
  adr/
eng/
patterns/
  Read-Pattern.md
  Write-Pattern.md
  Business-Read-Pattern.md
  Export-Pattern.md
  Interactive-Auto-OData-Pattern.md
samples/
  SchoolManagement/
```

This repository is intentionally .NET-specific. Future ASP.NET Core API, Razor Pages and MVC
profiles may be sibling projects that share the same core class libraries. Java, Rust, Go and Python
profiles belong in their own repositories so each can use its idiomatic language rules, build gate
and toolchain instead of imitating .NET structure.

## Experimental: Interactive Auto, WebAssembly and OData

**`CompileTimeFirst.Sample.BlazorServer` is the supported path.** It uses global Interactive Server;
its layout and generic `ErrorBoundary` form one interactive tree, and the project has no OData or
WebAssembly dependency.

Interactive Auto, WebAssembly and browser OData remain experimental. They stay in the same solution
for build and test coverage but are physically isolated in `CompileTimeFirst.Sample.BlazorAuto` and
`CompileTimeFirst.Sample.BlazorAuto.Client`. One component still exercises both Server and
WebAssembly execution without coupling the supported host to the spike. Nothing new is generated
for this path unless a feature specification explicitly asks for Interactive Auto. See [`AGENTS.md`](AGENTS.md),
section "Experimental render modes", and [ADR 0009](docs/adr/0009-experimental-render-modes.md).

Already validated by the sample:

- same-origin ASP.NET Core Identity authentication and authorization;
- tenant propagation through the server-issued cookie claim.

Still validate before enabling it in production:

- OData query limits and exposure of the read surface;
- generated-client metadata lifecycle;
- trimming and AOT compatibility of the browser provider.

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
file the default home for code-behind; makes global Interactive Server the supported Blazor profile
and physically isolates the specification-gated Auto/OData spike; makes the architecture analyzers scope by Roslyn symbol
and analyze Razor-generated code, which was previously unchecked; shares one EF Core model
configuration between the write and read contexts; encapsulates domain entities; and binds each
seeded ASP.NET Core Identity account to one tenant for both server and same-origin OData reads.

Validate provider-specific LINQ, installed API versions and production security constraints in each
application.

## License

MIT.
