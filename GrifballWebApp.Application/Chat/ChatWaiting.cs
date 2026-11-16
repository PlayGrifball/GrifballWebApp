namespace GrifballWebApp.Application.Chat;

/// <summary>
/// Where a change waits for the person's answer (<see cref="ChatAccounts.SaveWaitingAsync"/>): every API pod sees it, and of two
/// answers at once, on any pods, only one takes it (<see cref="TakeAsync"/>), so a change confirmed twice runs once. Scaled out,
/// Redis (Infrastructure's); in one process, <see cref="MemoryChatWaiting"/>.
/// </summary>
public interface IChatWaiting
{
    /// <summary><paramref name="action"/> waits under <paramref name="key"/> for <paramref name="expiresIn"/>, in place of any before it.</summary>
    Task SaveAsync(string key, ChatAction action, TimeSpan expiresIn, CancellationToken cancellationToken = default);

    /// <summary>The change waiting under <paramref name="key"/>, left waiting; null when none.</summary>
    Task<ChatAction?> PeekAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>The change waiting under <paramref name="key"/>, no longer waiting: of two takes at once, one gets it, the other null.</summary>
    Task<ChatAction?> TakeAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Nothing waits under <paramref name="key"/> any more.</summary>
    Task ForgetAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>The waiting changes of one process (no Redis): in its memory, each dropped once it expires.</summary>
public sealed class MemoryChatWaiting(TimeProvider time) : IChatWaiting
{
    private readonly Dictionary<string, (ChatAction Action, DateTimeOffset Until)> _waiting = [];
    private readonly Lock _lock = new();

    public Task SaveAsync(string key, ChatAction action, TimeSpan expiresIn, CancellationToken cancellationToken = default)
    {
        lock (_lock)
            _waiting[key] = (action, time.GetUtcNow() + expiresIn);
        return Task.CompletedTask;
    }

    public Task<ChatAction?> PeekAsync(string key, CancellationToken cancellationToken = default)
    {
        lock (_lock)
            return Task.FromResult(Live(key));
    }

    public Task<ChatAction?> TakeAsync(string key, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var action = Live(key);
            _waiting.Remove(key);
            return Task.FromResult(action);
        }
    }

    public Task ForgetAsync(string key, CancellationToken cancellationToken = default)
    {
        lock (_lock)
            _waiting.Remove(key);
        return Task.CompletedTask;
    }

    /// <summary>The change under <paramref name="key"/> while it waits; an expired one dropped. Under the lock.</summary>
    private ChatAction? Live(string key)
    {
        if (!_waiting.TryGetValue(key, out var waiting))
            return null;
        if (waiting.Until > time.GetUtcNow())
            return waiting.Action;
        _waiting.Remove(key);
        return null;
    }
}
