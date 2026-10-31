using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.Signups;
using GrifballWebApp.Server.Teams;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace GrifballWebApp.Server;

internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    /// <summary>What an unexpected error says: its message may hold internals (SQL, paths, keys), so it stays in the log.</summary>
    internal const string UnexpectedErrorDetail = "An unexpected error occurred. If it keeps happening, report it with the trace id.";

    private readonly ILogger<GlobalExceptionHandler> _logger;
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Client has disconnected");
            return true;
        }

        var problemDetails = Expected(exception);
        if (problemDetails is null)
        {
            _logger.LogError(exception, "Exception occurred: {Message}", exception.Message);
            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Server error",
                Detail = UnexpectedErrorDetail,
            };
        }
        else
        {
            _logger.LogInformation("Request refused ({Status}): {Message}", problemDetails.Status, exception.Message);
        }
        problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = problemDetails.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    /// <summary>
    /// The refusals the app means to make, whose messages are written for the user: these keep their message as the
    /// detail, with a 4xx status. Null for anything else.
    /// </summary>
    internal static ProblemDetails? Expected(Exception exception) => exception switch
    {
        TeamServiceException => Refusal(StatusCodes.Status400BadRequest, "Request refused", exception.Message),
        SignupsClosedException => Refusal(StatusCodes.Status400BadRequest, "Request refused", "Signups are closed for this season"),
        MergeConflictException => Refusal(StatusCodes.Status409Conflict, "Conflict", exception.Message),
        UserNotFoundException => Refusal(StatusCodes.Status404NotFound, "Not found", exception.Message),
        _ => null,
    };

    private static ProblemDetails Refusal(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
    };
}
