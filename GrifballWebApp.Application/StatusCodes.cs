namespace GrifballWebApp.Application;

/// <summary>
/// The HTTP statuses a use case's refusal asks the web host to answer with (ASP.NET Core's StatusCodes, by the same names),
/// without the use cases depending on the web host.
/// </summary>
public static class StatusCodes
{
    public const int Status400BadRequest = 400;
    public const int Status409Conflict = 409;
}
