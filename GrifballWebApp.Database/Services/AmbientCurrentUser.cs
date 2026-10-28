namespace GrifballWebApp.Database.Services;

/// <summary>
/// The current app user for the current async flow, for code with no DI scope of its own (contexts from
/// IDbContextFactory, background work started by a handler). The Discord equivalent of IHttpContextAccessor.
/// </summary>
public interface IAmbientCurrentUser
{
    /// <summary>
    /// The user, or null. Setting it inside a slot started by <see cref="Begin"/> is seen by everything in that slot,
    /// even when set from an awaited async method. Setting it with no slot throws: the value would silently be lost
    /// as soon as the setting async method returned.
    /// </summary>
    int? UserId { get; set; }

    /// <summary>Whether this async flow is inside a slot started by <see cref="Begin"/>, so <see cref="UserId"/> can be set.</summary>
    bool IsActive { get; }

    /// <summary>
    /// Starts a new, empty slot for this async flow and everything it awaits or starts from here on.
    /// Call it from the method whose callees should see the user, before they run: an AsyncLocal assigned inside an
    /// async method does not flow back out to that method's caller.
    /// </summary>
    void Begin();

    /// <summary>Clears the current slot, also for flows that captured it (e.g. a Task.Run still running), and leaves this flow with none.</summary>
    void End();
}

/// <summary>
/// Holder pattern, like ASP.NET Core's HttpContextAccessor: the AsyncLocal holds a mutable <see cref="Slot"/>, so a
/// user set by a nested async method (after an await) is still seen by the code that called Begin and its other callees.
/// </summary>
public class AmbientCurrentUser : IAmbientCurrentUser
{
    private static readonly AsyncLocal<Slot?> Current = new();

    private sealed class Slot
    {
        public int? UserId;
    }

    public int? UserId
    {
        get => Current.Value?.UserId;
        set
        {
            if (Current.Value is not { } slot)
                throw new InvalidOperationException($"Call {nameof(Begin)}() before setting the ambient user, from the method whose callees should see it.");
            slot.UserId = value;
        }
    }

    public bool IsActive => Current.Value is not null;

    public void Begin() => Current.Value = new Slot();

    public void End()
    {
        if (Current.Value is { } slot)
            slot.UserId = null;
        Current.Value = null;
    }
}
