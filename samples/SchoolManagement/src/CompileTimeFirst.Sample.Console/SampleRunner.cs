using CompileTimeFirst.Sample.Application.Dashboard;
using CompileTimeFirst.Sample.Application.Exports;
using CompileTimeFirst.Sample.Application.Questions;
using CompileTimeFirst.Sample.Data;
using CompileTimeFirst.Sample.Domain;
using CompileTimeFirst.Sample.ReadModel;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.ConsoleApp;

/// <summary>
/// Drives the console demo. A non-interactive host has no screen, so there is no component and no
/// screen state - the reads it needs are incidental to this run and stay inside the operation.
/// </summary>
public sealed class SampleRunner(
    IDbContextFactory<SchoolDbContext> writeFactory,
    IReadSchoolDbFactory readFactory,
    IReadQueryExecutor executor,
    ICreateQuestionUseCase createQuestionUseCase,
    IGetSchoolDashboardUseCase dashboardUseCase,
    IExportQuestionsUseCase exportQuestionsUseCase)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await SeedAsync(cancellationToken);

        var created = await CreateDemoQuestionAsync(cancellationToken);
        var dashboard = await dashboardUseCase.ExecuteAsync(
            new GetSchoolDashboardRequest(), cancellationToken);
        var jsonExport = await exportQuestionsUseCase.ExecuteAsync(
            new ExportQuestionsRequest(ExportFormat.Json), cancellationToken);

        System.Console.WriteLine($"Created question: {created.QuestionId}");
        System.Console.WriteLine(
            $"Dashboard: {dashboard.Subjects} subject(s), " +
            $"{dashboard.Grades} grade(s), {dashboard.Questions} question(s)");
        System.Console.WriteLine($"Export: {jsonExport.FileName}, {jsonExport.Content.Length} bytes");
    }

    private async Task<CreateQuestionResult> CreateDemoQuestionAsync(CancellationToken cancellationToken)
    {
        SelectOption subject;
        SelectOption grade;

        await using (var db = await readFactory.CreateAsync(cancellationToken))
        {
            subject = await ExecutorSingleAsync(
                db.Subjects
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.Name)
                    .Select(x => new SelectOption(x.Id, x.Name)),
                cancellationToken);

            grade = await ExecutorSingleAsync(
                db.Grades
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.Order)
                    .Select(x => new SelectOption(x.Id, x.Name)),
                cancellationToken);
        }

        var request = new CreateQuestionRequest(
            "What is a variable?",
            subject.Id,
            grade.Id,
            QuestionType.SingleChoice,
            [
                new CreateQuestionOptionRequest("A named storage location", true, 1),
                new CreateQuestionOptionRequest("A fixed programming language", false, 2)
            ]);

        return await createQuestionUseCase.ExecuteAsync(request, cancellationToken);
    }

    private async Task<SelectOption> ExecutorSingleAsync(
        IQueryable<SelectOption> query,
        CancellationToken cancellationToken) =>
        await executor.FirstOrDefaultAsync(query, cancellationToken)
            ?? throw new InvalidOperationException("The demo seed did not produce the expected catalog row.");

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        await using var seed = await writeFactory.CreateDbContextAsync(cancellationToken);
        await seed.Database.EnsureCreatedAsync(cancellationToken);

        if (await seed.Subjects.AnyAsync(cancellationToken))
        {
            return;
        }

        var tenantId = CompositionRoot.DemoTenantId;
        seed.Tenants.Add(new Tenant(tenantId, "Demo School"));
        seed.Subjects.Add(new Subject(Guid.NewGuid(), tenantId, "Computing"));
        seed.Grades.Add(new Grade(Guid.NewGuid(), tenantId, "Grade 5", 5));
        await seed.SaveChangesAsync(cancellationToken);
    }

    private sealed record SelectOption(Guid Id, string Name);
}
