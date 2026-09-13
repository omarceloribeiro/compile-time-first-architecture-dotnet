# Architecture analyzer rules (CTFA)

Compile-time enforcement of the read rules in [`Architecture.md`](../Architecture.md) and
[`AGENTS.md`](../AGENTS.md). A rule exists here because documentation alone did not hold it.

The reference implementation lives in the sample
(`samples/SchoolManagement/src/CompileTimeFirst.Sample.Analyzers/`). Type names in the rules below
are the sample's; an application adopting this architecture substitutes its own.

## Scope: which code the analyzer sees

Analyzers run with `GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics`.

Both flags are required. Blazor components only exist as Razor-generated `*.razor.g.cs`, which
Roslyn classifies as generated code; with the default configuration the rules never reach them.
`Analyze` on its own runs the rules over generated code but still suppresses the diagnostics they
report there, so only the pair produces enforcement. See ADR 0010.

This matters more than it sounds: with screen state and reads living inside components, `.razor` is
where nearly all read code is. A rule that holds in `.cs` and not in `.razor` is a rule that does
not hold.

## How a type enters analyzer scope

A type is in scope when its base-type chain reaches
`Microsoft.AspNetCore.Components.ComponentBase`, compared with `SymbolEqualityComparer`.

Scope is never decided by a class-name suffix. Until v0.5 these rules also matched any class whose
name ended in `ViewModel`; removing that layer would have silently disabled half the gate while the
build stayed green. Name-based scoping stops applying exactly when a convention changes, which is
the moment enforcement is most needed.

## Rule index

| ID | Rule | Severity | Since |
|---|---|---|---|
| CTFA001 | A component cannot inject the write `DbContext` or its factory | Error | v0.2 |
| CTFA002 | A component cannot call EF Core `ToListAsync()` | Error | v0.2 |
| CTFA003 | A component cannot call EF Core `FirstOrDefaultAsync()` | Error | v0.2 |
| CTFA004 | A component cannot store `IQueryable`, a read scope or a DbContext as state | Error | v0.2 |
| CTFA005 | A component cannot call EF Core `SingleOrDefaultAsync()`, `CountAsync()` or `AnyAsync()` | Error | v0.3 |

## CTFA001 — Write DbContext injected into a component

**Detects** a component whose constructor, primary constructor or `[Inject]` property takes the
write `DbContext` or `IDbContextFactory<TWriteContext>`.

**Why** writes pass through use cases. A component holding a write context can persist directly,
which bypasses the only place where write rules are stated.

```csharp
// Wrong
@inject IDbContextFactory<SchoolDbContext> DbFactory

// Right
@inject IReadSchoolDbFactory ReadDbFactory
@inject ICreateSubjectUseCase CreateSubject
```

**Enforces** "Write Through Use Cases"; `Architecture.md` §4.

## CTFA002, CTFA003, CTFA005 — EF Core terminals called directly

**Detects** a component calling `ToListAsync()`, `FirstOrDefaultAsync()`, `SingleOrDefaultAsync()`,
`CountAsync()` or `AnyAsync()` from `EntityFrameworkQueryableExtensions`.

**Why** the async terminal is the one place where read providers genuinely differ. Routing every
terminal through `IReadQueryExecutor` keeps feature code independent of which provider is resolved,
and keeps materialization inside the operation that owns the read scope.

```csharp
// Wrong
items = await db.Subjects.Where(x => x.IsActive).ToListAsync();

// Right
items = await QueryExecutor.ToListAsync(db.Subjects.Where(x => x.IsActive));
```

**Enforces** `Architecture.md` §5.1; ADR 0005.

## CTFA004 — Query or read scope stored as component state

**Detects** a field or property on a component typed `IQueryable<T>`, `IOrderedQueryable<T>`, the
read scope or a read `DbContext`.

**Why** a stored query keeps its provider and DbContext alive beyond the operation, and can be
enumerated later, concurrently, or from a render pass that no longer owns the scope. Only
materialized values are state.

```csharp
// Wrong
private IQueryable<SubjectReadItem>? _query;

// Right
private IReadOnlyList<SubjectReadItem> items = [];
```

**Enforces** "Page Before Materialization"; `Architecture.md` §5.1 and §6.

## Planned rules, and why they are not implemented

- **Unpaged terminal behind a paged control.** Detecting that a `ToListAsync` feeds a grid requires
  following the value into the markup; it is not reliably expressible as a symbol comparison today.
  The rule stays written in `AGENTS.md` and unenforced, which is recorded here rather than left to
  be discovered.
- **Non-Blazor classes.** Scope is `ComponentBase`, so a plain class calling an EF terminal is not
  reported. This narrowed when suffix matching was removed (ADR 0010) and is accepted: the layer
  that used to live outside components no longer exists.

## Adding or changing a rule

A rule exists in three places, and they change together:

1. the analyzer implementation and its test;
2. `AnalyzerReleases.Unshipped.md`;
3. this catalogue.

A pull request that changes one without the others is incomplete.

A rule must be expressible through Roslyn symbol comparison. If it can only be expressed by matching
a name, a namespace or a display string, it is not added — it is recorded under "Planned rules" with
the reason.

Every rule needs a test that fails when the rule is reverted. Two tests in the sample exist purely to
lock this document's first two sections: one analyzes a syntax tree named `*.razor.g.cs`, and one
asserts that a class named `ProductsViewModel` is *not* in scope.

## What these analyzers do not prove

They check the shape of read code inside components. They do not check that a specification chose the
right control, that a query is portable across providers, or that a use case implements its rule
correctly. Those remain the job of specifications, tests and review.
