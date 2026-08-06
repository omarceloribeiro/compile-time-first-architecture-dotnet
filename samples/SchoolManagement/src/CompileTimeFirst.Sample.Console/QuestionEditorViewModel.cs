using CompileTimeFirst.Sample.Application.Questions;
using CompileTimeFirst.Sample.Domain;
using CompileTimeFirst.Sample.ReadModel;

namespace CompileTimeFirst.Sample.ConsoleApp;

public sealed record SelectOption<T>(T Value, string Text);

public interface IViewModel;

public sealed class QuestionEditorViewModel(
    IReadSchoolDbFactory readFactory,
    IReadQueryExecutor executor,
    ICreateQuestionUseCase createQuestionUseCase)
    : IViewModel
{
    public IReadOnlyList<SelectOption<Guid>> Subjects { get; private set; } = [];
    public IReadOnlyList<SelectOption<Guid>> Grades { get; private set; } = [];

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await readFactory.CreateAsync(cancellationToken);

        Subjects = await executor.ToListAsync(
            db.Subjects
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new SelectOption<Guid>(x.Id, x.Name)),
            cancellationToken);

        Grades = await executor.ToListAsync(
            db.Grades
                .Where(x => x.IsActive)
                .OrderBy(x => x.Order)
                .Select(x => new SelectOption<Guid>(x.Id, x.Name)),
            cancellationToken);
    }

    public Task<CreateQuestionResult> SaveDemoQuestionAsync(CancellationToken cancellationToken = default)
    {
        var request = new CreateQuestionRequest(
            "What is a variable?",
            Subjects.Single().Value,
            Grades.Single().Value,
            QuestionType.SingleChoice,
            [
                new("A named storage location", true, 1),
                new("A fixed programming language", false, 2)
            ]);

        return createQuestionUseCase.ExecuteAsync(request, cancellationToken);
    }
}
