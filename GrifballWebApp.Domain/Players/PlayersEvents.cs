using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.Players;

// The Players context's domain events (IDomainEvent): what the site shows of a user and their accounts changed, and the
// merges of users. Each carries the aggregate it happened to, read once the change is saved.

/// <summary>
/// Something the site shows of the user changed: their name, their Xbox or Discord account, their region, whether they
/// are a dummy, their Late League record. A new placeholder of the import too.
/// </summary>
public sealed record UserChanged(User User) : IDomainEvent;

/// <summary>The Xbox account was found, renamed, or its past gamertags changed.</summary>
public sealed record XboxUserChanged(XboxUser XboxUser) : IDomainEvent;

/// <summary>The Discord account was found or renamed.</summary>
public sealed record DiscordUserChanged(DiscordUser DiscordUser) : IDomainEvent;

/// <summary>Two users were merged into one, or a merge was undone.</summary>
public sealed record UserMergeChanged(UserMerge Merge) : IDomainEvent;

/// <summary>A pair of users was dismissed as no duplicates.</summary>
public sealed record DuplicateDismissed(DuplicateDismissal Dismissal) : IDomainEvent;
