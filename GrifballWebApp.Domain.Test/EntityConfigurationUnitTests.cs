using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// Each aggregate's IEntityTypeConfiguration, on its own: a ModelBuilder with the SQL Server provider's full convention
/// set (no connection, no database: <see cref="SqlServerConventionSetBuilder.CreateModelBuilder"/>, the technique EF
/// Core's own docs use outside OnModelCreating) runs Configure() and the test reads the model it built (table/schema,
/// keys, required columns, check constraints, foreign keys and their delete behavior). GrifballContext (Infrastructure)
/// is what actually applies every configuration against a real database, with the same conventions; these are the
/// configurations' own rules, which is why they're the Domain's: no database, host, file or network.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public partial class EntityConfigurationUnitTests
{
    private static IReadOnlyEntityType Configured<TEntity>(IEntityTypeConfiguration<TEntity> configuration) where TEntity : class
    {
        var modelBuilder = SqlServerConventionSetBuilder.CreateModelBuilder();
        configuration.Configure(modelBuilder.Entity<TEntity>());
        return modelBuilder.Model.FindEntityType(typeof(TEntity))!;
    }

    /// <summary>
    /// Proves an Ignore(...) call: with the full convention set, a member named <paramref name="name"/> would otherwise
    /// be mapped (as a property, for a computed scalar; as a navigation, for a computed collection of entities, which
    /// FindProperty would never see even unignored), so deleting the Ignore makes this false.
    /// </summary>
    private static bool IsIgnored(IReadOnlyEntityType entity, string name) =>
        ((IConventionEntityType)entity).IsIgnored(name);

    /// <summary>The table's schema and name.</summary>
    private static (string? Schema, string? Table) TableOf(IReadOnlyEntityType entity) => (entity.GetSchema(), entity.GetTableName());

    /// <summary>The primary key's columns, in order.</summary>
    private static List<string> KeyOf(IReadOnlyEntityType entity) => [.. entity.FindPrimaryKey()!.Properties.Select(p => p.Name)];

    /// <summary>The foreign key behind <paramref name="navigation"/>, on whichever side it is declared.</summary>
    private static IReadOnlyForeignKey ForeignKeyOf(IReadOnlyEntityType entity, string navigation) => entity.FindNavigation(navigation)!.ForeignKey;

    /// <summary>The one index on exactly these columns, in this order (Single: there is one, or the test fails).</summary>
    private static IReadOnlyIndex IndexOn(IReadOnlyEntityType entity, string first, string? second = null)
    {
        List<string> columns = second is null ? [first] : [first, second];
        return entity.GetIndexes().Single(i => i.Properties.Select(p => p.Name).SequenceEqual(columns));
    }

    private static IReadOnlyProperty Property(IReadOnlyEntityType entity, string name) => entity.FindProperty(name)!;

    private static List<string> Names(IEnumerable<IReadOnlyProperty> properties) => [.. properties.Select(p => p.Name)];

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
            Assert.That(entity.FindCheckConstraint("CK_Event_SeasonMatches_MustBeDifferentTeams")!.Sql, Is.EqualTo(SeasonMatchConfiguration.MustBeDifferentTeams));
            Assert.That(entity.FindCheckConstraint("CK_Event_SeasonMatches_LegalResults")!.Sql, Is.EqualTo(SeasonMatchConfiguration.LegalResults));
            Assert.That(entity.FindCheckConstraint("CK_Event_SeasonMatches_ByeHasNoTeam")!.Sql, Is.EqualTo(SeasonMatchConfiguration.ByeHasNoTeam));
            Assert.That(IsIgnored(entity, nameof(SeasonMatch.IsDecided)), Is.True, "Worked out from the result, not a column");
            var home = entity.FindNavigation(nameof(SeasonMatch.HomeTeam))!.ForeignKey;
            var away = entity.FindNavigation(nameof(SeasonMatch.AwayTeam))!.ForeignKey;
            Assert.That((home.DeleteBehavior, away.DeleteBehavior), Is.EqualTo((DeleteBehavior.NoAction, DeleteBehavior.NoAction)),
                "NoAction, checked at the end of the statement: the database refuses to delete a team that still has matches");
        }
    }

    [Test]
    public void GamertagClaimConfiguration_TableKeyChecksAndForeignKeys()
    {
        var entity = Configured(new GamertagClaimConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((entity.GetSchema(), entity.GetTableName()), Is.EqualTo(("Event", "GamertagClaims")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindPrimaryKey()!.Properties.Select(p => p.Name), Is.EqualTo(new List<string> { "GamertagClaimID" }));
            Assert.That(entity.FindCheckConstraint("CK_Event_GamertagClaims_DecidedOnceDecided")!.Sql,
                Is.EqualTo("(Status = 1 AND DecidedAt IS NULL AND DecidedByUserID IS NULL) OR (Status <> 1 AND DecidedAt IS NOT NULL)"));
            Assert.That(entity.FindCheckConstraint("CK_Event_GamertagClaims_RejectionReasonOnlyWhenRejected")!.Sql,
                Is.EqualTo("Status = 3 OR RejectionReason IS NULL"));
            Assert.That(entity.FindProperty(nameof(GamertagClaim.Gamertag))!.GetMaxLength(), Is.EqualTo(50));
            Assert.That(entity.FindProperty(nameof(GamertagClaim.Gamertag))!.IsNullable, Is.False);
            Assert.That(entity.FindProperty(nameof(GamertagClaim.RejectionReason))!.GetMaxLength(), Is.EqualTo(GamertagClaim.MaxRejectionReasonLength));
            Assert.That(IsIgnored(entity, nameof(GamertagClaim.IsOpen)), Is.True, "Worked out from the status, not a column");
            var byXboxUser = entity.FindNavigation(nameof(GamertagClaim.XboxUser))!.ForeignKey;
            var byClaimant = entity.FindNavigation(nameof(GamertagClaim.ClaimantUser))!.ForeignKey;
            var byPlaceholder = entity.FindNavigation(nameof(GamertagClaim.PlaceholderUser))!.ForeignKey;
            var byTeam = entity.FindNavigation(nameof(GamertagClaim.Team))!.ForeignKey;
            var bySeason = entity.FindNavigation(nameof(GamertagClaim.Season))!.ForeignKey;
            Assert.That(
                (byXboxUser.DeleteBehavior, byClaimant.DeleteBehavior, byPlaceholder.DeleteBehavior, byTeam.DeleteBehavior, bySeason.DeleteBehavior),
                Is.EqualTo((DeleteBehavior.Cascade, DeleteBehavior.ClientSetNull, DeleteBehavior.SetNull, DeleteBehavior.ClientSetNull, DeleteBehavior.Cascade)));
            Assert.That(entity.FindIndex(entity.FindProperty(nameof(GamertagClaim.Status))!), Is.Not.Null);
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
            Assert.That(entity.FindCheckConstraint("CK_Event_PlayerSeasonStats_StintFromOne")!.Sql, Is.EqualTo("StintIndex >= 1"));
            // ConfigureTotals, shared with every stored-totals table
            Assert.That(entity.FindProperty(nameof(PlayerSeasonStat.Gamertag))!.GetMaxLength(), Is.EqualTo(50));
            Assert.That(IsIgnored(entity, nameof(PlayerSeasonStat.Totals)), Is.True, "Computed from the columns");
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
            Assert.That(entity.FindCheckConstraint("CK_Event_MatchReschedules_DecidedHasDecider")!.Sql,
                Is.EqualTo("(Status = 0 AND ApprovedByUserID IS NULL AND ProcessedAt IS NULL) OR (Status <> 0 AND ApprovedByUserID IS NOT NULL AND ProcessedAt IS NOT NULL)"));
            Assert.That(entity.FindProperty(nameof(MatchReschedule.Reason))!.IsNullable, Is.False);
            Assert.That(entity.FindProperty(nameof(MatchReschedule.Reason))!.GetMaxLength(), Is.EqualTo(1000));
            Assert.That(IsIgnored(entity, nameof(MatchReschedule.AwaitsDecision)), Is.True, "Computed from Status");
            var bySeasonMatch = entity.FindNavigation(nameof(MatchReschedule.SeasonMatch))!.ForeignKey;
            var byRequester = entity.FindNavigation(nameof(MatchReschedule.RequestedByUser))!.ForeignKey;
            var byApprover = entity.FindNavigation(nameof(MatchReschedule.ApprovedByUser))!.ForeignKey;
            Assert.That((bySeasonMatch.DeleteBehavior, byRequester.DeleteBehavior, byApprover.DeleteBehavior),
                Is.EqualTo((DeleteBehavior.Cascade, DeleteBehavior.Restrict, DeleteBehavior.Restrict)));
        }
    }

    [Test]
    public void SeasonConfiguration_TableKeyChecksIndexesAndForeignKeys()
    {
        var entity = Configured(new SeasonConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((entity.GetSchema(), entity.GetTableName()), Is.EqualTo(("Event", "Seasons")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindCheckConstraint("CK_Event_Seasons_DatesInOrder")!.Sql,
                Is.EqualTo("SignupsOpen <= SignupsClose AND DraftStart <= SeasonStart AND SeasonStart <= SeasonEnd"));
            Assert.That(entity.FindCheckConstraint("CK_Event_Seasons_SourceUrlOnlyImported")!.Sql,
                Is.EqualTo("HistoricalSourceUrl IS NULL OR HistoricalSourceID IS NOT NULL"));
            Assert.That(entity.FindProperty(nameof(Season.SeasonName))!.GetMaxLength(), Is.EqualTo(100));
            Assert.That(IsIgnored(entity, nameof(Season.IsImported)), Is.True, "Worked out from its fields");
            var uniqueSeasonName = entity.GetIndexes().Single(i => i.Properties.Count == 2);
            Assert.That(uniqueSeasonName.Properties.Select(p => p.Name), Is.EqualTo(new List<string> { "LeagueID", "SeasonName" }));
            Assert.That(uniqueSeasonName.IsUnique, Is.True, "Two leagues can each have a \"Winter League 2019\"");
            var uniqueHistoricalSourceID = entity.FindIndex(entity.FindProperty(nameof(Season.HistoricalSourceID))!)!;
            Assert.That(uniqueHistoricalSourceID.IsUnique, Is.True, "Unique where set, as the import upserts by it");
            var byLeague = entity.FindNavigation(nameof(Season.League))!.ForeignKey;
            Assert.That((byLeague.IsRequired, byLeague.DeleteBehavior), Is.EqualTo((true, DeleteBehavior.Restrict)));
            var byGameVersion = entity.FindNavigation(nameof(Season.GameVersion))!.ForeignKey;
            Assert.That((byGameVersion.IsRequired, byGameVersion.DeleteBehavior), Is.EqualTo((false, DeleteBehavior.Restrict)));
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
            Assert.That(entity.FindCheckConstraint("CK_Matchmaking_MatchedMatches_ActiveHasNoResult")!.Sql,
                Is.EqualTo("Status <> 1 OR (MatchID IS NULL AND RecordedGameID IS NULL)"));
            Assert.That(entity.FindCheckConstraint("CK_Matchmaking_MatchedMatches_WinnerSideOnlyWhenVoteDecided")!.Sql,
                Is.EqualTo("(Status = 3 AND WinnerSide IS NOT NULL) OR (Status <> 3 AND WinnerSide IS NULL)"));
            Assert.That(entity.FindCheckConstraint("CK_Matchmaking_MatchedMatches_OneKindOfResult")!.Sql,
                Is.EqualTo("MatchID IS NULL OR RecordedGameID IS NULL"));
            Assert.That(entity.FindCheckConstraint("CK_Matchmaking_MatchedMatches_ReportedHasResult")!.Sql,
                Is.EqualTo("Status <> 2 OR MatchID IS NOT NULL OR RecordedGameID IS NOT NULL"));
            Assert.That(entity.FindCheckConstraint("CK_Matchmaking_MatchedMatches_VoteMessageNeedsThread")!.Sql,
                Is.EqualTo("VoteMessageID IS NULL OR ThreadID IS NOT NULL"));
            Assert.That(IsIgnored(entity, nameof(MatchedMatch.Players)), Is.True, "Worked out from its teams and votes");
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
            Assert.That(entity.FindCheckConstraint("CK_Event_Vods_KnownStatus")!.Sql, Is.EqualTo(VodConfiguration.KnownStatus));
            Assert.That(entity.FindCheckConstraint("CK_Event_Vods_LinkedHasSeason")!.Sql, Is.EqualTo(VodConfiguration.LinkedHasSeason));
            Assert.That(entity.FindCheckConstraint("CK_Event_Vods_MatchHasSeason")!.Sql, Is.EqualTo(VodConfiguration.MatchHasSeason));
            Assert.That(entity.FindProperty(nameof(Vod.YouTubeVideoID))!.GetMaxLength(), Is.EqualTo(Vod.YouTubeVideoIDMaxLength));
            Assert.That(entity.FindIndex(entity.FindProperty(nameof(Vod.YouTubeVideoID))!)!.IsUnique, Is.True);
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
            Assert.That((entity.GetSchema(), entity.GetTableName()), Is.EqualTo(("Event", "ScheduleRecommendations")));
            Assert.That(entity.IsTemporal(), Is.False, "A job's output, written once");
            Assert.That(entity.FindCheckConstraint("CK_Event_ScheduleRecommendations_FitnessInRange")!.Sql, Is.EqualTo("Fitness >= 0 AND Fitness <= 1"));
            Assert.That(entity.FindCheckConstraint("CK_Event_ScheduleRecommendations_GenerationsNotNegative")!.Sql, Is.EqualTo("Generations >= 0"));
            Assert.That(entity.FindCheckConstraint("CK_Event_ScheduleRecommendations_AppliedByOnlyWhenApplied")!.Sql, Is.EqualTo("AppliedAt IS NOT NULL OR AppliedBy IS NULL"));
            Assert.That(entity.FindProperty(nameof(ScheduleRecommendation.RequestedBy))!.GetMaxLength(), Is.EqualTo(256));
            var bySeason = entity.FindNavigation(nameof(ScheduleRecommendation.Season))!.ForeignKey;
            Assert.That(bySeason.DeleteBehavior, Is.EqualTo(DeleteBehavior.Cascade));
            var matches = entity.FindNavigation(nameof(ScheduleRecommendation.Matches))!.ForeignKey;
            Assert.That(matches.DeleteBehavior, Is.EqualTo(DeleteBehavior.Cascade));
        }

        var matchEntity = Configured(new ScheduleRecommendationMatchConfiguration());
        using (Assert.EnterMultipleScope())
        {
            Assert.That((matchEntity.GetSchema(), matchEntity.GetTableName()), Is.EqualTo(("Event", "ScheduleRecommendationMatches")));
            Assert.That(IsIgnored(matchEntity, nameof(ScheduleRecommendationMatch.HasConflict)), Is.True, "Worked out from its flags");
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
            // Name and NormalizedName are Role's own (inherited from IdentityRole<int>): the full convention set
            // discovers them like any other scalar property, so the seeded rows' values are read here, not just their Ids.
            var seeded = entity.GetSeedData().ToList();
            Assert.That(seeded.Select(r => r["Id"]), Is.EqualTo(new List<int> { 1, 2, 3 }));
            Assert.That(seeded.Select(r => r["Name"]), Is.EqualTo(new List<string> { "Sysadmin", "Commissioner", "Player" }));
            Assert.That(seeded.Select(r => r["NormalizedName"]), Is.EqualTo(new List<string> { "SYSADMIN", "COMMISSIONER", "PLAYER" }));
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
            Assert.That(entity.FindCheckConstraint("CK_Recorded_Games_KnownOrigin")!.Sql, Is.EqualTo(RecordedGameConfiguration.KnownOrigin));
            Assert.That(entity.FindCheckConstraint("CK_Recorded_Games_ManualHasNoSeason")!.Sql, Is.EqualTo(RecordedGameConfiguration.ManualHasNoSeason));
            Assert.That(entity.FindProperty(nameof(RecordedGame.SourceKey))!.GetMaxLength(), Is.EqualTo(RecordedGame.SourceKeyMaxLength));
            Assert.That(entity.FindProperty(nameof(RecordedGame.Origin))!.GetDefaultValue(), Is.EqualTo(RecordedGameOrigin.HistoricalImport));
            Assert.That(entity.FindIndex(entity.FindProperty(nameof(RecordedGame.SourceKey))!)!.IsUnique, Is.True);
            var bySeason = entity.FindNavigation(nameof(RecordedGame.Season))!.ForeignKey;
            Assert.That((bySeason.DeleteBehavior, bySeason.IsRequired), Is.EqualTo((DeleteBehavior.ClientSetNull, false)));
        }
    }
}
