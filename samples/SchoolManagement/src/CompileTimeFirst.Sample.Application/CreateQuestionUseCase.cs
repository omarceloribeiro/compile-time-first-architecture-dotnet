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
    IDbContextFactory<SchoolDbContext> contextFactory,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
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
            throw new EntityNotFoundException("Subject or grade is invalid.");
        }

        var question = new Question(
            Guid.NewGuid(),
            RequireTenant(currentUser),
            request.SubjectId,
            request.GradeId,
            request.Statement.Trim(),
            request.Type,
            timeProvider.GetUtcNow());

        foreach (var option in request.Options.OrderBy(x => x.Order))
        {
            question.AddOption(option.Text.Trim(), option.IsCorrect, option.Order);
        }

        db.Questions.Add(question);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateQuestionResult(question.Id);
    }

    private static void Validate(CreateQuestionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var statement = request.Statement?.Trim();
        if (string.IsNullOrWhiteSpace(statement) || statement.Length > QuestionShape.MaxStatementLength)
        {
            throw new UseCaseValidationException("Statement must contain between 1 and 4,000 characters.");
        }

        if (!Enum.IsDefined(request.Type))
        {
            throw new UseCaseValidationException("Question type is invalid.");
        }

        ArgumentNullException.ThrowIfNull(request.Options);

        foreach (var option in request.Options)
        {
            var text = option.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text) || text.Length > QuestionShape.MaxOptionTextLength)
            {
                throw new UseCaseValidationException("Option text must contain between 1 and 1,000 characters.");
            }

            if (option.Order is < 1 || option.Order > QuestionShape.MaxOptions)
            {
                throw new UseCaseValidationException("Option order must be between 1 and 100.");
            }
        }

        if (request.Options.Select(x => x.Order).Distinct().Count() != request.Options.Count)
        {
            throw new UseCaseValidationException("Option orders must be unique within the question.");
        }

        if (QuestionShape.AllowsCustomOptions(request.Type) &&
            request.Options.Count < QuestionShape.MinObjectiveOptions)
        {
            throw new UseCaseValidationException("Objective questions require at least two options.");
        }

        if (request.Type == QuestionType.SingleChoice && request.Options.Count(x => x.IsCorrect) != 1)
        {
            throw new UseCaseValidationException("Single-choice questions require exactly one correct option.");
        }

        if (request.Type == QuestionType.MultipleChoice && request.Options.All(x => !x.IsCorrect))
        {
            throw new UseCaseValidationException("Multiple-choice questions require at least one correct option.");
        }

        if (request.Type == QuestionType.TrueOrFalse)
        {
            var orderedOptions = request.Options
                .OrderBy(x => x.Order)
                .Select(x => new QuestionOptionDraft(x.Text, x.IsCorrect, x.Order))
                .ToArray();

            if (!QuestionShape.MatchesTrueOrFalseShape(orderedOptions) ||
                orderedOptions.Count(x => x.IsCorrect) != 1)
            {
                throw new UseCaseValidationException(
                    "True-or-false questions require ordered True and False options and exactly one correct option.");
            }
        }

        if (!QuestionShape.UsesOptions(request.Type) && request.Options.Count != 0)
        {
            throw new UseCaseValidationException("Open-text questions cannot contain options.");
        }
    }
}
