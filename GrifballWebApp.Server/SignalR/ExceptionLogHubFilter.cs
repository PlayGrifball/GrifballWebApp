using Microsoft.AspNetCore.SignalR;

namespace GrifballWebApp.Server.SignalR;

public class ExceptionLogHubFilter : IHubFilter
{
    private readonly ILogger<ExceptionLogHubFilter> _logger;

    public ExceptionLogHubFilter(ILogger<ExceptionLogHubFilter> logger)
    {
        _logger = logger;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in the hub method: {MethodName}", invocationContext.HubMethodName);
            throw; // Re-throw the exception to ensure it propagates
        }

    }
}
