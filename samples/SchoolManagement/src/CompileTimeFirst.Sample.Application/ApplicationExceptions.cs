namespace CompileTimeFirst.Sample.Application;

/// <summary>
/// A request was rejected by a stated rule. Its message is written for the person who submitted it.
///
/// This exists so the UI can tell an expected rejection apart from a failure. An infrastructure
/// exception must never reach a screen as text: it means nothing to the user and leaks
/// implementation detail.
/// </summary>
public sealed class UseCaseValidationException : Exception
{
    public UseCaseValidationException(string error)
        : this([error])
    {
    }

    public UseCaseValidationException(IEnumerable<string> errors)
        : base(string.Join(" ", errors))
    {
        Errors = [.. errors.Distinct(StringComparer.Ordinal)];
    }

    public IReadOnlyList<string> Errors { get; }
}

/// <summary>
/// A referenced entity does not exist, or is not visible to the caller.
/// </summary>
public sealed class EntityNotFoundException(string message) : Exception(message);
