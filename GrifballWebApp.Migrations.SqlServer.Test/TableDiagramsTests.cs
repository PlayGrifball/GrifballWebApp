using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GrifballWebApp.Migrations.SqlServer.Test;

[TestFixture]
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
        var doc = File.ReadAllText(path).ReplaceLineEndings("\n");
        var begin = doc.IndexOf(TableDiagrams.Begin, StringComparison.Ordinal);
        var end = doc.IndexOf(TableDiagrams.End, StringComparison.Ordinal);
        Assert.That(begin >= 0 && end > begin, $"docs/database.md has no {TableDiagrams.Begin} ... {TableDiagrams.End} section");

        var committed = doc[(begin + TableDiagrams.Begin.Length)..end].Trim('\n') + "\n\n";
        if (Environment.GetEnvironmentVariable("GRIF_UPDATE_DOCS") == "1" && committed != generated)
        {
            File.WriteAllText(path, doc[..begin] + TableDiagrams.Begin + "\n\n" + generated + doc[end..]);
            committed = generated;
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
