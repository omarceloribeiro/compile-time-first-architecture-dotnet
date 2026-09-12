using System.ComponentModel.DataAnnotations;
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
    [property: Required(ErrorMessage = "Option text is required.")]
    [property: StringLength(1_000, ErrorMessage = "Option text must contain at most 1,000 characters.")]
    string Text,
    bool IsCorrect,
    [property: Range(1, 100, ErrorMessage = "Option order must be between 1 and 100.")]
    int Order);

public sealed record CreateQuestionOptionResult(Guid OptionId);

public sealed class CreateQuestionOptionUseCase(
    IDbContextFactory<SchoolDbContext> contextFactory,
    ICurrentUser currentUser)
    : UseCaseBase<CreateQuestionOptionRequest, CreateQuestionOptionResult>,
      ICreateQuestionOptionUseCase
{
    protected override async Task<CreateQuestionOptionResult> ExecuteCoreAsync(
        CreateQuestionOptionRequest request,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var question = await db.Questions
            .Include(q => q.Options)
            .FirstOrDefaultAsync(x => x.Id == request.QuestionId, cancellationToken);

        if (question is null)
        {
            throw new EntityNotFoundException("Question not found.");
        }

        var orderAlreadyExists = question.Options
            .Any(o => o.Order == request.Order);

        if (orderAlreadyExists)
        {
            throw new UseCaseValidationException($"An option with order '{request.Order}' already exists for this question.");
        }

        // Validate business rules based on question type
        if (question.Type == QuestionType.TrueOrFalse && question.Options.Count >= 2)
        {
            throw new UseCaseValidationException("True or False questions can only have 2 options.");
        }

        if (question.Type == QuestionType.OpenText)
        {
            throw new UseCaseValidationException("Open text questions cannot have options.");
        }

        if (question.Type == QuestionType.SingleChoice && request.IsCorrect)
        {
            var hasCorrectAnswer = question.Options.Any(o => o.IsCorrect);
            if (hasCorrectAnswer)
            {
                throw new UseCaseValidationException("Single choice questions can only have one correct answer.");
            }
        }

        var option = new QuestionOption(
            Guid.NewGuid(),
            RequireTenant(currentUser),
            request.QuestionId,
            request.Text.Trim(),
            request.IsCorrect,
            request.Order);

        db.QuestionOptions.Add(option);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateQuestionOptionResult(option.Id);
    }
}
