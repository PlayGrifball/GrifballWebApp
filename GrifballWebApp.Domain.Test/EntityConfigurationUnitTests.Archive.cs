using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
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
            Assert.That(entity.IsTemporal(), Is.False, "Each refresh stamps its fetch status");
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
            Assert.That(entity.IsTemporal(), Is.False, "Bookkeeping of its job, deleted with it");
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
    public void HistoricalProvenanceConfiguration_NoHistory_KnownKind_IndexedByEntityAndBySource()
    {
        var entity = Configured(new HistoricalProvenanceConfiguration());
        var lengths = new Dictionary<string, int?>
        {
            [nameof(HistoricalProvenance.Entity)] = 100, [nameof(HistoricalProvenance.EntityKey)] = 100, [nameof(HistoricalProvenance.SourceID)] = 100,
            [nameof(HistoricalProvenance.Sheet)] = 200, [nameof(HistoricalProvenance.Cells)] = 50, [nameof(HistoricalProvenance.Value)] = 1000,
            [nameof(HistoricalProvenance.Detail)] = 500,
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Event", "HistoricalProvenance")));
            Assert.That(entity.IsTemporal(), Is.False, "Its Read rows are replaced by every run");
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(HistoricalProvenance.HistoricalProvenanceID) }));
            Assert.That(entity.FindCheckConstraint("CK_Event_HistoricalProvenance_KnownKind")!.Sql, Is.EqualTo("Kind IN (1, 2, 3)"), "Created, Read and Changed");
            Assert.That(MaxLengths(entity, lengths), Is.EqualTo(lengths));
            Assert.That((NullableOf(entity, nameof(HistoricalProvenance.Entity)), NullableOf(entity, nameof(HistoricalProvenance.EntityKey)), NullableOf(entity, nameof(HistoricalProvenance.SourceID))),
                Is.EqualTo(((bool?)false, (bool?)false, (bool?)false)));
            Assert.That(entity.GetForeignKeys(), Is.Empty, "Keyed by name: a row of any table, a job or snapshot gone, still says where it came from");
            Assert.That(IndexIsUnique(entity, nameof(HistoricalProvenance.Entity), nameof(HistoricalProvenance.EntityKey)), Is.False, "An entity's rows");
            Assert.That(IndexIsUnique(entity, nameof(HistoricalProvenance.SourceID), nameof(HistoricalProvenance.Sheet)), Is.False, "What a tab made");
        }
    }

    [Test]
    public void XboxUserImageConfiguration_KeysLengthsAndNoHistory()
    {
        var image = Configured(new XboxUserImageConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(image), Is.EqualTo(("Xbox", "XboxUserImages")));
            Assert.That(image.IsTemporal(), Is.False, "Whole images rewritten would only grow a history");
            Assert.That(KeyOf(image), Is.EqualTo(new List<string> { nameof(XboxUserImage.XboxUserID), nameof(XboxUserImage.Kind) }), "One image of each kind an account");
            Assert.That((MaxLengthOf(image, nameof(XboxUserImage.Source)), MaxLengthOf(image, nameof(XboxUserImage.ContentType)), MaxLengthOf(image, nameof(XboxUserImage.Error))), Is.EqualTo((500, 100, 200)));
            var byAccount = ForeignKeyOf(image, nameof(XboxUserImage.XboxUser));
            Assert.That((ColumnsOf(byAccount), DeleteOf(byAccount)), Is.EqualTo((new List<string> { nameof(XboxUserImage.XboxUserID) }, (DeleteBehavior?)DeleteBehavior.Cascade)));
        }
    }

    [Test]
    public void XboxUserArmorConfiguration_KeysLengthsAndNoHistory()
    {
        var armor = Configured(new XboxUserArmorConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(armor), Is.EqualTo(("Xbox", "XboxUserArmor")));
            Assert.That(armor.IsTemporal(), Is.False, "Rewritten on every change, no history");
            Assert.That(KeyOf(armor), Is.EqualTo(new List<string> { nameof(XboxUserArmor.XboxUserID) }), "One row an account");
            Assert.That((MaxLengthOf(armor, nameof(XboxUserArmor.HelmetPath)), MaxLengthOf(armor, nameof(XboxUserArmor.Error))), Is.EqualTo((300, 200)));
            Assert.That(Property(armor, nameof(XboxUserArmor.WeaponCoatingsJson)).GetMaxLength(), Is.Null,
                "A path (up to 300) per weapon type Halo has: no cap, so a full account still saves");
            var byAccount = ForeignKeyOf(armor, nameof(XboxUserArmor.XboxUser));
            Assert.That((ColumnsOf(byAccount), DeleteOf(byAccount)), Is.EqualTo((new List<string> { nameof(XboxUserArmor.XboxUserID) }, (DeleteBehavior?)DeleteBehavior.Cascade)));
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
            Assert.That((lookup.IsTemporal(), settings.IsTemporal()), Is.EqualTo((false, true)), "A lookup is a retry queue's");
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
