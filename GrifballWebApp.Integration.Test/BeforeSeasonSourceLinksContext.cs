using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The model as the database had it before AddSeasonSourceLinks: Seasons without HistoricalSourceLinks. A migration
/// test that seeds a database migrated to before then writes its seasons through this, as the model of the day did;
/// today's model reads that column back after the insert. Its own context type, so its own cached model.
/// </summary>
internal sealed class BeforeSeasonSourceLinksContext(DbContextOptions<GrifballContext> options) : GrifballContext(options)
{
    /// <summary>On the same database as <paramref name="context"/>.</summary>
    public static BeforeSeasonSourceLinksContext On(GrifballContext context)
        => new(TestDatabase.Options(context.Database.GetConnectionString()!).Options);

    public static void IgnoreSourceLinks(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Season>().Ignore(s => s.HistoricalSourceLinks);
        // Every database this is for is older than the users' picture choice, which the model would insert and read back
        modelBuilder.Entity<User>().Ignore(u => u.ProfilePicture);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        IgnoreSourceLinks(modelBuilder);
    }
}
