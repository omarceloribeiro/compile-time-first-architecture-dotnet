namespace CompileTimeFirst.Sample.Domain;

public sealed class Subject
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Grade
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public int Order { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum QuestionType
{
    SingleChoice = 1,
    MultipleChoice = 2,
    TrueOrFalse = 3,
    OpenText = 4
}

public sealed class Question
{
    public Guid Id { get; set; }
    public Guid SubjectId { get; set; }
    public Guid GradeId { get; set; }
    public required string Statement { get; set; }
    public QuestionType Type { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<QuestionOption> Options { get; set; } = [];
}

public sealed class QuestionOption
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public required string Text { get; set; }
    public bool IsCorrect { get; set; }
    public int Order { get; set; }
}
