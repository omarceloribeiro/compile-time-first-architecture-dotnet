using CompileTimeFirst.Sample.Data;
using CompileTimeFirst.Sample.Domain;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Application.Questions;

public interface ICreateQuestionUseCase : IUseCase
{
    Task<CreateQuestionResult> ExecuteAsync(
        CreateQuestionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CreateQuestionRequest(
    string Statement,
    Guid SubjectId,
    Guid GradeId,
    QuestionType Type,
    IReadOnlyCollection<CreateQuestionOptionRequest> Options);

public sealed record CreateQuestionOptionRequest(string Text, bool IsCorrect, int Order);
public sealed record CreateQuestionResult(Guid QuestionId);

public sealed class CreateQuestionUseCase(
    IDbContextFactory<SchoolDbContext> contextFactory)
    : UseCaseBase<CreateQuestionRequest, CreateQuestionResult>,
      ICreateQuestionUseCase
{
    protected override async Task<CreateQuestionResult> ExecuteCoreAsync(
        CreateQuestionRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var referencesExist =
            await db.Subjects.AnyAsync(x => x.Id == request.SubjectId && x.IsActive, cancellationToken) &&
            await db.Grades.AnyAsync(x => x.Id == request.GradeId && x.IsActive, cancellationToken);

        if (!referencesExist)
        {
            throw new InvalidOperationException("Subject or grade is invalid.");
        }

        var question = new Question
        {
            Id = Guid.NewGuid(),
            Statement = request.Statement.Trim(),
            SubjectId = request.SubjectId,
            GradeId = request.GradeId,
            Type = request.Type,
            CreatedAt = DateTimeOffset.UtcNow,
            Options = request.Options
                .OrderBy(x => x.Order)
                .Select(x => new QuestionOption
                {
                    Id = Guid.NewGuid(),
                    Text = x.Text.Trim(),
                    IsCorrect = x.IsCorrect,
                    Order = x.Order
                })
                .ToList()
        };

        db.Questions.Add(question);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateQuestionResult(question.Id);
    }

    private static void Validate(CreateQuestionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var statement = request.Statement?.Trim();
        if (string.IsNullOrWhiteSpace(statement) || statement.Length > 4_000)
        {
            throw new ArgumentException("Statement must contain between 1 and 4,000 characters.");
        }

        if (!Enum.IsDefined(request.Type))
        {
            throw new ArgumentException("Question type is invalid.");
        }

        ArgumentNullException.ThrowIfNull(request.Options);

        foreach (var option in request.Options)
        {
            var text = option.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text) || text.Length > 1_000)
            {
                throw new ArgumentException("Option text must contain between 1 and 1,000 characters.");
            }

            if (option.Order is < 1 or > 100)
            {
                throw new ArgumentException("Option order must be between 1 and 100.");
            }
        }

        if (request.Options.Select(x => x.Order).Distinct().Count() != request.Options.Count)
        {
            throw new ArgumentException("Option orders must be unique within the question.");
        }

        if (request.Type is QuestionType.SingleChoice or QuestionType.MultipleChoice &&
            request.Options.Count < 2)
        {
            throw new ArgumentException("Objective questions require at least two options.");
        }

        if (request.Type == QuestionType.SingleChoice && request.Options.Count(x => x.IsCorrect) != 1)
        {
            throw new ArgumentException("Single-choice questions require exactly one correct option.");
        }

        if (request.Type == QuestionType.MultipleChoice && request.Options.All(x => !x.IsCorrect))
        {
            throw new ArgumentException("Multiple-choice questions require at least one correct option.");
        }

        if (request.Type == QuestionType.TrueOrFalse)
        {
            var orderedOptions = request.Options.OrderBy(x => x.Order).ToArray();
            var hasExpectedOptions = orderedOptions.Length == 2 &&
                orderedOptions[0].Order == 1 &&
                orderedOptions[0].Text.Trim().Equals("True", StringComparison.OrdinalIgnoreCase) &&
                orderedOptions[1].Order == 2 &&
                orderedOptions[1].Text.Trim().Equals("False", StringComparison.OrdinalIgnoreCase);

            if (!hasExpectedOptions || orderedOptions.Count(x => x.IsCorrect) != 1)
            {
                throw new ArgumentException(
                    "True-or-false questions require ordered True and False options and exactly one correct option.");
            }
        }

        if (request.Type == QuestionType.OpenText && request.Options.Count != 0)
        {
            throw new ArgumentException("Open-text questions cannot contain options.");
        }
    }
}
