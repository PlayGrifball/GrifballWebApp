using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.Halo;

// The Halo context's domain events (IDomainEvent). Each carries the aggregate it happened to, read once the change is saved.

/// <summary>
/// A Halo Infinite match was pulled from Halo's stats API, or what is known of it changed (its game variant, its decoded
/// scores). Its players are <see cref="Match.Participants"/>, added after it is pulled.
/// </summary>
public sealed record HaloMatchChanged(Match Match) : IDomainEvent;
