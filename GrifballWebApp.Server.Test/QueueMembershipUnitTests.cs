using GrifballWebApp.Domain.LateLeague;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using GrifballWebApp.Application.Matchmaking;

namespace GrifballWebApp.Server.Test;

/// <summary>QueueMembershipTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class QueueMembershipUnitTests
{
    [Test]
    public void Messages_AreTheDiscordButtonsOnes()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(QueueMessages.For(QueueJoinResult.NoGamertag), Is.EqualTo("You must set your gamertag first"));
            Assert.That(QueueMessages.For(QueueJoinResult.AlreadyInQueue), Is.EqualTo("You are already in the Late League queue!"));
            Assert.That(QueueMessages.For(QueueJoinResult.InMatch), Is.EqualTo("You are already in a Late League match!"));
            Assert.That(QueueMessages.For(QueueJoinResult.Joined), Is.EqualTo("You have joined the Late League queue! 🎉"));
            Assert.That(QueueMessages.For(QueueLeaveResult.NotInQueue), Is.EqualTo("You are not in the Late League queue."));
            Assert.That(QueueMessages.For(QueueLeaveResult.Left), Is.EqualTo("You have left the Late League queue"));
        }
    }
}
