using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// A user as an aggregate (#319): made by <see cref="User.Register"/> or <see cref="User.ImportPlaceholder"/>, its own
/// fields changed only by its methods, which refuse the moves no user makes (tied to two Xbox accounts, one Xbox account
/// tied to two users, the import overwriting a chosen region).
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class UserAggregateTests
{
    private const int West = 1;
    private const int Central = 2;

    private static readonly LateLeagueRules Rules = new(KFactor: 32, WinThreshold: 3, BonusPerWin: 10, MaxBonus: 20, LossThreshold: 3, PenaltyPerLoss: 10, MaxPenalty: 20);

    [Test]
    public void Register_IsARealUser_TiedToNothing()
    {
        var user = User.Register("alice", "alice@example.com", "Alice");

        using (Assert.EnterMultipleScope())
        {
            Assert.That((user.UserName, user.Email, user.DisplayName), Is.EqualTo(("alice", "alice@example.com", "Alice")));
            Assert.That(user.IsDummyUser, Is.False);
            Assert.That(user.IsImportPlaceholder, Is.False);
            Assert.That((user.XboxUserID, user.DiscordUserID, user.RegionID, user.RegionSource), Is.EqualTo(((long?)null, (long?)null, (int?)null, (RegionSource?)null)));
            Assert.That((user.MMR, user.Wins, user.Losses, user.WinStreak, user.LossStreak), Is.EqualTo((User.StartingMMR, 0, 0, 0, 0)));
            Assert.That(user.SecurityStamp, Is.Not.Empty);
            Assert.That(User.Register("bob").DisplayName, Is.Null);
            Assert.Throws<ArgumentException>(() => User.Register(" "), "A user has a user name");
            Assert.Throws<ArgumentException>(() => User.Register("carol", displayName: new string('n', User.DisplayNameMaxLength + 1)), "A display name fits its column");
        }
    }

    [Test]
    public void ImportPlaceholder_IsADummyUser_ShownByItsGamertag()
    {
        var placeholder = User.ImportPlaceholder("historical:Old Tag", "Old Tag", null);
        var tied = User.ImportPlaceholder("historical:Tag:123", "Tag", 123);
        var longName = User.ImportPlaceholder("historical:" + new string('g', 40), new string('g', 40), null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((placeholder.UserName, placeholder.NormalizedUserName, placeholder.DisplayName), Is.EqualTo(("historical:Old Tag", "HISTORICAL:OLD TAG", "Old Tag")));
            Assert.That(placeholder.IsDummyUser, Is.True);
            Assert.That(placeholder.IsImportPlaceholder, Is.True);
            Assert.That(tied.XboxUserID, Is.EqualTo(123));
            Assert.That(tied.IsImportPlaceholder, Is.False, "Tied to its account, it is that account's user");
            Assert.That(longName.DisplayName, Has.Length.EqualTo(User.DisplayNameMaxLength));
            Assert.Throws<ArgumentException>(() => User.ImportPlaceholder("oldtag", "Old Tag", null), "A placeholder's name has the prefix");
            Assert.Throws<ArgumentException>(() => User.ImportPlaceholder("historical:Old Tag", " ", null), "And a gamertag");
        }
    }

    [Test]
    public void TieToXbox_TiesOnce_AndRefusesASecondAccountOrAHeldOne()
    {
        var user = User.Register("alice");
        var xbox = XboxUser.FromXbox(10, "Alice");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(user.TieToXbox(xbox), Is.EqualTo(XboxTie.Tied));
            Assert.That(user.TieToXbox(xbox), Is.EqualTo(XboxTie.AlreadyTied));
            Assert.That(user.TieToXbox(10), Is.EqualTo(XboxTie.AlreadyTied));
            Assert.That((user.XboxUserID, user.XboxUser), Is.EqualTo(((long?)10, xbox)), "The same account again changes nothing");
            Assert.That(user.TieToXbox(20), Is.EqualTo(XboxTie.TiedToOther), "Tied already: untie first");
            Assert.That(user.TieToXbox(XboxUser.FromXbox(20, "Other")), Is.EqualTo(XboxTie.TiedToOther));
            Assert.That((user.XboxUserID, user.XboxUser), Is.EqualTo(((long?)10, xbox)), "Refused, nothing changed");
        }

        var holder = User.Seed(id: 1, xboxUser: XboxUser.FromXbox(30, "Held"));
        var held = holder.XboxUser!;
        // As EF fixes up the inverse of a tracked pair
        typeof(XboxUser).GetProperty(nameof(XboxUser.User))!.SetValue(held, holder);
        var taker = User.Register("taker");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(taker.TieToXbox(held), Is.EqualTo(XboxTie.HeldByOther), "Another user holds it");
            Assert.That((taker.XboxUserID, taker.XboxUser), Is.EqualTo(((long?)null, (XboxUser?)null)), "Refused, nothing changed");
            Assert.That(user.TieToXbox(held), Is.EqualTo(XboxTie.TiedToOther), "Its own account first: untie that first");
            Assert.That(holder.TieToXbox(held), Is.EqualTo(XboxTie.AlreadyTied), "The holder itself");
        }

        holder.UntieXbox();
        Assert.That(taker.TieToXbox(held), Is.EqualTo(XboxTie.Tied));
        using (Assert.EnterMultipleScope())
        {
            Assert.That((holder.XboxUserID, holder.XboxUser), Is.EqualTo(((long?)null, (XboxUser?)null)));
            Assert.That(taker.XboxUserID, Is.EqualTo(30), "Untied from its holder, it may be taken");
        }

        user.UntieXbox();
        user.UntieXbox();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(user.TieToXbox(20), Is.EqualTo(XboxTie.Tied));
            Assert.That(user.XboxUserID, Is.EqualTo(20), "Untied, the user may be tied to another");
        }
    }

    [Test]
    public void ADummyUser_BecomesARealOne_AndBack()
    {
        var placeholder = User.ImportPlaceholder("historical:Old Tag", "Old Tag", null);

        placeholder.BecomeRealUser();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(placeholder.IsDummyUser, Is.False);
            Assert.That(placeholder.IsImportPlaceholder, Is.False, "A real user is no placeholder");
        }

        placeholder.BecomeDummy();
        Assert.That(placeholder.IsDummyUser, Is.True);
    }

    [Test]
    public void Rename_KeepsADisplayNameToItsColumn()
    {
        var user = User.Register("alice", displayName: "Alice");

        user.Rename("Ally");
        Assert.That(user.DisplayName, Is.EqualTo("Ally"));
        user.Rename(null);
        Assert.That(user.DisplayName, Is.Null);
        Assert.Throws<ArgumentException>(() => user.Rename(new string('n', User.DisplayNameMaxLength + 1)));
        Assert.That(user.DisplayName, Is.Null, "Refused: nothing changed");
    }

    [Test]
    public void LinkDiscord_LinksOnce_AndRefusesASecondAccount()
    {
        var user = User.Register("alice");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(user.LinkDiscord(5), Is.EqualTo(DiscordLinking.Linked));
            Assert.That(user.LinkDiscord(5), Is.EqualTo(DiscordLinking.AlreadyLinked));
            Assert.That(user.LinkDiscord(6), Is.EqualTo(DiscordLinking.LinkedToOther));
            Assert.That(user.DiscordUserID, Is.EqualTo(5), "Refused: still the first");
        }

        var account = DiscordUser.FromDiscord(6, "alice");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(user.LinkDiscord(account), Is.EqualTo(DiscordLinking.LinkedToOther));
            Assert.That(user.DiscordUser, Is.Null, "Refused: the navigation stays too");
        }

        user.UnlinkDiscord();
        Assert.That(user.DiscordUserID, Is.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(user.LinkDiscord(account), Is.EqualTo(DiscordLinking.Linked));
            Assert.That((user.DiscordUserID, user.DiscordUser), Is.EqualTo(((long?)6, account)));
            Assert.That(user.ShownName(), Is.EqualTo("alice"), "Its Discord name is the name shown");
        }
    }

    [Test]
    public void ChooseRegion_ByTheUserOrASysadmin_SetsItWithWhoChose()
    {
        var user = User.Register("alice");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(user.ChooseRegion(West, RegionSource.User), Is.EqualTo(RegionChoice.Set));
            Assert.That((user.RegionID, user.RegionSource), Is.EqualTo(((int?)West, (RegionSource?)RegionSource.User)));
            Assert.That(user.ChooseRegion(West, RegionSource.User), Is.EqualTo(RegionChoice.Unchanged));
            Assert.That(user.ChooseRegion(West, RegionSource.Sysadmin), Is.EqualTo(RegionChoice.Set), "The same region, now a sysadmin's");
            Assert.That(user.RegionSource, Is.EqualTo(RegionSource.Sysadmin));
            Assert.That(user.ChooseRegion(null, RegionSource.Sysadmin), Is.EqualTo(RegionChoice.Set));
            Assert.That((user.RegionID, user.RegionSource), Is.EqualTo(((int?)null, (RegionSource?)null)), "Cleared: nobody's choice");
            Assert.That(user.ChooseRegion(null, RegionSource.User), Is.EqualTo(RegionChoice.Unchanged));
            Assert.Throws<ArgumentOutOfRangeException>(() => user.ChooseRegion(West, (RegionSource)9));
        }
    }

    [Test]
    public void ChooseRegion_ByTheImport_FillsOnlyWhatNoOneChose()
    {
        var empty = User.Register("empty");
        var chosen = User.Register("chosen");
        chosen.ChooseRegion(West, RegionSource.User);
        var admin = User.Register("admin");
        admin.ChooseRegion(West, RegionSource.Sysadmin);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(empty.ChooseRegion(null, RegionSource.Import), Is.EqualTo(RegionChoice.Unchanged), "No region to fill in");
            Assert.That(empty.ChooseRegion(West, RegionSource.Import), Is.EqualTo(RegionChoice.Set));
            Assert.That((empty.RegionID, empty.RegionSource), Is.EqualTo(((int?)West, (RegionSource?)RegionSource.Import)));
            Assert.That(empty.ChooseRegion(West, RegionSource.Import), Is.EqualTo(RegionChoice.Unchanged));
            Assert.That(empty.ChooseRegion(Central, RegionSource.Import), Is.EqualTo(RegionChoice.Set), "The import's own region, updated");
            Assert.That(empty.ChooseRegion(null, RegionSource.Import), Is.EqualTo(RegionChoice.Unchanged), "Never cleared by the import");
            Assert.That(empty.RegionID, Is.EqualTo(Central));

            Assert.That(chosen.ChooseRegion(Central, RegionSource.Import), Is.EqualTo(RegionChoice.KeptChosen));
            Assert.That(admin.ChooseRegion(Central, RegionSource.Import), Is.EqualTo(RegionChoice.KeptChosen));
            Assert.That((chosen.RegionID, admin.RegionID), Is.EqualTo(((int?)West, (int?)West)), "The user's and the sysadmin's choices stay");
            Assert.That(empty.ChooseRegion(West, RegionSource.User), Is.EqualTo(RegionChoice.Set), "A person's choice is over the import's");
        }
    }

    [Test]
    public void RecordLateLeagueWin_GrowsTheStreak_AndItsBonusUpToTheCap()
    {
        var user = User.Register("alice");
        user.SeedLateLeagueRecord(losses: 2, lossStreak: 2);

        var gains = Enumerable.Range(0, 5).Select(_ => user.RecordLateLeagueWin(Rules)).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(gains, Is.EqualTo(new[] { 24, 24, 34, 44, 44 }), "From the third win in a row +10 a win, up to +20");
            Assert.That((user.Wins, user.Losses, user.WinStreak, user.LossStreak), Is.EqualTo((5, 2, 5, 0)), "A win ends the loss streak");
            Assert.That(user.MMR, Is.EqualTo(User.StartingMMR + gains.Sum()));
        }
    }

    [Test]
    public void RecordLateLeagueLoss_GrowsTheStreak_AndNeverTakesTheMmrBelowZero()
    {
        var user = User.Register("alice");
        user.SeedLateLeagueRecord(wins: 1, winStreak: 1);

        var losses = Enumerable.Range(0, 4).Select(_ => user.RecordLateLeagueLoss(Rules)).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(losses, Is.EqualTo(new[] { 20, 20, 30, 40 }), "From the third loss in a row -10 a loss, up to -20");
            Assert.That((user.Wins, user.Losses, user.WinStreak, user.LossStreak), Is.EqualTo((1, 4, 0, 4)), "A loss ends the win streak");
            Assert.That(user.MMR, Is.EqualTo(User.StartingMMR - 110));
        }

        user.SeedLateLeagueRecord(mmr: 15);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(user.RecordLateLeagueLoss(Rules), Is.EqualTo(15), "Only what it had");
            Assert.That(user.MMR, Is.Zero);
        }
        Assert.Throws<ArgumentNullException>(() => user.RecordLateLeagueLoss(null!));
        Assert.Throws<ArgumentNullException>(() => user.RecordLateLeagueWin(null!));
    }

    [Test]
    public void TakeLateLeagueRecordFrom_WeighsTheMmrByGames_AndLeavesTheSourceWithNone()
    {
        var to = User.Register("to");
        to.SeedLateLeagueRecord(wins: 3, losses: 1, mmr: 1100, winStreak: 2);
        var from = User.Register("from");
        from.SeedLateLeagueRecord(wins: 1, losses: 3, mmr: 900, lossStreak: 3);

        to.TakeLateLeagueRecordFrom(from);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((to.MMR, to.Wins, to.Losses, to.WinStreak, to.LossStreak), Is.EqualTo((1000, 4, 4, 2, 0)), "Its own streak, having played");
            Assert.That((from.Wins, from.Losses, from.WinStreak, from.LossStreak), Is.EqualTo((0, 0, 0, 0)));
        }

        var fresh = User.Register("fresh");
        var veteran = User.Register("veteran");
        veteran.SeedLateLeagueRecord(wins: 2, mmr: 1200, winStreak: 2);
        fresh.TakeLateLeagueRecordFrom(veteran);
        var none = User.Register("none");
        fresh.TakeLateLeagueRecordFrom(none);
        fresh.TakeLateLeagueRecordFrom(fresh);
        Assert.That((fresh.MMR, fresh.Wins, fresh.WinStreak), Is.EqualTo((1200, 2, 2)), "Having played none, the source's streak; nothing from no games or itself");
    }

    [Test]
    public void TakeXboxAndDiscordFrom_MoveTheSourcesAccounts()
    {
        var to = User.Seed(id: 1, xboxUserID: 10, discordUserID: 5);
        var from = User.Seed(id: 2, xboxUserID: 20, discordUserID: 6);

        to.TakeXboxFrom(from);
        to.TakeDiscordFrom(from);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((to.XboxUserID, to.DiscordUserID), Is.EqualTo(((long?)20, (long?)6)), "The source's accounts, in place of its own");
            Assert.That((from.XboxUserID, from.DiscordUserID), Is.EqualTo(((long?)null, (long?)null)));
        }

        to.TakeXboxFrom(from);
        to.TakeDiscordFrom(from);
        to.TakeXboxFrom(to);
        to.TakeDiscordFrom(to);
        Assert.That((to.XboxUserID, to.DiscordUserID), Is.EqualTo(((long?)20, (long?)6)), "A source with none, or itself, changes nothing");
    }

    [Test]
    public void TakeExperiencesFrom_GivesTheVersionsItLacked_AndEmptiesTheSource()
    {
        var to = User.Register("to");
        var from = User.Register("from");
        to.TakeExperiencesFrom(to);
        to.SeedExperience(1);
        from.SeedExperience(1);
        from.SeedExperience(2);

        to.TakeExperiencesFrom(from);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(to.PersonExperiences.Select(e => e.GameVersionID), Is.EquivalentTo(new[] { 1, 2 }), "Each version once");
            Assert.That(from.PersonExperiences, Is.Empty);
        }
    }
}
