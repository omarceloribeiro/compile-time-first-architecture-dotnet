# School Management architecture rules

## UI read/write boundary

- ViewModels and Blazor components never inject `SchoolDbContext` or its factory.
- Incidental reads use `IReadSchoolDbFactory` and terminate through `IReadQueryExecutor`.
- Writes invoke a specific `IUseCase`; ViewModels never persist through the read store.
- Primary-constructor and direct-EF violations are compile-time errors from CTFA001–003.

## Dependency injection

- Registrations are explicit.
- Console and Web enable `ValidateOnBuild` and `ValidateScopes`.
- The normal build executes `--validate-di` and resolves every `IUseCase`, `IViewModel`, Blazor
  constructor, `[Inject]` property and keyed service.

See `../../docs/DEPENDENCY-INJECTION-VALIDATION.md` for the canonical DI documentation and
`EXAMPLES-READ-ONLY-ARCHITECTURE.md` for examples.
