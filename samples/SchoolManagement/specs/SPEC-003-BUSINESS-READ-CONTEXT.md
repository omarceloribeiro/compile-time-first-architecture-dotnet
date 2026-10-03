# SPEC-003 — Business reads over the original model

## Objective

Keep business reads on the original entity model by default, without intermediate read projections,
and reject accidental persistence at build time and SaveChanges at runtime. See ADR 0013.

## Contracts and behavior

- Every concrete use case implements exactly one of IReadUseCase and IWriteUseCase, both IUseCase.
- Writes keep SchoolDbContext. Dashboard and export use QuerySchoolDbContext, a sealed derived
  context inheriting the original model and rejecting all four SaveChanges overloads.
- Incidental reads keep IReadSchoolDbFactory and IReadQueryExecutor.
- A separate business read model requires explicit medium/strong CQRS in the project's initial
  architecture, or a later approved ADR. This sample does not select that profile.
- No schema, tenant, request/result or HTTP contract changes.

## Build protection

CTFA006 rejects missing/ambiguous classification. CTFA007 rejects EF SaveChanges, bulk update/delete
and ExecuteSql-family calls and method references in read use cases. CTFA008 rejects writable
SchoolDbContext/factory dependencies. Recognition uses semantic symbols, including inherited
contracts and EF overrides. External call graphs, reflection and arbitrary ADO.NET are outside
static coverage; the runtime guard only intercepts SaveChanges.

## Hosts and experimental boundary

Register the query factory in Server and Console, which execute the business use cases. The existing
Interactive Auto spike in SPEC-002 does not execute these use cases and needs no additional factory.
Keep it in full solution validation; do not add endpoints, providers, EDM or client references.

## Acceptance and tests

- Build consumers fail on the new diagnostics; valid writes and business queries still compile.
- All save overloads fail through derived and base references, even with tracked changes.
- The original and query contexts have equivalent models, including Identity and tenant filters.
- Tenant A/B and unresolved tenant reads remain isolated; queries default to NoTracking.
- Dashboard/export results remain correct and contexts are disposed on completion or failure.
- Restore, Release build with the DI gate, and the full test suite pass.

## Related data specification

DATA-SPEC-001-QUESTIONS.md. SchoolDbContext retains schema ownership; no new migrations.
