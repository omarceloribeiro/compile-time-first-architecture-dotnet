using CompileTimeFirst.Sample.Application.Grades;
using CompileTimeFirst.Sample.ReadModel;

namespace CompileTimeFirst.Sample.Web.Components.Pages.Grades;

public sealed class GradesViewModel(
    IReadSchoolDbFactory readFactory,
    IReadQueryExecutor executor,
    ICreateGradeUseCase createGrade)
    : IViewModel
{
    public IReadOnlyList<GradeReadItem> Items { get; private set; } = [];
    public string NewName { get; set; } = string.Empty;
    public int NewOrder { get; set; } = 1;
    public string? ErrorMessage { get; private set; }
    public string? SuccessMessage { get; private set; }
    public bool IsBusy { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await readFactory.CreateAsync(cancellationToken);
        Items = await executor.ToListAsync(
            db.Grades.Where(x => x.IsActive).OrderBy(x => x.Order),
            cancellationToken);
    }

    public async Task CreateAsync(CancellationToken cancellationToken = default)
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (string.IsNullOrWhiteSpace(NewName))
        {
            ErrorMessage = "Grade name is required.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await createGrade.ExecuteAsync(
                new CreateGradeRequest(NewName, NewOrder), cancellationToken);

            NewName = string.Empty;
            NewOrder = 1;
            SuccessMessage = $"Grade created with id {result.GradeId}.";
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
