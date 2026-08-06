using CompileTimeFirst.Sample.Application.Questions;
using CompileTimeFirst.Sample.Data;
using CompileTimeFirst.Sample.Domain;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Tests;

public sealed class CreateQuestionUseCaseTests
{
    [Theory]
    [MemberData(nameof(ValidRequests))]
    public async Task Creates_valid_question_atomically(
        QuestionType type,
        IReadOnlyCollection<CreateQuestionOptionRequest> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var fixture = await QuestionFixture.CreateAsync();
        var useCase = new CreateQuestionUseCase(fixture.Factory);

        var result = await useCase.ExecuteAsync(
            new CreateQuestionRequest(
                "  What is the answer?  ",
                fixture.SubjectId,
                fixture.GradeId,
                type,
                options));

        await using var db = await fixture.Factory.CreateDbContextAsync();
        var question = await db.Questions.Include(x => x.Options).SingleAsync(x => x.Id == result.QuestionId);

        Assert.Equal("What is the answer?", question.Statement);
        Assert.Equal(options.Count, question.Options.Count);
    }

    [Fact]
    public async Task Rejects_duplicate_option_orders_without_persisting()
    {
        var fixture = await QuestionFixture.CreateAsync();
        var useCase = new CreateQuestionUseCase(fixture.Factory);
        var request = new CreateQuestionRequest(
            "Question",
            fixture.SubjectId,
            fixture.GradeId,
            QuestionType.SingleChoice,
            [new("One", true, 1), new("Two", false, 1)]);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(request));

        await using var db = await fixture.Factory.CreateDbContextAsync();
        Assert.Empty(await db.Questions.ToListAsync());
    }

    [Fact]
    public async Task Rejects_options_for_open_text()
    {
        var fixture = await QuestionFixture.CreateAsync();
        var useCase = new CreateQuestionUseCase(fixture.Factory);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(
            new CreateQuestionRequest(
                "Question",
                fixture.SubjectId,
                fixture.GradeId,
                QuestionType.OpenText,
                [new("Unexpected", false, 1)])));
    }

    [Fact]
    public async Task Rejects_invalid_true_or_false_shape()
    {
        var fixture = await QuestionFixture.CreateAsync();
        var useCase = new CreateQuestionUseCase(fixture.Factory);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(
            new CreateQuestionRequest(
                "Question",
                fixture.SubjectId,
                fixture.GradeId,
                QuestionType.TrueOrFalse,
                [new("Yes", true, 1), new("No", false, 2)])));
    }

    public static TheoryData<QuestionType, IReadOnlyCollection<CreateQuestionOptionRequest>> ValidRequests =>
        new()
        {
            {
                QuestionType.SingleChoice,
                new[] { new CreateQuestionOptionRequest("One", true, 1), new("Two", false, 2) }
            },
            {
                QuestionType.MultipleChoice,
                new[] { new CreateQuestionOptionRequest("One", true, 1), new("Two", true, 2) }
            },
            {
                QuestionType.TrueOrFalse,
                new[] { new CreateQuestionOptionRequest("True", true, 1), new("False", false, 2) }
            },
            { QuestionType.OpenText, Array.Empty<CreateQuestionOptionRequest>() }
        };

    private sealed record QuestionFixture(
        TestDbContextFactory<SchoolDbContext> Factory,
        Guid SubjectId,
        Guid GradeId)
    {
        public static async Task<QuestionFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<SchoolDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var factory = new TestDbContextFactory<SchoolDbContext>(() => new SchoolDbContext(options));
            var subjectId = Guid.NewGuid();
            var gradeId = Guid.NewGuid();

            await using var db = await factory.CreateDbContextAsync();
            db.Subjects.Add(new Subject { Id = subjectId, Name = "Computing" });
            db.Grades.Add(new Grade { Id = gradeId, Name = "Grade 5", Order = 5 });
            await db.SaveChangesAsync();

            return new QuestionFixture(factory, subjectId, gradeId);
        }
    }
}
