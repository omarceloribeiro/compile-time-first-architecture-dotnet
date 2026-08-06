using System.Diagnostics;

namespace CompileTimeFirst.Sample.Application;

public interface IUseCase;

public abstract class UseCaseBase<TRequest, TResult> : IUseCase
{
    public async Task<TResult> ExecuteAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
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
}
