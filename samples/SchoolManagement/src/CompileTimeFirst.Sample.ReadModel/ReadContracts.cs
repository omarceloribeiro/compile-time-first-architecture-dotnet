using CompileTimeFirst.Sample.Domain;

namespace CompileTimeFirst.Sample.ReadModel;

public interface IReadSchoolDb
{
    IQueryable<SubjectReadItem> Subjects { get; }
    IQueryable<GradeReadItem> Grades { get; }
    IQueryable<QuestionReadItem> Questions { get; }
    IQueryable<QuestionOptionReadItem> QuestionOptions { get; }
}

public interface IReadSchoolDbScope : IReadSchoolDb, IAsyncDisposable;

public interface IReadSchoolDbFactory
{
    Task<IReadSchoolDbScope> CreateAsync(CancellationToken cancellationToken = default);
}

public interface IReadProviderInfo
{
    string Name { get; }
}

public sealed record PageResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount);

public interface IReadQueryExecutor
{
    Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);
    Task<PageResult<T>> ToPageAsync<T>(
        IQueryable<T> query,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
    Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);
    Task<T?> SingleOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);
    Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);
}

public sealed class SubjectReadItem
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public bool IsActive { get; init; }
}

public sealed class GradeReadItem
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public int Order { get; init; }
    public bool IsActive { get; init; }
}

public sealed class QuestionReadItem
{
    public Guid Id { get; init; }
    public Guid SubjectId { get; init; }
    public Guid GradeId { get; init; }
    public required string Statement { get; init; }
    public QuestionType Type { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class QuestionOptionReadItem
{
    public Guid Id { get; init; }
    public Guid QuestionId { get; init; }
    public required string Text { get; init; }
    public bool IsCorrect { get; init; }
    public int Order { get; init; }
}
