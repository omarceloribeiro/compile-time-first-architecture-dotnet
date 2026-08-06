# Read Pattern

Use direct reads when the query is incidental to one interface:

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

The read store exposes approved `IQueryable<T>` surfaces and cannot save changes.
