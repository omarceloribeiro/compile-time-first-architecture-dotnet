using CompileTimeFirst.Sample.Data;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Application.Grades;

public interface ICreateGradeUseCase : IUseCase
{
    Task<CreateGradeResult> ExecuteAsync(
        CreateGradeRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CreateGradeRequest(string Name, int Order);
public sealed record CreateGradeResult(Guid GradeId);

public sealed class CreateGradeUseCase(
    IDbContextFactory<SchoolDbContext> contextFactory)
    : UseCaseBase<CreateGradeRequest, CreateGradeResult>,
      ICreateGradeUseCase
{
    protected override async Task<CreateGradeResult> ExecuteCoreAsync(
        CreateGradeRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var nameAlreadyExists = await db.Grades
            .AnyAsync(x => x.Name == request.Name.Trim() && x.IsActive, cancellationToken);

        if (nameAlreadyExists)
        {
            throw new InvalidOperationException($"An active grade with name '{request.Name}' already exists.");
        }

        var orderAlreadyExists = await db.Grades
            .AnyAsync(x => x.Order == request.Order && x.IsActive, cancellationToken);

        if (orderAlreadyExists)
        {
            throw new InvalidOperationException($"An active grade with order '{request.Order}' already exists.");
        }

        var grade = new Domain.Grade
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Order = request.Order,
            IsActive = true
        };

        db.Grades.Add(grade);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateGradeResult(grade.Id);
    }

    private static void Validate(CreateGradeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100)
        {
            throw new ArgumentException("Grade name must contain between 1 and 100 characters.");
        }

        if (request.Order < 1 || request.Order > 20)
        {
            throw new ArgumentException("Grade order must be between 1 and 20.");
        }
    }
}
