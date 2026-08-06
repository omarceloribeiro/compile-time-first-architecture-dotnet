using CompileTimeFirst.Sample.Domain;
using CompileTimeFirst.Sample.ReadModel;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Data;

public sealed class ReadOnlySchoolDbContext(
    DbContextOptions<ReadOnlySchoolDbContext> options)
    : DbContext(options), IReadSchoolDbScope
{
    public IQueryable<SubjectReadItem> Subjects =>
        Set<Subject>().Select(x => new SubjectReadItem
        {
            Id = x.Id,
            Name = x.Name,
            IsActive = x.IsActive
        });

    public IQueryable<GradeReadItem> Grades =>
        Set<Grade>().Select(x => new GradeReadItem
        {
            Id = x.Id,
            Name = x.Name,
            Order = x.Order,
            IsActive = x.IsActive
        });

    public IQueryable<QuestionReadItem> Questions =>
        Set<Question>().Select(x => new QuestionReadItem
        {
            Id = x.Id,
            SubjectId = x.SubjectId,
            GradeId = x.GradeId,
            Statement = x.Statement,
            Type = x.Type,
            CreatedAt = x.CreatedAt
        });

    public IQueryable<QuestionOptionReadItem> QuestionOptions =>
        Set<QuestionOption>().Select(x => new QuestionOptionReadItem
        {
            Id = x.Id,
            QuestionId = x.QuestionId,
            Text = x.Text,
            IsCorrect = x.IsCorrect,
            Order = x.Order
        });

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Subject>().HasKey(x => x.Id);
        modelBuilder.Entity<Grade>().HasKey(x => x.Id);
        modelBuilder.Entity<Question>().HasKey(x => x.Id);
        modelBuilder.Entity<QuestionOption>().HasKey(x => x.Id);
    }

    public override int SaveChanges() => throw ReadOnlyException();
    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw ReadOnlyException();
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw ReadOnlyException();
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) => throw ReadOnlyException();

    private static InvalidOperationException ReadOnlyException() =>
        new("The read context does not allow persistence.");
}
