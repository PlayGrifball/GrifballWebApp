using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using GrifballWebApp.Application.Tools;

namespace GrifballWebApp.Application.Chat;

/// <summary>
/// How much of the one model each asker may have, whatever the host (the site, a Discord channel or DM): one reply at a time,
/// and <see cref="SignedInPerMinute"/> questions a minute for a site user, <see cref="OthersPerMinute"/> for anyone else (a
/// Discord user with no linked account, an address signed out). An asker is the site user when there is one, so their questions
/// count alike on the site and in Discord (<see cref="Asker"/>). And at most <see cref="MaxReplies"/> replies under way at all
/// (Chat:MaxReplies, 1 by default: the model answers one at a time, OLLAMA_NUM_PARALLEL): a question beyond them is refused at once
/// (<see cref="Full"/>), rather than wait behind the others until its time is up. Each process counts its own. And a user's spend on
/// paid models a month (UTC) at most <see cref="MonthlyLimit"/> US dollars (Chat:MonthlyLimit, 10 by default; the database counts it,
/// every process alike): reached, they are refused those models (<see cref="MonthlyLimitReached"/>) until the next month.
/// </summary>
public sealed class ChatLimits(int maxReplies = 1, decimal monthlyLimit = ChatLimits.DefaultMonthlyLimit) : IDisposable
{
    /// <summary>The setting for <see cref="MonthlyLimit"/>.</summary>
    public const string MonthlyLimitKey = "Chat:MonthlyLimit";
    public const decimal DefaultMonthlyLimit = 10m;

    public const string MonthlyLimitReached = "You've used this month's limit on the paid models: choose another model, or wait until next month.";

    /// <summary>The most a user may spend on paid models a month, in US dollars.</summary>
    public decimal MonthlyLimit { get; } = Math.Max(0m, monthlyLimit);

    /// <summary>The setting for <see cref="MaxReplies"/>.</summary>
    public const string MaxRepliesKey = "Chat:MaxReplies";
    public const int SignedInPerMinute = 10;
    public const int OthersPerMinute = 5;

    public const string Busy = "Wait for the answer to your last question first.";
    public const string TooMany = "That's a lot of questions: wait a minute, then ask again.";
    public const string Full = "The chat is busy answering someone else: try again shortly.";

    /// <summary>How many replies may be under way at once, every asker's together.</summary>
    public int MaxReplies { get; } = Math.Max(1, maxReplies);

    /// <summary>The replies under way.</summary>
    private int _replies;

    private const string UserPrefix = "user:";

    private readonly PartitionedRateLimiter<string> _questions = PartitionedRateLimiter.Create<string, string>(key =>
        RateLimitPartition.GetFixedWindowLimiter(key, k => new FixedWindowRateLimiterOptions
        {
            PermitLimit = k.StartsWith(UserPrefix, StringComparison.Ordinal) ? SignedInPerMinute : OthersPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));

    /// <summary>The askers with a reply under way, each with its slot.</summary>
    private readonly ConcurrentDictionary<string, Slot> _replying = new();

    /// <summary>Whose questions a caller's count: their site user's, else <paramref name="otherwise"/> (<see cref="Address"/>, <see cref="Discord"/>).</summary>
    public static string Asker(ToolCaller caller, string otherwise)
        => caller.UserID is { } id ? UserPrefix + id.ToString(CultureInfo.InvariantCulture) : otherwise;

    /// <summary>A Discord user with no linked site account.</summary>
    public static string Discord(ulong discordUserId) => "discord:" + discordUserId.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// A signed-out visitor by their address: an IPv4 address as it is, an IPv6 one by its /64 (a home or a host gets a whole /64,
    /// and could ask from a new address each time).
    /// </summary>
    public static string Address(IPAddress? address)
    {
        if (address is null)
            return "address:";
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        if (address.AddressFamily != AddressFamily.InterNetworkV6)
            return $"address:{address}";
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return $"address:{new IPAddress(bytes)}/64";
    }

    /// <summary>
    /// A reply for <paramref name="asker"/>: its slot, held until disposed, or why not (a reply already under way for them, the
    /// replies under way at their most, or their questions this minute used up: a question refused for the others uses none).
    /// </summary>
    public (IDisposable? Slot, string? Refusal) TryStart(string asker)
    {
        var slot = new Slot(this, asker);
        if (!_replying.TryAdd(asker, slot))
            return (null, Busy);
        if (Interlocked.Increment(ref _replies) > MaxReplies)
        {
            slot.Dispose();
            return (null, Full);
        }
        using var lease = _questions.AttemptAcquire(asker);
        if (lease.IsAcquired)
            return (slot, null);
        slot.Dispose();
        return (null, TooMany);
    }

    public void Dispose() => _questions.Dispose();

    /// <summary>
    /// A reply's hold on its asker's one slot and on one of the replies under way (taken as soon as the slot is): disposed (more than
    /// once is fine), it frees both, and no later reply's.
    /// </summary>
    private sealed class Slot(ChatLimits limits, string asker) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;
            limits._replying.TryRemove(KeyValuePair.Create(asker, this));
            Interlocked.Decrement(ref limits._replies);
        }
    }
}
