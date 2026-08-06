using CompileTimeFirst.Sample.Data;
using CompileTimeFirst.Sample.Domain;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Application.QuestionOptions;

public interface ICreateQuestionOptionUseCase : IUseCase
{
    Task<CreateQuestionOptionResult> ExecuteAsync(
        CreateQuestionOptionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CreateQuestionOptionRequest(
    Guid QuestionId,
    string Text,
    bool IsCorrect,
    int Order);

public sealed record CreateQuestionOptionResult(Guid OptionId);

public sealed class CreateQuestionOptionUseCase(
    IDbContextFactory<SchoolDbContext> contextFactory)
    : UseCaseBase<CreateQuestionOptionRequest, CreateQuestionOptionResult>,
      ICreateQuestionOptionUseCase
{
    protected override async Task<CreateQuestionOptionResult> ExecuteCoreAsync(
        CreateQuestionOptionRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var question = await db.Questions
            .Include(q => q.Options)
            .FirstOrDefaultAsync(x => x.Id == request.QuestionId, cancellationToken);

        if (question is null)
        {
            throw new InvalidOperationException("Question not found.");
        }

        var orderAlreadyExists = question.Options
            .Any(o => o.Order == request.Order);

        if (orderAlreadyExists)
        {
            throw new InvalidOperationException($"An option with order '{request.Order}' already exists for this question.");
        }

        // Validate business rules based on question type
        if (question.Type == QuestionType.TrueOrFalse && question.Options.Count >= 2)
        {
            throw new InvalidOperationException("True or False questions can only have 2 options.");
        }

        if (question.Type == QuestionType.OpenText)
        {
            throw new InvalidOperationException("Open text questions cannot have options.");
        }

        if (question.Type == QuestionType.SingleChoice && request.IsCorrect)
        {
            var hasCorrectAnswer = question.Options.Any(o => o.IsCorrect);
            if (hasCorrectAnswer)
            {
                throw new InvalidOperationException("Single choice questions can only have one correct answer.");
            }
        }

        var option = new QuestionOption
        {
            Id = Guid.NewGuid(),
            QuestionId = request.QuestionId,
            Text = request.Text.Trim(),
            IsCorrect = request.IsCorrect,
            Order = request.Order
        };

        db.QuestionOptions.Add(option);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateQuestionOptionResult(option.Id);
    }

    private static void Validate(CreateQuestionOptionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 1_000)
        {
            throw new ArgumentException("Option text must contain between 1 and 1,000 characters.");
        }

        if (request.Order < 1 || request.Order > 100)
        {
            throw new ArgumentException("Option order must be between 1 and 100.");
        }
    }
}
