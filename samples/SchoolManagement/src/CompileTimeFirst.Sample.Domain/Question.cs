namespace CompileTimeFirst.Sample.Domain;

public sealed class Question
{
    private readonly List<QuestionOption> _options = [];

    // Parameterless constructor for EF Core materialization only.
    private Question()
    {
    }

    public Question(
        Guid id,
        Guid tenantId,
        Guid subjectId,
        Guid gradeId,
        string statement,
        QuestionType type,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        SubjectId = subjectId;
        GradeId = gradeId;
        Statement = statement;
        Type = type;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SubjectId { get; private set; }
    public Guid GradeId { get; private set; }
    public string Statement { get; private set; } = string.Empty;
    public QuestionType Type { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<QuestionOption> Options => _options;

    public QuestionOption AddOption(string text, bool isCorrect, int order)
    {
        var option = new QuestionOption(Guid.NewGuid(), TenantId, Id, text, isCorrect, order);
        _options.Add(option);
        return option;
    }
}
