# Read Pattern

Use direct reads when the query is incidental to one interface. Keep the read scope and query local
to the operation, and terminate every query through `IReadQueryExecutor`.

## Dropdown

```csharp
await using var db = await readDbFactory.CreateAsync(cancellationToken);

Subjects = await executor.ToListAsync(
    db.Subjects
        .Where(x => x.IsActive)
        .OrderBy(x => x.Name)
        .Select(x => new SelectOption<Guid>(x.Id, x.Name)),
    cancellationToken);
```

The ViewModel owns the projection. It may change with the screen.

## Data grid, data table, result list, autocomplete or history

These controls use `ToPageAsync`:

```csharp
await using var db = await readDbFactory.CreateAsync(cancellationToken);

var query = db.Questions
    .Where(x => x.SubjectId == subjectId)
    .OrderByDescending(x => x.CreatedAt)
    .ThenBy(x => x.Id)
    .Select(x => new QuestionRow(x.Id, x.Statement, x.CreatedAt));

var page = await executor.ToPageAsync(
    query,
    skip,
    pageSize,
    cancellationToken);

Rows = page.Items;
TotalCount = page.TotalCount;
```

Every query passed to `ToPageAsync` defines deterministic ordering first. When the primary sort key
is not unique, add a stable unique tie-breaker such as `ThenBy(x => x.Id)`. This prevents rows with
equal sort values from moving between pages.

A component load callback performs this entire operation and receives only the materialized rows and
total count. Do not pass `query` or `db` to the component.

## Terminal selection

| Specified UI need | Terminal |
|---|---|
| Lookup by identifier | `FirstOrDefaultAsync` |
| Uniqueness | `SingleOrDefaultAsync` |
| Count | `CountAsync` |
| Existence | `AnyAsync` |
| Dropdown | `ToListAsync` |
| Data grid or data table | `ToPageAsync` |
| Result list | `ToPageAsync` |
| Autocomplete | `ToPageAsync` |
| History | `ToPageAsync` |
| Export | Read/export use case |

The specification chooses the control. Do not infer a different control from an expected row count.
Scalar terminals are also called only through `IReadQueryExecutor`, never through provider-specific
EF Core extensions in a ViewModel or component.

The read store exposes approved `IQueryable<T>` surfaces and cannot save changes. `IQueryable<T>`,
the read scope and DbContext never become ViewModel or component state.
