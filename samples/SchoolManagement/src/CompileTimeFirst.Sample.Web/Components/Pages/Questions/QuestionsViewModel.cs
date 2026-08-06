using CompileTimeFirst.Sample.Application.Questions;
using CompileTimeFirst.Sample.Domain;
using CompileTimeFirst.Sample.ReadModel;

namespace CompileTimeFirst.Sample.Web.Components.Pages.Questions;

public sealed class QuestionsViewModel(
    IReadSchoolDbFactory readFactory,
    IReadQueryExecutor executor,
    ICreateQuestionUseCase createQuestion)
    : IViewModel
{
    public IReadOnlyList<QuestionSelectOption> Subjects { get; private set; } = [];
    public IReadOnlyList<QuestionSelectOption> Grades { get; private set; } = [];
    public string Statement { get; set; } = string.Empty;
    public Guid? SubjectId { get; set; }
    public Guid? GradeId { get; set; }
    public QuestionType Type { get; set; } = QuestionType.SingleChoice;
    public List<QuestionOptionEditor> Options { get; private set; } = [];
    public string? ErrorMessage { get; private set; }
    public string? SuccessMessage { get; private set; }
    public bool IsBusy { get; private set; }

    public bool AllowsCustomOptions => Type is QuestionType.SingleChoice or QuestionType.MultipleChoice;
    public bool UsesOptions => Type != QuestionType.OpenText;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await readFactory.CreateAsync(cancellationToken);

        Subjects = await executor.ToListAsync(
            db.Subjects
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new QuestionSelectOption(x.Id, x.Name)),
            cancellationToken);

        Grades = await executor.ToListAsync(
            db.Grades
                .Where(x => x.IsActive)
                .OrderBy(x => x.Order)
                .Select(x => new QuestionSelectOption(x.Id, x.Name)),
            cancellationToken);

        ApplyTypeDefaults();
    }

    public void ApplyTypeDefaults()
    {
        ErrorMessage = null;

        if (Type == QuestionType.OpenText)
        {
            Options = [];
            return;
        }

        if (Type == QuestionType.TrueOrFalse)
        {
            Options =
            [
                new QuestionOptionEditor("True", true, 1),
                new QuestionOptionEditor("False", false, 2)
            ];
            return;
        }

        if (Options.Count < 2 || Options.Any(x => x.Text is "True" or "False"))
        {
            Options =
            [
                new QuestionOptionEditor(string.Empty, true, 1),
                new QuestionOptionEditor(string.Empty, false, 2)
            ];
        }

        NormalizeOrders();
        if (Type == QuestionType.SingleChoice)
        {
            MarkSingleCorrect(Options.First());
        }
    }

    public void AddOption()
    {
        if (!AllowsCustomOptions || Options.Count >= 100)
        {
            return;
        }

        Options.Add(new QuestionOptionEditor(string.Empty, false, Options.Count + 1));
    }

    public void RemoveOption(QuestionOptionEditor option)
    {
        if (!AllowsCustomOptions || Options.Count <= 2)
        {
            return;
        }

        Options.Remove(option);
        NormalizeOrders();

        if (Type == QuestionType.SingleChoice && Options.All(x => !x.IsCorrect))
        {
            Options[0].IsCorrect = true;
        }
    }

    public void MarkSingleCorrect(QuestionOptionEditor selected)
    {
        foreach (var option in Options)
        {
            option.IsCorrect = ReferenceEquals(option, selected);
        }
    }

    public async Task CreateAsync(CancellationToken cancellationToken = default)
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (SubjectId is null || GradeId is null)
        {
            ErrorMessage = "Subject and grade are required.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await createQuestion.ExecuteAsync(
                new CreateQuestionRequest(
                    Statement,
                    SubjectId.Value,
                    GradeId.Value,
                    Type,
                    Options.Select(x => new CreateQuestionOptionRequest(
                        x.Text,
                        x.IsCorrect,
                        x.Order)).ToArray()),
                cancellationToken);

            ResetEditor();
            SuccessMessage = $"Question created with id {result.QuestionId}.";
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ResetEditor()
    {
        Statement = string.Empty;
        SubjectId = null;
        GradeId = null;
        Type = QuestionType.SingleChoice;
        Options = [];
        ApplyTypeDefaults();
    }

    private void NormalizeOrders()
    {
        for (var index = 0; index < Options.Count; index++)
        {
            Options[index].Order = index + 1;
        }
    }
}

public sealed record QuestionSelectOption(Guid Id, string Name);

public sealed class QuestionOptionEditor(string text, bool isCorrect, int order)
{
    public string Text { get; set; } = text;
    public bool IsCorrect { get; set; } = isCorrect;
    public int Order { get; set; } = order;
}
