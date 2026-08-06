using CompileTimeFirst.Sample.ReadModel;

namespace CompileTimeFirst.Sample.Web.Client.Pages.AutoSubjects;

public sealed class AutoSubjectsViewModel(
    IReadSchoolDbFactory readFactory,
    IReadQueryExecutor executor,
    IReadProviderInfo providerInfo)
    : IViewModel
{
    public IReadOnlyList<SubjectReadItem> Items { get; private set; } = [];
    public string ProviderName => providerInfo.Name;
    public string? ErrorMessage { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        ErrorMessage = null;

        try
        {
            await using var db = await readFactory.CreateAsync(cancellationToken);
            Items = await executor.ToListAsync(
                db.Subjects
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.Name),
                cancellationToken);
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
    }
}
