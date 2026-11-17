using GrifballWebApp.Domain.League;
using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The model as the database had it before AddTeamArchivedPlacing: Teams without ArchivedRank and ArchivedFinish. A
/// migration test that seeds a database migrated to before an earlier migration writes its teams through this, as the
/// model of the day did; today's model would write columns the table doesn't have yet. Its own context type, so its own
/// cached model.
/// </summary>
internal sealed class BeforeTeamArchivedPlacingContext(DbContextOptions<GrifballContext> options) : GrifballContext(options)
{
    /// <summary>On the same database as <paramref name="context"/>.</summary>
    public static BeforeTeamArchivedPlacingContext On(GrifballContext context)
        => new(TestDatabase.Options(context.Database.GetConnectionString()!).Options);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var team = modelBuilder.Entity<Team>();
        team.Ignore(t => t.ArchivedRank);
        team.Ignore(t => t.ArchivedFinish);
    }
}
