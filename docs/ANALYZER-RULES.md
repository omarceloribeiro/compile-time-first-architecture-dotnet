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

Component rules cover Razor-generated code. Business-read rules also run in Application, which
explicitly references the analyzer project with `OutputItemType="Analyzer"` and
`ReferenceOutputAssembly="false"`; installing it only in UI projects would miss use-case bodies.

## How a type enters analyzer scope

A type enters the component rules when its base-type chain reaches
`Microsoft.AspNetCore.Components.ComponentBase`, compared with `SymbolEqualityComparer`.

Scope is never decided by a class-name suffix. Until v0.5 these rules also matched any class whose
name ended in `ViewModel`; removing that layer would have silently disabled half the gate while the
build stayed green. Name-based scoping stops applying exactly when a convention changes, which is
the moment enforcement is most needed.

For CTFA006, a concrete class or struct implementing `IUseCase` must implement exactly one of
`IReadUseCase` and `IWriteUseCase`. Abstract bases may remain unclassified. CTFA007/008 apply to
types implementing `IReadUseCase`, including inherited markers, abstract read bases and nested
helpers declared inside a read type. These rules use symbols rather than naming conventions.

## Rule index

| ID | Rule | Severity | Since |
|---|---|---|---|
| CTFA001 | A component cannot inject the write `DbContext` or its factory | Error | v0.2 |
| CTFA002 | A component cannot call EF Core `ToListAsync()` | Error | v0.2 |
| CTFA003 | A component cannot call EF Core `FirstOrDefaultAsync()` | Error | v0.2 |
| CTFA004 | A component cannot store `IQueryable`, a read scope or a DbContext as state | Error | v0.2 |
| CTFA005 | A component cannot call EF Core `SingleOrDefaultAsync()`, `CountAsync()` or `AnyAsync()` | Error | v0.3 |
| CTFA006 | A concrete use case must have exactly one read/write classification | Error | ADR 0013 |
| CTFA007 | A read use case cannot call or capture EF persistence methods | Error | ADR 0013 |
| CTFA008 | A read use case cannot receive the original writable context or its factory | Error | ADR 0013 |

## CTFA001 — Write DbContext injected into a component

**Detects** a component whose constructor, primary constructor or `[Inject]` property takes the
write `DbContext` or `IDbContextFactory<TWriteContext>`.
Subtypes, including the business query context, are also prohibited here: components keep the
incidental surface rather than taking a business-use-case context.

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
read scope or any EF `DbContext` subtype, including the business query context.

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

## CTFA006 — Mandatory use-case classification

Specific use-case interfaces inherit `IReadUseCase` or `IWriteUseCase`, both `IUseCase`. The rule
rejects concrete implementations carrying neither marker or both, even through inherited
interfaces or base classes. It does not require registering markers in DI or changing UseCaseBase.

## CTFA007 — Persistence referenced by a read use case

Rejects calls and method-group references to:

- all four `DbContext.SaveChanges` / `SaveChangesAsync` overloads, including overrides;
- `ExecuteUpdate`, `ExecuteUpdateAsync`, `ExecuteDelete`, `ExecuteDeleteAsync`;
- `ExecuteSql`, `ExecuteSqlRaw`, `ExecuteSqlInterpolated` and their async overloads.

The analyzer resolves EF method symbols once per compilation and compares original definitions and
override chains. EF Core 10 exposes bulk operations on `EntityFrameworkQueryableExtensions`;
the relational extension type is also recognized when it supplies those methods. Calls via base
casts, lambdas, local functions, private methods and nested helpers within a read type are covered.
Unrelated methods with the same name are allowed. Normal EF read terminals remain valid in use cases.

The sealed query context additionally rejects SaveChanges at runtime, even through a base reference.
Bulk and SQL calls bypass SaveChanges; their protection here is static, not a runtime override.

## CTFA008 — Writable dependency received by a read use case

Rejects constructor/primary-constructor parameters, declared fields and properties typed as the
original `SchoolDbContext` (or writable subtypes), or implementations of
`IDbContextFactory<SchoolDbContext>`. `QuerySchoolDbContext` and its factory are exempt. The approved
incidental/CQRS read surface is also allowed; selecting separate-model CQRS still requires a spec/ADR.
This is not data-flow analysis of contexts created locally or hidden behind other service types.

## Planned rules, and why they are not implemented

- **Unpaged terminal behind a paged control.** Detecting that a `ToListAsync` feeds a grid requires
  following the value into the markup; it is not reliably expressible as a symbol comparison today.
  The rule stays written in `AGENTS.md` and unenforced, which is recorded here rather than left to
  be discovered.
- **Incidental terminals outside Blazor.** CTFA001–005 use component scope; endpoint or plain-class
  incidental reads are not checked by these UI rules. Business-read persistence has its own marker
  scope, CTFA006–008.
- **Transitive side effects.** External services/helpers, inherited bodies in unmarked base classes,
  reflection, dynamic calls and arbitrary ADO.NET are not analyzed transitively. Delegating a write
  from a read use case is forbidden by architecture but not proven absent by this analyzer.

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

They check component read boundaries and known direct persistence in classified business reads.
They do not prove a database permission boundary, absence of all indirect writes, appropriate CQRS
selection, correct controls, provider portability or business correctness. Those remain the job of
specifications, tests and review. See ADR 0013 for runtime limits.
