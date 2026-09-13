using CompileTimeFirst.Sample.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace CompileTimeFirst.Sample.BlazorServer.Services;

public sealed class CurrentUserCircuitHandler(
    AuthenticationStateProvider authenticationStateProvider,
    ClaimsCurrentUser currentUser,
    ILogger<CurrentUserCircuitHandler> logger)
    : CircuitHandler, IDisposable
{
    public override async Task OnCircuitOpenedAsync(
        Circuit circuit,
        CancellationToken cancellationToken)
    {
        authenticationStateProvider.AuthenticationStateChanged += AuthenticationChanged;
        await CaptureAsync(authenticationStateProvider.GetAuthenticationStateAsync());
    }

    public override Task OnConnectionUpAsync(
        Circuit circuit,
        CancellationToken cancellationToken) =>
        CaptureAsync(authenticationStateProvider.GetAuthenticationStateAsync());

    public void Dispose() =>
        authenticationStateProvider.AuthenticationStateChanged -= AuthenticationChanged;

    private void AuthenticationChanged(Task<AuthenticationState> authenticationStateTask) =>
        _ = CaptureAsync(authenticationStateTask);

    private async Task CaptureAsync(Task<AuthenticationState> authenticationStateTask)
    {
        try
        {
            currentUser.SetPrincipal((await authenticationStateTask).User);
        }
        catch (Exception exception)
        {
            currentUser.SetPrincipal(null);
            logger.LogError(exception, "Could not refresh the authenticated user for the Blazor circuit.");
        }
    }
}
