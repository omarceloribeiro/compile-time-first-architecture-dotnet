# Dependency-injection build gate

## Objective

A successful C# compilation does not prove that the dependency-injection graph is valid. Missing
registrations, invalid scopes, unconstructable ViewModels and unresolved Blazor injections can
otherwise survive until a route is visited.

## Validation layers

1. Composition roots enable `ValidateOnBuild` and `ValidateScopes`.
2. `eng/DependencyInjectionGraphValidator.cs` resolves all marker-based `IUseCase` and `IViewModel`
   implementations and validates Blazor constructors, `[Inject]` properties and keyed services.
3. `Directory.Build.targets` executes the already-built application with `--validate-di` after each
   opted-in composition-root build.

The Web composition root also builds the browser service collection during validation, because a
WebAssembly assembly cannot be executed directly as a normal `dotnet` process.

## Commands

```bash
dotnet build samples/SchoolManagement/CompileTimeFirst.Sample.sln
dotnet run --project samples/SchoolManagement/src/CompileTimeFirst.Sample.Console -- --validate-di
dotnet run --project samples/SchoolManagement/src/CompileTimeFirst.Sample.Web -- --validate-di
```

`SkipDependencyInjectionValidation` exists only as an emergency local diagnostic bypass and must not
be used by CI or coding agents.

## What the gate does not prove

Business correctness, database connectivity, authorization, routes and external services still need
unit, integration and smoke tests.
