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
          No  → Direct read from IReadDb in the component or endpoint
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

Pages and components may query `IReadDb` directly. This is intentionally coupled to the UI because the projection exists to serve that UI.

```text
Component
  → IReadDbFactory
  → IReadDb
  → local IQueryable<T>
  → IReadQueryExecutor
  → materialized UI state
```

`IReadDb` and `IReadDbFactory` are justified boundaries under Well-Known First. They expose only the
approved provider-independent read surface and own its operation-scoped creation/lifetime across EF
Core and remote/OData implementations. Replacing them in Server code with
`IDbContextFactory<ReadOnlyDbContext>` would leak an EF-only construction contract into feature code.

They do not replace the public query language: the component still composes `IQueryable<T>` with
standard LINQ. `IReadQueryExecutor` owns only the separate async terminal and materialization
boundary.

The component may change when the screen changes. The application use case does not.

`IReadQueryExecutor` is the terminal path for every incidental read. The component does not call
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

Never store an `IQueryable<T>`, read scope or DbContext in component state. Never pass a
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

## 6. Component boundary

**The component is the screen boundary. There is no ViewModel layer.**

A page or component owns its own screen state:

- loading and busy flags;
- selected values;
- dropdown collections;
- filters;
- paging position;
- form models and their UI validation;
- mapping from screen model to use-case request.

A component must never persist directly. Its state holds materialized values, never `IQueryable<T>`,
a read scope or a DbContext.

The layer was removed in v0.5 (ADR 0007). Its stated benefits did not survive field use: the
portability argument only pays off when a second render provider is actually enabled, and the
separation doubled the file count per screen while splitting the reading path for every change. It
also forced the architecture analyzers to identify UI types by the class-name suffix `ViewModel` -
string matching this architecture forbids elsewhere, and a gate that removing the layer would have
silently switched off.

What replaced it is not "the same code in a different file". Logic that deserved to be tested
independently belongs in a use case, and logic reused by two screens belongs in a child component.
State that only one screen needs stays in that screen.

### 6.1 Code-behind placement

All code-behind for a page or component lives in the `@code` block of the same `.razor` file by
default. A `.razor.cs` partial is created only when a feature specification asks for it explicitly.

The trade-off is real and is not considered settled: one file per screen buys a single reading path
and a single diff, and costs C# editor tooling quality inside large `@code` blocks. The default is
chosen for consistency - an unstated default produces repositories containing both conventions,
which is worse than either. See ADR 0008.

## 6.2 Request validation, typed errors and time

A request carries its own DataAnnotations and `UseCaseBase` validates it before `ExecuteCoreAsync`.
The contract and its validation are the same artifact, visible to callers, tests and the screen that
builds the request.

The application layer defines typed failures - a validation exception carrying the rejected rules,
and a not-found exception. A screen catches those by type and renders their message inline. In the
supported Blazor Server profile, anything else reaches one global interactive layout error boundary
and is rendered generically. The boundary is recovered after navigation and by an explicit retry;
it covers lifecycle, rendering and event failures in its interactive subtree, not HTTP failures or
work detached from the renderer. An infrastructure exception is never shown as text, because its
message means nothing to the user and describes internals.

`TimeProvider` is injected wherever the current instant is needed. Ambient time is a dependency that
cannot be tested, so it is treated as one.

A rule that has both a constructive and a rejecting form lives once, in the application layer. The
editor builds its defaults from the same source the use case validates against. With screens
disposable (§6), leaving the constructive half in the screen would lose it on the next rewrite.

## 6.3 Tenant isolation

Two mechanisms, covering the two directions.

**Reads** are covered by a named EF Core global query filter on `TenantId`, applied by both contexts.
The comparison targets a property of the executing context, not a value captured when the model was
built: EF caches the model per context type, so a captured value would freeze the first tenant that
ever queried and serve its rows to everyone afterwards.

An unresolved tenant reads nothing. That falls out of comparing `Guid` to `Guid?` rather than needing
a clause of its own - and the shape that would need one, `TenantId == null || ...`, does the
opposite, returning every tenant's rows wherever a tenant failed to resolve.

**Writes** are covered by composite foreign keys carrying `TenantId`. A query filter does not
constrain `SaveChanges`; the keys do, and they make a cross-tenant reference unrepresentable rather
than merely discouraged.

The rejected alternative is a per-query tenant predicate. In field use, an application accumulated
dozens of hand-written `TenantId` comparisons across components, pages and use cases. Each was
individually correct; collectively they were an unverifiable surface, because a new query that
simply omitted the predicate produced a cross-tenant read with no compile error, no analyzer
diagnostic and no failing test. The occurrences inside components were the worst case, having
neither analyzer nor test coverage.

Two rules follow. Tenant isolation belongs in the model, where it cannot be omitted. And any rule
that depends on a developer remembering it in every query is not a rule - it is an unpaid debt whose
balance nobody knows.

Lifting the filter is possible, named and narrow:
`IgnoreQueryFilters([DomainModelConfiguration.TenantFilter])`, confined to composition roots,
seeders and migrations.

The tenant entity itself is deliberately unfiltered because Identity must resolve an account before
its tenant exists in the current operation. Each account has one required tenant foreign key. The
server claims factory removes persisted occurrences of the reserved `tenant_id` claim and emits
exactly one value from `SchoolUser.TenantId`. The reader accepts only an authenticated principal with
exactly one valid tenant claim; every other shape denies by default.

`ICurrentUser` is a scoped value holder, not a wrapper around `IHttpContextAccessor`. Middleware
captures the principal for SSR and HTTP/OData requests. A `CircuitHandler` captures it from
`AuthenticationStateProvider` when a Blazor circuit opens or reconnects and follows authentication
state changes. The client never chooses its own tenant.

## 7. Read-only context

The read context:

- defaults to `NoTracking`;
- rejects `SaveChanges`;
- may use a read-only database credential;
- may later point to a read replica;
- exposes only approved read surfaces.

## 8. Supported Server profile and experimental Interactive Auto

`CompileTimeFirst.Sample.BlazorServer` is the supported Blazor presentation. `HeadOutlet` and
`Routes` use global Interactive Server, so the layout, authentication state and generic error
boundary belong to one interactive tree. It has no reference to WebAssembly, OData or the Auto host.

`CompileTimeFirst.Sample.BlazorAuto` and `CompileTimeFirst.Sample.BlazorAuto.Client` are a physically
isolated experimental spike in the same solution. They retain a page-local interactive boundary:
the per-page Auto model cannot rely on a static ancestor layout to catch client-side component
failures. Full Blazor WebAssembly is a future architecture and is not created by this sample.

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

The Auto host validates same-origin cookie authentication and tenant propagation across OData.
Validate generated-client metadata lifecycle, broader OData exposure, trimming and AOT
compatibility before enabling the client provider in production. Tenant metadata is deliberately
absent from the portable read surface; a future tenant catalog requires an explicit authorized
contract.

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

Compilation alone does not prove that the runtime container can construct every use case or Blazor
injection. Executable composition roots therefore enable `ValidateOnBuild` and
`ValidateScopes`, implement a deterministic `--validate-di` mode and opt in to the repository's
post-build validation target.

The gate resolves `IUseCase` implementations and inspects Blazor constructor, `[Inject]` and keyed
dependencies. With screen logic inside components, `@inject` compiles to an `[Inject]` property, so
the gate covers more of the system than it did when that logic lived in a separate registered
class. Registration remains explicit; reflection validates the built graph and never performs
automatic service registration.
