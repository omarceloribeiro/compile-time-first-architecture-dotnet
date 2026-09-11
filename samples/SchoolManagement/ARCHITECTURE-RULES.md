# School Management sample — local rules

The architecture rules live at the repository root: [`../../AGENTS.md`](../../AGENTS.md),
[`../../Architecture.md`](../../Architecture.md), [`../../docs/ANALYZER-RULES.md`](../../docs/ANALYZER-RULES.md)
and [`../../patterns/`](../../patterns/). This file records only what is specific to this sample, so
there is one place to change when a rule changes.

## Concrete names

| Role | Type in this sample |
|---|---|
| Write context | `SchoolDbContext` |
| Read context | `ReadOnlySchoolDbContext` |
| Read surface | `IReadSchoolDb`, `IReadSchoolDbScope` |
| Read scope factory | `IReadSchoolDbFactory` |
| Read terminals | `IReadQueryExecutor` |
| Shared model configuration | `DomainModelConfiguration` |

## Composition roots

Two, deliberately: `CompileTimeFirst.Sample.Web` (Blazor) and `CompileTimeFirst.Sample.Console`
(non-interactive). The Console root exists to prove the architecture does not assume a UI — it runs
the same use cases with `ValidateBlazorComponents: false`.

## Experimental surface

`OData/`, `CompileTimeFirst.Sample.Web.Client/` and `AutoSubjects.razor` belong to the experimental
Interactive Auto path. See the sample README and ADR 0009 before touching them.

## Storage

EF Core InMemory, for demonstration only. It does not enforce foreign keys or composite keys, so it
cannot demonstrate database-level guarantees.
