# ADR 0012 — Tenant isolation through composite keys and named global query filters

## Status

Accepted for v0.5.

## Context

Per-query tenant predicates do not scale as a safety mechanism. In field use of this architecture,
one application accumulated dozens of hand-written `TenantId` comparisons spread across components,
pages and use cases. Every one of them was correct.

Collectively they were an unverifiable surface. A new query that simply omitted the predicate
produced a cross-tenant read with no compile error, no analyzer diagnostic and no failing test — and
the occurrences that lived inside components had neither analyzer nor test coverage at all, because
the analyzer did not see Razor-generated code (ADR 0010) and no test exercised a screen's reads.

A single omission is a data leak between customers. Discipline is the wrong control for that.

## Decision

Tenant-owned entities carry `TenantId`, have a `(TenantId, Id)` alternate key, and are indexed on
`TenantId`.

**Reads** are covered by an EF Core global query filter, named `Tenant`, applied by both the write
and read contexts. The filter compares against a property of the executing context, which the
factory stamps per operation — not a value captured when the model was built, which the per-type
model cache would freeze at the first tenant that ever queried.

An unresolved tenant reads nothing. The filter is written as `x.TenantId == context.TenantId`, so a
null tenant denies everything without a clause of its own. The shape `TenantId == null || ...` is
explicitly rejected: it returns every tenant's rows wherever a tenant fails to resolve.

**Writes** are covered by composite foreign keys carrying `TenantId`. A query filter does not
constrain `SaveChanges`, so the keys are what make a cross-tenant reference unrepresentable.

A hand-written `TenantId` comparison in feature code is forbidden, including a correct one.
`IgnoreQueryFilters([TenantFilter])` is confined to composition roots, seeders and migrations.

A write use case never accepts `TenantId` in its request; it reads the tenant from `ICurrentUser`.

The tenant accessor returns `Guid?` and never throws. Code runs without a user in more places than
it first appears — seeding, the dependency-injection build gate, background work, tests — and an
accessor that throws turns those into startup failures.

The tenant and Identity user entities are not filtered, because authentication must locate an
account before its tenant can be resolved. Each account has one required tenant foreign key. A
server-side claims principal factory projects that value into the Identity cookie as `tenant_id`.

## Consequences

- isolation is enforced by the provider and the database rather than by discipline;
- reads degrade safely when no tenant is resolved, and writes fail loudly, which is the correct
  asymmetry;
- the guarantee is testable, and is tested: a query with no predicate of its own cannot see another
  tenant, an unresolved tenant reads nothing, and a cross-tenant reference is rejected by the
  database;
- the sample uses a relational provider so the composite-key guarantee is actually enforced; the
  in-memory provider would have left it configured but unproven;
- tenant metadata is not exposed by the sample's authenticated read endpoints. An application that
  exposes a tenant catalog must authorize that surface explicitly;
- the same-origin Identity cookie crosses the experimental OData HTTP boundary. The server resolves
  `tenant_id` again for that request before applying the same global filter; the browser never sends
  a tenant identifier of its own;
- changing tenant requires logout and a new login, which creates a new request and Blazor circuit.
  Multi-profile account selection is outside this decision.
