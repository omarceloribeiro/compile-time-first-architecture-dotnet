using CompileTimeFirst.Sample.Data;
using CompileTimeFirst.Sample.ReadModel;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Web.OData;

public sealed class SchoolODataReadScope(
    IDbContextFactory<ReadOnlySchoolDbContext> contextFactory)
    : IReadSchoolDbScope
{
    private readonly ReadOnlySchoolDbContext _db = contextFactory.CreateDbContext();

    public IQueryable<SubjectReadItem> Subjects => _db.Subjects;
    public IQueryable<GradeReadItem> Grades => _db.Grades;
    public IQueryable<QuestionReadItem> Questions => _db.Questions;
    public IQueryable<QuestionOptionReadItem> QuestionOptions => _db.QuestionOptions;

    public ValueTask DisposeAsync() => _db.DisposeAsync();
}
