using CompileTimeFirst.Sample.Application.Subjects;
using CompileTimeFirst.Sample.ReadModel;
namespace CompileTimeFirst.Sample.Web.Components.Pages.Subjects;

public sealed class SubjectsViewModel(
    IReadSchoolDbFactory readFactory,
    IReadQueryExecutor executor,
    ICreateSubjectUseCase createSubject)
    : IViewModel
{
    public const int PageSize = 10;

    public IReadOnlyList<SubjectReadItem> Items { get; private set; } = [];
    public int CurrentPage { get; private set; }
    public int TotalCount { get; private set; }
    public int TotalPages => Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
    public bool HasPreviousPage => CurrentPage > 0;
    public bool HasNextPage => CurrentPage + 1 < TotalPages;
    public string NewName { get; set; } = string.Empty;
    public string? ErrorMessage { get; private set; }
    public string? SuccessMessage { get; private set; }
    public bool IsBusy { get; private set; }

    public async Task LoadAsync(
        int page = 0,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(0, page);
        await using var db = await readFactory.CreateAsync(cancellationToken);
        var result = await executor.ToPageAsync(
            db.Subjects
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Id),
            page * PageSize,
            PageSize,
            cancellationToken);

        Items = result.Items;
        TotalCount = result.TotalCount;
        CurrentPage = page;
    }

    public async Task CreateAsync(CancellationToken cancellationToken = default)
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(NewName))
        {
            ErrorMessage = "Subject name is required.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await createSubject.ExecuteAsync(
                new CreateSubjectRequest(NewName), cancellationToken);

            NewName = string.Empty;
            SuccessMessage = $"Subject created with id {result.SubjectId}.";
            await LoadAsync(CurrentPage, cancellationToken);
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
}
