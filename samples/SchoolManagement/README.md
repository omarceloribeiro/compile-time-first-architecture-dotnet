# School Management sample

This sample validates typed write use cases, read-only EF projections, Blazor MVVM, automatic DI
validation and a provider-independent Interactive Auto/OData read.

## Run

```bash
dotnet build CompileTimeFirst.Sample.sln
dotnet test CompileTimeFirst.Sample.sln --no-build
dotnet run --project src/CompileTimeFirst.Sample.Web
```

The Web app seeds one Subject and Grade and exposes:

- `/subjects` and `/grades` — simple catalog writes;
- `/questions` — atomic question and option creation;
- `/question-options` — add options to existing questions;
- `/auto-subjects` — the same ViewModel/query using EF in Interactive Server and OData in WASM;
- `/odata/$metadata` — the read-only OData metadata document.

See `specs/`, `../../Architecture.md` and `../../docs/DEPENDENCY-INJECTION-VALIDATION.md`.
