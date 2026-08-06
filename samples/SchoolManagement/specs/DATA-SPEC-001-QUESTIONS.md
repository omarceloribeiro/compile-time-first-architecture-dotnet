# DATA-SPEC-001 — Questions

## Objective

Document the persistence invariants already represented by the School Management sample.

## Entities

- `Subject`: reference catalog item; inactive items cannot receive new questions.
- `Grade`: ordered reference catalog item; inactive items cannot receive new questions.
- `Question`: owns its statement, type, creation timestamp and answer options.
- `QuestionOption`: child of exactly one question.

## Relationships and cardinalities

- A Question references exactly one Subject.
- A Question references exactly one Grade.
- A Question owns zero or more QuestionOptions.
- Deleting a Question cascades to its QuestionOptions.

## Invariants

- QuestionOption order is unique inside a Question.
- Type-specific option invariants are defined by `SPEC-001-CREATE-QUESTION.md`.
- The sample uses application validation; a production relational model should also add an index unique on `(QuestionId, Order)`.

## Multi-tenancy

Not modeled in this sample. No tenant boundary may be inferred from the current entities.

## History and deletion

Question deletion and archival are outside the current sample. Subject and Grade use `IsActive` for catalog availability.

## Idempotency and versioning

Not modeled. Every successful create request produces a new Question identifier.

## Decisions pending before production use

- Authorization model for content editors.
- Archive/delete policy for Questions.
- Tenant isolation.
- Optimistic concurrency and audit history.

## Related functional specs

- `SPEC-001-CREATE-QUESTION.md`
