using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace CompileTimeFirst.Sample.Application;

public interface IUseCase;

public abstract partial class UseCaseBase<TRequest, TResult> : IUseCase
    where TRequest : notnull
{
    public async Task<TResult> ExecuteAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateDataAnnotations(request);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await BeforeExecuteAsync(request, cancellationToken);
            var result = await ExecuteCoreAsync(request, cancellationToken);
            await AfterExecuteAsync(request, result, cancellationToken);
            return result;
        }
        catch (Exception exception)
        {
            await OnExceptionAsync(request, exception, cancellationToken);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            await OnCompletedAsync(request, stopwatch.Elapsed, cancellationToken);
        }
    }

    protected abstract Task<TResult> ExecuteCoreAsync(
        TRequest request,
        CancellationToken cancellationToken);

    protected virtual Task BeforeExecuteAsync(TRequest request, CancellationToken cancellationToken)
        => Task.CompletedTask;

    protected virtual Task AfterExecuteAsync(TRequest request, TResult result, CancellationToken cancellationToken)
        => Task.CompletedTask;

    protected virtual Task OnExceptionAsync(TRequest request, Exception exception, CancellationToken cancellationToken)
        => Task.CompletedTask;

    protected virtual Task OnCompletedAsync(TRequest request, TimeSpan elapsed, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// The request declares its own constraints, so the contract and the validation are the same
    /// artifact. A use case does not re-check what its annotations already state.
    /// </summary>
    private static void ValidateDataAnnotations(TRequest request)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(
                request,
                new ValidationContext(request),
                results,
                validateAllProperties: true))
        {
            throw new UseCaseValidationException(
                results.Select(result => result.ErrorMessage ?? "Invalid value."));
        }
    }
}
