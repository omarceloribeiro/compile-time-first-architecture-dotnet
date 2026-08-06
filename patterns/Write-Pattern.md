# Write Pattern

A write use case represents one actor intention and one consistency boundary.

```csharp
public interface ICreateQuestionUseCase : IUseCase
{
    Task<CreateQuestionResult> ExecuteAsync(
        CreateQuestionRequest request,
        CancellationToken cancellationToken = default);
}
```

The same file contains request, result and implementation. The implementation creates a write context using `IDbContextFactory<TContext>`.

Internal steps do not become endpoints unless the actor can invoke them independently.
