using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// Each aggregate's IEntityTypeConfiguration, on its own: a plain ModelBuilder (no provider, no database) runs
/// Configure() and the test reads the model it built (table/schema, keys, required columns, check constraints,
/// foreign keys and their delete behavior). GrifballContext (Infrastructure) is what actually applies every
/// configuration against a real database; these are the configurations' own rules, which is why they're the
/// Domain's: no database, host, file or network.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class EntityConfigurationUnitTests
{
    private static IReadOnlyEntityType Configured<TEntity>(IEntityTypeConfiguration<TEntity> configuration) where TEntity : class
    {
        var modelBuilder = new ModelBuilder();
        configuration.Configure(modelBuilder.Entity<TEntity>());
        return modelBuilder.Model.FindEntityType(typeof(TEntity))!;
    }

    private static string Check(IReadOnlyEntityType entity, string name) =>
        entity.GetCheckConstraints().Single(c => c.ModelName == name).Sql;

    [Test]
    public void SeasonMatchConfiguration_TableKeyAndChecks()
    {
        var entity = Configured(new SeasonMatchConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((entity.GetSchema(), entity.GetTableName()), Is.EqualTo(("Event", "SeasonMatches")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindPrimaryKey()!.Properties.Select(p => p.Name), Is.EqualTo(new List<string> { "SeasonMatchID" }));
            Assert.That(entity.GetCheckConstraints().Select(c => c.ModelName), Is.EquivalentTo(new List<string>
            {
                "CK_Event_SeasonMatches_MustBeDifferentTeams", "CK_Event_SeasonMatches_LegalResults", "CK_Event_SeasonMatches_ByeHasNoTeam",
            }));
            Assert.That(Check(entity, "CK_Event_SeasonMatches_LegalResults"), Is.EqualTo(SeasonMatchConfiguration.LegalResults));
            Assert.That(entity.FindProperty("IsDecided"), Is.Null, "Worked out from the result, not a column");
            var home = entity.FindNavigation(nameof(SeasonMatch.HomeTeam))!.ForeignKey;
            var away = entity.FindNavigation(nameof(SeasonMatch.AwayTeam))!.ForeignKey;
            Assert.That((home.DeleteBehavior, away.DeleteBehavior), Is.EqualTo((DeleteBehavior.NoAction, DeleteBehavior.NoAction)), "Neither side cascades: a team's own matches stay if it's removed another way");
        }
    }

    [Test]
    public void GamertagClaimConfiguration_TableKeyAndForeignKeys()
    {
        var entity = Configured(new GamertagClaimConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((entity.GetSchema(), entity.GetTableName()), Is.EqualTo(("Event", "GamertagClaims")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindPrimaryKey()!.Properties.Select(p => p.Name), Is.EqualTo(new List<string> { "GamertagClaimID" }));
            Assert.That(entity.FindProperty(nameof(GamertagClaim.Gamertag))!.GetMaxLength(), Is.EqualTo(50));
            Assert.That(entity.FindProperty(nameof(GamertagClaim.Gamertag))!.IsNullable, Is.False);
            Assert.That(entity.FindProperty(nameof(GamertagClaim.RejectionReason))!.GetMaxLength(), Is.EqualTo(GamertagClaim.MaxRejectionReasonLength));
            Assert.That(entity.FindProperty("IsOpen"), Is.Null, "Worked out from the status, not a column");
            var byXboxUser = entity.FindNavigation(nameof(GamertagClaim.XboxUser))!.ForeignKey;
            var byClaimant = entity.FindNavigation(nameof(GamertagClaim.ClaimantUser))!.ForeignKey;
            Assert.That((byXboxUser.DeleteBehavior, byClaimant.DeleteBehavior), Is.EqualTo((DeleteBehavior.Cascade, DeleteBehavior.ClientSetNull)));
            Assert.That(entity.GetIndexes().Select(i => i.Properties.Select(p => p.Name).Single()), Has.Member("Status"));
        }
    }

    [Test]
    public void PlayerSeasonStatConfiguration_TableKeyTotalsAndIndexes()
    {
        var entity = Configured(new PlayerSeasonStatConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((entity.GetSchema(), entity.GetTableName()), Is.EqualTo(("Event", "PlayerSeasonStats")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(Check(entity, "CK_Event_PlayerSeasonStats_StintFromOne"), Is.EqualTo("StintIndex >= 1"));
            // ConfigureTotals, shared with every stored-totals table
            Assert.That(entity.FindProperty(nameof(PlayerSeasonStat.Gamertag))!.GetMaxLength(), Is.EqualTo(50));
            Assert.That(entity.FindProperty("Totals"), Is.Null, "Computed from the columns");
            var byUser = entity.FindNavigation(nameof(PlayerSeasonStat.User))!.ForeignKey;
            Assert.That(byUser.DeleteBehavior, Is.EqualTo(DeleteBehavior.Restrict), "A merge moves a user's stats before it deletes the user");
            var unique = entity.GetIndexes().Single(i => i.IsUnique);
            Assert.That(unique.Properties.Select(p => p.Name), Is.EqualTo(new List<string> { "SeasonID", "UserID", "StintIndex", "IsPlayoffs" }));
        }
    }

    [Test]
    public void MatchRescheduleConfiguration_TableKeyPropertiesAndForeignKeys()
    {
        var entity = Configured(new MatchRescheduleConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((entity.GetSchema(), entity.GetTableName()), Is.EqualTo(("Event", "MatchReschedules")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindProperty(nameof(MatchReschedule.Reason))!.IsNullable, Is.False);
            Assert.That(entity.FindProperty(nameof(MatchReschedule.Reason))!.GetMaxLength(), Is.EqualTo(1000));
            Assert.That(entity.FindProperty("AwaitsDecision"), Is.Null, "Computed from Status");
            var bySeasonMatch = entity.FindNavigation(nameof(MatchReschedule.SeasonMatch))!.ForeignKey;
            var byRequester = entity.FindNavigation(nameof(MatchReschedule.RequestedByUser))!.ForeignKey;
            var byApprover = entity.FindNavigation(nameof(MatchReschedule.ApprovedByUser))!.ForeignKey;
            Assert.That((bySeasonMatch.DeleteBehavior, byRequester.DeleteBehavior, byApprover.DeleteBehavior),
                Is.EqualTo((DeleteBehavior.Cascade, DeleteBehavior.Restrict, DeleteBehavior.Restrict)));
        }
    }

    [Test]
    public void SeasonConfiguration_TableKeyIndexesAndForeignKeys()
    {
        var entity = Configured(new SeasonConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((entity.GetSchema(), entity.GetTableName()), Is.EqualTo(("Event", "Seasons")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindProperty(nameof(Season.SeasonName))!.GetMaxLength(), Is.EqualTo(100));
            Assert.That(entity.FindProperty("IsImported"), Is.Null, "Worked out from its fields");
            var uniqueSeasonName = entity.GetIndexes().Single(i => i.Properties.Count == 2);
            Assert.That(uniqueSeasonName.Properties.Select(p => p.Name), Is.EqualTo(new List<string> { "LeagueID", "SeasonName" }));
            Assert.That(uniqueSeasonName.IsUnique, Is.True, "Two leagues can each have a \"Winter League 2019\"");
            var byLeague = entity.FindNavigation(nameof(Season.League))!.ForeignKey;
            Assert.That(byLeague.IsRequired, Is.True);
            var byGameVersion = entity.FindNavigation(nameof(Season.GameVersion))!.ForeignKey;
            Assert.That(byGameVersion.IsRequired, Is.False);
        }
    }

    [Test]
    public void MatchedMatchConfiguration_TableKeyChecksAndForeignKeys()
    {
        var entity = Configured(new MatchedMatchConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((entity.GetSchema(), entity.GetTableName()), Is.EqualTo(("Matchmaking", "MatchedMatches")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.GetCheckConstraints().Select(c => c.ModelName), Is.EquivalentTo(new List<string>
            {
                "CK_Matchmaking_MatchedMatches_ActiveHasNoResult", "CK_Matchmaking_MatchedMatches_WinnerSideOnlyWhenVoteDecided",
                "CK_Matchmaking_MatchedMatches_OneKindOfResult", "CK_Matchmaking_MatchedMatches_ReportedHasResult",
                "CK_Matchmaking_MatchedMatches_VoteMessageNeedsThread",
            }));
            Assert.That(entity.FindProperty("Players"), Is.Null, "Worked out from its teams and votes");
            var byRecordedGame = entity.FindNavigation(nameof(MatchedMatch.RecordedGame))!.ForeignKey;
            var bySeason = entity.FindNavigation(nameof(MatchedMatch.MatchmakingSeason))!.ForeignKey;
            Assert.That((byRecordedGame.DeleteBehavior, byRecordedGame.IsUnique, bySeason.DeleteBehavior),
                Is.EqualTo((DeleteBehavior.Restrict, true, DeleteBehavior.Restrict)), "A recorded game is one matchmaking game at most");
        }
    }

    [Test]
    public void VodConfiguration_TableKeyChecksAndIndexes()
    {
        var entity = Configured(new VodConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((entity.GetSchema(), entity.GetTableName()), Is.EqualTo(("Event", "Vods")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(Check(entity, "CK_Event_Vods_KnownStatus"), Is.EqualTo(VodConfiguration.KnownStatus));
            Assert.That(Check(entity, "CK_Event_Vods_LinkedHasSeason"), Is.EqualTo(VodConfiguration.LinkedHasSeason));
            Assert.That(Check(entity, "CK_Event_Vods_MatchHasSeason"), Is.EqualTo(VodConfiguration.MatchHasSeason));
            Assert.That(entity.FindProperty(nameof(Vod.YouTubeVideoID))!.GetMaxLength(), Is.EqualTo(Vod.YouTubeVideoIDMaxLength));
            Assert.That(entity.GetIndexes().Single(i => i.Properties.Single().Name == nameof(Vod.YouTubeVideoID)).IsUnique, Is.True);
            var bySeasonMatch = entity.FindNavigation(nameof(Vod.SeasonMatch))!.ForeignKey;
            Assert.That(bySeasonMatch.DeleteBehavior, Is.EqualTo(DeleteBehavior.SetNull), "Schedules are regenerated with ExecuteDelete: the VOD keeps its season and teams");
        }
    }

    [Test]
    public void ScheduleRecommendationConfiguration_TableKeyChecksAndRelationships()
    {
        var entity = Configured(new ScheduleRecommendationConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entity.GetTableName(), Is.EqualTo("ScheduleRecommendations"));
            Assert.That(entity.IsTemporal(), Is.False, "A job's output, written once");
            Assert.That(Check(entity, "CK_Event_ScheduleRecommendations_FitnessInRange"), Is.EqualTo("Fitness >= 0 AND Fitness <= 1"));
            Assert.That(entity.FindProperty(nameof(ScheduleRecommendation.RequestedBy))!.GetMaxLength(), Is.EqualTo(256));
            var bySeason = entity.FindNavigation(nameof(ScheduleRecommendation.Season))!.ForeignKey;
            Assert.That(bySeason.DeleteBehavior, Is.EqualTo(DeleteBehavior.Cascade));
            var matches = entity.GetNavigations().Single(n => n.Name == nameof(ScheduleRecommendation.Matches)).ForeignKey;
            Assert.That(matches.DeleteBehavior, Is.EqualTo(DeleteBehavior.Cascade));
        }

        var matchEntity = Configured(new ScheduleRecommendationMatchConfiguration());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(matchEntity.GetTableName(), Is.EqualTo("ScheduleRecommendationMatches"));
            Assert.That(matchEntity.FindProperty("HasConflict"), Is.Null, "Worked out from its flags");
            var unique = matchEntity.GetIndexes().Single();
            Assert.That(unique.Properties.Select(p => p.Name), Is.EqualTo(new List<string> { "ScheduleRecommendationID", "SeasonMatchID" }));
            Assert.That(unique.IsUnique, Is.True);
        }
    }

    [Test]
    public void RoleConfiguration_TableKeyAndSeedData()
    {
        var entity = Configured(new RoleConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((entity.GetSchema(), entity.GetTableName()), Is.EqualTo(("Auth", "Roles")));
            Assert.That(entity.IsTemporal(), Is.True);
            // Name/NormalizedName aren't in the model here: Identity's base configuration adds them (IdentityDbContext,
            // not RoleConfiguration), so a plain ModelBuilder running only RoleConfiguration never sees them. The seeded
            // rows' Ids (Sysadmin, Commissioner, Player) are RoleConfiguration's own.
            var seeded = entity.GetSeedData().ToList();
            Assert.That(seeded.Select(r => r["Id"]), Is.EqualTo(new List<int> { 1, 2, 3 }));
        }
    }

    [Test]
    public void PlayerChampionshipConfiguration_TableKeyIndexesAndForeignKeys()
    {
        var entity = Configured(new PlayerChampionshipConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((entity.GetSchema(), entity.GetTableName()), Is.EqualTo(("Event", "PlayerChampionships")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindProperty(nameof(PlayerChampionship.Tier))!.GetMaxLength(), Is.EqualTo(50));
            var byUser = entity.FindNavigation(nameof(PlayerChampionship.User))!.ForeignKey;
            var bySeason = entity.FindNavigation(nameof(PlayerChampionship.Season))!.ForeignKey;
            var byTeam = entity.FindNavigation(nameof(PlayerChampionship.Team))!.ForeignKey;
            Assert.That((byUser.DeleteBehavior, bySeason.DeleteBehavior, byTeam.DeleteBehavior),
                Is.EqualTo((DeleteBehavior.Restrict, DeleteBehavior.Cascade, DeleteBehavior.NoAction)));
            var unique = entity.GetIndexes().Single(i => i.IsUnique);
            Assert.That(unique.Properties.Select(p => p.Name), Is.EqualTo(new List<string> { "SeasonID", "Tier", "UserID" }));
        }
    }

    [Test]
    public void RecordedGameConfiguration_TableKeyChecksAndForeignKeys()
    {
        var entity = Configured(new RecordedGameConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((entity.GetSchema(), entity.GetTableName()), Is.EqualTo(("Recorded", "Games")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(Check(entity, "CK_Recorded_Games_KnownOrigin"), Is.EqualTo(RecordedGameConfiguration.KnownOrigin));
            Assert.That(Check(entity, "CK_Recorded_Games_ManualHasNoSeason"), Is.EqualTo(RecordedGameConfiguration.ManualHasNoSeason));
            Assert.That(entity.FindProperty(nameof(RecordedGame.SourceKey))!.GetMaxLength(), Is.EqualTo(RecordedGame.SourceKeyMaxLength));
            Assert.That(entity.FindProperty(nameof(RecordedGame.Origin))!.GetDefaultValue(), Is.EqualTo(RecordedGameOrigin.HistoricalImport));
            Assert.That(entity.GetIndexes().Single(i => i.Properties.Single().Name == nameof(RecordedGame.SourceKey)).IsUnique, Is.True);
            var bySeason = entity.FindNavigation(nameof(RecordedGame.Season))!.ForeignKey;
            Assert.That((bySeason.DeleteBehavior, bySeason.IsRequired), Is.EqualTo((DeleteBehavior.ClientSetNull, false)));
        }
    }
}
