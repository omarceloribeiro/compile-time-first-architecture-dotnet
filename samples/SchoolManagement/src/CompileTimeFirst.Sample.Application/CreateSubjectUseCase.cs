using CompileTimeFirst.Sample.Data;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Application.Subjects;

public interface ICreateSubjectUseCase : IUseCase
{
    Task<CreateSubjectResult> ExecuteAsync(
        CreateSubjectRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CreateSubjectRequest(string Name);
public sealed record CreateSubjectResult(Guid SubjectId);

public sealed class CreateSubjectUseCase(
    IDbContextFactory<SchoolDbContext> contextFactory)
    : UseCaseBase<CreateSubjectRequest, CreateSubjectResult>,
      ICreateSubjectUseCase
{
    protected override async Task<CreateSubjectResult> ExecuteCoreAsync(
        CreateSubjectRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var nameAlreadyExists = await db.Subjects
            .AnyAsync(x => x.Name == request.Name.Trim() && x.IsActive, cancellationToken);

        if (nameAlreadyExists)
        {
            throw new InvalidOperationException($"An active subject with name '{request.Name}' already exists.");
        }

        var subject = new Domain.Subject
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            IsActive = true
        };

        db.Subjects.Add(subject);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateSubjectResult(subject.Id);
    }

    private static void Validate(CreateSubjectRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200)
        {
            throw new ArgumentException("Subject name must contain between 1 and 200 characters.");
        }
    }
}
