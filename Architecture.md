# Architecture

## 1. Architectural intent

This architecture optimizes for five properties:

- predictable delivery;
- strong compile-time feedback;
- low accidental complexity;
- low private context debt;
- safe evolution by humans and AI agents.

It is a pragmatic vertical architecture. Features are grouped by actor and business capability. Use cases are the unit of execution; features are the unit of organization.

## 2. Well-Known First

Prefer public, well-known APIs, protocols, types and conventions when they solve the problem
adequately. Do not hide a suitable public abstraction behind a private abstraction unless the
private abstraction adds concrete product semantics or owns a real boundary that exists now.

See [`docs/WELL-KNOWN-FIRST.md`](docs/WELL-KNOWN-FIRST.md) for the detailed rationale, private-language
cost model, semantic-transparency rule and public-to-public UI migration example.

The **public semantic surface** is the part of the system whose meaning is already documented and
recognized outside this repository. In this profile it includes constructs such as `HttpClient`,
HTTP, JSON, OpenAPI, `IQueryable<T>`, `DbContext`, `IDbContextFactory<TContext>`, `ILogger<T>`,
DataAnnotations, ASP.NET Core Identity and authorization policies, OData and the selected UI
library's public components.

Using those constructs directly reduces **context debt**: the private knowledge a human or agent
must load before it can modify the system correctly. The intended balance is:

```text
public mechanics
  + small, explicit private product semantics
  + compile-time and build-time validation
  = a low-context architecture for humans and AI agents
```

A private abstraction is justified when it adds at least one current, concrete value:

- product or domain meaning;
- an architectural or lifecycle boundary;
- policy that must be applied consistently;
- multiple providers that are actually supported;
- isolation of a specific external capability;
- shared behavior beyond forwarding or renaming calls.

Examples may include `ICurrentUser`, `IEmailService`, an enrollment policy or a client named after a
specific external business capability. Their names carry application meaning that `HttpClient`, a
database API or a generic gateway does not.

“Reduce coupling”, “future flexibility”, “best practice” or “clean architecture” are not sufficient
alone. The abstraction must name the present problem it solves. Public documentation and the
installed version remain authoritative; model familiarity is useful context, not a substitute for
compilation, analyzers, tests or version verification.

Well-known is not synonymous with popular or automatically suitable. Dependency selection still
considers security, maintenance, license, compatibility, performance and API quality. Public
familiarity is an additional architectural criterion because it improves documentation reach,
tooling support, agent recognition and migration discoverability.

Well-Known First also supports **architecture compression**: reduce the number of private concepts
that must be understood to implement a feature correctly. Compression removes accidental
vocabulary; it does not remove product semantics or necessary boundaries.

This principle applies differently at each boundary:

- **Reads:** compose with the public `IQueryable<T>` and standard LINQ surface. `IReadDb` and
  `IReadDbFactory` define the approved provider-independent read surface and its operation-scoped
  lifetime across EF Core and remote/OData providers. `IReadQueryExecutor` owns the separate async
  terminal/materialization boundary because those providers do not share a provider-neutral async
  terminal API.
- **UI:** use Blazor and the selected component library directly. Product components are valid when
  they express concepts such as enrollment or attendance; mechanical `BaseGrid` or `BaseButton`
  wrappers are not the default. A design system documents approved use of the public library and
  product tokens; it does not exist merely to hide the vendor.
- **Dependency injection:** use the platform container and explicit registrations directly.
  Validation may inspect the graph but does not introduce a private service-locator vocabulary.
- **Integrations:** use `HttpClient`, HTTP, JSON and generated/public protocol contracts directly
  until a product-specific client is needed to own authentication, normalization, versioning or
  another concrete integration behavior.
- **Authentication:** use ASP.NET Core Identity types and APIs directly for framework-owned sign-in,
  password, token, lockout, claims/roles primitives and authentication state. Product operations
  such as invitation, provisioning, tenant linkage, access activation/deactivation or eligibility
  remain application use cases when they carry product semantics.

Explicit vendor coupling localized to a regenerable boundary can be cheaper to understand and
migrate than artificial independence expressed through private wrappers. Protect durable product
knowledge, not regenerable implementation.

## 3. Decision tree

```text
Does the operation change product/application state?
  Yes → Write Use Case
  No  → Does the read represent a business capability?
          Yes → Read Use Case
          No  → Direct read from IReadDb in the ViewModel or endpoint
```

Examples:

| Need | Pattern |
|---|---|
| Fill a subject dropdown | Direct read + `ToListAsync` |
| Load a screen-specific grid | Direct read + `ToPageAsync` |
| Create a lesson | Write use case |
| Mark a lesson as delivered | Write use case |
| School dashboard | Read use case |
| Student progress summary | Read use case |
| Export assessment report | Read/export use case |

## 4. Write path

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

## 5. Read path

### 5.1 Incidental UI reads

ViewModels may query `IReadDb` directly. This is intentionally coupled to the UI because the projection exists to serve that UI.

```text
ViewModel
  → IReadDbFactory
  → IReadDb
  → local IQueryable<T>
  → IReadQueryExecutor
  → materialized UI state
```

`IReadDb` and `IReadDbFactory` are justified boundaries under Well-Known First. They expose only the
approved provider-independent read surface and own its operation-scoped creation/lifetime across EF
Core and remote/OData implementations. Replacing them in Server code with
`IDbContextFactory<ReadOnlyDbContext>` would leak an EF-only construction contract into feature code
and break the same ViewModel's Server, WebAssembly and Interactive Auto portability.

They do not replace the public query language: the ViewModel still composes `IQueryable<T>` with
standard LINQ. `IReadQueryExecutor` owns only the separate async terminal and materialization
boundary.

The ViewModel may change when the screen changes. The application use case does not.

`IReadQueryExecutor` is the terminal path for every incidental read. The ViewModel does not call
EF Core, OData or another provider's terminal extensions directly. Dependency injection selects the
executor implementation for the current runtime.

Use the terminal selected by the feature specification:

| Specified UI need | Terminal |
|---|---|
| Lookup by identifier | `FirstOrDefaultAsync` |
| Dropdown | `ToListAsync` |
| Data grid or data table | `ToPageAsync` |
| Result list | `ToPageAsync` |
| Autocomplete | `ToPageAsync` |
| History | `ToPageAsync` |
| Export | Read/export use case |

The specification chooses the control. An agent does not replace a dropdown with an autocomplete,
invent row thresholds or add adaptive behavior unless the feature specification requests it.

The query and read scope are operation-local:

```text
open read scope
  → compose IQueryable
  → execute through IReadQueryExecutor
  → materialize result
  → dispose read scope
```

Never store an `IQueryable<T>`, read scope or DbContext in ViewModel/component state. Never pass a
live query provider to a visual component. A grid load callback composes and executes one page
inside its operation and gives the component only the materialized rows and total count.

### 5.2 Business reads

Dashboards, indicators, home summaries, reports and progress views are business capabilities. They use typed read use cases.

```text
UI / API
  → Read Use Case
  → Read-only DbContext factory
  → typed result
```

## 6. MVVM boundary

The ViewModel is the system boundary. It may contain:

- loading state;
- selected values;
- dropdown collections;
- filters;
- pagination state;
- UI validation;
- mapping from screen model to use-case request.

The ViewModel must never persist directly.
Its durable UI state contains materialized values, never `IQueryable<T>`, a read scope or a DbContext.

## 7. Read-only context

The read context:

- defaults to `NoTracking`;
- rejects `SaveChanges`;
- may use a read-only database credential;
- may later point to a read replica;
- exposes only approved read surfaces.

## 8. Render-mode-independent terminals and Interactive Auto

A shared `IReadDb` contract can be implemented by two providers:

```text
Interactive Server → EF Core IQueryable → SQL
Interactive WASM   → OData IQueryable   → HTTP → EF Core → SQL
```

Portable queries use the common subset of LINQ supported by both providers. Async terminal execution is abstracted by `IReadQueryExecutor`.

The executor contract is the standard incidental-read terminal even in a server-only application.
Providing the OData/browser implementation is an optional capability. This keeps feature code
independent from its current render mode and allows a Server component to move to WebAssembly or
Interactive Auto without replacing provider-specific terminals.

`ToPageAsync` preserves paging before materialization in both runtimes. The EF implementation counts
and loads the requested page sequentially on the same context. The OData implementation requests
`$count`, `$skip` and `$top` and materializes through browser `HttpClient`.

Validate authentication, OData limits, trimming and AOT compatibility before enabling the client
provider in production.

## 9. Exports

Export is a business read use case:

```text
Export Use Case
  → authorization and filters
  → read model
  → typed report model
  → Excel / CSV / JSON / PDF exporter
```

Formats share one report definition. Exporters never query the database.

## 10. Data modeling

Agents must not invent persistent relationships silently. Domain and data specifications define:

- cardinalities;
- ownership;
- historical retention;
- tenant isolation;
- idempotency;
- versioning;
- delete/archive rules.

A functional spec defines behavior. A data spec defines shared persistence invariants.

## 11. AOT and trimming

Code should remain AOT- and trimming-friendly when practical:

- explicit DI registration;
- source-generated JSON where required;
- no unnecessary assembly scanning;
- no runtime code generation in business flows;
- warnings are investigated rather than suppressed blindly.

AOT compatibility is a design target, not a promise that every project must publish Native AOT immediately.

## 12. Dependency-injection build gate

Compilation alone does not prove that the runtime container can construct every use case, ViewModel
or Blazor injection. Executable composition roots therefore enable `ValidateOnBuild` and
`ValidateScopes`, implement a deterministic `--validate-di` mode and opt in to the repository's
post-build validation target.

The gate resolves marker-based services and inspects Blazor constructor, `[Inject]` and keyed
dependencies. Registration remains explicit; reflection validates the built graph and never performs
automatic service registration.
