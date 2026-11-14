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
/// count alike on the site and in Discord (<see cref="Asker"/>). Each process counts its own.
/// </summary>
public sealed class ChatLimits : IDisposable
{
    public const int SignedInPerMinute = 10;
    public const int OthersPerMinute = 5;

    public const string Busy = "Wait for the answer to your last question first.";
    public const string TooMany = "That's a lot of questions: wait a minute, then ask again.";

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
            return "address:" + address;
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return "address:" + new IPAddress(bytes) + "/64";
    }

    /// <summary>
    /// A reply for <paramref name="asker"/>: its slot, held until disposed, or why not (a reply already under way for them, or their
    /// questions this minute used up).
    /// </summary>
    public (IDisposable? Slot, string? Refusal) TryStart(string asker)
    {
        var slot = new Slot(this, asker);
        if (!_replying.TryAdd(asker, slot))
            return (null, Busy);
        using var lease = _questions.AttemptAcquire(asker);
        if (lease.IsAcquired)
            return (slot, null);
        slot.Dispose();
        return (null, TooMany);
    }

    public void Dispose() => _questions.Dispose();

    /// <summary>A reply's hold on its asker's one slot: disposed (more than once is fine), it frees that slot and no later one.</summary>
    private sealed class Slot(ChatLimits limits, string asker) : IDisposable
    {
        public void Dispose() => limits._replying.TryRemove(KeyValuePair.Create(asker, this));
    }
}
