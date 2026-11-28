using GrifballWebApp.Application.Extensions;
using GrifballWebApp.Application.Scheduling;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Server.Commands;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Integration.Test;

/// <summary>/reschedule suggest, and new_time's choices on /reschedule request (<see cref="RescheduleSuggestions"/>): a match's best times.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class RescheduleSuggestionsTests
{
    private GrifballContext _context = null!;
    private RescheduleSuggestions _suggestions = null!;
    private SchedulingTestData _s = null!;
    private readonly CommandContextRecorder _discord = new();

    // The UTC the request reads: 20:00 Eastern on 1 and 8 June (Mondays) and 9 June (Tuesday)
    private static readonly string[] BothMondaysThenTheSecondTuesday = ["2026-06-02 00:00", "2026-06-09 00:00", "2026-06-10 00:00"];
    private static readonly string[] TheSecondTuesday = ["2026-06-10 00:00"];

    [SetUp]
    public async Task SetUp()
    {
        _context = await TestDatabase.NewGrifballContext();
        _s = await SchedulingTestData.SeedAsync(_context);
        var seasons = new SeasonService(_context, new SeasonStatsService(_context), new SeasonVisibility(_context));
        var lookups = new CommandLookups(_context, seasons, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(_context), new SeasonVisibility(_context));
        _suggestions = new RescheduleSuggestions(lookups, new SchedulingService(_context, new FakeTimeProvider(SchedulingTestData.Now)));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task Suggest_ListsTheBestTimes_EachWithItsPlayers_AndItsWarnings()
    {
        await _suggestions.Suggest(_discord.Context, _s.Match.SeasonMatchID.ToString());

        var lines = _discord.Content()!.Split('\n');
        using (Assert.EnterMultipleScope())
        {
            Assert.That(lines[1], Does.StartWith("1. " + SchedulingTestData.Monday.DiscordTimeEmbed()).And.Contain("4 and 4 players"));
            Assert.That(lines[2], Does.StartWith("2. " + SchedulingTestData.Monday.AddDays(7).DiscordTimeEmbed()));
            Assert.That(_discord.Content(), Does.Contain("3. " + SchedulingTestData.Tuesday.AddDays(7).DiscordTimeEmbed()).And.Contain("⚠"));
            Assert.That(_discord.Content(), Does.Contain("/reschedule request"));
        }
    }

    [Test]
    public async Task Suggest_NoSuchMatch_SaysSo()
    {
        await _suggestions.Suggest(_discord.Context, "999999");

        Assert.That(_discord.Content(), Is.EqualTo(CommandLookups.NoSuchMatch));
    }

    [Test]
    public async Task TimeChoices_TheBestTimes_AsTheUtcTheRequestReads_NarrowedByWhatIsTyped()
    {
        var all = await _suggestions.TimeChoices(_s.Match.SeasonMatchID.ToString(), null);
        var typed = await _suggestions.TimeChoices(_s.Match.SeasonMatchID.ToString(), "Tue");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(all.Select(c => c.StringValue), Is.EqualTo(BothMondaysThenTheSecondTuesday));
            Assert.That(CommandLookups.TryParseUtc(all[0].StringValue!, out var parsed) && parsed == SchedulingTestData.Monday, Is.True);
            Assert.That(all[0].Name, Does.Contain("Mon 1 Jun").And.Contain("8:00 PM ET").And.Contain("4 and 4 players"));
            Assert.That(all[2].Name, Does.Contain("⚠"));
            Assert.That(typed.Select(c => c.StringValue), Is.EqualTo(TheSecondTuesday));
            Assert.That(await _suggestions.BestTime(_s.Match.SeasonMatchID), Is.EqualTo(all[0].StringValue), "what the request form starts with");
            Assert.That(await _suggestions.BestTime(_s.Match.SeasonMatchID, new CancellationToken(canceled: true)), Is.Null, "not found in time: the form opens without it");
        }
    }

    [Test]
    public async Task AMatchMissingATeam_HasNoTimeToSuggest()
    {
        var tbd = SeasonMatch.Seed(_s.SeasonID, _s.Reds.TeamID, null);
        _context.SeasonMatches.Add(tbd);
        await _context.SaveChangesAsync();

        await _suggestions.Suggest(_discord.Context, tbd.SeasonMatchID.ToString());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_discord.Content(), Does.StartWith("I have no time to suggest for that match"));
            Assert.That(await _suggestions.TimeChoices(tbd.SeasonMatchID.ToString(), null), Is.Empty);
            Assert.That(await _suggestions.BestTime(tbd.SeasonMatchID), Is.Null);
        }
    }

    [Test]
    public async Task TimeChoices_NoMatchPickedYet_IsNone()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _suggestions.TimeChoices(null, null), Is.Empty);
            Assert.That(await _suggestions.TimeChoices("999999", null), Is.Empty);
            Assert.That(await _suggestions.BestTime(999999), Is.Null);
        }
    }
}
