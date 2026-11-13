using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Infrastructure.Matchmaking;
using NetCord.Rest;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>MatchMessages: pure rendering of the matchmaking log channel's posts, no database, host or network.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class MatchMessagesTests
{
    private static readonly int[] HomeUserIds = [1, 2, 3];
    private static readonly int[] AwayUserIds = [4, 5];

    private static EmbedProperties Embed(MessageProperties message) => message.Embeds!.Single();

    private static string Field(MessageProperties message, string name) => Embed(message).Fields!.Single(f => f.Name == name).Value!;

    [Test]
    public void Created_NamesTheMatchAndCountsItsPlayers()
    {
        var match = MatchedMatch.Start(HomeUserIds, AwayUserIds);

        var message = MatchMessages.Created(match);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.Content, Does.Contain($"Match #{match.Id}").And.Contain("created successfully"));
            Assert.That(Embed(message).Title, Is.EqualTo("Match Created"));
            Assert.That(Field(message, "Match ID"), Is.EqualTo(match.Id.ToString()));
            Assert.That(Field(message, "Players"), Is.EqualTo("5"));
        }
    }

    [Test]
    public void Ended_NamesTheWinnerDurationAndMmrChanges()
    {
        var payload = new MatchEndedPayload(42, 0, "5m", "a: 1000 -> 1010 (+10)", "b: 1000 -> 990 (-10)");

        var message = MatchMessages.Ended(payload);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Embed(message).Title, Is.EqualTo("Match Ended"));
            Assert.That(Embed(message).Description, Does.Contain("Match #42").And.Contain("Team 0 is the winner"));
            Assert.That(Field(message, "Match ID"), Is.EqualTo("42"));
            Assert.That(Field(message, "Winning Team"), Is.EqualTo("0"));
            Assert.That(Field(message, "Duration"), Is.EqualTo("5m"));
            Assert.That(Field(message, "Home Team MMR Changes"), Is.EqualTo("a: 1000 -> 1010 (+10)"));
            Assert.That(Field(message, "Away Team MMR Changes"), Is.EqualTo("b: 1000 -> 990 (-10)"));
        }
    }

    [Test]
    public void Canceled_Unannounced_SaysItsPlayersWentBackToTheQueue_WithNoMatchIdField()
    {
        var message = MatchMessages.Canceled(new MatchCanceledPayload(42, Unannounced: true));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Embed(message).Title, Is.EqualTo("Match Canceled"));
            Assert.That(Embed(message).Description, Does.Contain("Match #42").And.Contain("back in the queue"));
            Assert.That(Embed(message).Fields, Is.Null);
        }
    }

    [Test]
    public void Canceled_ByVoteOrStale_NamesTheMatch_WithAMatchIdField()
    {
        var message = MatchMessages.Canceled(new MatchCanceledPayload(42, Unannounced: false));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Embed(message).Description, Does.Contain("Match #42").And.Not.Contain("queue"));
            Assert.That(Field(message, "Match ID"), Is.EqualTo("42"));
        }
    }
}
