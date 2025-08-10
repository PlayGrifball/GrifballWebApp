using GrifballWebApp.Database.Services;
using Microsoft.AspNetCore.SignalR;

namespace GrifballWebApp.Server.SignalR;

public class UserContextHubFilter : IHubFilter
{
    private readonly ICurrentUserService _currentUserService;

    public UserContextHubFilter(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        _currentUserService.SetCurrentUserIdFromClaims(invocationContext.Context.User);
        return next(invocationContext);
    }
}


