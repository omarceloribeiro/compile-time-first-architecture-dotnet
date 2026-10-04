# SPEC-003 — Business reads over the original model

## Objective

Keep business reads on the original entity model by default, without intermediate read projections,
and reject accidental persistence at build time. See ADR 0013.

## Contracts and behavior

- Every concrete use case implements exactly one of IReadUseCase and IWriteUseCase, both IUseCase.
- Reads and writes use IDbContextFactory<SchoolDbContext>, with one context per operation.
  Dashboard and export query the original entities directly. There is no business-read context
  subtype or runtime SaveChanges override.
- Every EF read-use-case query uses AsNoTracking (or AsNoTrackingWithIdentityResolution) immediately
  after each DbSet/Set<T>() source, before composition or assigning a query variable. With FromSql*,
  apply it immediately after that call. Counts, existence checks and scalar-only projections follow
  the same convention. AsTracking is forbidden. Do not change the shared factory's tracking default
  for writes or apply the rule to logging/middleware outside read-use-case types.
- Incidental reads keep IReadSchoolDbFactory and IReadQueryExecutor.
- A separate business read model requires explicit medium/strong CQRS in the project's initial
  architecture, or a later approved ADR. This sample does not select that profile.
- No schema, tenant, request/result or HTTP contract changes.

## Build protection

CTFA006 rejects missing/ambiguous classification. CTFA007 rejects EF SaveChanges, bulk update/delete
and ExecuteSql-family calls and method references in read use cases. Recognition uses semantic
symbols, including inherited contracts and EF overrides. Original-context/factory dependencies
are allowed. External call graphs, reflection and arbitrary ADO.NET are outside static coverage;
no runtime guard blocks those writes on the original context. NoTracking does not prevent explicit
side effects in helpers or middleware.

CTFA009 reports missing immediate modifiers on direct DbSet member/Set<T>()/FromSql* sources and
rejects EF AsTracking calls and method references. It uses semantic symbols, without inferring
materialization or following query data flow through external helpers. Agents and reviewers enforce
the convention on queries outside these bounded checks too.

## Hosts and experimental boundary

Use the existing original-context factory in Server and Console, which execute the business use
cases. The Interactive Auto spike in SPEC-002 does not execute these use cases and needs no additional factory.
Keep it in full solution validation; do not add endpoints, providers, EDM or client references.

## Acceptance and tests

- Build consumers fail on the new diagnostics; valid writes and business queries still compile.
- All save overloads and known bulk/SQL writes are rejected by the analyzer in read-use-case code.
- The original factory resolves for business reads; all EF query sources opt out of tracking explicitly.
- CTFA009 rejects omissions on scalar and entity queries, late modifiers and AsTracking, while
  accepting both no-tracking modifiers and unrelated homonyms. Each source is checked independently.
- Writes and external logging/middleware remain outside the tracking rule; generated code and
  inherited/nested read-use-case scopes remain covered. Tests record the external-helper limitation.
- Tenant A/B and unresolved tenant reads remain isolated; writes retain the original tracking default.
- Dashboard/export results remain correct and contexts are disposed on completion or failure.
- Restore, Release build with the DI gate, and the full test suite pass.

## Related data specification

DATA-SPEC-001-QUESTIONS.md. SchoolDbContext retains schema ownership; no new migrations.
