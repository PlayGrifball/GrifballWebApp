using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// ProfileStatsService.GetAsync and ServiceRecord.LoadAsync against the fake IApplicationDbContext: who the player is
/// (gamertag, past gamertags, region), their titles, their season and Late League lines (stored totals here; the games
/// path is SeasonStatsServiceMoreUnitTests' and MatchmakingStatsServiceMoreUnitTests'), their career totals and the
/// service record strip, with its MMR and its Infinite games whose goals can't be read.
/// </summary>
[TestFixture]
internal sealed class ProfileStatsServiceUnitTests : ApplicationDbContextTests
{
    private const int HaloInfiniteGameVersionID = 6; // GameVersionConfiguration seeds this through HasData
    private const int WestNorthAmericaRegionID = 1; // RegionConfiguration seeds this through HasData
    private static readonly string[] PastGamertagsLatestFirst = ["OldTag", "Older", "Unknown When"];
    private static readonly int[] HomeUserIDs = [1];
    private static readonly int[] AwayUserIDs = [2];

    private ProfileStatsService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
        _service = new ProfileStatsService(Context, new SeasonStatsService(Context, time), new MatchmakingStatsService(Context), time);
    }

    [Test]
    public async Task GetAsync_NoSuchUser_ReturnsNull()
    {
        var profile = await _service.GetAsync(999);

        Assert.That(profile, Is.Null);
    }

    /// <summary>User 1: Xbox account "CurrentTag" with past gamertags, a region, a title, a stored season line and a stored
    /// Late League line.</summary>
    private async Task<(Season Season, Team Team, MatchmakingSeason Late)> SeedPlayer()
    {
        var xbox = XboxUser.FromXbox(10, "CurrentTag");
        _ = xbox.RecordPastGamertag("Older", null, new DateTime(2019, 6, 1, 0, 0, 0, DateTimeKind.Utc), GamertagHistorySource.Manual);
        _ = xbox.RecordPastGamertag("OldTag", null, new DateTime(2021, 6, 1, 0, 0, 0, DateTimeKind.Utc), GamertagHistorySource.Manual);
        _ = xbox.RecordPastGamertag("Unknown When", null, null, GamertagHistorySource.Manual);
        // The gamertag it has now, spelled another way: its history has it, the profile doesn't list it as a past one
        _ = xbox.RecordPastGamertag("currenttag", null, new DateTime(2020, 6, 1, 0, 0, 0, DateTimeKind.Utc), GamertagHistorySource.Manual);
        Context.XboxUsers.Add(xbox);
        Context.Users.Add(User.Seed(userName: "alice", displayName: "Alice", xboxUser: xbox, regionID: WestNorthAmericaRegionID, regionSource: RegionSource.User, id: 1));
        var season = Season.Seed(seasonName: "S1");
        var late = MatchmakingSeason.Seed("Late League 8", new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2020, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            gameVersionID: HaloInfiniteGameVersionID);
        Context.Seasons.Add(season);
        Context.MatchmakingSeasons.Add(late);
        await Context.SaveChangesAsync();
        var team = Team.Seed(teamName: "Hammers", seasonID: season.SeasonID);
        Context.Teams.Add(team);
        await Context.SaveChangesAsync();
        Context.Set<PlayerChampionship>().Add(PlayerChampionship.Seed(seasonID: season.SeasonID, teamID: team.TeamID, userID: 1, tier: "Pro", gamertagUsed: "OldTag",
            isCaptain: true, gameVersionID: HaloInfiniteGameVersionID));
        Context.PlayerSeasonStats.Add(PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = "OldTag", GamesPlayed = 10, Wins = 7, Losses = 3, Kills = 50, Deaths = 25, Goals = 12 },
            seasonID: season.SeasonID, userID: 1, gameVersionID: HaloInfiniteGameVersionID, teamID: team.TeamID));
        Context.MatchmakingPlayerStats.Add(MatchmakingPlayerStat.Seed(new StatSheetTotals { Gamertag = "CurrentTag", GamesPlayed = 4, Wins = 1, Losses = 3, Kills = 20, Deaths = 20, Goals = 2 },
            matchmakingSeasonID: late.MatchmakingSeasonID, userID: 1, gameVersionID: HaloInfiniteGameVersionID));
        await Context.SaveChangesAsync();
        return (season, team, late);
    }

    [Test]
    public async Task GetAsync_APlayer_ReturnsWhoTheyAreAndTheirTitles()
    {
        var (season, team, _) = await SeedPlayer();

        var profile = await _service.GetAsync(1);

        Assert.That(profile, Is.Not.Null);
        var title = profile!.Championships.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((profile.UserID, profile.Gamertag, profile.DisplayName, profile.IsDummyUser), Is.EqualTo((1, "CurrentTag", "Alice", false)));
            Assert.That((profile.RegionID, profile.RegionName), Is.EqualTo(((int?)WestNorthAmericaRegionID, "West North America")));
            Assert.That(profile.PastGamertags, Is.EqualTo(PastGamertagsLatestFirst), "latest first, unknown dates last, the current one left out");
            Assert.That((title.Season.SeasonID, title.Season.SeasonName), Is.EqualTo((season.SeasonID, "S1")));
            Assert.That((title.Tier, title.TeamID, title.TeamName, title.IsCaptain, title.GamertagUsed), Is.EqualTo(("Pro", team.TeamID, "Hammers", true, "OldTag")));
            Assert.That((title.GameVersionID, title.GameVersionName, title.GameVersionSlug), Is.EqualTo(((int?)HaloInfiniteGameVersionID, "Halo Infinite", "infinite")));
        }
    }

    [Test]
    public async Task GetAsync_APlayer_ReturnsTheirLinesCareerAndServiceRecord()
    {
        var (season, team, late) = await SeedPlayer();

        var profile = await _service.GetAsync(1);

        Assert.That(profile, Is.Not.Null);
        var line = profile!.Seasons.Single();
        var lateLine = profile.LateLeague.Single();
        var record = profile.ServiceRecord!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That((line.Season.SeasonID, line.TeamName, line.GamesPlayed, line.Goals), Is.EqualTo((season.SeasonID, "Hammers", 10, 12L)));
            Assert.That((line.GameVersionID, line.GameVersionName), Is.EqualTo(((int?)HaloInfiniteGameVersionID, "Halo Infinite")));
            Assert.That(line.TeamID, Is.EqualTo(team.TeamID));
            Assert.That((lateLine.MatchmakingSeasonID, lateLine.MatchmakingSeasonName, lateLine.Source), Is.EqualTo(((int?)late.MatchmakingSeasonID, "Late League 8", "Recorded")));
            Assert.That((lateLine.GamesPlayed, lateLine.Wins, lateLine.Kills), Is.EqualTo((4, 1, 20)));
            Assert.That((profile.OverallCareer!.GamesPlayed, profile.OverallCareer.Wins, profile.OverallCareer.Kills, profile.OverallCareer.Deaths), Is.EqualTo((14, 8, 70, 45)));
            Assert.That(profile.CareerByGame.Select(c => (c.GameVersionID, c.GamesPlayed)), Is.EqualTo(new[] { ((int?)HaloInfiniteGameVersionID, 14) }));
            Assert.That(record.Mmr, Is.Null, "never played the queue");
            Assert.That((record.League.GamesPlayed, record.League.Wins, record.League.BombScores), Is.EqualTo((10, 7, 12L)));
            Assert.That((record.Matchmaking.GamesPlayed, record.Matchmaking.Wins, record.Matchmaking.BombScores), Is.EqualTo((4, 1, 2L)));
            Assert.That((record.All.GamesPlayed, record.All.Wins, record.All.Kills, record.All.Deaths, record.All.BombScores), Is.EqualTo((14, 8, 70, 45, 14L)));
            Assert.That(record.All.UncountedBombScoreGames, Is.Zero);
        }
    }

    [Test]
    public async Task GetAsync_APlayerWithNothing_HasAnEmptyProfile()
    {
        Context.Users.Add(User.Seed(userName: "newbie", displayName: "Newbie", id: 1));
        await Context.SaveChangesAsync();

        var profile = await _service.GetAsync(1);

        Assert.That(profile, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((profile!.Gamertag, profile.RegionID, profile.RegionName), Is.EqualTo(((string?)null, (int?)null, (string?)null)));
            Assert.That(profile.PastGamertags, Is.Empty);
            Assert.That(profile.Championships, Is.Empty);
            Assert.That(profile.Seasons, Is.Empty);
            Assert.That(profile.LateLeague, Is.Empty);
            Assert.That(profile.CareerByGame, Is.Empty);
            Assert.That(profile.OverallCareer, Is.Null);
            Assert.That(profile.ServiceRecord!.All.GamesPlayed, Is.Zero);
            Assert.That(profile.ServiceRecord.Mmr, Is.Null);
        }
    }

    // ---------- ServiceRecord.LoadAsync ----------

    [Test]
    public async Task LoadAsync_AUserWithALateLeagueRecord_ShowsTheirMmr()
    {
        var user = User.Seed(userName: "player", mmr: 1100, wins: 3, losses: 1, id: 1);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var record = await ServiceRecord.LoadAsync(Context, user, Array.Empty<StatLine>(), Array.Empty<StatLine>());

        Assert.That(record.Mmr, Is.EqualTo(1100));
    }

    /// <summary>An Infinite match of one participant, Xbox account 10, with this Score (above Match.PlainMaxScore, and no
    /// layout to decode it with, its goals can't be read).</summary>
    private Guid AddMatch(int number, int score)
    {
        var id = new Guid($"00000000-0000-0000-0000-00000000000{number}");
        var match = Match.Seed(id, startTime: new DateTime(2026, 10, number, 20, 0, 0, DateTimeKind.Utc), duration: TimeSpan.FromMinutes(10));
        match.AddTeam(0, 0, Outcomes.Won);
        match.AddParticipant(0, 10, new MatchParticipantStats { Score = score, Kills = 5 });
        Context.Matches.Add(match);
        return id;
    }

    [Test]
    public async Task LoadAsync_InfiniteGamesWhoseGoalsCantBeRead_AreCountedPerKind_AndAReportedQueueMatchShowsTheMmr()
    {
        var xbox = XboxUser.FromXbox(10, "Player");
        Context.XboxUsers.Add(xbox);
        var user = User.Seed(userName: "player", xboxUser: xbox, mmr: 1234, id: 1);
        Context.Users.AddRange(user, User.Seed(userName: "opponent", id: 2));
        var season = Season.Seed(seasonName: "S1");
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        var seasonMatch = SeasonMatch.Seed(season.SeasonID, null, null, bestOf: 3);
        Context.SeasonMatches.Add(seasonMatch);
        await Context.SaveChangesAsync();

        var unreadLeague = AddMatch(1, score: 25);
        var readLeague = AddMatch(2, score: 5);
        var unreadQueue = AddMatch(3, score: 25);
        _ = AddMatch(4, score: 25); // in neither a series nor the queue: not counted
        Context.Set<SeriesGame>().AddRange(
            new SeriesGame { MatchID = unreadLeague, SeasonMatchID = seasonMatch.SeasonMatchID, GameNumber = 1 },
            new SeriesGame { MatchID = readLeague, SeasonMatchID = seasonMatch.SeasonMatchID, GameNumber = 2 });
        var queueMatch = MatchedMatch.Start(HomeUserIDs, AwayUserIDs, new DateTime(2026, 10, 3, 20, 0, 0, DateTimeKind.Utc));
        queueMatch.Report(unreadQueue);
        Context.MatchedMatches.Add(queueMatch);
        await Context.SaveChangesAsync();

        var record = await ServiceRecord.LoadAsync(Context, user, Array.Empty<StatLine>(), Array.Empty<StatLine>());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.Mmr, Is.EqualTo(1234), "a reported queue match of theirs, though their record is empty");
            Assert.That(record.League.UncountedBombScoreGames, Is.EqualTo(1));
            Assert.That(record.Matchmaking.UncountedBombScoreGames, Is.EqualTo(1));
            Assert.That(record.All.UncountedBombScoreGames, Is.EqualTo(2));
        }
    }
}
