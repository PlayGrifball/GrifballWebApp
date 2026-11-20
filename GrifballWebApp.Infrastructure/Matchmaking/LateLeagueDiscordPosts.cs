using System.Text;
using GrifballWebApp.Application.DomainEvents;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Infrastructure.Matchmaking;

/// <summary>
/// Late League's match posts (<see cref="MatchOutbox"/>), for its matches' domain events: queued on
/// <paramref name="outbox"/> before the change commits, in the match's group, so they are sent in order once it has and
/// never for one rolled back. A match the queue made is announced; one decided gets its result, with each player's MMR
/// before and after (the commit's <see cref="LateLeagueGameRecorded"/>); one called off says so.
/// </summary>
public sealed class LateLeagueDiscordPosts(GrifballContext context, IOutbox outbox)
    : IBeforeCommitHandler<LateLeagueMatchStarted>, IBeforeCommitHandler<LateLeagueMatchDecided>, IBeforeCommitHandler<LateLeagueMatchCanceled>
{
    public Task Handle(LateLeagueMatchStarted domainEvent, IReadOnlyList<IDomainEvent> commit, CancellationToken cancellationToken)
        => outbox.EnqueueAnnouncementAsync(domainEvent.Match.Id, mayBePosted: false, cancellationToken);

    public Task Handle(LateLeagueMatchCanceled domainEvent, IReadOnlyList<IDomainEvent> commit, CancellationToken cancellationToken)
        => outbox.EnqueueCanceledAsync(domainEvent.Match.Id, domainEvent.Unannounced, cancellationToken);

    public async Task Handle(LateLeagueMatchDecided domainEvent, IReadOnlyList<IDomainEvent> commit, CancellationToken cancellationToken)
    {
        var match = domainEvent.Match;
        int winnerID;
        TimeSpan? duration = null;
        if (match.MatchID is Guid haloMatchID)
        {
            // Reported: the Halo match's winning team, and how long it ran
            var halo = await context.Matches.AsNoTracking()
                .Where(m => m.MatchID == haloMatchID)
                .Select(m => new { m.Duration, Winner = m.MatchTeams.Where(t => t.Outcome == Outcomes.Won).Select(t => (int?)t.TeamID).FirstOrDefault() })
                .FirstAsync(cancellationToken);
            winnerID = halo.Winner ?? throw new InvalidOperationException($"Halo match {haloMatchID} has no winning team");
            duration = halo.Duration;
        }
        else
        {
            // Decided by vote: the side the players voted for
            winnerID = match.WinnerSide is MatchedSide.Home ? 0 : 1;
        }

        // Each player's MMR before the game, as recorded with it (the first, should one be recorded twice)
        var before = new Dictionary<long, int>();
        foreach (var game in commit.OfType<LateLeagueGameRecorded>())
            before.TryAdd(game.User.Id, game.MmrBefore);

        await outbox.EnqueueEndedAsync(new MatchEndedPayload(match.Id, winnerID, duration?.ToString() ?? "Unknown",
            MmrChanges(before, match, MatchedSide.Home), MmrChanges(before, match, MatchedSide.Away)), cancellationToken);
    }

    /// <summary>A side's players still in the match (kicked ones neither win nor lose), each "name: before -> after (change)".</summary>
    internal static string MmrChanges(IReadOnlyDictionary<long, int> before, MatchedMatch match, MatchedSide side)
    {
        var sb = new StringBuilder();
        var players = match.PlayersStillIn(side).ToList();
        foreach (var (player, index) in players.Select((player, index) => (player, index)))
        {
            sb.Append(player.User.DiscordUser?.DiscordUsername ?? player.User.XboxUser?.Gamertag);
            sb.Append(": ");
            int? old = before.TryGetValue(player.UserID, out var mmr) ? mmr : null;
            sb.Append(old is int known ? known.ToString(System.Globalization.CultureInfo.InvariantCulture) : "UNK");
            sb.Append(" -> ");
            sb.Append(player.User.MMR);
            sb.Append(" (");
            if (old is not int was)
            {
                sb.Append("UNK");
            }
            else
            {
                var change = player.User.MMR - was;
                if (change > 0)
                    sb.Append('+');
                sb.Append(change);
            }
            sb.Append(')');

            if (index != players.Count - 1)
                sb.AppendLine();
        }
        return sb.Length == 0 ? "No players" : sb.ToString(); // An empty embed field value is rejected by Discord
    }
}
