# ADR 0009 — Interactive Auto, WebAssembly and OData are experimental and specification-gated

## Status

Accepted for v0.5. Amends ADR 0005.

## Context

v0.3 and v0.4 carried the Interactive Auto and browser OData provider inside the main sample with no
status marker, which implied production readiness. The spike that introduced it explicitly excluded
authentication, OData exposure limits, generated-client metadata lifecycle and AOT publishing —
exactly the concerns that decide whether it can ship.

Isolating the path into a separate sample was considered and rejected: Interactive Auto exercises
**both** Interactive Server and WebAssembly from a single component, so splitting would duplicate
hosts and remove the coverage that justifies the mode existing at all.

## Decision

Interactive Server is the default and supported path. The Interactive Auto, WebAssembly and OData
code stays in the same solution, marked experimental, and is contained by a rule rather than by
isolation:

> Do not create, extend or wire any file, class, endpoint, controller, EDM registration, client
> provider, project reference or render-mode attribute for Interactive Auto, WebAssembly or OData
> unless the feature specification explicitly requests Interactive Auto and names what it needs.

The existing code is a reference to read, not a template to replicate. Its limitations are stated in
the user-facing README and in the agent-facing rules, not only in a spike document.

The portable-LINQ subset applies only to queries a specification has scoped for Interactive Auto;
Interactive Server code is not constrained by a provider it does not use.

## Consequences

- the coverage of exercising two render modes from one component is preserved;
- nothing new is generated for the experimental path by default, so its surface does not grow
  silently;
- containment depends on a written rule rather than on a physical boundary, which makes the wording
  of that rule load-bearing;
- readers no longer infer that browser OData is a supported production path.
