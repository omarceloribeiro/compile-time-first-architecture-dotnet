# School Management sample

This sample validates typed write use cases, read-only EF projections, component-owned screen
state, automatic DI validation and paged incidental reads.

## Run

```bash
dotnet build CompileTimeFirst.Sample.sln
dotnet test CompileTimeFirst.Sample.sln --no-build
dotnet run --project src/CompileTimeFirst.Sample.Web
```

The Web app seeds one Subject and Grade and exposes:

- `/subjects` and `/grades` — simple catalog writes with paged data tables;
- `/questions` — atomic question and option creation;
- `/question-options` — add options to existing questions;
- `/auto-subjects` — **experimental**, see below;
- `/odata/$metadata` — **experimental**, the read-only OData metadata document.

All functional routes require the sample's ASP.NET Core Identity login:

| Username | Password | Tenant |
|---|---|---|
| `account1` | `Sample123!` | North School |
| `account2` | `Sample123!` | South School |

These are development-only accounts in the ephemeral SQLite database. Logout followed by a new
login is the only tenant switch; the browser never selects or submits a tenant identifier.

## Experimental surface

Interactive Server is the supported path in this sample. These files belong to the experimental
Interactive Auto / WebAssembly / OData path and exist as a reference to read, not as a template to
copy:

```text
src/CompileTimeFirst.Sample.Web/OData/                          OData controllers, EDM model, read scope
src/CompileTimeFirst.Sample.Web.Client/                         WebAssembly client and OData read provider
src/CompileTimeFirst.Sample.Web.Client/Pages/AutoSubjects/      the only Interactive Auto page
tests/CompileTimeFirst.Sample.Tests/ODataQueryTests.cs          portable-LINQ translation tests
tests/CompileTimeFirst.Sample.Tests/ODataEndToEndTests.cs       OData endpoint tests
```

Do not add files, classes, endpoints or render-mode attributes to this path unless a feature
specification explicitly asks for Interactive Auto. See "Experimental render modes" in
`../../AGENTS.md`.

See `specs/`, `../../Architecture.md` and `../../docs/DEPENDENCY-INJECTION-VALIDATION.md`.
