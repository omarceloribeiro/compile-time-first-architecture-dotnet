using CompileTimeFirst.Sample.Domain;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Data;

public sealed class SchoolDbContext(DbContextOptions<SchoolDbContext> options)
    : DbContext(options)
{
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Subject>().HasKey(x => x.Id);
        modelBuilder.Entity<Grade>().HasKey(x => x.Id);
        modelBuilder.Entity<Question>().HasKey(x => x.Id);
        modelBuilder.Entity<QuestionOption>().HasKey(x => x.Id);

        modelBuilder.Entity<Question>()
            .HasMany(x => x.Options)
            .WithOne()
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
