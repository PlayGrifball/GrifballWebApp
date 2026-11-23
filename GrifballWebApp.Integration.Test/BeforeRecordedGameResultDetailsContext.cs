using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The model as the database had it before AddRecordedGameResultDetails: recorded games without Vod, Host and IsForfeit,
/// and (from the later AddRecordedGameGrabs) their lines without Grabs.
/// A migration test that seeds recorded games in a database migrated to before an earlier migration writes them through
/// this (or a context that ignores the same, <see cref="IgnoreResultDetails"/>), as the model of the day did. Its own
/// context type, so its own cached model.
/// </summary>
internal sealed class BeforeRecordedGameResultDetailsContext(DbContextOptions<GrifballContext> options) : GrifballContext(options)
{
    /// <summary>On the same database as <paramref name="context"/>.</summary>
    public static BeforeRecordedGameResultDetailsContext On(GrifballContext context)
        => new(TestDatabase.Options(context.Database.GetConnectionString()!).Options);

    /// <summary>A recorded game as the model had it before the migration.</summary>
    public static void IgnoreResultDetails(ModelBuilder modelBuilder)
    {
        var game = modelBuilder.Entity<RecordedGame>();
        game.Ignore(g => g.Vod);
        game.Ignore(g => g.Host);
        game.Ignore(g => g.IsForfeit);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        IgnoreResultDetails(modelBuilder);
        modelBuilder.Entity<RecordedGameParticipant>().Ignore(p => p.Grabs);
        BeforeSeasonSourceLinksContext.IgnoreSourceLinks(modelBuilder);
    }
}
