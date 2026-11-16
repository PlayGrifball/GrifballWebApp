namespace GrifballWebApp.HistoricalArchive.Test;

/// <summary>The copy of the sources, and the build of the import's input from it.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ArchiveSourceTests
{
    /// <summary>
    /// A copy with the database's snapshots has every source the import reads. A copy without them (downloaded only, as
    /// on GitHub's runners) can't have a sheet that went private or was deleted, which only the database still has: those
    /// are a warning (the fill's problems say why each failed), and their seasons are left out of the run.
    /// </summary>
    [Test]
    public void Copy_HasEverySourceTheImportReads()
    {
        var missing = ArchiveSnapshots.ImportedEntries().Select(e => e.HistoricalSourceID).Where(id => !Archive.Snapshots.Manifest.ContainsKey(id)).ToList();
        if (!Archive.Snapshots.HasDatabaseSnapshots && missing.Count > 0)
            Assert.Warn($"Not in a copy without the database's snapshots, so not imported in this run: {string.Join(", ", missing)}");
        else
            Assert.That(missing, Is.Empty, "Sources with a config that the copy has no content of");
    }

    [Test]
    public void Build_HasNoWarnings()
    {
        Assert.That(Archive.Build.Warnings, Is.Empty, "What the build could not read (a missing tab, an unreadable snapshot, a link without a match)");
    }

    [Test]
    public void Build_HasSeasonsOfEverySourceWithStats()
    {
        var withStats = ArchiveSnapshots.ImportedEntries().Where(e => e.Config!.Tabs.Count > 0 || e.Config.Tables.Count > 0).Select(e => e.HistoricalSourceID);
        var built = Archive.Build.Input.Seasons.Seasons.Select(s => s.SourceWorkbook).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.That(withStats.Where(id => !built.Contains(id)), Is.Empty, "Sources whose stats tabs or tables made no season");
    }
}
