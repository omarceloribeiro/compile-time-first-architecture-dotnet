# Changelog

## v0.5

Architecture:

- removes the ViewModel layer; screen state and screen logic live in the component (ADR 0007);
- makes the `.razor` file the default home for all page and component code-behind, with `.razor.cs`
  allowed only when a specification requires it (ADR 0008);
- shares one EF Core model configuration between the write and read contexts, so the two models
  cannot drift apart silently;
- encapsulates domain entities: private constructor for materialization, private setters and
  behaviour methods instead of public property assignment;
- moves the shape of a question into the application layer, so the editor builds defaults from the
  same source the write use case validates against;
- moves request validation to DataAnnotations evaluated by `UseCaseBase`, and introduces typed
  validation and not-found exceptions caught by a filtered `catch when` in the screen; an
  infrastructure exception now reaches a layout error boundary instead of being rendered as text
  (ADR 0011);
- validates normalized text lengths after `Trim()` through one DataAnnotation shared by question
  contracts and the form, while persistence performs the normalization;
- injects `TimeProvider` instead of reading `DateTimeOffset.UtcNow`, with `FakeTimeProvider` in
  tests asserting the persisted timestamp;
- adds structural multi-tenancy: `(TenantId, Id)` alternate keys, composite foreign keys that make a
  cross-tenant reference unrepresentable, and a named EF Core global query filter applied by both
  contexts (ADR 0012). A hand-written tenant predicate in feature code is now forbidden, including a
  correct one; an unresolved tenant reads nothing;
- moves the sample from the in-memory provider to SQLite, so the composite-key guarantee is enforced
  and testable rather than configured and unproven. Each operation owns an independent connection
  to a named ephemeral database preserved by a separate keeper connection;
- replaces the tenant selector with ASP.NET Core Identity cookie authentication. Each seeded account
  has one tenant foreign key, projected by the server as `tenant_id` for both Blazor and same-origin
  OData requests. Server and Auto use distinct cookie names so their sessions coexist on localhost;
- reserves `tenant_id` as a server-owned claim, rejects missing, invalid or duplicate values, and
  captures the current principal independently for HTTP requests and Blazor circuits;
- removes tenant enumeration from the portable read surface; an authorized tenant catalog remains
  a future product contract;
- names the table of every entity in the shared configuration. Table naming otherwise follows the
  `DbSet` property, which the read context does not declare - the two contexts mapped the same
  entity to different tables while every other part of the model matched.

Render modes:

- renames the supported host to `CompileTimeFirst.Sample.BlazorServer`, makes `HeadOutlet` and
  `Routes` globally Interactive Server and gives the interactive layout one recoverable generic
  error boundary;
- isolates Interactive Auto, WebAssembly and OData in `CompileTimeFirst.Sample.BlazorAuto` and
  `CompileTimeFirst.Sample.BlazorAuto.Client`, while keeping them in the same solution for build and
  test coverage and forbidding extension without an explicit specification (ADR 0009);
- preserves prerendered state during hydration and places unexpected Auto/OData failures under an
  interactive error boundary that never renders exception messages.
- keeps anonymous OData responses status-only and converts invalid login/logout antiforgery tokens
  into generic `400` responses.

Enforcement:

- makes the architecture analyzers analyze Razor-generated code, which was previously unchecked.
  The same violation produced a build error in a `.cs` file and compiled with zero warnings inside
  an `@code` block; with screen logic now living in components, that gap covered nearly the whole
  read surface (ADR 0010);
- scopes analyzer rules by the `ComponentBase` symbol instead of a class-name suffix;
- adds `docs/ANALYZER-RULES.md` as the root catalogue of CTFA rules, and the rule that a rule exists
  in three places that change together;
- retires the `IViewModel` dependency-injection marker; the gate validates `IUseCase` implementations
  and Blazor injections, and now covers every service a screen uses rather than one registered state
  class;
- declares `CTFA004` and `CTFA005` severities in `.editorconfig`, which were missing.

Notes from field use:

- client-side paging over a fully materialized table, and the absence of the architecture analyzers,
  were observed in production use of this architecture. Both are recorded as regressions to avoid,
  not as simplifications to adopt. `ToPageAsync` remains mandatory for grids, data tables, result
  lists, autocompletes and histories.

Documentation:

- rewrites the ViewModel/MVVM vocabulary across the root documents, patterns and sample;
- removes the sample's read-only architecture examples document, which taught the removed pattern
  and had drifted from the analyzer rule set, plus three overlapping validation notes;
- adds ADRs 0007 to 0012 and amendment notes to ADRs 0002 and 0005;
- corrects the repository structure listing, which referenced a file that does not exist and omitted
  two that do.

## v0.4

- adopts Well-Known First as a core architecture and agent rule;
- defines Public Semantic Surface and Context Debt;
- prefers suitable public APIs, protocols, types and conventions over mechanical private wrappers;
- requires concrete product meaning, policy, provider variation or an architectural boundary before adding an abstraction;
- documents `IReadQueryExecutor` as a narrow, justified exception at the provider-specific async terminal boundary;
- documents `IReadDb` and `IReadDbFactory` as the approved read-surface, lifetime and provider boundary;
- relates Well-Known First to reads, UI, dependency injection, integrations and regenerable vendor boundaries;
- adds the semantic-transparency guide with private-language, UI migration, design-system and ASP.NET Core Identity examples;
- narrows direct Identity use to framework mechanics while preserving product access decisions as application use cases;
- keeps official documentation and the installed package version authoritative over remembered API knowledge;
- records why v0.4 does not add a context-blind wrapper-detector analyzer.

## v0.3

- makes `IReadQueryExecutor` the terminal path for every incidental UI read;
- adds `PageResult<T>` and provider-independent `ToPageAsync`;
- keeps `IQueryable<T>`, read scopes and DbContexts local to one operation;
- forbids binding live query providers to visual components;
- maps UI controls from the feature specification to explicit query terminals;
- pages sample tables and result lists before materialization;
- validates EF Core and browser OData paging with contract and end-to-end tests;
- preserves the v0 write, business-read, DI-validation and optional Auto/OData foundations.
