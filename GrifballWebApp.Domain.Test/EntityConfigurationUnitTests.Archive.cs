using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GrifballWebApp.Domain.Test;

/// <summary>The Archive configurations (recorded games' teams and players, the historical import's sources, jobs and
/// reviews, XUID lookups): see the class's main file.</summary>
public partial class EntityConfigurationUnitTests
{
    /// <summary>Each named column's max length, as the configuration set it.</summary>
    private static Dictionary<string, int?> MaxLengths(IReadOnlyEntityType entity, IReadOnlyDictionary<string, int?> expected) =>
        expected.Keys.ToDictionary(name => name, name => MaxLengthOf(entity, name));

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
            Assert.That(MaxLengthOf(entity, nameof(RecordedGameTeam.Name)), Is.EqualTo(Team.TeamNameColumnLength));
            var byGame = ForeignKeyOf(entity, nameof(RecordedGameTeam.RecordedGame));
            var byTeam = ForeignKeyOf(entity, nameof(RecordedGameTeam.Team));
            Assert.That((DeleteOf(byGame), DeleteOf(byTeam), RequiredOf(byTeam)),
                Is.EqualTo(((DeleteBehavior?)DeleteBehavior.Cascade, (DeleteBehavior?)DeleteBehavior.ClientSetNull, (bool?)false)),
                "A team's deletion clears the link, never the recorded game");
            Assert.That(IndexIsUnique(entity, nameof(RecordedGameTeam.RecordedGameID), nameof(RecordedGameTeam.Side)), Is.True, "One team a side");
            Assert.That(IndexIsUnique(entity, nameof(RecordedGameTeam.TeamID)), Is.False);
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
            Assert.That((NullableOf(entity, nameof(RecordedGameParticipant.Gamertag)), Property(entity, nameof(RecordedGameParticipant.Gamertag)).GetMaxLength()),
                Is.EqualTo(((bool?)false, (int?)null)), "Xbox.XboxUsers.Gamertag's type, which it is UNION ALLed with");
            var byGame = ForeignKeyOf(entity, nameof(RecordedGameParticipant.RecordedGame));
            var byTeam = ForeignKeyOf(entity, nameof(RecordedGameParticipant.RecordedGameTeam));
            var byUser = ForeignKeyOf(entity, nameof(RecordedGameParticipant.User));
            Assert.That((DeleteOf(byGame), DeleteOf(byTeam), RequiredOf(byTeam), DeleteOf(byUser)),
                Is.EqualTo(((DeleteBehavior?)DeleteBehavior.Cascade, (DeleteBehavior?)DeleteBehavior.ClientSetNull, (bool?)false, (DeleteBehavior?)DeleteBehavior.Restrict)),
                "One cascade path from the game; a user with games isn't deleted");
            Assert.That(IndexIsUnique(entity, nameof(RecordedGameParticipant.RecordedGameID), nameof(RecordedGameParticipant.UserID)), Is.True,
                "A player has one line in a game");
            Assert.That(IndexIsUnique(entity, nameof(RecordedGameParticipant.UserID)), Is.False);
        }
    }

    [Test]
    public void HistoricalSourceConfiguration_TemporalWithItsLengths()
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
            Assert.That(entity.IsTemporal(), Is.True, "Its fetch status changes once a day at most");
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(HistoricalSource.HistoricalSourceID) }));
            Assert.That(MaxLengths(entity, lengths), Is.EqualTo(lengths));
            Assert.That(new List<string> { nameof(HistoricalSource.Category), nameof(HistoricalSource.Title), nameof(HistoricalSource.Url) }
                .Select(name => NullableOf(entity, name)), Is.All.False);
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
            Assert.That(MaxLengthOf(entity, nameof(HistoricalImportJob.Stage)), Is.EqualTo(HistoricalImportJob.MaxStageLength));
            Assert.That(IndexIsUnique(entity, nameof(HistoricalImportJob.Status)), Is.False);
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
            Assert.That(entity.IsTemporal(), Is.True, "Who decided what, and when");
            Assert.That(entity.FindCheckConstraint("CK_Event_HistoricalSeasonReviews_DecidedOnceDecided")!.Sql,
                Is.EqualTo("(Status IN (3, 4) AND DecidedAt IS NOT NULL) OR (Status IN (1, 2) AND DecidedAt IS NULL)"));
            Assert.That(entity.FindCheckConstraint("CK_Event_HistoricalSeasonReviews_ApprovalJobOnlyWhenApproved")!.Sql,
                Is.EqualTo("ApprovalJobID IS NULL OR Status = 3"));
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(HistoricalSeasonReview.HistoricalSeasonReviewID) }));
            Assert.That((NullableOf(entity, nameof(HistoricalSeasonReview.SeasonKey)), MaxLengthOf(entity, nameof(HistoricalSeasonReview.SeasonKey))),
                Is.EqualTo(((bool?)false, (int?)HistoricalSeasonReview.MaxSeasonKeyLength)));
            Assert.That(MaxLengthOf(entity, nameof(HistoricalSeasonReview.SeasonName)), Is.EqualTo(HistoricalSeasonReview.MaxSeasonNameLength));
            var byJob = ForeignKeyOf(entity, nameof(HistoricalSeasonReview.Job));
            Assert.That((ColumnsOf(byJob), DeleteOf(byJob)),
                Is.EqualTo((new List<string> { nameof(HistoricalSeasonReview.HistoricalImportJobID) }, (DeleteBehavior?)DeleteBehavior.Cascade)), "Removed with its job");
            Assert.That(IndexIsUnique(entity, nameof(HistoricalSeasonReview.Status)), Is.False);
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
            var sha = nameof(HistoricalSourceSnapshot.Sha256);
            Assert.That((NullableOf(entity, sha), MaxLengthOf(entity, sha), Property(entity, sha).IsFixedLength()),
                Is.EqualTo(((bool?)false, (int?)64, (bool?)true)), "A SHA-256 in hex: always 64 characters");
            Assert.That((NullableOf(entity, nameof(HistoricalSourceSnapshot.HistoricalSourceID)), MaxLengthOf(entity, nameof(HistoricalSourceSnapshot.HistoricalSourceID))),
                Is.EqualTo(((bool?)false, (int?)100)));
            Assert.That(MaxLengthOf(entity, nameof(HistoricalSourceSnapshot.ContentType)), Is.EqualTo(HistoricalSourceSnapshot.MaxContentTypeLength));
            Assert.That(NullableOf(entity, nameof(HistoricalSourceSnapshot.Content)), Is.False);
            Assert.That(DeleteOf(ForeignKeyOf(entity, nameof(HistoricalSourceSnapshot.Source))), Is.EqualTo(DeleteBehavior.Cascade));
            Assert.That(IndexIsUnique(entity, nameof(HistoricalSourceSnapshot.HistoricalSourceID), nameof(HistoricalSourceSnapshot.FetchedAt)), Is.False,
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
            Assert.That((lookup.IsTemporal(), settings.IsTemporal()), Is.EqualTo((true, true)));
            Assert.That(KeyOf(lookup), Is.EqualTo(new List<string> { nameof(XuidLookup.UserID) }), "One lookup a user");
            Assert.That(MaxLengthOf(lookup, nameof(XuidLookup.Error)), Is.EqualTo(200));
            var byUser = ForeignKeyOf(lookup, nameof(XuidLookup.User));
            Assert.That((ColumnsOf(byUser), byUser.IsUnique, DeleteOf(byUser), byUser.DeclaringEntityType.ClrType),
                Is.EqualTo((new List<string> { nameof(XuidLookup.UserID) }, true, (DeleteBehavior?)DeleteBehavior.Cascade, typeof(XuidLookup))));
            Assert.That(IndexIsUnique(lookup, nameof(XuidLookup.LookedUpAt)), Is.False);
            Assert.That(settings.FindCheckConstraint("CK_Event_HistoricalImportSettings_Singleton")!.Sql, Is.EqualTo("HistoricalImportSettingsID = 1"),
                "One row of settings");
            Assert.That(KeyOf(settings), Is.EqualTo(new List<string> { nameof(HistoricalImportSettings.HistoricalImportSettingsID) }));
            Assert.That(ValueGeneratedOf(settings, nameof(HistoricalImportSettings.HistoricalImportSettingsID)), Is.EqualTo(ValueGenerated.Never));
            Assert.That(MaxLengthOf(settings, nameof(HistoricalImportSettings.LastScheduledRunNote)), Is.EqualTo(300));
        }
    }
}
