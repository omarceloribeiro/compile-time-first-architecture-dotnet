using CompileTimeFirst.Sample.Data;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Application.Dashboard;

public interface IGetSchoolDashboardUseCase : IUseCase
{
    Task<GetSchoolDashboardResult> ExecuteAsync(
        GetSchoolDashboardRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record GetSchoolDashboardRequest;
public sealed record GetSchoolDashboardResult(int Subjects, int Grades, int Questions);

public sealed class GetSchoolDashboardUseCase(
    IDbContextFactory<ReadOnlySchoolDbContext> contextFactory)
    : UseCaseBase<GetSchoolDashboardRequest, GetSchoolDashboardResult>,
      IGetSchoolDashboardUseCase
{
    protected override async Task<GetSchoolDashboardResult> ExecuteCoreAsync(
        GetSchoolDashboardRequest request,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var subjectCount = await db.Subjects.CountAsync(cancellationToken);
        var gradeCount = await db.Grades.CountAsync(cancellationToken);
        var questionCount = await db.Questions.CountAsync(cancellationToken);

        return new GetSchoolDashboardResult(subjectCount, gradeCount, questionCount);
    }
}
