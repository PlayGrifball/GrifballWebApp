using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GrifballWebApp.Domain.Test;

/// <summary>The Archive configurations (recorded games' teams and players, the historical import's sources, jobs and
/// reviews, XUID lookups): see the class's main file.</summary>
public partial class EntityConfigurationUnitTests
{
    /// <summary>Each named column's max length, as the model has it.</summary>
    private static Dictionary<string, int?> MaxLengths(IReadOnlyEntityType entity, IReadOnlyDictionary<string, int?> expected) =>
        expected.Keys.ToDictionary(name => name, name => Property(entity, name).GetMaxLength());

    [Test]
    public void RecordedGameTeamConfiguration_KnownOutcomeAndOneTeamASide()
    {
        var entity = Configured(new RecordedGameTeamConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Recorded", "GameTeams")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindCheckConstraint("CK_Recorded_GameTeams_KnownOutcome")!.Sql, Is.EqualTo("Outcome IN (1, 2, 3, 4)"),
                "Tie, Won, Lost and DidNotFinish");
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(RecordedGameTeam.RecordedGameTeamID) }));
            Assert.That(Property(entity, nameof(RecordedGameTeam.Name)).GetMaxLength(), Is.EqualTo(Team.TeamNameColumnLength));
            var byGame = ForeignKeyOf(entity, nameof(RecordedGameTeam.RecordedGame));
            var byTeam = ForeignKeyOf(entity, nameof(RecordedGameTeam.Team));
            Assert.That((byGame.DeleteBehavior, byTeam.DeleteBehavior, byTeam.IsRequired), Is.EqualTo((DeleteBehavior.Cascade, DeleteBehavior.ClientSetNull, false)),
                "A team's deletion clears the link, never the recorded game");
            Assert.That(byTeam.PrincipalToDependent, Is.Null, "A league team doesn't list its recorded sides");
            Assert.That(IndexOn(entity, nameof(RecordedGameTeam.RecordedGameID), nameof(RecordedGameTeam.Side)).IsUnique, Is.True, "One team a side");
            Assert.That(IndexOn(entity, nameof(RecordedGameTeam.TeamID)).IsUnique, Is.False);
        }
    }

    [Test]
    public void RecordedGameParticipantConfiguration_OneLineAPlayerAGame()
    {
        var entity = Configured(new RecordedGameParticipantConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Recorded", "GameParticipants")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(RecordedGameParticipant.RecordedGameParticipantID) }));
            var gamertag = Property(entity, nameof(RecordedGameParticipant.Gamertag));
            Assert.That((gamertag.IsNullable, gamertag.GetMaxLength()), Is.EqualTo((false, (int?)null)), "Xbox.XboxUsers.Gamertag's type, which it is UNION ALLed with");
            var byGame = ForeignKeyOf(entity, nameof(RecordedGameParticipant.RecordedGame));
            var byTeam = ForeignKeyOf(entity, nameof(RecordedGameParticipant.RecordedGameTeam));
            var byUser = ForeignKeyOf(entity, nameof(RecordedGameParticipant.User));
            Assert.That((byGame.DeleteBehavior, byTeam.DeleteBehavior, byTeam.IsRequired, byUser.DeleteBehavior),
                Is.EqualTo((DeleteBehavior.Cascade, DeleteBehavior.ClientSetNull, false, DeleteBehavior.Restrict)),
                "One cascade path from the game; a user with games isn't deleted");
            Assert.That(IndexOn(entity, nameof(RecordedGameParticipant.RecordedGameID), nameof(RecordedGameParticipant.UserID)).IsUnique, Is.True,
                "A player has one line in a game");
            Assert.That(IndexOn(entity, nameof(RecordedGameParticipant.UserID)).IsUnique, Is.False);
        }
    }

    [Test]
    public void HistoricalSourceConfiguration_NotTemporalWithItsLengths()
    {
        var entity = Configured(new HistoricalSourceConfiguration());
        var lengths = new Dictionary<string, int?>
        {
            [nameof(HistoricalSource.HistoricalSourceID)] = 100,
            [nameof(HistoricalSource.Category)] = 50,
            [nameof(HistoricalSource.Title)] = 300,
            [nameof(HistoricalSource.Url)] = 1000,
            [nameof(HistoricalSource.SheetKey)] = 100,
            [nameof(HistoricalSource.Gid)] = 30,
            [nameof(HistoricalSource.CsvSheetName)] = 100,
            [nameof(HistoricalSource.WaybackTimestamp)] = 14,
            [nameof(HistoricalSource.League)] = 50,
            [nameof(HistoricalSource.GameVersion)] = 30,
            [nameof(HistoricalSource.Format)] = 30,
            [nameof(HistoricalSource.LastFetchStatus)] = HistoricalSource.MaxFetchStatusLength,
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Event", "HistoricalSources")));
            Assert.That(entity.IsTemporal(), Is.False, "The worker writes the fetch status on every refresh");
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(HistoricalSource.HistoricalSourceID) }));
            Assert.That(MaxLengths(entity, lengths), Is.EqualTo(lengths));
            Assert.That(new List<string> { nameof(HistoricalSource.Category), nameof(HistoricalSource.Title), nameof(HistoricalSource.Url) }
                .Select(name => Property(entity, name).IsNullable), Is.All.False);
            Assert.That((IsIgnored(entity, nameof(HistoricalSource.IsDownloadable)), IsIgnored(entity, nameof(HistoricalSource.IsImported))),
                Is.EqualTo((true, true)), "The model's rules, not columns");
        }
    }

    [Test]
    public void HistoricalImportJobConfiguration_ChecksDefaultsAndStatusIndex()
    {
        var entity = Configured(new HistoricalImportJobConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Event", "HistoricalImportJobs")));
            Assert.That(entity.IsTemporal(), Is.False, "Progress is written often and the payload is megabytes");
            Assert.That(entity.FindCheckConstraint("CK_Event_HistoricalImportJobs_FinishedOnceEnded")!.Sql,
                Is.EqualTo("(Status IN (3, 4) AND FinishedAt IS NOT NULL) OR (Status IN (1, 2) AND FinishedAt IS NULL)"));
            Assert.That(entity.FindCheckConstraint("CK_Event_HistoricalImportJobs_PayloadUntilEnded")!.Sql, Is.EqualTo("Payload IS NULL OR Status IN (1, 2)"));
            Assert.That(entity.FindCheckConstraint("CK_Event_HistoricalImportJobs_PayloadOnlyForImports")!.Sql, Is.EqualTo("Payload IS NULL OR Kind = 1"));
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(HistoricalImportJob.HistoricalImportJobID) }));
            Assert.That(IsIgnored(entity, nameof(HistoricalImportJob.IsAppliablePreview)), Is.True);
            Assert.That(Property(entity, nameof(HistoricalImportJob.Stage)).GetMaxLength(), Is.EqualTo(HistoricalImportJob.MaxStageLength));
            Assert.That(IndexOn(entity, nameof(HistoricalImportJob.Status)).IsUnique, Is.False);
            Assert.That((Property(entity, nameof(HistoricalImportJob.Kind)).GetDefaultValue(), Property(entity, nameof(HistoricalImportJob.Trigger)).GetDefaultValue()),
                Is.EqualTo((HistoricalImportJobKind.Import, HistoricalImportJobTrigger.Manual)), "Jobs queued before these columns were manual imports");
        }
    }

    [Test]
    public void HistoricalSeasonReviewConfiguration_ChecksAndItsJob()
    {
        var entity = Configured(new HistoricalSeasonReviewConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Event", "HistoricalSeasonReviews")));
            Assert.That(entity.IsTemporal(), Is.False, "Bookkeeping of its job");
            Assert.That(entity.FindCheckConstraint("CK_Event_HistoricalSeasonReviews_DecidedOnceDecided")!.Sql,
                Is.EqualTo("(Status IN (3, 4) AND DecidedAt IS NOT NULL) OR (Status IN (1, 2) AND DecidedAt IS NULL)"));
            Assert.That(entity.FindCheckConstraint("CK_Event_HistoricalSeasonReviews_ApprovalJobOnlyWhenApproved")!.Sql,
                Is.EqualTo("ApprovalJobID IS NULL OR Status = 3"));
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(HistoricalSeasonReview.HistoricalSeasonReviewID) }));
            var seasonKey = Property(entity, nameof(HistoricalSeasonReview.SeasonKey));
            Assert.That((seasonKey.IsNullable, seasonKey.GetMaxLength()), Is.EqualTo((false, HistoricalSeasonReview.MaxSeasonKeyLength)));
            Assert.That(Property(entity, nameof(HistoricalSeasonReview.SeasonName)).GetMaxLength(), Is.EqualTo(HistoricalSeasonReview.MaxSeasonNameLength));
            var byJob = ForeignKeyOf(entity, nameof(HistoricalSeasonReview.Job));
            Assert.That((Names(byJob.Properties), byJob.DeleteBehavior),
                Is.EqualTo((new List<string> { nameof(HistoricalSeasonReview.HistoricalImportJobID) }, DeleteBehavior.Cascade)), "Removed with its job");
            Assert.That(IndexOn(entity, nameof(HistoricalSeasonReview.Status)).IsUnique, Is.False);
        }
    }

    [Test]
    public void HistoricalSourceSnapshotConfiguration_HashContentAndSource()
    {
        var entity = Configured(new HistoricalSourceSnapshotConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Event", "HistoricalSourceSnapshots")));
            Assert.That(entity.IsTemporal(), Is.False);
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(HistoricalSourceSnapshot.HistoricalSourceSnapshotID) }));
            var sha = Property(entity, nameof(HistoricalSourceSnapshot.Sha256));
            Assert.That((sha.IsNullable, sha.GetMaxLength(), sha.IsFixedLength()), Is.EqualTo((false, 64, (bool?)true)), "A SHA-256 in hex: always 64 characters");
            Assert.That(Property(entity, nameof(HistoricalSourceSnapshot.HistoricalSourceID)).GetMaxLength(), Is.EqualTo(100));
            Assert.That(Property(entity, nameof(HistoricalSourceSnapshot.ContentType)).GetMaxLength(), Is.EqualTo(HistoricalSourceSnapshot.MaxContentTypeLength));
            Assert.That(Property(entity, nameof(HistoricalSourceSnapshot.Content)).IsNullable, Is.False);
            Assert.That(ForeignKeyOf(entity, nameof(HistoricalSourceSnapshot.Source)).DeleteBehavior, Is.EqualTo(DeleteBehavior.Cascade));
            Assert.That(IndexOn(entity, nameof(HistoricalSourceSnapshot.HistoricalSourceID), nameof(HistoricalSourceSnapshot.FetchedAt)).IsUnique, Is.False,
                "A source's snapshots, latest first");
        }
    }

    [Test]
    public void XuidLookupAndImportSettingsConfigurations_KeysAndChecks()
    {
        var lookup = Configured(new XuidLookupConfiguration());
        var settings = Configured(new HistoricalImportSettingsConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((TableOf(lookup), TableOf(settings)), Is.EqualTo((("Xbox", "XuidLookups"), ("Event", "HistoricalImportSettings"))));
            Assert.That((lookup.IsTemporal(), settings.IsTemporal()), Is.EqualTo((false, false)));
            Assert.That(KeyOf(lookup), Is.EqualTo(new List<string> { nameof(XuidLookup.UserID) }), "One lookup a user");
            Assert.That(Property(lookup, nameof(XuidLookup.Error)).GetMaxLength(), Is.EqualTo(200));
            var byUser = ForeignKeyOf(lookup, nameof(XuidLookup.User));
            Assert.That((byUser.IsUnique, byUser.DeleteBehavior, byUser.DeclaringEntityType.ClrType), Is.EqualTo((true, DeleteBehavior.Cascade, typeof(XuidLookup))));
            Assert.That(IndexOn(lookup, nameof(XuidLookup.LookedUpAt)).IsUnique, Is.False);
            Assert.That(settings.FindCheckConstraint("CK_Event_HistoricalImportSettings_Singleton")!.Sql, Is.EqualTo("HistoricalImportSettingsID = 1"),
                "One row of settings");
            Assert.That(Property(settings, nameof(HistoricalImportSettings.HistoricalImportSettingsID)).ValueGenerated, Is.EqualTo(ValueGenerated.Never));
            Assert.That(Property(settings, nameof(HistoricalImportSettings.LastScheduledRunNote)).GetMaxLength(), Is.EqualTo(300));
        }
    }
}
