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
/// <para>
/// Where EF's conventions could produce the same value on their own (a key, an FK column's index, a required FK's
/// cascade, an FK named after its navigation, a required non-nullable reference), the helpers below read it only when
/// the configuration set it (ConfigurationSource.Explicit) and give null otherwise: deleting the configuration's line
/// fails the test, which the model snapshot (the migrations tests' layer) can't tell apart.
/// </para>
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

    private static bool IsExplicit(ConfigurationSource? source) => source == ConfigurationSource.Explicit;

    private static List<string> Names(IEnumerable<IReadOnlyProperty> properties) => [.. properties.Select(p => p.Name)];

    /// <summary>The table's schema and name.</summary>
    private static (string? Schema, string? Table) TableOf(IReadOnlyEntityType entity) => (entity.GetSchema(), entity.GetTableName());

    /// <summary>The primary key's columns, in order, as HasKey set them; null when EF's key convention found the key.</summary>
    private static List<string>? KeyOf(IReadOnlyEntityType entity) =>
        IsExplicit(((IConventionEntityType)entity).GetPrimaryKeyConfigurationSource()) ? Names(entity.FindPrimaryKey()!.Properties) : null;

    /// <summary>The foreign key behind <paramref name="navigation"/>, on whichever side it is declared.</summary>
    private static IReadOnlyForeignKey ForeignKeyOf(IReadOnlyEntityType entity, string navigation) => entity.FindNavigation(navigation)!.ForeignKey;

    /// <summary>The foreign key's columns as HasForeignKey named them; null when EF's convention found them.</summary>
    private static List<string>? ColumnsOf(IReadOnlyForeignKey foreignKey) =>
        IsExplicit(((IConventionForeignKey)foreignKey).GetPropertiesConfigurationSource()) ? Names(foreignKey.Properties) : null;

    /// <summary>The delete behavior OnDelete set; null when EF's convention chose it (Cascade when required, ClientSetNull when not).</summary>
    private static DeleteBehavior? DeleteOf(IReadOnlyForeignKey foreignKey) =>
        IsExplicit(((IConventionForeignKey)foreignKey).GetDeleteBehaviorConfigurationSource()) ? foreignKey.DeleteBehavior : null;

    /// <summary>Whether the relationship is required, as IsRequired set it; null when EF worked it out.</summary>
    private static bool? RequiredOf(IReadOnlyForeignKey foreignKey) =>
        IsExplicit(((IConventionForeignKey)foreignKey).GetIsRequiredConfigurationSource()) ? foreignKey.IsRequired : null;

    /// <summary>
    /// Whether the configuration's HasIndex on exactly these columns made it unique (IsUnique()); null when no index on them
    /// is the configuration's own (the indexes EF's FK convention adds aren't).
    /// </summary>
    private static bool? IndexIsUnique(IReadOnlyEntityType entity, IReadOnlyList<string> columns)
    {
        var index = (IConventionIndex?)entity.GetIndexes().SingleOrDefault(i => i.Properties.Select(p => p.Name).SequenceEqual(columns));
        if (index is null || !IsExplicit(index.GetConfigurationSource()))
            return null;
        return index.IsUnique && IsExplicit(index.GetIsUniqueConfigurationSource());
    }

    private static bool? IndexIsUnique(IReadOnlyEntityType entity, string first, string? second = null) =>
        IndexIsUnique(entity, second is null ? [first] : [first, second]);

    private static IConventionProperty Property(IReadOnlyEntityType entity, string name) => (IConventionProperty)entity.FindProperty(name)!;

    /// <summary>The column's max length as HasMaxLength set it; null otherwise.</summary>
    private static int? MaxLengthOf(IReadOnlyEntityType entity, string name)
    {
        var property = Property(entity, name);
        return IsExplicit(property.GetMaxLengthConfigurationSource()) ? property.GetMaxLength() : null;
    }

    /// <summary>Whether the column is nullable, as IsRequired set it; null when EF worked it out (from the CLR type's nullability).</summary>
    private static bool? NullableOf(IReadOnlyEntityType entity, string name)
    {
        var property = Property(entity, name);
        return IsExplicit(property.GetIsNullableConfigurationSource()) ? property.IsNullable : null;
    }

    /// <summary>When the column's value is generated, as ValueGeneratedNever (or the like) set it; null when by convention.</summary>
    private static ValueGenerated? ValueGeneratedOf(IReadOnlyEntityType entity, string name)
    {
        var property = Property(entity, name);
        return IsExplicit(property.GetValueGeneratedConfigurationSource()) ? property.ValueGenerated : null;
    }

    [Test]
    public void SeasonMatchConfiguration_TableKeyAndChecks()
    {
        var entity = Configured(new SeasonMatchConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Event", "SeasonMatches")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { "SeasonMatchID" }));
            Assert.That(entity.GetCheckConstraints().Select(c => c.ModelName), Is.EquivalentTo(new List<string>
            {
                "CK_Event_SeasonMatches_MustBeDifferentTeams", "CK_Event_SeasonMatches_LegalResults", "CK_Event_SeasonMatches_ByeHasNoTeam",
            }));
            Assert.That(entity.FindCheckConstraint("CK_Event_SeasonMatches_MustBeDifferentTeams")!.Sql, Is.EqualTo(SeasonMatchConfiguration.MustBeDifferentTeams));
            Assert.That(entity.FindCheckConstraint("CK_Event_SeasonMatches_LegalResults")!.Sql, Is.EqualTo(SeasonMatchConfiguration.LegalResults));
            Assert.That(entity.FindCheckConstraint("CK_Event_SeasonMatches_ByeHasNoTeam")!.Sql, Is.EqualTo(SeasonMatchConfiguration.ByeHasNoTeam));
            Assert.That(IsIgnored(entity, nameof(SeasonMatch.IsDecided)), Is.True, "Worked out from the result, not a column");
            var home = ForeignKeyOf(entity, nameof(SeasonMatch.HomeTeam));
            var away = ForeignKeyOf(entity, nameof(SeasonMatch.AwayTeam));
            Assert.That((DeleteOf(home), DeleteOf(away)), Is.EqualTo(((DeleteBehavior?)DeleteBehavior.NoAction, (DeleteBehavior?)DeleteBehavior.NoAction)),
                "NoAction, checked at the end of the statement: the database refuses to delete a team that still has matches");
            Assert.That((ColumnsOf(home), ColumnsOf(away)),
                Is.EqualTo((new List<string> { nameof(SeasonMatch.HomeTeamID) }, new List<string> { nameof(SeasonMatch.AwayTeamID) })));
        }
    }

    [Test]
    public void GamertagClaimConfiguration_TableKeyChecksAndForeignKeys()
    {
        var entity = Configured(new GamertagClaimConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Event", "GamertagClaims")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { "GamertagClaimID" }));
            Assert.That(entity.FindCheckConstraint("CK_Event_GamertagClaims_DecidedOnceDecided")!.Sql,
                Is.EqualTo("(Status = 1 AND DecidedAt IS NULL AND DecidedByUserID IS NULL) OR (Status <> 1 AND DecidedAt IS NOT NULL)"));
            Assert.That(entity.FindCheckConstraint("CK_Event_GamertagClaims_RejectionReasonOnlyWhenRejected")!.Sql,
                Is.EqualTo("Status = 3 OR RejectionReason IS NULL"));
            Assert.That((MaxLengthOf(entity, nameof(GamertagClaim.Gamertag)), NullableOf(entity, nameof(GamertagClaim.Gamertag))), Is.EqualTo(((int?)50, (bool?)false)));
            Assert.That(MaxLengthOf(entity, nameof(GamertagClaim.RejectionReason)), Is.EqualTo(GamertagClaim.MaxRejectionReasonLength));
            Assert.That(IsIgnored(entity, nameof(GamertagClaim.IsOpen)), Is.True, "Worked out from the status, not a column");
            var byXboxUser = ForeignKeyOf(entity, nameof(GamertagClaim.XboxUser));
            var byClaimant = ForeignKeyOf(entity, nameof(GamertagClaim.ClaimantUser));
            var byPlaceholder = ForeignKeyOf(entity, nameof(GamertagClaim.PlaceholderUser));
            var byTeam = ForeignKeyOf(entity, nameof(GamertagClaim.Team));
            var bySeason = ForeignKeyOf(entity, nameof(GamertagClaim.Season));
            Assert.That(
                (DeleteOf(byXboxUser), DeleteOf(byClaimant), DeleteOf(byPlaceholder), DeleteOf(byTeam), DeleteOf(bySeason)),
                Is.EqualTo(((DeleteBehavior?)DeleteBehavior.Cascade, (DeleteBehavior?)DeleteBehavior.ClientSetNull, (DeleteBehavior?)DeleteBehavior.SetNull,
                    (DeleteBehavior?)DeleteBehavior.ClientSetNull, (DeleteBehavior?)DeleteBehavior.Cascade)));
            Assert.That(new[] { byXboxUser, byClaimant, byPlaceholder, byTeam, bySeason }.Select(RequiredOf), Is.All.False, "Each link optional");
            Assert.That(IndexIsUnique(entity, nameof(GamertagClaim.Status)), Is.False, "The open claims, by status");
        }
    }

    [Test]
    public void PlayerSeasonStatConfiguration_TableKeyTotalsAndIndexes()
    {
        var entity = Configured(new PlayerSeasonStatConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Event", "PlayerSeasonStats")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindCheckConstraint("CK_Event_PlayerSeasonStats_StintFromOne")!.Sql, Is.EqualTo("StintIndex >= 1"));
            // ConfigureTotals, shared with every stored-totals table
            Assert.That(MaxLengthOf(entity, nameof(PlayerSeasonStat.Gamertag)), Is.EqualTo(50));
            Assert.That(IsIgnored(entity, nameof(PlayerSeasonStat.Totals)), Is.True, "Computed from the columns");
            Assert.That(DeleteOf(ForeignKeyOf(entity, nameof(PlayerSeasonStat.User))), Is.EqualTo(DeleteBehavior.Restrict),
                "A merge moves a user's stats before it deletes the user");
            Assert.That(IndexIsUnique(entity, new List<string> { "SeasonID", "UserID", "StintIndex", "IsPlayoffs" }), Is.True);
        }
    }

    [Test]
    public void MatchRescheduleConfiguration_TableKeyPropertiesAndForeignKeys()
    {
        var entity = Configured(new MatchRescheduleConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Event", "MatchReschedules")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindCheckConstraint("CK_Event_MatchReschedules_DecidedHasDecider")!.Sql,
                Is.EqualTo("(Status = 0 AND ApprovedByUserID IS NULL AND ProcessedAt IS NULL) OR (Status <> 0 AND ApprovedByUserID IS NOT NULL AND ProcessedAt IS NOT NULL)"));
            Assert.That((NullableOf(entity, nameof(MatchReschedule.Reason)), MaxLengthOf(entity, nameof(MatchReschedule.Reason))), Is.EqualTo(((bool?)false, (int?)1000)));
            Assert.That(IsIgnored(entity, nameof(MatchReschedule.AwaitsDecision)), Is.True, "Computed from Status");
            var bySeasonMatch = ForeignKeyOf(entity, nameof(MatchReschedule.SeasonMatch));
            var byRequester = ForeignKeyOf(entity, nameof(MatchReschedule.RequestedByUser));
            var byApprover = ForeignKeyOf(entity, nameof(MatchReschedule.ApprovedByUser));
            Assert.That((DeleteOf(bySeasonMatch), DeleteOf(byRequester), DeleteOf(byApprover)),
                Is.EqualTo(((DeleteBehavior?)DeleteBehavior.Cascade, (DeleteBehavior?)DeleteBehavior.Restrict, (DeleteBehavior?)DeleteBehavior.Restrict)));
        }
    }

    [Test]
    public void SeasonConfiguration_TableKeyChecksIndexesAndForeignKeys()
    {
        var entity = Configured(new SeasonConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Event", "Seasons")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindCheckConstraint("CK_Event_Seasons_DatesInOrder")!.Sql,
                Is.EqualTo("SignupsOpen <= SignupsClose AND DraftStart <= SeasonStart AND SeasonStart <= SeasonEnd"));
            Assert.That(entity.FindCheckConstraint("CK_Event_Seasons_SourceUrlOnlyImported")!.Sql,
                Is.EqualTo("HistoricalSourceUrl IS NULL OR HistoricalSourceID IS NOT NULL"));
            Assert.That(MaxLengthOf(entity, nameof(Season.SeasonName)), Is.EqualTo(100));
            Assert.That(IsIgnored(entity, nameof(Season.IsImported)), Is.True, "Worked out from its fields");
            Assert.That(IndexIsUnique(entity, nameof(Season.LeagueID), nameof(Season.SeasonName)), Is.True, "Two leagues can each have a \"Winter League 2019\"");
            Assert.That(IndexIsUnique(entity, nameof(Season.HistoricalSourceID)), Is.True, "Unique where set, as the import upserts by it");
            var byLeague = ForeignKeyOf(entity, nameof(Season.League));
            Assert.That((RequiredOf(byLeague), DeleteOf(byLeague)), Is.EqualTo(((bool?)true, (DeleteBehavior?)DeleteBehavior.Restrict)));
            var byGameVersion = ForeignKeyOf(entity, nameof(Season.GameVersion));
            Assert.That((RequiredOf(byGameVersion), DeleteOf(byGameVersion)), Is.EqualTo(((bool?)false, (DeleteBehavior?)DeleteBehavior.Restrict)));
        }
    }

    [Test]
    public void MatchedMatchConfiguration_TableKeyChecksAndForeignKeys()
    {
        var entity = Configured(new MatchedMatchConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Matchmaking", "MatchedMatches")));
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
            var byRecordedGame = ForeignKeyOf(entity, nameof(MatchedMatch.RecordedGame));
            var bySeason = ForeignKeyOf(entity, nameof(MatchedMatch.MatchmakingSeason));
            Assert.That((DeleteOf(byRecordedGame), byRecordedGame.IsUnique, DeleteOf(bySeason)),
                Is.EqualTo(((DeleteBehavior?)DeleteBehavior.Restrict, true, (DeleteBehavior?)DeleteBehavior.Restrict)), "A recorded game is one matchmaking game at most");
        }
    }

    [Test]
    public void VodConfiguration_TableKeyChecksAndIndexes()
    {
        var entity = Configured(new VodConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Event", "Vods")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindCheckConstraint("CK_Event_Vods_KnownStatus")!.Sql, Is.EqualTo(VodConfiguration.KnownStatus));
            Assert.That(entity.FindCheckConstraint("CK_Event_Vods_LinkedHasSeason")!.Sql, Is.EqualTo(VodConfiguration.LinkedHasSeason));
            Assert.That(entity.FindCheckConstraint("CK_Event_Vods_MatchHasSeason")!.Sql, Is.EqualTo(VodConfiguration.MatchHasSeason));
            Assert.That(MaxLengthOf(entity, nameof(Vod.YouTubeVideoID)), Is.EqualTo(Vod.YouTubeVideoIDMaxLength));
            Assert.That(IndexIsUnique(entity, nameof(Vod.YouTubeVideoID)), Is.True);
            Assert.That(DeleteOf(ForeignKeyOf(entity, nameof(Vod.SeasonMatch))), Is.EqualTo(DeleteBehavior.SetNull),
                "Schedules are regenerated with ExecuteDelete: the VOD keeps its season and teams");
        }
    }

    [Test]
    public void ScheduleRecommendationConfiguration_TableKeyChecksAndRelationships()
    {
        var entity = Configured(new ScheduleRecommendationConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Event", "ScheduleRecommendations")));
            Assert.That(entity.IsTemporal(), Is.False, "A job's output, written once");
            Assert.That(entity.FindCheckConstraint("CK_Event_ScheduleRecommendations_FitnessInRange")!.Sql, Is.EqualTo("Fitness >= 0 AND Fitness <= 1"));
            Assert.That(entity.FindCheckConstraint("CK_Event_ScheduleRecommendations_GenerationsNotNegative")!.Sql, Is.EqualTo("Generations >= 0"));
            Assert.That(entity.FindCheckConstraint("CK_Event_ScheduleRecommendations_AppliedByOnlyWhenApplied")!.Sql, Is.EqualTo("AppliedAt IS NOT NULL OR AppliedBy IS NULL"));
            Assert.That(MaxLengthOf(entity, nameof(ScheduleRecommendation.RequestedBy)), Is.EqualTo(256));
            Assert.That(DeleteOf(ForeignKeyOf(entity, nameof(ScheduleRecommendation.Season))), Is.EqualTo(DeleteBehavior.Cascade));
            Assert.That(DeleteOf(ForeignKeyOf(entity, nameof(ScheduleRecommendation.Matches))), Is.EqualTo(DeleteBehavior.Cascade));
        }

        var matchEntity = Configured(new ScheduleRecommendationMatchConfiguration());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(matchEntity), Is.EqualTo(("Event", "ScheduleRecommendationMatches")));
            Assert.That(IsIgnored(matchEntity, nameof(ScheduleRecommendationMatch.HasConflict)), Is.True, "Worked out from its flags");
            Assert.That(IndexIsUnique(matchEntity, "ScheduleRecommendationID", "SeasonMatchID"), Is.True);
        }
    }

    [Test]
    public void RoleConfiguration_TableKeyAndSeedData()
    {
        var entity = Configured(new RoleConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Auth", "Roles")));
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
            Assert.That(TableOf(entity), Is.EqualTo(("Event", "PlayerChampionships")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(MaxLengthOf(entity, nameof(PlayerChampionship.Tier)), Is.EqualTo(50));
            var byUser = ForeignKeyOf(entity, nameof(PlayerChampionship.User));
            var bySeason = ForeignKeyOf(entity, nameof(PlayerChampionship.Season));
            var byTeam = ForeignKeyOf(entity, nameof(PlayerChampionship.Team));
            Assert.That((DeleteOf(byUser), DeleteOf(bySeason), DeleteOf(byTeam)),
                Is.EqualTo(((DeleteBehavior?)DeleteBehavior.Restrict, (DeleteBehavior?)DeleteBehavior.Cascade, (DeleteBehavior?)DeleteBehavior.NoAction)));
            Assert.That(IndexIsUnique(entity, new List<string> { "SeasonID", "Tier", "UserID" }), Is.True);
        }
    }

    [Test]
    public void RecordedGameConfiguration_TableKeyChecksAndForeignKeys()
    {
        var entity = Configured(new RecordedGameConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Recorded", "Games")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindCheckConstraint("CK_Recorded_Games_KnownOrigin")!.Sql, Is.EqualTo(RecordedGameConfiguration.KnownOrigin));
            Assert.That(entity.FindCheckConstraint("CK_Recorded_Games_ManualHasNoSeason")!.Sql, Is.EqualTo(RecordedGameConfiguration.ManualHasNoSeason));
            Assert.That(MaxLengthOf(entity, nameof(RecordedGame.SourceKey)), Is.EqualTo(RecordedGame.SourceKeyMaxLength));
            Assert.That(Property(entity, nameof(RecordedGame.Origin)).GetDefaultValue(), Is.EqualTo(RecordedGameOrigin.HistoricalImport));
            Assert.That(IndexIsUnique(entity, nameof(RecordedGame.SourceKey)), Is.True);
            var bySeason = ForeignKeyOf(entity, nameof(RecordedGame.Season));
            Assert.That((DeleteOf(bySeason), RequiredOf(bySeason)), Is.EqualTo(((DeleteBehavior?)DeleteBehavior.ClientSetNull, (bool?)false)));
        }
    }
}
