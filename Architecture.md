# Architecture

## 1. Architectural intent

This architecture optimizes for four properties:

- predictable delivery;
- strong compile-time feedback;
- low accidental complexity;
- safe evolution by humans and AI agents.

It is a pragmatic vertical architecture. Features are grouped by actor and business capability. Use cases are the unit of execution; features are the unit of organization.

## 2. Decision tree

```text
Does the operation change state?
  Yes → Write Use Case
  No  → Does the read represent a business capability?
          Yes → Read Use Case
          No  → Direct read from IReadDb in the ViewModel or endpoint
```

Examples:

| Need | Pattern |
|---|---|
| Fill a subject dropdown | Direct read |
| Load a screen-specific grid | Direct read |
| Create a lesson | Write use case |
| Mark a lesson as delivered | Write use case |
| School dashboard | Read use case |
| Student progress summary | Read use case |
| Export assessment report | Read/export use case |

## 3. Write path

```text
UI / API
  → typed request
  → IUseCase
  → UseCaseBase<TRequest,TResult>
  → ExecuteCoreAsync
  → IDbContextFactory<WriteDbContext>
  → one DbContext per execution
```

A use case may coordinate many internal operations. Endpoint count follows actor intentions, not internal database steps.

## 4. Read path

### 4.1 Incidental UI reads

ViewModels may query `IReadDb` directly. This is intentionally coupled to the UI because the projection exists to serve that UI.

```text
ViewModel
  → IReadDbFactory
  → IReadDb
  → IQueryable<T>
```

The ViewModel may change when the screen changes. The application use case does not.

### 4.2 Business reads

Dashboards, indicators, home summaries, reports and progress views are business capabilities. They use typed read use cases.

```text
UI / API
  → Read Use Case
  → Read-only DbContext factory
  → typed result
```

## 5. MVVM boundary

The ViewModel is the system boundary. It may contain:

- loading state;
- selected values;
- dropdown collections;
- filters;
- pagination state;
- UI validation;
- mapping from screen model to use-case request.

The ViewModel must never persist directly.

## 6. Read-only context

The read context:

- defaults to `NoTracking`;
- rejects `SaveChanges`;
- may use a read-only database credential;
- may later point to a read replica;
- exposes only approved read surfaces.

## 7. Interactive Auto and OData

A shared `IReadDb` contract can be implemented by two providers:

```text
Interactive Server → EF Core IQueryable → SQL
Interactive WASM   → OData IQueryable   → HTTP → EF Core → SQL
```

Portable queries use the common subset of LINQ supported by both providers. Async terminal execution is abstracted by `IReadQueryExecutor`.

This is an optional advanced pattern. Validate OData, trimming and AOT compatibility in a dedicated spike before adopting it broadly.

## 8. Exports

Export is a business read use case:

```text
Export Use Case
  → authorization and filters
  → read model
  → typed report model
  → Excel / CSV / JSON / PDF exporter
```

Formats share one report definition. Exporters never query the database.

## 9. Data modeling

Agents must not invent persistent relationships silently. Domain and data specifications define:

- cardinalities;
- ownership;
- historical retention;
- tenant isolation;
- idempotency;
- versioning;
- delete/archive rules.

A functional spec defines behavior. A data spec defines shared persistence invariants.

## 10. AOT and trimming

Code should remain AOT- and trimming-friendly when practical:

- explicit DI registration;
- source-generated JSON where required;
- no unnecessary assembly scanning;
- no runtime code generation in business flows;
- warnings are investigated rather than suppressed blindly.

AOT compatibility is a design target, not a promise that every project must publish Native AOT immediately.

## 11. Dependency-injection build gate

Compilation alone does not prove that the runtime container can construct every use case, ViewModel
or Blazor injection. Executable composition roots therefore enable `ValidateOnBuild` and
`ValidateScopes`, implement a deterministic `--validate-di` mode and opt in to the repository's
post-build validation target.

The gate resolves marker-based services and inspects Blazor constructor, `[Inject]` and keyed
dependencies. Registration remains explicit; reflection validates the built graph and never performs
automatic service registration.
