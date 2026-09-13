# ADR 0009 — Interactive Auto, WebAssembly and OData are experimental and isolated

## Status

Accepted for v0.5. Amends ADR 0005.

## Context

v0.3 and v0.4 carried the Interactive Auto and browser OData provider inside the main Blazor host
with no status marker. That implied production readiness and made the supported Server composition
root pay for WebAssembly hosting, OData and a second runtime model.

Interactive Auto still has useful architectural value: one component exercises an EF-backed read
during Server execution and the equivalent remote provider after switching to WebAssembly. The
experiment therefore needs one Auto host and one browser project, but it does not need to share a
host with the supported Server application.

## Decision

Interactive Server is the default and supported Blazor path. The sample separates presentations:

```text
CompileTimeFirst.Sample.BlazorServer       supported, global Interactive Server
CompileTimeFirst.Sample.BlazorAuto         experimental Auto host and OData surface
CompileTimeFirst.Sample.BlazorAuto.Client  experimental WebAssembly client/provider
```

All projects remain in the same solution so restore, compilation, analyzers, dependency-injection
validation and tests cover both profiles. Physical isolation prevents the experimental dependencies,
routes and failure model from leaking into the supported Server composition root.

The Server host makes `HeadOutlet` and `Routes` globally interactive. Its layout therefore owns one
global generic error boundary. The Auto host keeps only the specified spike and its local interactive
boundary, because a static ancestor cannot catch failures that occur later in the client-rendered
subtree.

The experimental rule remains:

> Do not create, extend or wire any file, class, endpoint, controller, EDM registration, client
> provider, project reference or render-mode attribute for Interactive Auto, WebAssembly or OData
> unless the feature specification explicitly requests Interactive Auto and names what it needs.

The portable-LINQ subset applies only to queries a specification has scoped for Interactive Auto;
Interactive Server code is not constrained by a provider it does not use.

## Consequences

- the Server profile has one runtime, one interactive tree and no Auto/OData dependencies;
- the Auto component still validates both Server and WebAssembly execution;
- both profiles remain covered by one solution and one build;
- experimental growth remains specification-gated and physically visible;
- Full Blazor WebAssembly remains a separate future architecture with its own API, authentication
  and HTTP error-contract decisions.
