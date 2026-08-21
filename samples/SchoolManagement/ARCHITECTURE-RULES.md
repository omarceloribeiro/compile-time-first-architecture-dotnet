# School Management architecture rules

## UI read/write boundary

- ViewModels and Blazor components never inject `SchoolDbContext` or its factory.
- Incidental reads use `IReadSchoolDbFactory` and terminate through `IReadQueryExecutor`.
- Dropdowns use `ToListAsync`; data grids, data tables, result lists, autocompletes and histories use `ToPageAsync`.
- Uniqueness, count and existence use `SingleOrDefaultAsync`, `CountAsync` and `AnyAsync` through `IReadQueryExecutor`.
- Every paged query defines deterministic ordering and a unique tie-breaker when its primary sort key is not unique.
- The spec selects the control. ViewModels do not invent thresholds or adaptive behavior.
- Queries and read scopes are local to one operation and never become component state.
- Visual components receive materialized values, never a live `IQueryable` provider.
- Writes invoke a specific `IUseCase`; ViewModels never persist through the read store.
- Primary-constructor, direct-EF and escaped-read-state violations are compile-time errors from CTFA001–005 in both Web and Web.Client.

## Dependency injection

- Registrations are explicit.
- Console and Web enable `ValidateOnBuild` and `ValidateScopes`.
- The normal build executes `--validate-di` and resolves every `IUseCase`, `IViewModel`, Blazor
  constructor, `[Inject]` property and keyed service.

See `../../docs/DEPENDENCY-INJECTION-VALIDATION.md` for the canonical DI documentation and
`EXAMPLES-READ-ONLY-ARCHITECTURE.md` for examples.
