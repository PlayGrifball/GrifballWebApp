using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Integration.Test;

/// <summary>A controller's answers, as the API's conventions (#270) shape them.</summary>
internal static class ActionResultAssertions
{
    /// <summary>The result is a ProblemDetails refusal with this status (and detail, when given); returns it.</summary>
    internal static ProblemDetails AssertProblem(this IActionResult? result, int status, string? detail = null)
    {
        Assert.That(result, Is.InstanceOf<ObjectResult>());
        var objectResult = (ObjectResult)result!;
        Assert.That(objectResult.Value, Is.InstanceOf<ProblemDetails>(), "the body is a ProblemDetails, not a bare string");
        var problem = (ProblemDetails)objectResult.Value!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(objectResult.StatusCode, Is.EqualTo(status));
            if (detail is not null)
                Assert.That(problem.Detail, Is.EqualTo(detail));
        }
        return problem;
    }

    /// <summary>The same for an ActionResult&lt;T&gt;'s result.</summary>
    internal static ProblemDetails AssertProblem<T>(this ActionResult<T> result, int status, string? detail = null)
        => result.Result.AssertProblem(status, detail);

    /// <summary>A create's answer: a 201 (its Location from the action it names), and the body it carries.</summary>
    internal static T Created<T>(this ActionResult<T> result)
    {
        Assert.That(result.Result, Is.InstanceOf<CreatedAtActionResult>());
        return (T)((CreatedAtActionResult)result.Result!).Value!;
    }

    /// <summary>A queued job's answer: a 202, and the status it carries.</summary>
    internal static T Accepted<T>(this ActionResult<T> result)
    {
        Assert.That(result.Result, Is.InstanceOf<AcceptedResult>());
        return (T)((AcceptedResult)result.Result!).Value!;
    }
}
