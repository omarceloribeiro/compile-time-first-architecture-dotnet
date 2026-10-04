# ADR 0013 — Business reads use the original context with build-time persistence checks

## Status

Accepted. Clarifies ADR 0002 and the scope of ADRs 0005/0006. Implements SPEC-003.

## Context

Field use exposed an ambiguity: treating every read as a separate read model forces business use
cases to depend on intermediate projections intended for incidental queries. The rule is to retain
the original entity model for business reads; the anti-pattern is creating a second model merely
because an operation returns data. Separating actor intentions does not mandate CQRS model separation.

## Decision

| Operation | Access |
|---|---|
| Write use case | Original DbContext and its factory |
| Default read use case | Original DbContext and its factory, EF terminals |
| Explicit medium/strong CQRS read use case | Separate read model, read factory and IReadQueryExecutor |
| Incidental query | IReadDb, read factory and IReadQueryExecutor |

Medium/strong CQRS must be explicitly selected in the initial project architecture and specify the
required separation; a later adoption requires an approved ADR. No runtime flag, API style or
render mode selects it. A replica alone does not require projecting every domain entity into a
second model. The sample implements the default, with no CQRS synchronization infrastructure.

Business reads use `IDbContextFactory<SchoolDbContext>`, just like writes. Each operation creates
and disposes its own context, stamped with its tenant by the existing factory. The original context
owns the entity model, Identity, tenant filters, schema creation and migrations.
`ReadOnlySchoolDbContext` retains the incidental projections and its existing runtime save guard.

Every EF query in a read use case applies `AsNoTracking()` (or
`AsNoTrackingWithIdentityResolution()` when needed) immediately after each DbSet/Set<T>() source,
before composition or assigning a query variable. For `FromSql*`, apply it immediately after that
call. Counts, existence checks and scalar-only projections use the same modifier even though they
do not track entities. This redundant syntax keeps one predictable convention without asking agents
or the analyzer to infer materialization. Keep the original context/factory's tracking default for
writes and opt out per read query. Do not use `AsTracking()` in read use cases.

Specific use-case interfaces inherit IReadUseCase or IWriteUseCase, both IUseCase. UseCaseBase keeps
the execution pipeline unchanged. CTFA006 requires exactly one classification on concrete types;
CTFA007 rejects EF SaveChanges, ExecuteUpdate/Delete and ExecuteSql-family calls and method
references in read code. CTFA009 checks immediate modifiers on direct query sources and rejects
EF AsTracking calls and method references. All rules use semantic symbols. Application installs
the analyzer explicitly. The original context and its factory are valid dependencies on both
execution paths.

The DI gate recognizes all three markers as classification rather than resolvable service contracts.
Specific use-case interfaces remain explicitly registered and validated.

## Consequences and limits

- Business reads share the existing context and factory with writes. They introduce no additional
  context type, factory registration or EF model cache entry.
- Ordinary read use cases project entities directly into their typed results. They do not need an
  incidental read item for each entity they query.
- There is no runtime save guard on the original context. The analyzer rejects known persistence
  calls within marked read types, including their local/nested helpers.
- External service/helper call graphs, inherited implementation in unmarked base classes,
  reflection, dynamic dispatch and arbitrary ADO.NET are not analyzed transitively. The context is
  not a database read-only credential or an absolute prohibition of side effects.
- A read use case must not invoke a write use case or delegate persistence to another service. This
  remains a review rule; the analyzer does not prove the absence of every indirect side effect.
- NoTracking controls query materialization. It does not disable SaveChanges, explicit attachment,
  bulk/SQL writes or side effects in helpers, logging and middleware.
- CTFA009 covers direct DbSet members, Set<T>() and FromSql* sources, without following query data
  flow through external helpers or custom providers. Agents and review enforce the convention on
  queries outside that coverage. Logging/middleware outside read-use-case types is not in scope.
- Existing Server/Console consumers use the original factory. The Auto spike only serves incidental
  queries and receives no new registrations, routes or client capabilities.

## Alternatives

A business-read subtype overriding SaveChanges would also reject saves invoked by external helpers
on that instance. This extra runtime protection would require a context type and factory of its own,
while bulk/SQL writes would still depend on the analyzer. The selected default accepts that bounded
static protection and keeps one original context for business operations.

Requiring a modifier only when a query materializes entities avoids redundant syntax on counts and
scalar-only projections, but asks agents and reviewers to classify every query. Enforcing that
distinction through projections and helper calls adds query/data-flow analysis. The selected uniform
convention accepts the redundant syntax and checks only the obvious sources at build time.

## Validation

Tests cover classification, EF overloads and overrides, method groups, local helpers, unrelated
homonyms, failing consumer builds, original-context dependencies and tenant isolation. Tracking
tests cover entity/scalar queries, late or missing modifiers, multiple sources, AsTracking, static
calls, FromSql*, inherited/nested scope, generated code and the external-helper limitation.
Dashboard/export results, context disposal and the DI gate remain validated. Full solution
validation retains the experimental host without extending it.

## References

- [EF Core context lifetime and factories](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)
- [Tracking and custom projections](https://learn.microsoft.com/en-us/ef/core/querying/tracking#tracking-and-custom-projections)
- [Bulk writes bypass SaveChanges](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete)
