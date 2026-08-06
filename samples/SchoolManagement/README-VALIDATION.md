# Validation quick reference

The canonical validation documentation is `../../docs/DEPENDENCY-INJECTION-VALIDATION.md`.

Run `dotnet build CompileTimeFirst.Sample.sln`; the build automatically compiles the analyzers and
executes the DI graph gate for Console and Web. Run `dotnet test CompileTimeFirst.Sample.sln
--no-build` for business-rule, executor, analyzer and OData translation tests.
