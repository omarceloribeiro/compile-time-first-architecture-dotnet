using System.ComponentModel.DataAnnotations;
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
    [property: Required(ErrorMessage = "Statement is required.")]
    [property: StringLength(
        QuestionShape.MaxStatementLength,
        ErrorMessage = "Statement must contain at most 4,000 characters.")]
    string Statement,
    Guid SubjectId,
    Guid GradeId,
    [property: EnumDataType(typeof(QuestionType), ErrorMessage = "Question type is invalid.")]
    QuestionType Type,
    [property: Required(ErrorMessage = "Options are required.")]
    IReadOnlyCollection<CreateQuestionOptionRequest> Options)
    : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var optionsAreValid = true;
        var index = 0;
        foreach (var option in Options)
        {
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(
                    option,
                    new ValidationContext(option),
                    results,
                    validateAllProperties: true))
            {
                optionsAreValid = false;
                foreach (var result in results)
                {
                    yield return new ValidationResult(
                        result.ErrorMessage,
                        [$"Options[{index}]"]);
                }
            }

            index++;
        }

        if (!optionsAreValid)
        {
            yield break;
        }

        if (Options.Select(x => x.Order).Distinct().Count() != Options.Count)
        {
            yield return new ValidationResult(
                "Option orders must be unique within the question.",
                [nameof(Options)]);
        }

        if (QuestionShape.AllowsCustomOptions(Type) &&
            Options.Count < QuestionShape.MinObjectiveOptions)
        {
            yield return new ValidationResult(
                "Objective questions require at least two options.",
                [nameof(Options)]);
        }

        if (Type == QuestionType.SingleChoice && Options.Count(x => x.IsCorrect) != 1)
        {
            yield return new ValidationResult(
                "Single-choice questions require exactly one correct option.",
                [nameof(Options)]);
        }

        if (Type == QuestionType.MultipleChoice && Options.All(x => !x.IsCorrect))
        {
            yield return new ValidationResult(
                "Multiple-choice questions require at least one correct option.",
                [nameof(Options)]);
        }

        if (Type == QuestionType.TrueOrFalse)
        {
            var orderedOptions = Options
                .OrderBy(x => x.Order)
                .Select(x => new QuestionOptionDraft(x.Text.Trim(), x.IsCorrect, x.Order))
                .ToArray();

            if (!QuestionShape.MatchesTrueOrFalseShape(orderedOptions) ||
                orderedOptions.Count(x => x.IsCorrect) != 1)
            {
                yield return new ValidationResult(
                    "True-or-false questions require ordered True and False options and exactly one correct option.",
                    [nameof(Options)]);
            }
        }

        if (!QuestionShape.UsesOptions(Type) && Options.Count != 0)
        {
            yield return new ValidationResult(
                "Open-text questions cannot contain options.",
                [nameof(Options)]);
        }
    }
}

public sealed record CreateQuestionOptionRequest(
    [property: Required(ErrorMessage = "Option text is required.")]
    [property: StringLength(
        QuestionShape.MaxOptionTextLength,
        ErrorMessage = "Option text must contain at most 1,000 characters.")]
    string Text,
    bool IsCorrect,
    [property: Range(
        1,
        QuestionShape.MaxOptions,
        ErrorMessage = "Option order must be between 1 and 100.")]
    int Order);
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
}
