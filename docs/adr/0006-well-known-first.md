# ADR 0006 — Well-Known First and the public semantic surface

## Status

Accepted for v0.4.

## Context

Humans and coding agents already understand many public platform, framework, library and protocol
constructs. Wrapping those constructs without adding meaning creates a private repository language.
Before changing a feature, a contributor must locate the wrapper, inspect its implementation,
identify the underlying API, discover which capabilities were hidden and learn local conventions.

That private knowledge creates **context debt**: the amount of repository-specific context required
before a person or agent can make a correct change.

Examples of public semantic surfaces in this .NET profile include `HttpClient`, `IQueryable<T>`,
`DbContext`, `IDbContextFactory<TContext>`, `ILogger<T>`, DataAnnotations, ASP.NET Core Identity and
authorization policies, HTTP, JSON, OpenAPI, OData and public Blazor/component-library APIs.

## Decision

Adopt **Well-Known First** as a core architecture principle:

> Prefer public, well-known constructs because they minimize the private context required for an
> agent to understand, generate, validate and migrate software.

Use a suitable public construct directly until a concrete product or architectural reason justifies
a private semantic layer. The burden of justification belongs to the new abstraction.

A private abstraction is justified when it adds at least one current value:

- product or domain semantics;
- a real architecture or lifecycle boundary;
- shared policy or behavior;
- provider switching that the application actually supports;
- necessary isolation of a specific external capability.

It is not justified merely by a desire to rename a public API, hide all vendor references, prepare
for hypothetical providers or follow an abstract “best practice”.

Well-known does not mean automatically suitable. Security, maintenance, licensing, compatibility,
performance and API quality remain required dependency criteria. Public familiarity is an
additional architectural signal, not a substitute for technical fitness.

## Canonical example

The provider-independent read stack demonstrates the intended boundaries rather than contradicting
the principle:

```text
IQueryable<T>
  = public composition language retained directly

IReadDb / IReadDbFactory
  = approved read surface plus operation lifetime and provider creation

IReadQueryExecutor
  = private terminal boundary required because EF Core and browser OData
    have different asynchronous execution mechanisms
```

`IReadDb` and its factory prevent an EF-only construction contract from leaking into shared feature
code while supporting EF and remote/OData implementations. The executor owns the separate
provider/runtime terminal difference. None of these contracts replaces `Where`, `Select`,
`OrderBy`, `Skip`, `Take` or another public LINQ composition surface with a private query DSL.

## Operational rule

The detailed operational and UI-migration examples live in
[`docs/WELL-KNOWN-FIRST.md`](../WELL-KNOWN-FIRST.md).

Before adding a relevant private abstraction, an agent or ADR must answer:

1. Why is the public API insufficient?
2. What concrete capability or product meaning is added?
3. What current problem exists without the abstraction?
4. Which boundary owns the abstraction, and how narrow can it remain?

Answers such as “reduce coupling”, “future flexibility”, “clean architecture” or “best practice”
need concrete supporting evidence.

## Compile-Time First relationship

Well-Known First reduces private knowledge. Compile-Time First validates the explicit code that uses
the public surface:

```text
well-known public construct
  → explicit typed code
  → compiler validation
  → semantic architecture analysis
  → behavior tests
```

Public APIs behave like a preloaded semantic surface for coding agents, but they are not literally an
MCP or an infallible memory. Official documentation and the installed version remain authoritative.
The compiler, analyzers and tests validate the actual use.

No generic “wrapper detector” analyzer is added in v0.4. Whether an abstraction adds product meaning
is contextual, and a name-based rule would conflict with the architecture's preference for Roslyn
semantic identity over string matching. A future analyzer may enforce specific, objectively
identifiable wrapper rules when it can do so semantically with an acceptable false-positive rate.

## UI and vendor coupling

UI code is a regenerable boundary. It may depend directly on Blazor, CSS and the selected component
library. Product components remain valid when they add product semantics. Mechanical wrappers such
as generic grids, buttons or fields are not introduced merely to hide the chosen library.

Explicit vendor coupling in that localized boundary can be easier to migrate than artificial vendor
independence because the source preserves the vendor's public meaning. Durable Domain and
Application knowledge remain protected from UI dependencies.

## Consequences

Positive:

- fewer files, adapters and one-use types;
- lower context debt for humans and agents;
- more recognizable code and better use of public documentation;
- more direct compiler, IDE and analyzer feedback;
- more mechanical migrations at regenerable boundaries;
- clearer separation between public mechanics and private product meaning.

Trade-offs:

- vendor dependencies remain visible at their intended boundary;
- replacing a dependency may require edits in multiple direct consumers;
- public APIs and remembered model knowledge can be version-sensitive;
- abstraction decisions still require judgment when product semantics are emerging;
- some valuable wrappers cannot be recognized mechanically by a universal rule.
