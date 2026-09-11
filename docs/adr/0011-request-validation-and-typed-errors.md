# ADR 0011 — Request validation with DataAnnotations and typed application errors

## Status

Accepted for v0.5.

## Context

Validation was written by hand inside each use case, and rule violations were signalled with
`ArgumentException` and `InvalidOperationException`. Screens caught `Exception` and rendered
`ex.Message`.

Three problems followed. The request contract did not state its own constraints, so a caller had to
read the use-case body to learn them. A screen could not tell a stated rejection from a failure,
because both arrived as framework exception types. And an infrastructure exception — a timeout, a
constraint violation — reached the user as text describing internals.

## Decision

A request declares its constraints with DataAnnotations. `UseCaseBase` validates the request before
`ExecuteCoreAsync` runs and throws a validation exception carrying the rejected rules. Cross-field
and asynchronous rules stay in `ExecuteCoreAsync`.

The application layer defines two failure types: a validation exception with the rejected rules, and
a not-found exception. A screen catches those with a filtered `catch when` and renders the message
inline. Everything else reaches the layout error boundary and is rendered generically.

`TimeProvider` is injected wherever the current instant is read.

A rule that has both a constructive and a rejecting form lives once, in the application layer, and
the screen projects it.

## Consequences

- the request is the contract and the validation, visible to callers, tests and generators;
- a screen can distinguish a rejection it should display from a failure it should not;
- infrastructure detail stops reaching users;
- DataAnnotations is reflective, which is in tension with Compile-Time First. Accepted: it is a
  well-known public surface, it runs only on the server, and the alternative was hand-written
  validation that drifted from the contract it validated;
- messages live in annotations, so a localized application needs a localization strategy for them;
- tests assert typed exceptions instead of framework ones, which makes an assertion about a rule
  distinguishable from an assertion about a bad argument.
