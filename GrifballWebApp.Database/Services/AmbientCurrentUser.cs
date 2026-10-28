namespace GrifballWebApp.Database.Services;

/// <summary>
/// The current app user for the current async flow, for code with no DI scope of its own (contexts from
/// IDbContextFactory, background work started by a handler). The Discord equivalent of IHttpContextAccessor.
/// </summary>
public interface IAmbientCurrentUser
{
    /// <summary>
    /// The user, or null. Setting it inside a slot started by <see cref="Begin"/> is seen by everything in that slot,
    /// even when set from an awaited async method.
    /// </summary>
    int? UserId { get; set; }

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
            if (Current.Value is { } slot)
                slot.UserId = value;
            else
                // No enclosing slot: only this flow (until its async method returns) and what it starts see it
                Current.Value = new Slot { UserId = value };
        }
    }

    public void Begin() => Current.Value = new Slot();

    public void End()
    {
        if (Current.Value is { } slot)
            slot.UserId = null;
        Current.Value = null;
    }
}
