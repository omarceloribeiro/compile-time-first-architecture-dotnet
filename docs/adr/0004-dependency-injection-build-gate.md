# ADR 0004 — Validate dependency injection after composition-root builds

## Status

Accepted for v0.

## Context

The compiler validates types but cannot prove that the DI container can construct the application.
AI-assisted changes commonly add constructor or `@inject` dependencies without updating the
composition root.

## Decision

Each executable composition root must enable provider validation, expose `--validate-di`, validate
known marker-based services and Blazor injections, and opt in to the repository post-build target.

Service registration remains explicit. Reflection is restricted to validation and does not perform
assembly-scanning registration.

## Consequences

- Missing registrations, keyed services and invalid lifetimes fail the build.
- Executable builds take slightly longer.
- Runtime factories and external dependencies still require dedicated tests.
