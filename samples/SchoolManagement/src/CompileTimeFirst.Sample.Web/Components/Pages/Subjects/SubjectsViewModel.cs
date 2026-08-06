using CompileTimeFirst.Sample.Application.Subjects;
using CompileTimeFirst.Sample.ReadModel;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Web.Components.Pages.Subjects;

public sealed class SubjectsViewModel(
    IReadSchoolDbFactory readFactory,
    IReadQueryExecutor executor,
    ICreateSubjectUseCase createSubject)
    : IViewModel
{
    public IReadOnlyList<SubjectReadItem> Items { get; private set; } = [];
    public string NewName { get; set; } = string.Empty;
    public string? ErrorMessage { get; private set; }
    public string? SuccessMessage { get; private set; }
    public bool IsBusy { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await readFactory.CreateAsync(cancellationToken);
        Items = await executor.ToListAsync(
            db.Subjects.Where(x => x.IsActive).OrderBy(x => x.Name),
            cancellationToken);
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
            await LoadAsync(cancellationToken);
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
