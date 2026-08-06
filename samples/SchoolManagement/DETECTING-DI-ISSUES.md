# Detecting dependency-injection issues

DI issues are detected by the normal build. The composition roots enable provider validation and the
post-build gate resolves all known use cases, ViewModels, Blazor injections and keyed services.

See `../../docs/DEPENDENCY-INJECTION-VALIDATION.md` and ADR 0004 for the implementation and rationale.
