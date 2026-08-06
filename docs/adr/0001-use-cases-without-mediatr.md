# ADR 0001 — Use cases without MediatR

## Status
Accepted for v0.

## Context
The project needs explicit actor-oriented operations but does not need an in-process message bus for every call.

## Decision
Use specific interfaces and concrete use-case classes. A small base class provides an explicit technical pipeline. Register use cases explicitly in DI.

## Consequences
- fewer files and less indirection;
- easier agent navigation;
- direct calls and stack traces;
- no automatic pipeline composition;
- introduce decorators or messaging later only for demonstrated needs.
