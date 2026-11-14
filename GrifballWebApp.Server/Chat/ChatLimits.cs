using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.RateLimiting;
using GrifballWebApp.Application.Tools;

namespace GrifballWebApp.Server.Chat;

/// <summary>
/// How much of the model one person may have on the site's chat (<see cref="ChatHub"/>): one reply at a time per connection, and
/// <see cref="SignedInPerMinute"/> questions a minute per signed-in user, <see cref="SignedOutPerMinute"/> per address signed out.
/// Each API pod counts its own connections.
/// </summary>
public sealed class ChatLimits : IDisposable
{
    public const int SignedInPerMinute = 10;
    public const int SignedOutPerMinute = 5;

    public const string Busy = "Wait for the answer to your last question first.";
    public const string TooMany = "That's a lot of questions: wait a minute, then ask again.";

    private const string UserPrefix = "user:";

    private readonly PartitionedRateLimiter<string> _questions = PartitionedRateLimiter.Create<string, string>(key =>
        RateLimitPartition.GetFixedWindowLimiter(key, k => new FixedWindowRateLimiterOptions
        {
            PermitLimit = k.StartsWith(UserPrefix, StringComparison.Ordinal) ? SignedInPerMinute : SignedOutPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));

    /// <summary>The connections with a reply under way.</summary>
    private readonly ConcurrentDictionary<string, byte> _replying = new();

    /// <summary>Whose questions a caller's count: their user's, or signed out, their address's.</summary>
    public static string Partition(ToolCaller caller, string? address)
        => caller.UserID is { } id ? UserPrefix + id.ToString(CultureInfo.InvariantCulture) : "address:" + address;

    /// <summary>
    /// Starts a reply on <paramref name="connectionId"/> for <paramref name="partition"/>'s question, or says why not: a reply is
    /// already under way there, or the partition has asked its fill this minute. A reply started is ended with <see cref="Finish"/>.
    /// </summary>
    public string? TryStart(string connectionId, string partition)
    {
        if (!_replying.TryAdd(connectionId, 0))
            return Busy;
        using var lease = _questions.AttemptAcquire(partition);
        if (lease.IsAcquired)
            return null;
        Finish(connectionId);
        return TooMany;
    }

    /// <summary>The reply on <paramref name="connectionId"/> is over: it may ask again.</summary>
    public void Finish(string connectionId) => _replying.TryRemove(connectionId, out _);

    public void Dispose() => _questions.Dispose();
}
