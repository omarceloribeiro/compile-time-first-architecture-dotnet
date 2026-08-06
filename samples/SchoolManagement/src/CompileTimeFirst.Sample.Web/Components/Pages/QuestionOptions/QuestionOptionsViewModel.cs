using CompileTimeFirst.Sample.Application.QuestionOptions;
using CompileTimeFirst.Sample.Domain;
using CompileTimeFirst.Sample.ReadModel;

namespace CompileTimeFirst.Sample.Web.Components.Pages.QuestionOptions;

public sealed class QuestionOptionsViewModel(
    IReadSchoolDbFactory readFactory,
    IReadQueryExecutor executor,
    ICreateQuestionOptionUseCase createQuestionOption)
    : IViewModel
{
    public IReadOnlyList<QuestionListItem> Questions { get; private set; } = [];
    public IReadOnlyList<QuestionOptionReadItem> Options { get; private set; } = [];

    public Guid? SelectedQuestionId { get; set; }
    public string NewText { get; set; } = string.Empty;
    public bool NewIsCorrect { get; set; }
    public int NewOrder { get; set; } = 1;

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public bool IsBusy { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await readFactory.CreateAsync(cancellationToken);

        var questionsQuery = from q in db.Questions
                            join s in db.Subjects on q.SubjectId equals s.Id
                            join g in db.Grades on q.GradeId equals g.Id
                            orderby q.CreatedAt descending
                            select new QuestionListItem(
                                q.Id,
                                q.Statement,
                                q.Type,
                                s.Name,
                                g.Name);

        Questions = await executor.ToListAsync(questionsQuery, cancellationToken);

        if (SelectedQuestionId.HasValue)
        {
            await LoadOptionsAsync(cancellationToken);
        }
    }

    public async Task LoadOptionsAsync(CancellationToken cancellationToken = default)
    {
        if (!SelectedQuestionId.HasValue)
        {
            Options = [];
            return;
        }

        await using var db = await readFactory.CreateAsync(cancellationToken);
        Options = await executor.ToListAsync(
            db.QuestionOptions
                .Where(o => o.QuestionId == SelectedQuestionId.Value)
                .OrderBy(o => o.Order),
            cancellationToken);
    }

    public async Task CreateAsync(CancellationToken cancellationToken = default)
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (!SelectedQuestionId.HasValue)
        {
            ErrorMessage = "Please select a question first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewText))
        {
            ErrorMessage = "Option text is required.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await createQuestionOption.ExecuteAsync(
                new CreateQuestionOptionRequest(
                    SelectedQuestionId.Value,
                    NewText,
                    NewIsCorrect,
                    NewOrder),
                cancellationToken);

            NewText = string.Empty;
            NewIsCorrect = false;
            NewOrder = Options.Count + 1;
            SuccessMessage = $"Option created with id {result.OptionId}.";

            await LoadOptionsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SelectQuestionAsync(Guid questionId, CancellationToken cancellationToken = default)
    {
        SelectedQuestionId = questionId;
        NewOrder = 1;
        await LoadOptionsAsync(cancellationToken);
    }
}

public sealed record QuestionListItem(
    Guid Id,
    string Statement,
    QuestionType Type,
    string SubjectName,
    string GradeName);
