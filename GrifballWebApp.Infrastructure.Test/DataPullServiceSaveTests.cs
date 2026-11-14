using Microsoft.Extensions.Logging.Testing;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure.HaloInfinite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using Surprenant.Grunt.Models.HaloInfinite;
using Surprenant.Grunt.Models.HaloInfinite.Medals;
using HaloMedal = Surprenant.Grunt.Models.HaloInfinite.Medals.Medal;
using Match = GrifballWebApp.Domain.Halo.Match;
using Medal = GrifballWebApp.Domain.Halo.Medal;
using Team = Surprenant.Grunt.Models.HaloInfinite.Team;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// DataPullService saving one game (SaveMatchStats, GetAndSaveMatch) and the medals' catalogue (DownloadMedals), on a
/// real (temp-file SQLite) GrifballContext with Halo substituted: a player who switched teams, bots, medals; the games
/// Halo didn't give or the app has already; what's refused (an XUID that isn't one, a team the game hasn't); and a save
/// that fails letting go of only the game.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DataPullServiceSaveTests : SqliteContextFixture
{
    private static readonly long[] OnlyThePending = [99];
    private static readonly Outcomes[] WonThenLost = [Outcomes.Won, Outcomes.Lost];
    private IHaloInfiniteClient _halo = null!;
    private IGetsertXboxUserService _getsert = null!;
    private DataPullService _service = null!;
    private readonly FakeLogger<DataPullService> _log = new();

    [SetUp]
    public void SetUp()
    {
        _halo = Substitute.For<IHaloInfiniteClient>();
        _getsert = Substitute.For<IGetsertXboxUserService>();
        _getsert.GetsertXboxUserByXuid(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(ci => XboxUser.FromXbox(ci.Arg<long>(), $"gt{ci.Arg<long>()}"));
        _service = new DataPullService(_log, _halo, Context, _getsert);
    }

    private static CoreStats Core(int score = 1) => new() { Score = score, AverageLifeDuration = TimeSpan.FromSeconds(10), Medals = [] };

    private static Player PlayerOf(string playerId, int lastTeam, params (int Team, int Score)[] teamStats) => new()
    {
        PlayerId = playerId,
        LastTeamId = lastTeam,
        PlayerTeamStats = teamStats.Select(t => new PlayerTeamStat { TeamId = t.Team, Stats = new Stats { CoreStats = Core(t.Score) } }).ToArray(),
        ParticipationInfo = new ParticipationInfo
        {
            FirstJoinedTime = new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.FromHours(-5)),
            LastLeaveTime = new DateTimeOffset(2024, 1, 1, 10, 30, 0, TimeSpan.FromHours(-5)),
            LeftInProgress = true,
            TimePlayed = TimeSpan.FromMinutes(30),
        },
    };

    private static MatchStats StatsOf(Guid id, IEnumerable<(int TeamId, int Outcome)> teams, params Player[] players) => new()
    {
        MatchId = id,
        MatchInfo = new MatchInfo
        {
            StartTime = new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.FromHours(-5)),
            EndTime = new DateTimeOffset(2024, 1, 1, 10, 30, 0, TimeSpan.FromHours(-5)),
            Duration = TimeSpan.FromMinutes(30),
        },
        Teams = teams.Select(t => new Team { TeamId = t.TeamId, Outcome = t.Outcome, Stats = new Stats { CoreStats = Core(t.TeamId * 10) } }).ToArray(),
        Players = players,
    };

    private async Task AddMedalAsync(long nameId)
    {
        Context.MedalTypes.Add(MedalType.Imported(1, "t"));
        Context.MedalDifficulties.Add(MedalDifficulty.Imported(1, "d"));
        Context.Medals.Add(Medal.Imported(nameId, "Grifball", "x", spriteIndex: 0, sortingWeight: 0, medalDifficultyID: 1, medalTypeID: 1, personalScore: 0));
        await Context.SaveChangesAsync();
    }

    [Test]
    public async Task Save_ATeamSwitcher_IsOnTheirLastTeam_WithItsStatsAndMedals_AndBotsAreLeftOut()
    {
        await AddMedalAsync(77);
        var id = Guid.NewGuid();
        var switcher = PlayerOf("xuid(11)", 1, (0, 3), (1, 7));
        switcher.PlayerTeamStats.Single(x => x.TeamId == 1).Stats.CoreStats.Medals = [new() { NameId = 77, Count = 2, TotalPersonalScoreAwarded = 50 }];
        var bot = PlayerOf("bid(1.0)", 0, (0, 1));
        bot.BotAttributes = new();

        await _service.SaveMatchStats(StatsOf(id, [(0, 2), (1, 3)], switcher, bot));

        Context.ChangeTracker.Clear();
        var participant = await Context.Set<MatchParticipant>().Include(x => x.MatchTeam).Include(x => x.MedalEarned).AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((participant.MatchID, participant.XboxUserID, participant.MatchTeam.TeamID, participant.Score), Is.EqualTo((id, 11L, 1, 7)));
            Assert.That(participant.MedalEarned.Select(m => (m.MedalID, m.Count, m.TotalPersonalScoreAwarded)), Is.EqualTo(new[] { (77L, 2, 50) }));
        }
        await _getsert.DidNotReceive().GetsertXboxUserByXuid(Arg.Is<long>(x => x != 11), Arg.Any<CancellationToken>());
    }

    [Test]
    public void Save_AnXuidThatIsntANumber_IsRefused()
        => Assert.That(async () => await _service.SaveMatchStats(StatsOf(Guid.NewGuid(), [(0, 2)], PlayerOf("xuid(abc)", 0, (0, 1)))),
            Throws.Exception.With.Message.EqualTo("XUID not long"));

    [Test]
    public void Save_APlayerOnATeamTheGameHasnt_IsRefused()
        => Assert.That(async () => await _service.SaveMatchStats(StatsOf(Guid.NewGuid(), [(0, 2), (1, 3)], PlayerOf("xuid(5)", 2, (2, 1)))),
            Throws.Exception.With.Message.EqualTo("Missing team!!!"));

    [Test]
    public async Task Save_ThatFails_LetsGoOfOnlyTheGame_SoTheCallersChangesStay_AndItCanBeSavedAgain()
    {
        var pending = XboxUser.FromXbox(99, "pending");
        Context.XboxUsers.Add(pending);
        var player = PlayerOf("xuid(5)", 0, (0, 1));
        player.PlayerTeamStats.Single().Stats.CoreStats.Medals = [new() { NameId = 404, Count = 1 }];
        var stats = StatsOf(Guid.NewGuid(), [(0, 2), (1, 3)], player);

        // No medal 404 yet: the save fails on its foreign key
        Assert.That(async () => await _service.SaveMatchStats(stats), Throws.InstanceOf<DbUpdateException>());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Context.Entry(pending).State, Is.EqualTo(EntityState.Added));
            Assert.That(Context.ChangeTracker.Entries<Match>(), Is.Empty);
            Assert.That(Context.ChangeTracker.Entries<MatchTeam>(), Is.Empty);
            Assert.That(Context.ChangeTracker.Entries<MatchParticipant>(), Is.Empty);
            Assert.That(Context.ChangeTracker.Entries<MedalEarned>(), Is.Empty);
            Assert.That(Context.ChangeTracker.Entries<XboxUser>().Select(e => e.Entity.XboxUserID), Is.EqualTo(OnlyThePending));
        }

        await AddMedalAsync(404);
        await _service.SaveMatchStats(stats);

        Assert.That(await Context.Set<MatchParticipant>().AsNoTracking().CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task GetAndSave_AGameNotSavedYet_IsFetchedAndSaved()
    {
        var id = Guid.NewGuid();
        var stats = StatsOf(id, [(0, 2), (1, 3)], PlayerOf("xuid(5)", 0, (0, 4)), PlayerOf("xuid(6)", 1, (1, 2)));
        _halo.StatsGetMatchStats(id).Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(stats, null!));

        await _service.GetAndSaveMatch(id);

        Context.ChangeTracker.Clear();
        var saved = await Context.Matches.Include(m => m.MatchTeams).AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved.MatchID, Is.EqualTo(id));
            Assert.That(saved.MatchTeams.OrderBy(t => t.TeamID).Select(t => t.Outcome), Is.EqualTo(WonThenLost));
            Assert.That(await Context.Set<MatchParticipant>().CountAsync(), Is.EqualTo(2));
        }
    }

    [Test]
    public async Task GetAndSave_AGameSavedAlready_AsksHaloNothing()
    {
        var id = Guid.NewGuid();
        Context.Matches.Add(Match.Seed(id));
        await Context.SaveChangesAsync();

        await _service.GetAndSaveMatch(id);

        Assert.That(_log.At(Microsoft.Extensions.Logging.LogLevel.Debug), Is.EqualTo(new[] { $"Match {id} already exists" }));
        await _halo.DidNotReceiveWithAnyArgs().StatsGetMatchStats(default(Guid));
    }

    [Test]
    public async Task GetAndSave_AGameHaloDoesntGive_SavesNothing()
    {
        var id = Guid.NewGuid();
        _halo.StatsGetMatchStats(id).Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(null!, new HaloApiErrorContainer { Code = 404, Message = "not found" }));

        await _service.GetAndSaveMatch(id);

        Assert.That(await Context.Matches.AnyAsync(), Is.False);
        await _halo.Received(1).StatsGetMatchStats(id);
    }

    [Test]
    public async Task Save_AGameWithNoTeams_IsNoTeamGame_AndIsntSaved()
    {
        var id = Guid.NewGuid();

        await _service.SaveMatchStats(StatsOf(id, [], PlayerOf("xuid(5)", 0, (0, 1))));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Context.Matches.AnyAsync(), Is.False);
            Assert.That(_log.At(Microsoft.Extensions.Logging.LogLevel.Debug), Is.EqualTo(new[] { $"Ignoring non team match {id}" }));
        }
    }

    [Test]
    public async Task Save_APlayerWhoSwitchedTeams_IsLogged()
    {
        var id = Guid.NewGuid();

        await _service.SaveMatchStats(StatsOf(id, [(0, 2), (1, 3)], PlayerOf("xuid(11)", 1, (0, 3), (1, 7))));

        Assert.That(_log.At(Microsoft.Extensions.Logging.LogLevel.Warning), Is.EqualTo(new[] { $"Player xuid(11) played on multiple teams in match {id}" }));
    }

    // ---- DownloadMedals ----

    private static MedalMetadataResponse Catalogue() => new()
    {
        Types = ["spree", "mode"],
        Difficulties = ["normal", "legendary"],
        Medals =
        [
            new HaloMedal { NameID = 77, Name = new TextWithTranslations { Value = "Grifball" }, Description = new TextWithTranslations { Value = "Score a goal" },
                SpriteIndex = 3, SortingWeight = 10, DifficultyIndex = 1, TypeIndex = 0, PersonalScore = 50 },
        ],
    };

    [Test]
    public async Task DownloadMedals_SavesTheCatalogue_TypesAndDifficultiesNumberedFromOne()
    {
        var catalogue = Catalogue();
        _halo.Medals().Returns(new HaloApiResultContainer<MedalMetadataResponse, HaloApiErrorContainer>(catalogue, null!));

        await _service.DownloadMedals();

        Context.ChangeTracker.Clear();
        var medal = await Context.Medals.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((medal.MedalID, medal.MedalName, medal.MedalDifficultyID, medal.MedalTypeID, medal.PersonalScore),
                Is.EqualTo((77L, "Grifball", 2, 1, 50)));
            Assert.That(await Context.MedalTypes.CountAsync(), Is.EqualTo(2));
            Assert.That(await Context.MedalDifficulties.CountAsync(), Is.EqualTo(2));
        }
    }

    [Test]
    public async Task DownloadMedals_WithMedalsAlready_IsRefused_AskingHaloNothing()
    {
        await AddMedalAsync(77);

        Assert.That(async () => await _service.DownloadMedals(), Throws.TypeOf<HaloPullRefusedException>().With.Message.EqualTo("The medals are downloaded already."));
        await _halo.DidNotReceive().Medals();
    }

    [Test]
    public void DownloadMedals_HaloGivingNone_IsRefused()
    {
        _halo.Medals().Returns(new HaloApiResultContainer<MedalMetadataResponse, HaloApiErrorContainer>(null!, null!));

        Assert.That(async () => await _service.DownloadMedals(), Throws.Exception.With.Message.EqualTo("Result null. Failed to get medals"));
    }
}
