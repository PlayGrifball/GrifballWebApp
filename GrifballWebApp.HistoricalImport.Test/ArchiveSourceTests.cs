namespace GrifballWebApp.HistoricalImport.Test;

/// <summary>The copy of the sources, and the build of the import's input from it.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ArchiveSourceTests
{
    [Test]
    public void Copy_HasEverySourceTheImportReads()
    {
        var missing = ArchiveSnapshots.ImportedEntries().Select(e => e.HistoricalSourceID).Where(id => !Archive.Snapshots.Manifest.ContainsKey(id)).ToList();

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
