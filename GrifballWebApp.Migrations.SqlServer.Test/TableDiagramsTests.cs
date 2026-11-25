using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GrifballWebApp.Migrations.SqlServer.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class TableDiagramsTests
{
    [Test]
    public void DatabaseDoc_Should_HaveTheModelsTableDiagrams()
    {
        var options = new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.SqlServer, "Server=none;Database=none;")
            .Options;
        using var context = new GrifballContext(options);
        var generated = TableDiagrams.Generate(context.GetService<IDesignTimeModel>().Model);

        var path = Path.Combine(RepositoryRoot(), "docs", "database.md");
        var original = File.ReadAllText(path);
        var newline = original.Contains("\r\n") ? "\r\n" : "\n";
        var doc = original.ReplaceLineEndings("\n");
        var begin = doc.IndexOf(TableDiagrams.Begin, StringComparison.Ordinal);
        var end = doc.IndexOf(TableDiagrams.End, StringComparison.Ordinal);
        Assert.That(begin >= 0 && end > begin, $"docs/database.md has no {TableDiagrams.Begin} ... {TableDiagrams.End} section");

        var committed = doc[(begin + TableDiagrams.Begin.Length)..end].Trim('\n') + "\n\n";
        // Never rewrite in CI: drift there must fail, not fix itself in the runner's checkout and pass
        var inCi = Environment.GetEnvironmentVariable("CI") is not null || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is not null;
        if (Environment.GetEnvironmentVariable("GRIF_UPDATE_DOCS") == "1" && !inCi && committed != generated)
        {
            var updated = doc[..begin] + TableDiagrams.Begin + "\n\n" + generated + doc[end..];
            File.WriteAllText(path, updated.ReplaceLineEndings(newline));
            Assert.Fail("docs/database.md's table diagrams were out of date and are rewritten: review and commit the change, then run again");
        }

        Assert.That(committed, Is.EqualTo(generated),
            "docs/database.md's table diagrams are not the model's: run this test with GRIF_UPDATE_DOCS=1 and commit the change");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GrifballWebApp.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException($"No GrifballWebApp.slnx above {AppContext.BaseDirectory}");
    }
}
