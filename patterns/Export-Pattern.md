# Export Pattern

An export is a business read use case. Its specific interface implements `IReadUseCase`, and its
implementation inherits `UseCaseBase<TRequest, TResult>` and implements `ExecuteCoreAsync`.

1. The use case validates the actor, period and filters.
2. It creates and disposes one original context through `IDbContextFactory<SchoolDbContext>`.
   The factory supplies the tenant for the model's global query filters. It queries the original
   entities directly with EF Core, applying `AsNoTracking()` (or
   `AsNoTrackingWithIdentityResolution()`) immediately at each query source before composition.
3. It builds one strongly typed report model.
4. It delegates formatting to an exporter that receives the report model and never queries the database.
5. It returns a file result or a streaming handle.

Do not create one use case per format when the business report is the same.

The query conventions, persistence restrictions and explicit CQRS exception are the same as in
[Business Read Pattern](Business-Read-Pattern.md). A report model passed to an exporter is the
typed result of this query; it does not require a separate persistence read model.
