using CompileTimeFirst.Sample.ReadModel;

namespace CompileTimeFirst.Sample.Web.Client.Pages.AutoSubjects;

public sealed class AutoSubjectsViewModel(
    IReadSchoolDbFactory readFactory,
    IReadQueryExecutor executor,
    IReadProviderInfo providerInfo)
    : IViewModel
{
    public const int PageSize = 10;

    public IReadOnlyList<SubjectReadItem> Items { get; private set; } = [];
    public int CurrentPage { get; private set; }
    public int TotalCount { get; private set; }
    public int TotalPages => Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
    public bool HasPreviousPage => CurrentPage > 0;
    public bool HasNextPage => CurrentPage + 1 < TotalPages;
    public string ProviderName => providerInfo.Name;
    public string? ErrorMessage { get; private set; }

    public async Task LoadAsync(
        int page = 0,
        CancellationToken cancellationToken = default)
    {
        ErrorMessage = null;
        page = Math.Max(0, page);

        try
        {
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
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
    }
}
