using System.Text;
using System.Text.Json;
using CompileTimeFirst.Sample.Data;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Application.Exports;

public enum ExportFormat { Csv = 1, Json = 2 }

public interface IExportQuestionsUseCase : IUseCase
{
    Task<ExportFileResult> ExecuteAsync(
        ExportQuestionsRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ExportQuestionsRequest(ExportFormat Format);
public sealed record QuestionReportRow(Guid Id, string Statement, string Type, DateTimeOffset CreatedAt);
public sealed record QuestionReport(IReadOnlyList<QuestionReportRow> Rows);
public sealed record ExportFileResult(string FileName, string ContentType, byte[] Content);

public sealed class ExportQuestionsUseCase(
    IDbContextFactory<ReadOnlySchoolDbContext> contextFactory)
    : UseCaseBase<ExportQuestionsRequest, ExportFileResult>,
      IExportQuestionsUseCase
{
    protected override async Task<ExportFileResult> ExecuteCoreAsync(
        ExportQuestionsRequest request,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var rows = await db.Questions
            .OrderBy(x => x.CreatedAt)
            .Select(x => new QuestionReportRow(x.Id, x.Statement, x.Type.ToString(), x.CreatedAt))
            .ToListAsync(cancellationToken);

        var report = new QuestionReport(rows);

        return request.Format switch
        {
            ExportFormat.Json => ExportJson(report),
            ExportFormat.Csv => ExportCsv(report),
            _ => throw new NotSupportedException($"Format {request.Format} is not supported.")
        };
    }

    private static ExportFileResult ExportJson(QuestionReport report)
        => new(
            "questions.json",
            "application/json",
            JsonSerializer.SerializeToUtf8Bytes(report, new JsonSerializerOptions { WriteIndented = true }));

    private static ExportFileResult ExportCsv(QuestionReport report)
    {
        var builder = new StringBuilder("Id,Statement,Type,CreatedAt\n");
        foreach (var row in report.Rows)
        {
            builder.Append(row.Id).Append(',')
                .Append('"').Append(row.Statement.Replace("\"", "\"\"")).Append('"').Append(',')
                .Append(row.Type).Append(',')
                .Append(row.CreatedAt.ToString("O"))
                .AppendLine();
        }

        return new ExportFileResult("questions.csv", "text/csv", Encoding.UTF8.GetBytes(builder.ToString()));
    }
}
