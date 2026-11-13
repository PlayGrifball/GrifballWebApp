using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Dtos;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using HaloMatch = GrifballWebApp.Domain.Halo.Match;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// Seeding for the SeasonMatchService unit tests (SeasonMatchServiceUnitTests, SeasonMatchServiceManualGamesUnitTests),
/// on the fake IApplicationDbContext: a season with fixed dates, two teams of four players with Xbox accounts, a pool of
/// Xbox accounts on neither roster, and a season match between the teams. Integration.Test's SeasonMatchTestData, adapted.
/// </summary>
internal static class SeasonMatchUnitTestData
{
    public static readonly long[] HomeXbox = [101, 102, 103, 104];
    public static readonly long[] AwayXbox = [201, 202, 203, 204];
    public static readonly long[] OtherXbox = [301, 302, 303, 304, 305, 306, 307, 308];
    public static readonly long[] OtherFour = [301, 302, 303, 304];
    public static readonly long[] OtherFourMore = [305, 306, 307, 308];
    public static readonly long[] HomeThree = [101, 102, 103];
    public static readonly long[] HomeThreeAndRinger = [101, 102, 103, 301];
    public static readonly long[] HomeFirstTwo = [101, 102];
    public static readonly long[] HomeLastTwo = [103, 104];
    public static readonly long[] MixedFirst = [101, 102, 201, 202];
    public static readonly long[] MixedLast = [103, 104, 203, 204];

    public static readonly DateTime SeasonStart = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime SeasonEnd = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    /// <summary>An evening within the season.</summary>
    public static readonly DateTime InSeason = new(2026, 1, 15, 20, 0, 0, DateTimeKind.Utc);

    /// <summary>Halo 3 (GameVersionConfiguration's seed).</summary>
    public const int Halo3 = 1;

    public sealed record Seeded(Season Season, Team Home, Team Away, SeasonMatch SeasonMatch, User[] HomeUsers, User[] AwayUsers);

    /// <summary>
    /// The season ("Unit Season", <see cref="SeasonStart"/> to <see cref="SeasonEnd"/>; imported from the archive when
    /// <paramref name="historicalSourceID"/> is given), "Home Team" and "Away Team" with four players each (users
    /// "user101".. with Xbox accounts "GT101"..), the Xbox accounts <see cref="OtherXbox"/> with no user, and a season
    /// match between the teams; when <paramref name="withAwayTeam"/> is false the match has no away team yet.
    /// </summary>
    public static async Task<Seeded> SeedAsync(FakeApplicationDbContext context, int bestOf = 3, DateTime? scheduledTime = null,
        string? historicalSourceID = null, bool withAwayTeam = true)
    {
        var season = Season.Seed(seasonName: "Unit Season", historicalSourceID: historicalSourceID).WithDates(seasonStart: SeasonStart, seasonEnd: SeasonEnd);
        context.Seasons.Add(season);
        await context.SaveChangesAsync();

        var home = Team.Seed(seasonID: season.SeasonID, teamName: "Home Team");
        var away = Team.Seed(seasonID: season.SeasonID, teamName: "Away Team");
        context.Teams.AddRange(home, away);
        await context.SaveChangesAsync();

        var homeUsers = HomeXbox.Select(NewUser).ToArray();
        var awayUsers = AwayXbox.Select(NewUser).ToArray();
        foreach (var user in homeUsers)
            home.SeedPlayer(user: user);
        foreach (var user in awayUsers)
            away.SeedPlayer(user: user);
        foreach (var id in OtherXbox)
            context.XboxUsers.Add(XboxUser.FromXbox(id, $"GT{id}"));
        await context.SaveChangesAsync();

        var seasonMatch = SeasonMatch.Seed(season.SeasonID, home.TeamID, withAwayTeam ? away.TeamID : null, bestOf: bestOf, scheduledTime: scheduledTime);
        context.SeasonMatches.Add(seasonMatch);
        await context.SaveChangesAsync();

        return new Seeded(season, home, away, seasonMatch, homeUsers, awayUsers);
    }

    private static User NewUser(long xboxUserID)
    {
        var user = User.Seed(userName: $"user{xboxUserID}", displayName: $"User {xboxUserID}");
        user.TieToXbox(XboxUser.FromXbox(xboxUserID, $"GT{xboxUserID}"));
        return user;
    }

    /// <summary>A fixed match ID per number.</summary>
    public static Guid MatchID(int number) => Guid.Parse($"00000000-0000-0000-0000-{number:D12}");

    /// <summary>
    /// A Halo Infinite match of two teams (in-game teams 0 and 1), saved. Each player's kills are their XUID's last digit,
    /// their deaths 1 (team 0) or 2 (team 1), their Score (their goals) the last digit for team 0 and 0 for team 1.
    /// </summary>
    public static async Task<HaloMatch> AddMatchAsync(FakeApplicationDbContext context, int number, DateTime start,
        long[] team0, Outcomes team0Outcome, long[] team1, Outcomes team1Outcome, int team0Score = 5, int team1Score = 3)
    {
        var match = HaloMatch.Seed(MatchID(number), startTime: start, endTime: start.AddMinutes(10), duration: TimeSpan.FromMinutes(10));
        match.AddTeam(0, team0Score, team0Outcome);
        match.AddTeam(1, team1Score, team1Outcome);
        foreach (var x in team0)
            match.AddParticipant(0, x, new MatchParticipantStats { Kills = (int)(x % 10), Deaths = 1, Score = (int)(x % 10), PersonalScore = 100 });
        foreach (var x in team1)
            match.AddParticipant(1, x, new MatchParticipantStats { Kills = (int)(x % 10), Deaths = 2, Score = 0, PersonalScore = 50 });
        context.Matches.Add(match);
        await context.SaveChangesAsync();
        return match;
    }

    /// <summary>A game entered by hand on the match page, as the service leaves one: home (side 1) beat away (side 2)
    /// <paramref name="homeScore"/> to <paramref name="awayScore"/>, or the other way round. Not saved.</summary>
    public static RecordedGame ManualGame(Seeded seeded, string sourceKey, int homeScore, int awayScore, DateTime? playedAt = null)
    {
        var homeWon = homeScore > awayScore;
        var game = RecordedGame.Seed(sourceKey, gameVersionID: Halo3, origin: RecordedGameOrigin.Manual, playedAt: playedAt);
        game.SeedSide(1, seeded.Home.TeamID, homeWon ? Outcomes.Won : Outcomes.Lost, homeScore, seeded.Home.TeamName);
        game.SeedSide(2, seeded.Away.TeamID, homeWon ? Outcomes.Lost : Outcomes.Won, awayScore, seeded.Away.TeamName);
        return game;
    }

    /// <summary>A four-team double elimination bracket's slots: W1 = seed 1 vs seed 4, W2 = seed 3 vs seed 2, W3 = the
    /// winners of W1 and W2, L1 = their losers, L2 = W3's loser vs L1's winner, the grand final (W3's winner vs L2's), then
    /// its sudden death.</summary>
    public sealed record Bracket4(int SeasonID, int[] SeedTeamIDs, List<MatchBracketInfo> BracketSlots)
    {
        /// <summary>A slot's season match: by its bracket, and its number there when the bracket has more than one.</summary>
        public int Sm(Bracket bracket, int? matchNumber = null) =>
            BracketSlots.Single(x => x.Bracket == bracket && (matchNumber == null || x.MatchNumber == matchNumber)).SeasonMatchID;
    }

    /// <summary>
    /// A season ("Bracket Season", the same dates) with four teams without players ("Seed 1".."Seed 4"), drawn into a
    /// double elimination bracket of best-of-1 matches by the real BracketService and seeded 1 to 4. The change tracker is
    /// cleared after.
    /// </summary>
    public static async Task<Bracket4> SeedDoubleEliminationAsync(FakeApplicationDbContext context, BracketService bracketService)
    {
        var season = Season.Seed(seasonName: "Bracket Season").WithDates(seasonStart: SeasonStart, seasonEnd: SeasonEnd);
        context.Seasons.Add(season);
        await context.SaveChangesAsync();
        var teams = Enumerable.Range(1, 4).Select(n => Team.Seed(seasonID: season.SeasonID, teamName: $"Seed {n}")).ToArray();
        context.Teams.AddRange(teams);
        await context.SaveChangesAsync();

        await bracketService.CreateBracket(4, season.SeasonID, doubleElimination: true, bestOf: 1);
        await bracketService.SetSeeds(season.SeasonID, teams.Select((t, i) => new CustomSeedDto { Seed = i + 1, TeamID = t.TeamID }).ToArray());
        var slots = await context.Set<MatchBracketInfo>().AsNoTracking().Where(x => x.SeasonMatch.SeasonID == season.SeasonID).ToListAsync();
        context.ChangeTracker.Clear();
        return new Bracket4(season.SeasonID, teams.Select(t => t.TeamID).ToArray(), slots);
    }

    /// <summary>A season match's two sides: each one's team and result.</summary>
    public static (int? HomeTeamID, SeasonMatchResult? HomeTeamResult, int? AwayTeamID, SeasonMatchResult? AwayTeamResult) Slots(SeasonMatch seasonMatch) =>
        (seasonMatch.HomeTeamID, seasonMatch.HomeTeamResult, seasonMatch.AwayTeamID, seasonMatch.AwayTeamResult);

    /// <summary>What <see cref="Slots"/> is expected to read.</summary>
    public static (int?, SeasonMatchResult?, int?, SeasonMatchResult?) Expect(int? homeTeamID, SeasonMatchResult? homeTeamResult, int? awayTeamID, SeasonMatchResult? awayTeamResult) =>
        (homeTeamID, homeTeamResult, awayTeamID, awayTeamResult);
}
