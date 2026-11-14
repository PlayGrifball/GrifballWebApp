using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GrifballWebApp.Domain.Test;

/// <summary>The Players configurations (users, their accounts, logins, merges and reset links): see the class's main file.</summary>
public partial class EntityConfigurationUnitTests
{
    [Test]
    public void UserConfiguration_TableChecksAndRelationships()
    {
        var entity = Configured(new UserConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Auth", "Users")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindCheckConstraint("CK_Auth_Users_KnownRegionSource")!.Sql,
                Is.EqualTo("RegionSource IS NULL OR RegionSource IN (1, 2, 3)"), "User, Sysadmin and Import: the region sources there are");
            Assert.That(entity.FindCheckConstraint("CK_Auth_Users_RegionHasSource")!.Sql, Is.EqualTo(UserConfiguration.RegionHasSource));
            Assert.That(entity.FindCheckConstraint("CK_Auth_Users_LateLeagueRecord")!.Sql, Is.EqualTo(
                "MMR >= 0 AND Wins >= 0 AND Losses >= 0 AND WinStreak >= 0 AND LossStreak >= 0 AND (WinStreak = 0 OR LossStreak = 0) AND WinStreak <= Wins AND LossStreak <= Losses"));
            Assert.That(MaxLengthOf(entity, nameof(User.DisplayName)), Is.EqualTo(User.DisplayNameMaxLength));
            Assert.That(IsIgnored(entity, nameof(User.IsImportPlaceholder)), Is.True, "Worked out from the user name");
            var queued = ForeignKeyOf(entity, nameof(User.QueuedPlayer));
            Assert.That((queued.DeclaringEntityType.ClrType, ColumnsOf(queued), queued.IsUnique),
                Is.EqualTo((typeof(QueuedPlayer), new List<string> { nameof(QueuedPlayer.UserID) }, true)), "A user is queued once at most");
            Assert.That(DeleteOf(ForeignKeyOf(entity, nameof(User.MatchedPlayers))), Is.EqualTo(DeleteBehavior.Restrict),
                "A player leaves a match only through it, never with its user");
        }
    }

    [Test]
    public void PasswordResetLinkConfiguration_TokenConcurrencyAndIndexes()
    {
        var entity = Configured(new PasswordResetLinkConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Auth", "PasswordResetLinks")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindCheckConstraint("CK_Auth_PasswordResetLinks_UsedBeforeExpiry")!.Sql, Is.EqualTo("UsedAt IS NULL OR UsedAt < ExpiresAt"));
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(PasswordResetLink.Id) }));
            Assert.That((NullableOf(entity, nameof(PasswordResetLink.Token)), MaxLengthOf(entity, nameof(PasswordResetLink.Token))),
                Is.EqualTo(((bool?)false, (int?)PasswordResetLink.TokenMaxLength)));
            Assert.That(Property(entity, nameof(PasswordResetLink.UsedAt)).IsConcurrencyToken, Is.True, "Two requests racing on one link can't both use it");
            Assert.That(Property(entity, nameof(PasswordResetLink.Token)).IsConcurrencyToken, Is.False);
            Assert.That(IndexIsUnique(entity, nameof(PasswordResetLink.Token)), Is.True, "Looked up by the token's hash");
            Assert.That(IndexIsUnique(entity, nameof(PasswordResetLink.ExpiresAt)), Is.False, "For the cleanup");
            Assert.That(DeleteOf(ForeignKeyOf(entity, nameof(PasswordResetLink.User))), Is.EqualTo(DeleteBehavior.Cascade));
        }
    }

    [Test]
    public void XboxGamertagHistoryConfiguration_ChecksLengthsAndIndexes()
    {
        var entity = Configured(new XboxGamertagHistoryConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Xbox", "PastGamertags")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindCheckConstraint("CK_Xbox_PastGamertags_SeenInOrder")!.Sql,
                Is.EqualTo("FirstSeen IS NULL OR LastSeen IS NULL OR FirstSeen <= LastSeen"));
            Assert.That(entity.FindCheckConstraint("CK_Xbox_PastGamertags_KnownSource")!.Sql, Is.EqualTo("Source IN (1, 2, 3, 4)"),
                "Every GamertagHistorySource, and nothing else");
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(XboxGamertagHistory.XboxGamertagHistoryID) }));
            Assert.That((NullableOf(entity, nameof(XboxGamertagHistory.Gamertag)), MaxLengthOf(entity, nameof(XboxGamertagHistory.Gamertag)),
                NullableOf(entity, nameof(XboxGamertagHistory.NormalizedGamertag)), MaxLengthOf(entity, nameof(XboxGamertagHistory.NormalizedGamertag))),
                Is.EqualTo(((bool?)false, (int?)50, (bool?)false, (int?)50)));
            Assert.That(MaxLengthOf(entity, nameof(XboxGamertagHistory.Note)), Is.EqualTo(XboxGamertagHistory.MaxNoteLength));
            Assert.That(IsIgnored(entity, nameof(XboxGamertagHistory.IsRemovable)), Is.True);
            Assert.That(DeleteOf(ForeignKeyOf(entity, nameof(XboxGamertagHistory.XboxUser))), Is.EqualTo(DeleteBehavior.Cascade),
                "One taken out of its account is deleted");
            Assert.That(IndexIsUnique(entity, nameof(XboxGamertagHistory.XboxUserID), nameof(XboxGamertagHistory.NormalizedGamertag)), Is.True,
                "An account's past gamertag is listed once");
            Assert.That(IndexIsUnique(entity, nameof(XboxGamertagHistory.NormalizedGamertag)), Is.False, "Two accounts can have worn one gamertag");
        }
    }

    [Test]
    public void UserRoleLoginAndClaimConfigurations_KeysAndOwners()
    {
        var role = Configured(new UserRoleConfiguration());
        var login = Configured(new UserLoginConfiguration());
        var claim = Configured(new UserClaimConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((TableOf(role), TableOf(login), TableOf(claim)),
                Is.EqualTo((("Auth", "UserRoles"), ("Auth", "UserLogins"), ("Auth", "UserClaims"))));
            Assert.That((role.IsTemporal(), login.IsTemporal(), claim.IsTemporal()), Is.EqualTo((true, true, true)));
            Assert.That(KeyOf(role), Is.EqualTo(new List<string> { nameof(UserRole.UserId), nameof(UserRole.RoleId) }));
            Assert.That(KeyOf(login), Is.EqualTo(new List<string> { nameof(UserLogin.LoginProvider), nameof(UserLogin.ProviderKey) }));
            Assert.That(ColumnsOf(ForeignKeyOf(role, nameof(UserRole.User))), Is.EqualTo(new List<string> { nameof(UserRole.UserId) }));
            Assert.That(ColumnsOf(ForeignKeyOf(role, nameof(UserRole.Role))), Is.EqualTo(new List<string> { nameof(UserRole.RoleId) }));
            Assert.That(ColumnsOf(ForeignKeyOf(login, nameof(UserLogin.User))), Is.EqualTo(new List<string> { nameof(UserLogin.UserId) }));
            Assert.That(ColumnsOf(ForeignKeyOf(claim, nameof(UserClaim.User))), Is.EqualTo(new List<string> { nameof(UserClaim.UserId) }));
        }
    }

    [Test]
    public void UserMergeConfiguration_TemporalChecksAndIndexes()
    {
        var entity = Configured(new UserMergeConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Auth", "UserMerges")));
            Assert.That(entity.IsTemporal(), Is.True, "A merge undone keeps its record");
            Assert.That(entity.FindCheckConstraint("CK_Auth_UserMerges_DifferentUsers")!.Sql, Is.EqualTo("SourceUserID <> TargetUserID"));
            Assert.That(entity.FindCheckConstraint("CK_Auth_UserMerges_UndoOnlyWhenUndone")!.Sql,
                Is.EqualTo("UndoneAt IS NOT NULL OR (UndoneByUserID IS NULL AND UndoNote IS NULL)"));
            Assert.That(entity.FindCheckConstraint("CK_Auth_UserMerges_RowCountsNotNegative")!.Sql,
                Is.EqualTo("RowsMoved >= 0 AND RowsRemoved >= 0 AND RowsAdded >= 0"));
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(UserMerge.UserMergeID) }));
            Assert.That(MaxLengthOf(entity, nameof(UserMerge.Reason)), Is.EqualTo(UserMerge.ReasonMaxLength));
            Assert.That(MaxLengthOf(entity, nameof(UserMerge.SourceUserName)), Is.EqualTo(UserMerge.UserNameMaxLength));
            Assert.That(MaxLengthOf(entity, nameof(UserMerge.TargetUserName)), Is.EqualTo(UserMerge.UserNameMaxLength));
            Assert.That(MaxLengthOf(entity, nameof(UserMerge.UndoNote)), Is.EqualTo(UserMerge.UndoNoteMaxLength));
            Assert.That((NullableOf(entity, nameof(UserMerge.Manifest)), Property(entity, nameof(UserMerge.Manifest)).GetMaxLength()),
                Is.EqualTo(((bool?)false, (int?)null)), "Required, and as long as it needs");
            Assert.That(IsIgnored(entity, nameof(UserMerge.IsStanding)), Is.True, "Worked out from UndoneAt");
            Assert.That(IndexIsUnique(entity, nameof(UserMerge.MergedAt)), Is.False);
            Assert.That(IndexIsUnique(entity, nameof(UserMerge.SourceUserID)), Is.False);
            Assert.That(IndexIsUnique(entity, nameof(UserMerge.TargetUserID)), Is.False, "A user can take in several merges");
        }
    }

    [Test]
    public void DiscordAndXboxUserConfigurations_IdsFromOutsideAndOptionalUser()
    {
        var discord = Configured(new DiscordUserConfiguration());
        var xbox = Configured(new XboxUserConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((TableOf(discord), TableOf(xbox)), Is.EqualTo((("User", "Discord"), ("Xbox", "XboxUsers"))));
            Assert.That((discord.IsTemporal(), xbox.IsTemporal()), Is.EqualTo((true, true)));
            Assert.That(discord.FindCheckConstraint("CK_User_Discord_UsernameNotBlank")!.Sql, Is.EqualTo("TRIM(DiscordUsername) <> ''"));
            Assert.That((KeyOf(discord), KeyOf(xbox)),
                Is.EqualTo((new List<string> { nameof(DiscordUser.DiscordUserID) }, new List<string> { nameof(XboxUser.XboxUserID) })));
            Assert.That(ValueGeneratedOf(discord, nameof(DiscordUser.DiscordUserID)), Is.EqualTo(ValueGenerated.Never), "Discord's own ID");
            Assert.That(ValueGeneratedOf(xbox, nameof(XboxUser.XboxUserID)), Is.EqualTo(ValueGenerated.Never), "The XUID");
            Assert.That(IsIgnored(xbox, nameof(XboxUser.LastRenamedAt)), Is.True);
            var byDiscord = ForeignKeyOf(discord, nameof(DiscordUser.User));
            var byXbox = ForeignKeyOf(xbox, nameof(XboxUser.User));
            Assert.That((byDiscord.DeclaringEntityType.ClrType, ColumnsOf(byDiscord), byDiscord.IsUnique, RequiredOf(byDiscord)),
                Is.EqualTo((typeof(User), new List<string> { nameof(User.DiscordUserID) }, true, (bool?)false)), "A user may have one Discord account");
            Assert.That((byXbox.DeclaringEntityType.ClrType, ColumnsOf(byXbox), byXbox.IsUnique, RequiredOf(byXbox)),
                Is.EqualTo((typeof(User), new List<string> { nameof(User.XboxUserID) }, true, (bool?)false)), "A user may have one Xbox account");
        }
    }

    [Test]
    public void DuplicateDismissalAndUserExperienceConfigurations_CompositeKeys()
    {
        var dismissal = Configured(new DuplicateDismissalConfiguration());
        var experience = Configured(new UserExperienceConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((TableOf(dismissal), TableOf(experience)), Is.EqualTo((("Auth", "DuplicateDismissals"), ("Other", "UserExperiences"))));
            Assert.That((dismissal.IsTemporal(), experience.IsTemporal()), Is.EqualTo((true, true)));
            Assert.That(dismissal.FindCheckConstraint("CK_Auth_DuplicateDismissals_Order")!.Sql, Is.EqualTo("LowUserID < HighUserID"),
                "A pair is stored one way round");
            Assert.That(KeyOf(dismissal), Is.EqualTo(new List<string> { nameof(DuplicateDismissal.LowUserID), nameof(DuplicateDismissal.HighUserID) }));
            Assert.That(KeyOf(experience), Is.EqualTo(new List<string> { nameof(UserExperience.UserID), nameof(UserExperience.GameVersionID) }));
        }
    }
}
