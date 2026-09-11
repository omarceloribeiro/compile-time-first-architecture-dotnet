namespace CompileTimeFirst.Sample.Domain;

public sealed class QuestionOption
{
    // Parameterless constructor for EF Core materialization only.
    private QuestionOption()
    {
    }

    public QuestionOption(Guid id, Guid tenantId, Guid questionId, string text, bool isCorrect, int order)
    {
        Id = id;
        TenantId = tenantId;
        QuestionId = questionId;
        Text = text;
        IsCorrect = isCorrect;
        Order = order;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid QuestionId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public bool IsCorrect { get; private set; }
    public int Order { get; private set; }
}
