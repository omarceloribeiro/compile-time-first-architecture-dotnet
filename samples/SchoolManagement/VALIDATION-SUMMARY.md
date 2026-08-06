# Validation summary

Validation is executable rather than duplicated in this file:

- CTFA001–003 enforce the UI read/write boundary at compile time.
- `Directory.Build.targets` runs the Console and Web DI gates after build.
- `tests/CompileTimeFirst.Sample.Tests` covers use-case invariants, executor terminals, primary
  constructors and OData LINQ translation.

See `../../docs/DEPENDENCY-INJECTION-VALIDATION.md` for details.
