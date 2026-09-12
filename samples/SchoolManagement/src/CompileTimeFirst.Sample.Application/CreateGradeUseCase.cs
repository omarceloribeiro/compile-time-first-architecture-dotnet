using System.ComponentModel.DataAnnotations;
using CompileTimeFirst.Sample.Data;
using CompileTimeFirst.Sample.Domain;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Application.Grades;

public interface ICreateGradeUseCase : IUseCase
{
    Task<CreateGradeResult> ExecuteAsync(
        CreateGradeRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CreateGradeRequest(
    [property: Required(ErrorMessage = "Grade name is required.")]
    [property: StringLength(100, ErrorMessage = "Grade name must contain at most 100 characters.")]
    string Name,
    [property: Range(1, 20, ErrorMessage = "Grade order must be between 1 and 20.")]
    int Order);
public sealed record CreateGradeResult(Guid GradeId);

public sealed class CreateGradeUseCase(
    IDbContextFactory<SchoolDbContext> contextFactory,
    ICurrentUser currentUser)
    : UseCaseBase<CreateGradeRequest, CreateGradeResult>,
      ICreateGradeUseCase
{
    protected override async Task<CreateGradeResult> ExecuteCoreAsync(
        CreateGradeRequest request,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var nameAlreadyExists = await db.Grades
            .AnyAsync(x => x.Name == request.Name.Trim() && x.IsActive, cancellationToken);

        if (nameAlreadyExists)
        {
            throw new UseCaseValidationException($"An active grade with name '{request.Name}' already exists.");
        }

        var orderAlreadyExists = await db.Grades
            .AnyAsync(x => x.Order == request.Order && x.IsActive, cancellationToken);

        if (orderAlreadyExists)
        {
            throw new UseCaseValidationException($"An active grade with order '{request.Order}' already exists.");
        }

        var grade = new Domain.Grade(Guid.NewGuid(), RequireTenant(currentUser), request.Name.Trim(), request.Order);

        db.Grades.Add(grade);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateGradeResult(grade.Id);
    }
}
