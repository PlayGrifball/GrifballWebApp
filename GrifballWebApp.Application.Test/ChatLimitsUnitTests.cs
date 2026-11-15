using System.Net;
using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Tools;

namespace GrifballWebApp.Application.Test;

/// <summary>Every host's askers' limits: whose questions count together, one reply at a time, and how many a minute.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ChatLimitsUnitTests
{
    private readonly ChatLimits _limits = new(maxReplies: 3);

    [TearDown]
    public void TearDown() => _limits.Dispose();

    /// <summary>The refusals of <paramref name="count"/> questions by <paramref name="asker"/>, each answered before the next.</summary>
    private List<string?> Ask(string asker, int count)
        => Enumerable.Range(0, count).Select(_ =>
        {
            var (slot, refusal) = _limits.TryStart(asker);
            slot?.Dispose();
            return refusal;
        }).ToList();

    [Test]
    public void Asker_ASiteUser_IsTheirAccount_WhateverHostAsks_ElseWhatTheHostSays()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatLimits.Asker(new ToolCaller(7, []), ChatLimits.Discord(55)), Is.EqualTo("user:7"));
            Assert.That(ChatLimits.Asker(new ToolCaller(7, []), ChatLimits.Address(IPAddress.Parse("203.0.113.9"))), Is.EqualTo("user:7"));
            Assert.That(ChatLimits.Asker(ToolCaller.Anonymous, ChatLimits.Discord(55)), Is.EqualTo("discord:55"));
            Assert.That(ChatLimits.Asker(ToolCaller.Anonymous, ChatLimits.Address(IPAddress.Parse("203.0.113.9"))), Is.EqualTo("address:203.0.113.9"));
        }
    }

    [Test]
    public void Address_IPv6_IsItsSlash64_SoAHostCantAskFromANewAddressEachTime()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatLimits.Address(IPAddress.Parse("2001:db8:1:2:aaaa:bbbb:cccc:dddd")), Is.EqualTo("address:2001:db8:1:2::/64"));
            Assert.That(ChatLimits.Address(IPAddress.Parse("2001:db8:1:2::1")), Is.EqualTo(ChatLimits.Address(IPAddress.Parse("2001:db8:1:2:ffff::9"))));
            Assert.That(ChatLimits.Address(IPAddress.Parse("2001:db8:1:3::1")), Is.Not.EqualTo(ChatLimits.Address(IPAddress.Parse("2001:db8:1:2::1"))));
            Assert.That(ChatLimits.Address(IPAddress.Parse("::ffff:203.0.113.9")), Is.EqualTo("address:203.0.113.9"), "IPv4, as the dual stack gives it");
            Assert.That(ChatLimits.Address(null), Is.EqualTo("address:"));
        }
    }

    [Test]
    public void TryStart_OneReplyAtATime_PerAsker()
    {
        var (first, _) = _limits.TryStart("discord:1");
        var (second, busy) = _limits.TryStart("discord:1");
        var (other, _) = _limits.TryStart("discord:2");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first, Is.Not.Null);
            Assert.That((second, busy), Is.EqualTo(((IDisposable?)null, ChatLimits.Busy)));
            Assert.That(other, Is.Not.Null);
        }
        first!.Dispose();
        first.Dispose();
        var (again, _) = _limits.TryStart("discord:1");
        Assert.That(again, Is.Not.Null);
        // An old slot disposed late frees no newer one
        first.Dispose();
        Assert.That(_limits.TryStart("discord:1").Refusal, Is.EqualTo(ChatLimits.Busy));
    }

    [Test]
    public void TryStart_ASiteUser_MayAsk10AMinute_AnyoneElse5()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Ask("user:7", ChatLimits.SignedInPerMinute + 1), Is.EqualTo(Enumerable.Repeat<string?>(null, ChatLimits.SignedInPerMinute).Append(ChatLimits.TooMany)));
            Assert.That(Ask("discord:5", ChatLimits.OthersPerMinute + 1), Is.EqualTo(Enumerable.Repeat<string?>(null, ChatLimits.OthersPerMinute).Append(ChatLimits.TooMany)));
            Assert.That(Ask("address:2001:db8::/64", ChatLimits.OthersPerMinute + 1)[^1], Is.EqualTo(ChatLimits.TooMany));
        }
    }

    [Test]
    public void TryStart_BeyondTheRepliesUnderWayAtOnce_IsRefusedAtOnce_UntilOneIsDone_UsingNoneOfTheAskersQuestions()
    {
        using var limits = new ChatLimits();
        var (first, _) = limits.TryStart("user:1");

        var (second, full) = limits.TryStart("user:2");
        var refusedAgain = Enumerable.Range(0, ChatLimits.SignedInPerMinute).Select(_ => limits.TryStart("user:2").Refusal).ToList();
        first!.Dispose();
        var (third, _) = limits.TryStart("user:2");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(limits.MaxReplies, Is.EqualTo(1), "the model answers one at a time");
            Assert.That((second, full), Is.EqualTo(((IDisposable?)null, ChatLimits.Full)));
            Assert.That(refusedAgain, Is.All.EqualTo(ChatLimits.Full));
            Assert.That(third, Is.Not.Null, "and none of their questions used by those refusals");
        }
        third!.Dispose();
    }

    [TestCase(0, 1)]
    [TestCase(2, 2)]
    public void MaxReplies_AtLeastOne(int setting, int replies)
    {
        using var limits = new ChatLimits(setting);

        Assert.That(limits.MaxReplies, Is.EqualTo(replies));
    }
}
