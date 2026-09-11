# ADR 0007 — Remove the ViewModel layer; components own screen state

## Status

Accepted for v0.5. Amends ADR 0002.

## Context

Until v0.4 each screen was a pair: a `.razor` component and an `XViewModel` class registered in the
container. The layer was justified by render-mode portability — one state class shared by Interactive
Server and WebAssembly — and by unit-testability of screen logic.

Field use did not support either justification.

The portability benefit only materializes when a second read provider is actually enabled, and that
path is now classified as experimental (ADR 0009). Meanwhile the layer cost one extra file per
screen and split the reading path: understanding or changing a screen meant opening two files and
holding the mapping between them.

The layer also damaged enforcement. The architecture analyzers scoped their rules with
`className.EndsWith("ViewModel")` — string matching this architecture forbids elsewhere. That gate
would have silently stopped applying to half its targets the moment the naming convention changed.

## Decision

There is no ViewModel layer. A page or component holds its own screen state and calls use cases
directly.

Logic that deserves independent tests belongs in a use case. Logic shared by two screens belongs in
a child component. State that one screen needs stays in that screen. A support type used by a single
screen is declared privately inside it.

Reintroducing the same layer under another name — `XPageState`, `XScreenModel`, `XPresenter` — is
the same decision and is equally excluded.

Analyzer scoping moves to the `ComponentBase` symbol (ADR 0010).

## Consequences

- one file per screen, one reading path, one diff per screen change;
- one fewer private concept to load before changing a feature;
- the dependency-injection gate covers more, not less: `@inject` compiles to an `[Inject]` property
  and the gate already resolves those, so it now validates every service a screen uses instead of
  one registered state class;
- screen logic is no longer unit-testable without a component test host. Accepted: the logic that
  warranted unit tests belonged in a use case, and moving it there is the intended outcome;
- large screens surface pressure earlier. That pressure must be answered by extracting a use case or
  a child component, never by restoring a state class.
