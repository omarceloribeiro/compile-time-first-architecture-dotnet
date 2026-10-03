# Write Pattern

A write use case represents one actor intention and one consistency boundary.

```csharp
public interface ICreateQuestionUseCase : IWriteUseCase
{
    Task<CreateQuestionResult> ExecuteAsync(
        CreateQuestionRequest request,
        CancellationToken cancellationToken = default);
}
```

The same file contains request, result and implementation. The implementation inherits
`UseCaseBase<CreateQuestionRequest, CreateQuestionResult>` and implements `ExecuteCoreAsync`.
It creates and disposes one original context per operation using `IDbContextFactory<SchoolDbContext>`.
The factory retains its tracking default for writes; the use case explicitly calls `SaveChangesAsync`.

The specific interface implements exactly one classification marker. `IWriteUseCase` already
inherits `IUseCase`; inheriting only `IUseCase` leaves a concrete implementation unclassified and
fails CTFA006.

Internal steps do not become endpoints unless the actor can invoke them independently.
