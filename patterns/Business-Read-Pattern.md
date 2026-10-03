# Business Read Pattern

Use a read use case when the query itself is a business capability:

- dashboard;
- progress summary;
- institutional indicators;
- report;
- home recommendation;
- auditable reusable view.

The specific interface implements `IReadUseCase`. The implementation inherits
`UseCaseBase<TRequest, TResult>` and implements `ExecuteCoreAsync`, returning a stable typed result.
By default, it uses `IDbContextFactory<SchoolDbContext>`, the same original-context factory as writes,
and queries the original entities with EF Core directly. Each operation creates and disposes its
own context; the factory stamps its tenant for the model's global query filters.

```csharp
using CompileTimeFirst.Sample.Application;
using CompileTimeFirst.Sample.Data;
using Microsoft.EntityFrameworkCore;

public interface IGetSchoolDashboardUseCase : IReadUseCase
{
    Task<GetSchoolDashboardResult> ExecuteAsync(
        GetSchoolDashboardRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record GetSchoolDashboardRequest;
public sealed record GetSchoolDashboardResult(int Subjects, int Grades, int Questions);

public sealed class GetSchoolDashboardUseCase(IDbContextFactory<SchoolDbContext> contextFactory)
    : UseCaseBase<GetSchoolDashboardRequest, GetSchoolDashboardResult>, IGetSchoolDashboardUseCase
{
    protected override async Task<GetSchoolDashboardResult> ExecuteCoreAsync(
        GetSchoolDashboardRequest request, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var subjects = await db.Subjects.AsNoTracking().CountAsync(cancellationToken);
        var grades = await db.Grades.AsNoTracking().CountAsync(cancellationToken);
        var questions = await db.Questions.AsNoTracking().CountAsync(cancellationToken);

        return new GetSchoolDashboardResult(subjects, grades, questions);
    }
}
```

Apply `AsNoTracking()` (or `AsNoTrackingWithIdentityResolution()` when needed) immediately after
each query source, before composition or assigning a query variable, including counts, existence
checks and scalar projections. For `FromSql*`, apply it immediately after that call. CTFA009 checks
direct sources and rejects `AsTracking`; keep the shared factory's tracking default for writes.

CTFA007 rejects known EF persistence calls in read use cases. A read use case must also never
delegate persistence to another use case or helper, even when the analyzer cannot follow that call.

A separate business read model, read factory and `IReadQueryExecutor` require medium/strong CQRS
explicitly selected in the initial architecture or a later approved ADR. See
[ADR 0013](../docs/adr/0013-business-reads-on-original-model.md). Incidental UI reads keep their
own boundary, described in [Read Pattern](Read-Pattern.md).
