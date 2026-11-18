using GrifballWebApp.Application.Live;
using GrifballWebApp.Server.SignalR;
using Microsoft.AspNetCore.SignalR;
using TypedSignalR.Client;

namespace GrifballWebApp.Server.Live;

/// <summary>
/// The site's live updates, one connection a tab: a page watches the topics of what it shows (<see cref="Watch"/>) and loads
/// them again when told one changed (<see cref="ILiveHubClient.Changed"/>, sent by <see cref="LiveUpdates"/> once a change
/// is committed); a player's own queue place comes to them whatever they watch. And who is on the site: each tab says which
/// page it shows (<see cref="Viewing"/>), and every tab is sent the count and the pages, as the footer shows them
/// (<see cref="ILiveHubClient.Presence"/>, <see cref="PresenceTracker"/>). Signed in or out; a connection is its user's
/// (the session cookie), so the client connects again on a sign-in or out.
/// </summary>
public class LiveHub(LiveTopicAccess access, HubUser user, PresenceTracker presence, PresencePages pages) : Hub<ILiveHubClient>, ILiveHubServer
{
    public const string Path = "LiveHub";

    /// <summary>The most topics a connection watches at once: a page watches a few.</summary>
    public const int MaxWatched = 32;

    private const string WatchedKey = "live-watched";

    private HashSet<string> Watched
    {
        get
        {
            if (Context.Items.TryGetValue(WatchedKey, out var watched) && watched is HashSet<string> set)
                return set;
            var created = new HashSet<string>(StringComparer.Ordinal);
            Context.Items[WatchedKey] = created;
            return created;
        }
    }

    public async Task<List<string>> Watch(List<string> topics)
    {
        var refused = new List<string>();
        foreach (var topic in topics.Distinct(StringComparer.Ordinal))
        {
            var watched = Watched;
            if (watched.Contains(topic))
                continue;
            // The caller's roles now (HubSessionFilter), not as the connection's claims had them
            if (watched.Count >= MaxWatched || !await access.MayWatch(topic, user.Roles, Context.ConnectionAborted))
            {
                refused.Add(topic);
                continue;
            }
            await Groups.AddToGroupAsync(Context.ConnectionId, topic, Context.ConnectionAborted);
            watched.Add(topic);
        }
        return refused;
    }

    public async Task Unwatch(List<string> topics)
    {
        foreach (var topic in topics.Distinct(StringComparer.Ordinal))
        {
            if (Watched.Remove(topic))
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, topic, Context.ConnectionAborted);
        }
    }

    public async Task<PresenceSnapshot> Viewing(string path)
    {
        var shown = await pages.Shown(path, Context.ConnectionAborted);
        await presence.Viewing(Context.ConnectionId, user.State.UserId, shown, Context.ConnectionAborted);
        return presence.Current;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        presence.Left(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

/// <summary>Methods that can be invoked on back end server (the typed client's hub proxy).</summary>
[Hub]
public interface ILiveHubServer
{
    /// <summary>
    /// Watches <paramref name="topics"/> (LiveTopics' names) until <see cref="Unwatch"/> or the connection ends. Returns those
    /// refused: one the caller may not see (a season not announced yet, a staff page's), one not spelt as the app spells it,
    /// or one past <see cref="LiveHub.MaxWatched"/>.
    /// </summary>
    Task<List<string>> Watch(List<string> topics);

    /// <summary>Stops watching <paramref name="topics"/>.</summary>
    Task Unwatch(List<string> topics);

    /// <summary>The tab shows the page at <paramref name="path"/> (the route, no query) now. Returns who is on the site.</summary>
    Task<PresenceSnapshot> Viewing(string path);
}

/// <summary>Methods that can be invoked on front end client (the typed client's receiver).</summary>
[Receiver]
public interface ILiveHubClient
{
    /// <summary>What <paramref name="topic"/> names changed: a page showing it loads it again.</summary>
    Task Changed(string topic);

    /// <summary>Who is on the site now.</summary>
    Task Presence(PresenceSnapshot snapshot);
}
