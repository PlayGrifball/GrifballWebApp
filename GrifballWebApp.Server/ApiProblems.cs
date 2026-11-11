using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace GrifballWebApp.Server;

/// <summary>
/// The API's refusals as ProblemDetails (RFC 9457), never a bare string: the status, a title for it, and the reason
/// written for the user as the detail (what the client shows). The same titles as <see cref="GlobalExceptionHandler"/>.
/// </summary>
public static class ApiProblems
{
    /// <summary>400: the request itself is wrong (a missing or invalid value).</summary>
    public static ObjectResult BadRequestProblem(this ControllerBase controller, string detail)
        => controller.Refusal(StatusCodes.Status400BadRequest, detail);

    /// <summary>401: the request needs a signed-in user and has none.</summary>
    public static ObjectResult UnauthorizedProblem(this ControllerBase controller, string detail)
        => controller.Refusal(StatusCodes.Status401Unauthorized, detail);

    /// <summary>403: the signed-in user may not do this.</summary>
    public static ObjectResult ForbiddenProblem(this ControllerBase controller, string detail)
        => controller.Refusal(StatusCodes.Status403Forbidden, detail);

    /// <summary>404: there is no such resource.</summary>
    public static ObjectResult NotFoundProblem(this ControllerBase controller, string detail)
        => controller.Refusal(StatusCodes.Status404NotFound, detail);

    /// <summary>409: the resource's current state refuses the change.</summary>
    public static ObjectResult ConflictProblem(this ControllerBase controller, string detail)
        => controller.Refusal(StatusCodes.Status409Conflict, detail);

    /// <summary>A refusal with this status; a 5xx's detail must say nothing of the server's internals.</summary>
    public static ObjectResult Refusal(this ControllerBase controller, int status, string detail)
    {
        var title = Title(status);
        // The app's factory adds the trace id; a controller made by hand (a unit test) has none, and gets the same shape
        var factory = controller.HttpContext?.RequestServices?.GetService(typeof(ProblemDetailsFactory)) as ProblemDetailsFactory;
        var problem = factory?.CreateProblemDetails(controller.HttpContext!, status, title, detail: detail)
            ?? new ProblemDetails { Status = status, Title = title, Detail = detail };
        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
    }

    /// <summary>The title of a status: what kind of refusal it is (the detail says why).</summary>
    public static string Title(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Request refused",
        StatusCodes.Status401Unauthorized => "Not signed in",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not found",
        StatusCodes.Status409Conflict => "Conflict",
        >= 500 => "Server error",
        _ => "Request refused",
    };
}
