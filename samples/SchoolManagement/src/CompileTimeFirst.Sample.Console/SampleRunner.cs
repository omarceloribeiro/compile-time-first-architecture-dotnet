using CompileTimeFirst.Sample.Application.Dashboard;
using CompileTimeFirst.Sample.Application.Exports;
using CompileTimeFirst.Sample.Data;
using CompileTimeFirst.Sample.Domain;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.ConsoleApp;

public sealed class SampleRunner(
    IDbContextFactory<SchoolDbContext> writeFactory,
    QuestionEditorViewModel viewModel,
    IGetSchoolDashboardUseCase dashboardUseCase,
    IExportQuestionsUseCase exportQuestionsUseCase)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await SeedAsync(cancellationToken);
        await viewModel.LoadAsync(cancellationToken);

        var created = await viewModel.SaveDemoQuestionAsync(cancellationToken);
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

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        await using var seed = await writeFactory.CreateDbContextAsync(cancellationToken);
        if (await seed.Subjects.AnyAsync(cancellationToken))
        {
            return;
        }

        seed.Subjects.Add(new Subject { Id = Guid.NewGuid(), Name = "Computing" });
        seed.Grades.Add(new Grade { Id = Guid.NewGuid(), Name = "Grade 5", Order = 5 });
        await seed.SaveChangesAsync(cancellationToken);
    }
}
