using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.HaloInfinite;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// CustomScoreLayouts.DecodeAsync and VariantLayoutsAsync, on a real (temp-file SQLite) GrifballContext: a Plain
/// match (every Score 20 or less, which the stats always read as goals) needs no game variant to decode, and a game
/// variant version's layout is read from its own matches. CustomScoreLayouts.Apply (the pure decoder itself) is
/// already exercised through DataPullService's saves; this is only the EF side around it.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class CustomScoreLayoutsDecodeUnitTests
{
    private string _path = null!;
    private GrifballContext _context = null!;
    private CustomScoreLayouts _layouts = null!;

    [SetUp]
    public async Task Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"grif-score-layouts-{Guid.NewGuid():N}.db");
        _context = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Sqlite, $"Data Source={_path};Pooling=False").Options);
        await _context.Database.EnsureCreatedAsync();
        _layouts = new CustomScoreLayouts(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DisposeAsync();
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
            if (File.Exists(file))
                File.Delete(file);
    }

    private long _nextXuid;

    private async Task<Match> SeedMatch(Guid matchId, int scoreA, int scoreB, Guid? gameVariantVersion = null)
    {
        var match = Match.Seed(matchId, ugcGameVariantVersionID: gameVariantVersion, ugcGameVariantAssetID: gameVariantVersion is null ? null : Guid.NewGuid());
        match.AddTeam(0, scoreA, Outcomes.Won);
        match.AddTeam(1, scoreB, Outcomes.Lost);
        var xuidA = ++_nextXuid;
        var xuidB = ++_nextXuid;
        match.AddParticipant(0, xuidA, new MatchParticipantStats { Score = scoreA }, [], XboxUser.FromXbox(xuidA, "A"));
        match.AddParticipant(1, xuidB, new MatchParticipantStats { Score = scoreB }, [], XboxUser.FromXbox(xuidB, "B"));
        _context.Matches.Add(match);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return match;
    }

    [Test]
    public async Task DecodeAsync_NoUndecodedMatches_ReturnsZero()
        => Assert.That(await _layouts.DecodeAsync([]), Is.Zero);

    [Test]
    public async Task DecodeAsync_APlainMatch_DecodesGoalsFromScores_WithNoGameVariantNeeded()
    {
        var matchId = Guid.NewGuid();
        await SeedMatch(matchId, 5, 3);

        var done = 0;
        var changed = await _layouts.DecodeAsync([], (d, _) => done = d);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(done, Is.EqualTo(1));
        }
        var saved = await _context.Matches.AsNoTracking().Include(m => m.MatchTeams).ThenInclude(t => t.MatchParticipants)
            .SingleAsync(m => m.MatchID == matchId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved.ScoreLayout, Is.EqualTo(CustomScoreLayout.Plain));
            Assert.That(saved.MatchTeams.SelectMany(t => t.MatchParticipants).Select(p => p.Goals), Is.EquivalentTo(new int?[] { 5, 3 }));
        }
    }

    [Test]
    public async Task DecodeAsync_ADecodedMatch_IsLeftAlone_UnlessItsVariantChanged()
    {
        var matchId = Guid.NewGuid();
        await SeedMatch(matchId, 5, 3);
        await _layouts.DecodeAsync([]);

        var changed = await _layouts.DecodeAsync([]);

        Assert.That(changed, Is.Zero, "Already decoded, and its variant (none) didn't change");
    }

    [Test]
    public async Task VariantLayoutsAsync_TwoMatchesOfAVersion_PicksPlainWhenBothFitIt()
    {
        var version = Guid.NewGuid();
        await SeedMatch(Guid.NewGuid(), 5, 3, version);
        await SeedMatch(Guid.NewGuid(), 2, 1, version);

        var layouts = await _layouts.VariantLayoutsAsync([version]);

        Assert.That(layouts[version], Is.EqualTo(CustomScoreLayout.Plain));
    }

    [Test]
    public async Task VariantLayoutsAsync_AVersionWithNoMatches_IsNull()
    {
        var layouts = await _layouts.VariantLayoutsAsync([Guid.NewGuid()]);

        Assert.That(layouts.Values.Single(), Is.Null);
    }

    [Test]
    public async Task ApplyAsync_UsesTheVersionsLayout_WhenTheMatchHasOne()
    {
        var version = Guid.NewGuid();
        await SeedMatch(Guid.NewGuid(), 5, 3, version);
        var second = await SeedMatch(Guid.NewGuid(), 2, 1, version);

        await _layouts.ApplyAsync(second);

        Assert.That(second.ScoreLayout, Is.EqualTo(CustomScoreLayout.Plain));
    }
}
