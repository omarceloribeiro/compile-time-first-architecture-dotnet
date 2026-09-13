using System.ComponentModel.DataAnnotations;
using CompileTimeFirst.Sample.Data;
using CompileTimeFirst.Sample.Domain;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Application.Subjects;

public interface ICreateSubjectUseCase : IUseCase
{
    Task<CreateSubjectResult> ExecuteAsync(
        CreateSubjectRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CreateSubjectRequest(
    [property: Required(ErrorMessage = "Subject name is required.")]
    [property: StringLength(200, ErrorMessage = "Subject name must contain at most 200 characters.")]
    string Name);
public sealed record CreateSubjectResult(Guid SubjectId);

public sealed class CreateSubjectUseCase(
    IDbContextFactory<SchoolDbContext> contextFactory,
    ICurrentUser currentUser)
    : UseCaseBase<CreateSubjectRequest, CreateSubjectResult>,
      ICreateSubjectUseCase
{
    protected override async Task<CreateSubjectResult> ExecuteCoreAsync(
        CreateSubjectRequest request,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var nameAlreadyExists = await db.Subjects
            .AnyAsync(x => x.Name == request.Name.Trim() && x.IsActive, cancellationToken);

        if (nameAlreadyExists)
        {
            throw new UseCaseValidationException($"An active subject with name '{request.Name}' already exists.");
        }

        var subject = new Domain.Subject(Guid.NewGuid(), RequireTenant(currentUser), request.Name.Trim());

        db.Subjects.Add(subject);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateSubjectResult(subject.Id);
    }
}
