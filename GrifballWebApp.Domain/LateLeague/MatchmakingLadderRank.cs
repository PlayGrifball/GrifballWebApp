using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using System.Linq.Expressions;

namespace GrifballWebApp.Domain.LateLeague;

/// <summary>
/// A player's place on a matchmaking season's ladder (Late League's ranked standings), as its ladder sheet gives it: their
/// league ("Gold 2", "Masters 3", "Placements (1/3)"), rank, league points, streak and MMR, and their place among the
/// season's players. One row per (MatchmakingSeasonID, UserID), a small aggregate of its own: the historical import writes a
/// season's rows as a batch from its sheet (<see cref="FromSheet"/>, <see cref="Restate"/>) and removes the ones the sheet
/// no longer has; nothing else changes them but a user merge (<see cref="ReassignUser"/>).
/// </summary>
[AggregateRoot]
public class MatchmakingLadderRank : AuditableEntity
{
    /// <summary>The longest league name a row keeps ("Placements (1/3)", "Challenger").</summary>
    public const int LeagueMaxLength = 50;
    /// <summary>The longest division a row keeps (a tier number, "Pro", or the sheet's points).</summary>
    public const int DivisionMaxLength = 20;
    public const int GamertagMaxLength = 50;

    /// <summary>For EF. A row begins from its sheet (<see cref="FromSheet"/>).</summary>
    private MatchmakingLadderRank() { }

    /// <summary>The ladder sheet's row of <paramref name="userID"/> for <paramref name="matchmakingSeasonID"/>.</summary>
    public static MatchmakingLadderRank FromSheet(int matchmakingSeasonID, int userID, LadderStanding standing)
        => Seed(standing, matchmakingSeasonID, userID);

    /// <summary>A row of saved entities by their keys, or of entities not saved yet (the navigations): for tests.</summary>
    internal static MatchmakingLadderRank Seed(LadderStanding standing, int matchmakingSeasonID = 0, int userID = 0,
        MatchmakingSeason? matchmakingSeason = null, User? user = null)
    {
        var row = new MatchmakingLadderRank
        {
            MatchmakingSeasonID = matchmakingSeason is { MatchmakingSeasonID: not 0 } ? matchmakingSeason.MatchmakingSeasonID : matchmakingSeasonID,
            MatchmakingSeason = matchmakingSeason!,
            UserID = user is { Id: not 0 } ? user.Id : userID,
            User = user!,
        };
        row.Record(standing);
        return row;
    }

    // The key is internal: the database makes it
    public int MatchmakingLadderRankID { get; internal set; }
    public int MatchmakingSeasonID { get; private set; }
    public MatchmakingSeason MatchmakingSeason { get; private set; } = null!;
    public int UserID { get; private set; }
    public User User { get; private set; } = null!;
    /// <summary>The gamertag the sheet lists the player under.</summary>
    public string Gamertag { get; private set; } = null!;
    /// <summary>The player's place on the ladder (1 for its top), as the sheet orders it or says; null when it gives none.</summary>
    public int? Position { get; private set; }
    /// <summary>The sheet's division column as it is: a tier number in the early seasons, Pro or Amateur in Season 5, a
    /// points figure in the bot's sheets; null when it gives none.</summary>
    public string? Division { get; private set; }
    /// <summary>The league (tier) the player finished in, e.g. "Gold 2", "Masters 3", "Placements (1/3)".</summary>
    public string? League { get; private set; }
    /// <summary>The rank within the league, where the sheet keeps it apart from the league (Gold, rank 2).</summary>
    public int? Rank { get; private set; }
    /// <summary>League points (LP).</summary>
    public int? LeaguePoints { get; private set; }
    /// <summary>The win (positive) or loss (negative) streak the player ended on.</summary>
    public int? Streak { get; private set; }
    public int? Mmr { get; private set; }
    public int? Wins { get; private set; }
    public int? Losses { get; private set; }

    /// <summary>The row's values, as its sheet gave them (<see cref="Record"/>). Computed: not mapped.</summary>
    public LadderStanding Standing => new()
    {
        Gamertag = Gamertag,
        Position = Position,
        Division = Division,
        League = League,
        Rank = Rank,
        LeaguePoints = LeaguePoints,
        Streak = Streak,
        Mmr = Mmr,
        Wins = Wins,
        Losses = Losses,
    };

    /// <summary>The row is the same player's of the same season as <paramref name="other"/> (the unique index's slot).</summary>
    public bool IsSameSlotAs(MatchmakingLadderRank other) => other is not null && MatchmakingSeasonID == other.MatchmakingSeasonID && UserID == other.UserID;

    /// <summary>
    /// The rows of <paramref name="toUserID"/> a row of <paramref name="fromUserID"/> among <paramref name="rows"/> shares a
    /// season with: what a merge of the one onto the other refuses first, as <see cref="ReassignUser"/> would break the
    /// unique index.
    /// </summary>
    public static Expression<Func<MatchmakingLadderRank, bool>> ClashesOnMerge(IQueryable<MatchmakingLadderRank> rows, int fromUserID, int toUserID)
        => r => r.UserID == toUserID && rows.Any(o => o.UserID == fromUserID && o.MatchmakingSeasonID == r.MatchmakingSeasonID);

    /// <summary>
    /// A re-run of the import: the sheet's row for the same player (<see cref="IsSameSlotAs"/>) replaces this one's values.
    /// Only what changed is written: the same sheet changes nothing. Refused (thrown) for another slot's row.
    /// </summary>
    public void Restate(MatchmakingLadderRank sheet)
    {
        if (!IsSameSlotAs(sheet))
            throw new InvalidOperationException("A sheet's row restates the ladder row of the same player and season only");
        Record(sheet.Standing);
    }

    /// <summary>
    /// A user merge: the row of <paramref name="fromUserID"/> becomes <paramref name="toUserID"/>'s. False, nothing changed,
    /// for a row of someone else. The merge refuses first where the target has a row of its own for the season
    /// (<see cref="ClashesOnMerge"/>).
    /// </summary>
    public bool ReassignUser(int fromUserID, int toUserID)
    {
        if (UserID != fromUserID)
            return false;
        UserID = toUserID;
        return true;
    }

    /// <summary>The sheet's values for the row, all of them. Refused (thrown) without a gamertag, for a place below 1, or for
    /// a negative count of wins or losses.</summary>
    private void Record(LadderStanding standing)
    {
        ArgumentNullException.ThrowIfNull(standing);
        ArgumentException.ThrowIfNullOrWhiteSpace(standing.Gamertag, nameof(standing));
        if (standing.Position is < 1)
            throw new ArgumentException($"A ladder place starts at 1, not {standing.Position}", nameof(standing));
        if (standing.Wins is < 0 || standing.Losses is < 0)
            throw new ArgumentException("A ladder row's wins and losses are never negative", nameof(standing));
        Gamertag = Cut(standing.Gamertag, GamertagMaxLength)!;
        Position = standing.Position;
        Division = Cut(standing.Division, DivisionMaxLength);
        League = Cut(standing.League, LeagueMaxLength);
        Rank = standing.Rank;
        LeaguePoints = standing.LeaguePoints;
        Streak = standing.Streak;
        Mmr = standing.Mmr;
        Wins = standing.Wins;
        Losses = standing.Losses;
    }

    private static string? Cut(string? value, int length)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        return trimmed.Length > length ? trimmed[..length] : trimmed;
    }
}

/// <summary>A ladder sheet's row for a player (<see cref="MatchmakingLadderRank"/>): every value but the gamertag may be missing.</summary>
public record LadderStanding
{
    public required string Gamertag { get; init; }
    public int? Position { get; init; }
    public string? Division { get; init; }
    public string? League { get; init; }
    public int? Rank { get; init; }
    public int? LeaguePoints { get; init; }
    public int? Streak { get; init; }
    public int? Mmr { get; init; }
    public int? Wins { get; init; }
    public int? Losses { get; init; }
}
