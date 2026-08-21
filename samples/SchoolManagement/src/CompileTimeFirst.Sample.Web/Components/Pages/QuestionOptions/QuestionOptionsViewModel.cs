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
    public const int QuestionsPageSize = 10;
    public const int OptionsPageSize = 10;

    public IReadOnlyList<QuestionListItem> Questions { get; private set; } = [];
    public IReadOnlyList<QuestionOptionReadItem> Options { get; private set; } = [];
    public int QuestionsCurrentPage { get; private set; }
    public int QuestionsTotalCount { get; private set; }
    public int QuestionsTotalPages => Math.Max(
        1,
        (QuestionsTotalCount + QuestionsPageSize - 1) / QuestionsPageSize);
    public bool HasPreviousQuestionsPage => QuestionsCurrentPage > 0;
    public bool HasNextQuestionsPage => QuestionsCurrentPage + 1 < QuestionsTotalPages;
    public int OptionsCurrentPage { get; private set; }
    public int OptionsTotalCount { get; private set; }
    public int OptionsTotalPages => Math.Max(
        1,
        (OptionsTotalCount + OptionsPageSize - 1) / OptionsPageSize);
    public bool HasPreviousOptionsPage => OptionsCurrentPage > 0;
    public bool HasNextOptionsPage => OptionsCurrentPage + 1 < OptionsTotalPages;

    public Guid? SelectedQuestionId { get; set; }
    public string NewText { get; set; } = string.Empty;
    public bool NewIsCorrect { get; set; }
    public int NewOrder { get; set; } = 1;

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public bool IsBusy { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await LoadQuestionsAsync(0, cancellationToken);

        if (SelectedQuestionId.HasValue)
        {
            await LoadOptionsAsync(0, cancellationToken);
        }
    }

    public async Task LoadQuestionsAsync(
        int page,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(0, page);
        await using var db = await readFactory.CreateAsync(cancellationToken);

        var questionsQuery = from q in db.Questions
                            join s in db.Subjects on q.SubjectId equals s.Id
                            join g in db.Grades on q.GradeId equals g.Id
                            orderby q.CreatedAt descending, q.Id
                            select new QuestionListItem(
                                q.Id,
                                q.Statement,
                                q.Type,
                                s.Name,
                                g.Name);

        var result = await executor.ToPageAsync(
            questionsQuery,
            page * QuestionsPageSize,
            QuestionsPageSize,
            cancellationToken);

        Questions = result.Items;
        QuestionsTotalCount = result.TotalCount;
        QuestionsCurrentPage = page;
    }

    public async Task LoadOptionsAsync(
        int page = 0,
        CancellationToken cancellationToken = default)
    {
        if (!SelectedQuestionId.HasValue)
        {
            Options = [];
            OptionsTotalCount = 0;
            OptionsCurrentPage = 0;
            return;
        }

        page = Math.Max(0, page);
        await using var db = await readFactory.CreateAsync(cancellationToken);
        var result = await executor.ToPageAsync(
            db.QuestionOptions
                .Where(o => o.QuestionId == SelectedQuestionId.Value)
                .OrderBy(o => o.Order)
                .ThenBy(o => o.Id),
            page * OptionsPageSize,
            OptionsPageSize,
            cancellationToken);

        Options = result.Items;
        OptionsTotalCount = result.TotalCount;
        OptionsCurrentPage = page;
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
            NewOrder = OptionsTotalCount + 1;
            SuccessMessage = $"Option created with id {result.OptionId}.";

            await LoadOptionsAsync(OptionsCurrentPage, cancellationToken);
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
        await LoadOptionsAsync(0, cancellationToken);
    }
}

public sealed record QuestionListItem(
    Guid Id,
    string Statement,
    QuestionType Type,
    string SubjectName,
    string GradeName);
