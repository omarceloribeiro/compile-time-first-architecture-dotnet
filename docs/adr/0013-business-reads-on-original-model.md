# ADR 0013 — Business reads use the original model with persistence guards

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
| Default read use case | Sealed original-model subtype, SaveChanges disabled, EF terminals |
| Explicit medium/strong CQRS read use case | Separate read model, read factory and IReadQueryExecutor |
| Incidental query | IReadDb, read factory and IReadQueryExecutor |

Medium/strong CQRS must be explicitly selected in the initial project architecture and specify the
required separation; a later adoption requires an approved ADR. No runtime flag, API style or
render mode selects it. A replica alone does not require projecting every domain entity into a
second model. The sample implements the default, with no CQRS synchronization infrastructure.

`QuerySchoolDbContext` inherits `SchoolDbContext`, including Identity, tenant filters and relational
mapping. It defaults to NoTracking and rejects all four save overloads, even with no changes and
through base references. Its factory stamps the tenant per operation. Only the original context
owns schema creation and migrations. `ReadOnlySchoolDbContext` retains the incidental projections.

Specific use-case interfaces inherit IReadUseCase or IWriteUseCase, both IUseCase. UseCaseBase keeps
the execution pipeline unchanged. CTFA006 requires exactly one classification on concrete types;
CTFA007 rejects EF SaveChanges, ExecuteUpdate/Delete and ExecuteSql-family calls and method
references in read code; CTFA008 rejects original writable context/factory dependencies. All rules
use semantic symbols. Application installs the analyzer explicitly.

The DI gate recognizes all three markers as classification rather than resolvable service contracts.
Specific use-case interfaces remain explicitly registered and validated.

## Consequences and limits

- Three context types express three roles without duplicating the original model or adding another
  database. EF may cache a model per context type; shared configuration is not a promise of one
  shared runtime model instance.
- Ordinary read use cases project entities directly into their typed results. They do not need an
  incidental read item for each entity they query.
- The runtime override intercepts SaveChanges only. ExecuteUpdate/Delete and ExecuteSql bypass it;
  the analyzer rejects known calls within marked read types, including their local/nested helpers.
- External service/helper call graphs, inherited implementation in unmarked base classes,
  reflection, dynamic dispatch and arbitrary ADO.NET are not analyzed transitively. The context is
  not a database read-only credential or an absolute prohibition of side effects.
- A read use case must not invoke a write use case or delegate persistence to another service. This
  remains a review rule; the analyzer does not prove the absence of every indirect side effect.
- Save rejection is an infrastructure programming error, not a new user-facing validation failure.
- Existing Server/Console consumers receive the new factory. The Auto spike only serves incidental
  queries and receives no new registrations, routes or client capabilities.

## Validation

Tests cover classification, EF overloads and overrides, method groups, local helpers, unrelated
homonyms, a failing consumer build, runtime save rejection, model equivalence, tenant isolation,
NoTracking, disposal, dashboard/export results and the DI gate. Full solution validation retains
the experimental host without extending it.

## References

- [EF Core context inheritance](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)
- [Bulk writes bypass SaveChanges](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete)
