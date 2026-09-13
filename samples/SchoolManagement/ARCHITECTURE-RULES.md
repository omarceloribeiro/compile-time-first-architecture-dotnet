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

Three, deliberately:

- `CompileTimeFirst.Sample.BlazorServer` is the supported global Interactive Server profile;
- `CompileTimeFirst.Sample.BlazorAuto` is the experimental Auto/OData host and validates its
  `CompileTimeFirst.Sample.BlazorAuto.Client` browser service graph;
- `CompileTimeFirst.Sample.Console` proves the architecture does not assume a UI and runs the same
  use cases with `ValidateBlazorComponents: false`.

## Experimental surface

`CompileTimeFirst.Sample.BlazorAuto/`, `CompileTimeFirst.Sample.BlazorAuto.Client/` and
`AutoSubjects.razor` belong to the experimental Interactive Auto path. The supported Server project
must not reference them or their OData/WebAssembly packages. See the sample README and ADR 0009
before touching them.

## Storage

Both web hosts use SQLite in-memory with one open connection for their process lifetime. SQLite is a
relational provider that enforces the configured tenant foreign keys, so the tests demonstrate the
composite-key guarantee instead of merely inspecting EF metadata. The databases remain ephemeral
and are not a production persistence configuration.
