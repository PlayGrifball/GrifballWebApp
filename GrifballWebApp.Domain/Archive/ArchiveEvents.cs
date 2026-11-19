using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.Archive;

// The Archive context's domain events (IDomainEvent): the historical import's jobs and sources, its recorded games, and
// the gamertag claims. Each carries the aggregate it happened to, read once the change is saved.

/// <summary>A gamertag claim was made, decided, or moved by a merge.</summary>
public sealed record GamertagClaimChanged(GamertagClaim Claim) : IDomainEvent;

/// <summary>
/// An import job was queued, or a season of it was stopped for review or decided. Its runs' moves (claimed, progress,
/// finished) are single statements of the worker's, not the aggregate's methods: they raise nothing.
/// </summary>
public sealed record HistoricalImportJobChanged(HistoricalImportJob Job) : IDomainEvent;

/// <summary>A source of the archive was listed, changed, enabled or disabled, or downloaded.</summary>
public sealed record HistoricalSourceChanged(HistoricalSource Source) : IDomainEvent;

/// <summary>A recorded game was imported or entered by hand, or its facts, sides or players changed.</summary>
public sealed record RecordedGameChanged(RecordedGame Game) : IDomainEvent;
