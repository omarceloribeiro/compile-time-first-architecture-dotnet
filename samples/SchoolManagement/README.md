# School Management sample

This sample validates typed write use cases, read-only EF projections, component-owned screen
state, automatic DI validation and paged incidental reads.

## Run

```bash
dotnet build CompileTimeFirst.Sample.sln
dotnet test CompileTimeFirst.Sample.sln --no-build
dotnet run --project src/CompileTimeFirst.Sample.BlazorServer
```

The supported Server app runs at `http://localhost:5088`, seeds one Subject and Grade per tenant and
exposes:

- `/subjects` and `/grades` — simple catalog writes with paged data tables;
- `/questions` — atomic question and option creation;
- `/question-options` — add options to existing questions;

The isolated experimental host runs at `http://localhost:5089`:

```bash
dotnet run --project src/CompileTimeFirst.Sample.BlazorAuto
```

It exposes `/auto-subjects` and the read-only `/odata/$metadata` surface only.

All functional routes require the sample's ASP.NET Core Identity login:

| Username | Password | Tenant |
|---|---|---|
| `account1` | `Sample123!` | North School |
| `account2` | `Sample123!` | South School |

These are development-only accounts in the ephemeral SQLite database. Logout followed by a new
login is the only tenant switch; the browser never selects or submits a tenant identifier. Server
and Auto use different Identity cookie names, so both profiles can remain authenticated on
`localhost` while they are compared side by side.

## Experimental surface

`CompileTimeFirst.Sample.BlazorServer` is the supported path. It uses global Interactive Server and
has no WebAssembly or OData dependency. These isolated projects belong to the experimental
Interactive Auto / WebAssembly / OData path and exist as a reference to read, not as a template to
copy:

```text
src/CompileTimeFirst.Sample.BlazorAuto/OData/                   OData controllers, EDM model, read scope
src/CompileTimeFirst.Sample.BlazorAuto.Client/                  WebAssembly client and OData read provider
src/CompileTimeFirst.Sample.BlazorAuto.Client/Pages/AutoSubjects/ the only Interactive Auto page
tests/CompileTimeFirst.Sample.Tests/ODataQueryTests.cs          portable-LINQ translation tests
tests/CompileTimeFirst.Sample.Tests/ODataEndToEndTests.cs       OData endpoint tests
```

Do not add files, classes, endpoints or render-mode attributes to this path unless a feature
specification explicitly asks for Interactive Auto. See "Experimental render modes" in
`../../AGENTS.md`.

Full Blazor WebAssembly is not part of v0.5. Its future API, authentication and error-contract
decisions are recorded in `../../docs/FULL-WEBASSEMBLY-DIRECTION.md`.

See `specs/`, `../../Architecture.md` and `../../docs/DEPENDENCY-INJECTION-VALIDATION.md`.
