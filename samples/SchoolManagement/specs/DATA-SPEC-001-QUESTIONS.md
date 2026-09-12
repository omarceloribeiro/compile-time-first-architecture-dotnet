# DATA-SPEC-001 — Questions

## Objective

Document the persistence invariants already represented by the School Management sample.

## Entities

- `Subject`: reference catalog item; inactive items cannot receive new questions.
- `Grade`: ordered reference catalog item; inactive items cannot receive new questions.
- `Question`: owns its statement, type, creation timestamp and answer options.
- `QuestionOption`: child of exactly one question.
- `Tenant`: isolation boundary for school content.
- `SchoolUser`: ASP.NET Core Identity account bound to exactly one Tenant.

## Relationships and cardinalities

- A Question references exactly one Subject.
- A Question references exactly one Grade.
- A Question owns zero or more QuestionOptions.
- Deleting a Question cascades to its QuestionOptions.
- A Tenant has zero or more SchoolUsers; every SchoolUser references exactly one Tenant.

## Invariants

- QuestionOption order is unique inside a Question.
- Type-specific option invariants are defined by `SPEC-001-CREATE-QUESTION.md`.
- The sample uses application validation; a production relational model should also add an index unique on `(QuestionId, Order)`.

## Multi-tenancy

Subject, Grade, Question and QuestionOption are tenant-owned. Each carries `TenantId`, has a
`(TenantId, Id)` alternate key and is protected by the named global query filter `Tenant` in both
contexts. Foreign keys between tenant-owned entities include `TenantId`, making cross-tenant
references unrepresentable.

An unresolved tenant reads no tenant-owned rows. Writes obtain the tenant from `ICurrentUser`, never
from the request. ASP.NET Core Identity stores the account-to-tenant foreign key and emits its value
as the server-owned `tenant_id` claim. The same Identity cookie authenticates server components and
same-origin OData requests. Switching tenant requires logout and login as another account.

## History and deletion

Question deletion and archival are outside the current sample. Subject and Grade use `IsActive` for catalog availability.

## Idempotency and versioning

Not modeled. Every successful create request produces a new Question identifier.

## Decisions pending before production use

- Authorization model for content editors.
- Archive/delete policy for Questions.
- Optimistic concurrency and audit history.

## Related functional specs

- `SPEC-001-CREATE-QUESTION.md`
