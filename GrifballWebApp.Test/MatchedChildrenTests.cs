using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using System.Reflection;

namespace GrifballWebApp.Test;

/// <summary>A Late League match's parts (its sides, players and votes) are made and changed only by the match (#348).</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class MatchedChildrenTests
{
    private static readonly Type[] Parts = [typeof(MatchedTeam), typeof(MatchedPlayer), typeof(MatchedWinnerVote), typeof(MatchedKickVote)];

    [Test]
    public void A_matchs_parts_are_its_own_and_set_their_state_only_privately_but_for_a_key()
    {
        var key = typeof(MatchedPlayer).GetProperty(nameof(MatchedPlayer.Id))!;
        Assert.Multiple(() =>
        {
            // The key is internal: the database makes it, and tests may give one
            Assert.That(key.SetMethod!.IsAssembly, Is.True, "MatchedPlayer.Id: internal");
            foreach (var part in Parts)
            {
                Assert.That(part.GetCustomAttribute<AggregatePartAttribute>()?.Root, Is.EqualTo(typeof(MatchedMatch)), part.Name);
                Assert.That(part.GetConstructors(BindingFlags.Instance | BindingFlags.Public), Is.Empty, $"{part.Name}: made by the match");
                foreach (var property in part.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(p => p.SetMethod is not null && p != key))
                    Assert.That(property.SetMethod!.IsPrivate, Is.True, $"{part.Name}.{property.Name}: changed only by its methods, which the match calls");
            }
        });
    }

    [Test]
    public void A_player_is_kicked_once()
    {
        var player = new MatchedPlayer(1);

        player.Kick();

        Assert.Multiple(() =>
        {
            Assert.That(player.Kicked, Is.True);
            Assert.That(player.Kick, Throws.InvalidOperationException, "Kicked already");
        });
    }

    [Test]
    public void A_player_made_with_its_user_has_that_users_key()
    {
        var user = User.Seed(id: 7);

        var player = new MatchedPlayer(user);

        Assert.That((player.UserID, player.User, player.Kicked), Is.EqualTo((7, user, false)));
    }

    [Test]
    public void The_match_makes_its_votes_with_their_players_and_changes_only_a_winner_vote()
    {
        var match = MatchedMatch.Start([1, 2, 3], [4, 5, 6]);

        match.VoteForWinner(1, WinnerVote.Home);
        match.VoteForWinner(1, WinnerVote.Cancel);
        match.VoteToKick(1, 4);

        var winnerVote = match.MatchedWinnerVotes.Single();
        var kickVote = match.MatchedKickVotes.Single();
        Assert.Multiple(() =>
        {
            Assert.That((winnerVote.MatchedMatch, winnerVote.MatchedPlayer.UserID, winnerVote.WinnerVote), Is.EqualTo((match, 1, WinnerVote.Cancel)));
            Assert.That((kickVote.MatchedMatch, kickVote.VoterMatchedPlayer.UserID, kickVote.KickMatchedPlayer.UserID), Is.EqualTo((match, 1, 4)));
        });
    }

    [Test]
    public void A_merge_gives_the_player_to_the_target_user()
    {
        var match = MatchedMatch.Start([1], [2]);

        Assert.Multiple(() =>
        {
            Assert.That(match.ReassignPlayer(1, 3), Is.EqualTo(PlayerReassignment.Moved));
            Assert.That(match.HomeTeam.Players.Single().UserID, Is.EqualTo(3));
        });
    }
}
