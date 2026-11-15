using System.Diagnostics;
using GrifballWebApp.Application.Chat;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// Turns on the chat's spans (<see cref="ChatModel.SourceName"/>), the Discord bot's (<see cref="MessageCreateHandler.ActivitySourceName"/>)
/// and a test's stand-in for a host's own (<see cref="Host"/>, the hub call's span SignalR starts) while it listens, as the app's
/// tracer does: for the hosts' tests of which trace a reply's spans are in.
/// </summary>
internal sealed class ChatSpans : IDisposable
{
    private const string HostName = "GrifballWebApp.Server.Test.Host";

    public static readonly ActivitySource Host = new(HostName);

    private readonly ActivityListener _listener = new()
    {
        // By name: the static source may be made only as the listener is added, and so be missed
        ShouldListenTo = source => source.Name is HostName or ChatModel.SourceName or MessageCreateHandler.ActivitySourceName,
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    };

    public ChatSpans() => ActivitySource.AddActivityListener(_listener);

    /// <summary><paramref name="span"/>'s parent, its parent's, and so on to the trace's root (last).</summary>
    public static List<Activity> Ancestors(Activity span)
    {
        var ancestors = new List<Activity>();
        for (var parent = span.Parent; parent is not null; parent = parent.Parent)
            ancestors.Add(parent);
        return ancestors;
    }

    public void Dispose() => _listener.Dispose();
}
