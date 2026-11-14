namespace GrifballWebApp.Application.Identity;

/// <summary>
/// The users' live sessions beyond a request: the site's SignalR connections, on every API pod. Infrastructure ends them for each
/// user whose sign-in or roles change in the database (signed out, security stamp changed, locked, deleted, a role added or
/// removed); the web host implements it.
/// </summary>
public interface IUserSessions
{
    /// <summary>Closes every live connection of <paramref name="userIds"/>, wherever it is: they connect again as they are now.</summary>
    Task EndAsync(IReadOnlyCollection<int> userIds, CancellationToken ct = default);
}
