using System.Reflection;
using GrifballWebApp.Database;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Match = GrifballWebApp.Domain.Halo.Match;

namespace GrifballWebApp.Test;

/// <summary>
/// A series game is a child of its season match (#298): the app has no public route to make, change or delete one but
/// through the match. The context's set is internal (tests reach it through InternalsVisibleTo), the context's public
/// route is read-only, and the games' links back to it are set by EF only.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class SeriesGameAccessTests
{
    private const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance;

    [Test]
    public void TheContext_HasNoSetOfSeriesGames_OnlyAReadOnlyQuery()
    {
        var sets = typeof(GrifballContext).GetProperties(Public | BindingFlags.NonPublic).Where(p => p.PropertyType == typeof(DbSet<SeriesGame>));
        var query = typeof(GrifballContext).GetProperty(nameof(GrifballContext.SeasonSeriesGames), Public)!;

        Assert.Multiple(() =>
        {
            Assert.That(sets, Is.Empty, "A part: reached through its season match");
            Assert.That(query.PropertyType, Is.EqualTo(typeof(IQueryable<SeriesGame>)), "To read, not to add or remove");
            Assert.That(query.CanWrite, Is.False);
        });
    }

    [TestCase(typeof(RecordedGame), nameof(RecordedGame.SeriesGame))]
    [TestCase(typeof(Match), nameof(Match.SeriesGame))]
    public void AGamesLinkToItsSeries_IsSetByEFOnly(Type type, string navigation)
    {
        var property = type.GetProperty(navigation, Public)!;

        Assert.That(property.SetMethod is { IsPublic: false, IsAssembly: false }, Is.True, $"{type.Name}.{navigation} has a private setter");
    }

    [Test]
    public void ASeriesGame_IsMadeAndChangedOnlyInsideTheDatabaseLayer()
    {
        // Its own and the audit columns it inherits (AuditableEntity)
        var setters = typeof(SeriesGame).GetProperties(Public).Where(p => p.SetMethod is not null).Select(p => (p.Name, p.SetMethod!.IsPublic));
        var constructors = typeof(SeriesGame).GetConstructors(Public);

        Assert.Multiple(() =>
        {
            Assert.That(setters.Where(s => s.IsPublic).Select(s => s.Name), Is.Empty);
            Assert.That(constructors, Is.Empty);
        });
    }
}
