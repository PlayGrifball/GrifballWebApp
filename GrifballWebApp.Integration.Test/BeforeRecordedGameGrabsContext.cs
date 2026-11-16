using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The model as the database had it before AddRecordedGameGrabs: recorded game lines without Grabs. A migration test that
/// seeds lines on a database migrated to before an earlier migration writes them through this, as the model of the day
/// did; today's model would write a column the table doesn't have yet. Its own context type, so its own cached model.
/// </summary>
internal sealed class BeforeRecordedGameGrabsContext(DbContextOptions<GrifballContext> options) : GrifballContext(options)
{
    /// <summary>On the same database as <paramref name="context"/>.</summary>
    public static BeforeRecordedGameGrabsContext On(GrifballContext context)
        => new(TestDatabase.Options(context.Database.GetConnectionString()!).Options);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<RecordedGameParticipant>().Ignore(p => p.Grabs);
    }
}
