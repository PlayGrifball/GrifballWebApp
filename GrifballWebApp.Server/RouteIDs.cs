namespace GrifballWebApp.Server;

/// <summary>
/// A change whose body also names the resource its route does (the draft's, a signup's, a user's edit): the route
/// says which, and a body that names another (not 0, "unset") is refused rather than silently applied elsewhere.
/// </summary>
public static class RouteIDs
{
    /// <summary>Whether the body's ID names another resource than the route's.</summary>
    public static bool Differs(int bodyID, int routeID) => bodyID != 0 && bodyID != routeID;

    /// <summary>The refusal's detail, ex. "The season in the body is not the route's".</summary>
    public static string Mismatch(string resource) => $"The {resource} in the body is not the route's";
}
