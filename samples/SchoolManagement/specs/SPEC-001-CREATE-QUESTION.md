# SPEC-001 — Create question

## Actor

School content editor.

## Objective

Create one question, including every required answer option, in a single operation.

## Preconditions

- The selected subject exists and is active.
- The selected grade exists and is active.

## Main flow

1. Load active subjects and grades.
2. Enter the statement and select the question type.
3. Enter the options required by that type.
4. Submit one typed `CreateQuestionRequest`.
5. Persist the question and options atomically and show the created identifier.

## Business rules

- Statement: 1 to 4,000 non-whitespace characters after trimming.
- Option text: 1 to 1,000 non-whitespace characters after trimming.
- Option order: unique within the question and between 1 and 100.
- `SingleChoice`: at least two options and exactly one correct option.
- `MultipleChoice`: at least two options and at least one correct option.
- `TrueOrFalse`: exactly two options, ordered as `True` and `False`, and exactly one correct option.
- `OpenText`: no options.

## Request

`CreateQuestionRequest(Statement, SubjectId, GradeId, Type, Options)`.

## Result

`CreateQuestionResult(QuestionId)`.

## Acceptance criteria

- [ ] A valid question is visible to read-side queries after creation.
- [ ] Invalid references and invalid option combinations are rejected before persistence.
- [ ] No partial question or option data is persisted after a failure.
- [ ] The Blazor form mirrors the rules but the use case remains authoritative.

## Minimum tests

- One valid case for each question type.
- Invalid/inactive subject or grade.
- Blank/oversized statement and option text.
- Missing, duplicate, out-of-range or incorrectly marked options.

## Related data specs

- `DATA-SPEC-001-QUESTIONS.md`
