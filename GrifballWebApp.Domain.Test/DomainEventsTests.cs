using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// The aggregates raise their domain events (IDomainEvent) as their methods make the changes, and only then: a change
/// refused raises nothing. A sample of each context's; the events reach their handlers once saved (DomainEventsInterceptor).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class DomainEventsTests
{
    private static readonly DateTime Open = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    private static IReadOnlyList<IDomainEvent> Events(object aggregate) => ((IHasDomainEvents)aggregate).DomainEvents;

    private static void Clear(object aggregate) => ((IHasDomainEvents)aggregate).ClearDomainEvents();

    [Test]
    public void ClearDomainEvents_ForgetsWhatWasRaised()
    {
        var season = Season.Organized("Season 1", Shared.League.DefaultLeagueID, null);

        Clear(season);

        Assert.That(Events(season), Is.Empty);
    }

    [Test]
    public void Season_RaisesItsChanges_AndNothingForARefusal()
    {
        var season = Season.Organized("Season 1", Shared.League.DefaultLeagueID, null);
        var created = Events(season).ToList();
        Clear(season);

        season.LockCaptains(true);
        var refused = season.Schedule(Open, Open.AddDays(-1), Open, Open, Open);
        var scheduled = season.Schedule(Open, Open.AddDays(1), Open.AddDays(2), Open.AddDays(3), Open.AddDays(30));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created, Is.EqualTo(new IDomainEvent[] { new SeasonCreated(season) }));
            Assert.That(refused, Is.EqualTo(SeasonScheduleResult.SignupsCloseBeforeTheyOpen));
            Assert.That(scheduled, Is.EqualTo(SeasonScheduleResult.Scheduled));
            Assert.That(Events(season), Is.EqualTo(new IDomainEvent[] { new CaptainsLockChanged(season), new SeasonScheduled(season) }));
        }
    }

    [Test]
    public void AnImportedSeason_RefusesAnEdit_AndRaisesNothing()
    {
        var season = Season.Imported("wb:sheet", "Old", Shared.League.DefaultLeagueID, null, null!, null!, SeasonFormat.LeagueSeason);
        Clear(season);

        var result = season.Edit("New", Shared.League.DefaultLeagueID, null, null!, SeasonFormat.LeagueSeason);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(SeasonEditResult.Imported));
            Assert.That(Events(season), Is.Empty);
        }
    }

    [Test]
    public void Team_RaisesItsRosterChanges_WithWhoMoved_ATradeOnBothTeams()
    {
        var reds = Team.Drafted(5, "Reds", 1, 1);
        var blues = Team.Drafted(5, "Blues", 2, 2);
        reds.Draft(3);
        Clear(reds);
        Clear(blues);

        var again = reds.Draft(3);
        reds.TradePick(3, blues, 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(again, Is.EqualTo(TeamRosterResult.AlreadyOnTeam));
            Assert.That(Events(reds).OfType<TeamRosterChanged>().Select(e => e.UserIDs), Is.EqualTo(new[] { new[] { 3 } }));
            Assert.That(Events(blues).OfType<TeamRosterChanged>().Select(e => e.UserIDs), Is.EqualTo(new[] { new[] { 3 } }));
        }
    }

    [Test]
    public void Team_FormedAndDisbanded_SaySo_WithItsRoster()
    {
        var team = Team.Drafted(5, "Reds", 1, 1);
        team.Draft(4);

        team.Disband();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Events(team)[0], Is.EqualTo(new TeamFormed(team)));
            Assert.That(Events(team).OfType<TeamDisbanded>().Single().UserIDs, Is.EquivalentTo(new[] { 1, 4 }));
        }
    }

    [Test]
    public void SeasonMatch_RaisesItsSchedule_Result_AndCancellation()
    {
        var match = SeasonMatch.Between(5, 1, 2, bestOf: 3);
        var created = Events(match).ToList();
        Clear(match);

        match.ScheduleAt(Open);
        match.DecideByGames(2, 0);
        match.Cancel();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created, Is.EqualTo(new IDomainEvent[] { new SeasonMatchCreated(match) }));
            Assert.That(Events(match), Is.EqualTo(new IDomainEvent[]
            {
                new SeasonMatchScheduled(match), new SeasonMatchResultChanged(match), new SeasonMatchCanceled(match),
            }));
        }
    }

    [Test]
    public void SeasonSignup_SignedUp_AndWithdrawn_RefusedRaisesNothing()
    {
        var season = Season.Seed(seasonName: "Signups", seasonID: 5)
            .WithDates(signupsOpen: Open, signupsClose: Open.AddDays(7), draftStart: Open.AddDays(7), seasonStart: Open.AddDays(8), seasonEnd: Open.AddDays(30));
        var choices = new SignupChoices(null, false, false, []);

        var closed = SeasonSignup.SignUp(season, 7, Open.AddDays(10), choices);
        var signup = SeasonSignup.SignUp(season, 7, Open.AddDays(1), choices).Signup!;
        signup.Withdraw();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(closed.Signup, Is.Null);
            Assert.That(Events(signup), Is.EqualTo(new IDomainEvent[] { new PlayerSignedUp(signup), new SignupWithdrawn(signup) }));
        }
    }

    [Test]
    public void QueuedPlayer_JoinsAndLeaves_ARefusedJoinMakesNoSpot()
    {
        var refused = QueuedPlayer.Join(7, hasGamertag: false, inMatch: false, inQueue: false, Open);
        var spot = QueuedPlayer.Join(7, hasGamertag: true, inMatch: false, inQueue: false, Open).Spot!;

        spot.Leave();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refused.Spot, Is.Null);
            Assert.That(Events(spot), Is.EqualTo(new IDomainEvent[] { new PlayerQueued(spot), new PlayerLeftQueue(spot) }));
        }
    }

    [Test]
    public void MatchedMatch_StartedAndEnded_NameItsPlayers()
    {
        var match = MatchedMatch.Start([1, 2], [3, 4], Open);

        match.Cancel();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Events(match).OfType<LateLeagueMatchStarted>().Single().UserIDs, Is.EquivalentTo(new[] { 1, 2, 3, 4 }));
            Assert.That(Events(match).OfType<LateLeagueMatchEnded>().Single().UserIDs, Is.EquivalentTo(new[] { 1, 2, 3, 4 }));
        }
    }

    [Test]
    public void User_RaisesAChangeOfWhatTheSiteShows_ARefusedLinkNothing()
    {
        var user = User.Register("login");
        user.LinkDiscord(100);
        Clear(user);

        var refused = user.LinkDiscord(200);
        user.Rename("Shown");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refused, Is.EqualTo(DiscordLinking.LinkedToOther));
            Assert.That(Events(user), Is.EqualTo(new IDomainEvent[] { new UserChanged(user) }));
        }
    }

    [Test]
    public void HaloMatch_Pulled_SaysSo()
    {
        var match = Match.FromHalo(Guid.NewGuid(), Open, Open.AddMinutes(10), TimeSpan.FromMinutes(10), Open, null, null,
            [new MatchTeamResult(0, 5, Outcomes.Won), new MatchTeamResult(1, 3, Outcomes.Lost)]);

        Assert.That(Events(match), Is.EqualTo(new IDomainEvent[] { new HaloMatchChanged(match) }));
    }
}
