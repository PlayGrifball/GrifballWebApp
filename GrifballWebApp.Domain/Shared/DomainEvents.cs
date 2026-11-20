namespace GrifballWebApp.Domain.Shared;

/// <summary>
/// Something that happened to an aggregate, raised by the aggregate's own method as it makes the change (a captain placed,
/// a series decided, a player signed up). Named in the past tense, it carries the aggregate it happened to: an aggregate
/// made by the same save has its key only once it is saved, and the event's handlers run after that (the Infrastructure's
/// DomainEventsInterceptor sends a save's events once it is committed, none for one rolled back).
/// </summary>
public interface IDomainEvent;

/// <summary>An aggregate (or a part of one) with the events its methods raised since its last save.</summary>
public interface IHasDomainEvents
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    /// <summary>Forgets the events raised so far: their save is done, and they are on their way.</summary>
    void ClearDomainEvents();
}

/// <summary>
/// Keeps the events an aggregate raises (<see cref="Raise"/>) until its save sends them. A field, not a column: EF maps no
/// field without a property, and the interface's members are explicit.
/// </summary>
public abstract class RaisesDomainEvents : IHasDomainEvents
{
    private readonly List<IDomainEvent> _raised = [];

    IReadOnlyList<IDomainEvent> IHasDomainEvents.DomainEvents => _raised;

    void IHasDomainEvents.ClearDomainEvents() => _raised.Clear();

    /// <summary>Records that <paramref name="domainEvent"/> happened, to be sent once the change is saved.</summary>
    protected void Raise(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _raised.Add(domainEvent);
    }
}
