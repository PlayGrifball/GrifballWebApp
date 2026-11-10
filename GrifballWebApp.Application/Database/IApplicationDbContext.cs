using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GrifballWebApp.Application.Database;

/// <summary>
/// The database as the use cases see it: the aggregate roots' sets (their parts are reached through them), the identity
/// sets they read, and the unit of work (save, transactions, the change tracker). The EF context (Infrastructure's
/// GrifballContext) implements it; a request's scope resolves both to the same context.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<DiscordUser> DiscordUsers { get; }
    DbSet<DiscordMessage> DiscordMessages { get; }
    DbSet<AvailabilityOption> Availability { get; }
    DbSet<GameVersion> GameVersions { get; }
    DbSet<Match> Matches { get; }
    DbSet<RecordedGame> RecordedGames { get; }
    DbSet<Medal> Medals { get; }
    DbSet<MedalDifficulty> MedalDifficulties { get; }
    DbSet<MedalType> MedalTypes { get; }
    DbSet<Region> Regions { get; }
    DbSet<League> Leagues { get; }
    DbSet<Season> Seasons { get; }
    DbSet<PlayerSeasonStat> PlayerSeasonStats { get; }
    DbSet<GamertagClaim> GamertagClaims { get; }
    DbSet<HistoricalImportJob> HistoricalImportJobs { get; }
    DbSet<XuidLookup> XuidLookups { get; }
    DbSet<HistoricalSource> HistoricalSources { get; }
    DbSet<HistoricalImportSettings> HistoricalImportSettings { get; }
    DbSet<UserMerge> UserMerges { get; }
    DbSet<DuplicateDismissal> DuplicateDismissals { get; }
    DbSet<Vod> Vods { get; }
    DbSet<SeasonMatch> SeasonMatches { get; }
    DbSet<SeasonSignup> SeasonSignups { get; }
    DbSet<Team> Teams { get; }
    DbSet<XboxUser> XboxUsers { get; }
    DbSet<Rank> Ranks { get; }
    DbSet<QueuedPlayer> QueuedPlayer { get; }
    DbSet<MatchedMatch> MatchedMatches { get; }
    DbSet<MatchmakingSeason> MatchmakingSeasons { get; }
    DbSet<MatchmakingPlayerStat> MatchmakingPlayerStats { get; }
    DbSet<MatchReschedule> MatchReschedules { get; }
    DbSet<PasswordResetLink> PasswordResetLinks { get; }
    DbSet<ScheduleRecommendation> ScheduleRecommendations { get; }
    DbSet<OutboxMessage> Outbox { get; }

    // Identity's
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<UserLogin> UserLogins { get; }
    DbSet<UserClaim> UserClaims { get; }

    /// <summary>The games of every season match's series, to read (a series game is changed only through its match).</summary>
    IQueryable<SeriesGame> SeasonSeriesGames { get; }

    /// <summary>The players' lines of every recorded game, to read (a line is made and changed only through its game).</summary>
    IQueryable<RecordedGameParticipant> RecordedGameLines { get; }

    /// <summary>The teams of every Halo Infinite match, to read (a team is made only through its match).</summary>
    IQueryable<MatchTeam> HaloMatchTeams { get; }

    /// <summary>The players of every Halo Infinite match, to read (a player is made only through their match).</summary>
    IQueryable<MatchParticipant> HaloMatchParticipants { get; }

    /// <summary>Deletes a recorded game with its sides and players' lines (all loaded).</summary>
    void RemoveRecordedGame(RecordedGame game);

    /// <summary>Deletes a game taken out of its series (<see cref="SeasonMatch.RemoveGame"/>).</summary>
    void RemoveSeriesGame(SeasonMatch match, SeriesGame game);

    /// <summary>Deletes all of a series' games (<see cref="SeasonMatch.ClearSeries"/>), and returns them.</summary>
    IReadOnlyList<SeriesGame> ClearSeries(SeasonMatch match);

    /// <summary>Deletes a game a re-import no longer has in its imported series (<see cref="SeasonMatch.RemoveImportedGame"/>).
    /// Returns whether the series has games left.</summary>
    bool RemoveImportedSeriesGame(SeasonMatch match, SeriesGame game, bool homeWon, bool awayWon);

    DatabaseFacade Database { get; }
    ChangeTracker ChangeTracker { get; }
    IModel Model { get; }

    DbSet<TEntity> Set<TEntity>() where TEntity : class;
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
    EntityEntry Entry(object entity);
    EntityEntry<TEntity> Add<TEntity>(TEntity entity) where TEntity : class;
    void AddRange(params object[] entities);
    Task AddRangeAsync(IEnumerable<object> entities, CancellationToken cancellationToken = default);
    EntityEntry<TEntity> Remove<TEntity>(TEntity entity) where TEntity : class;
    void RemoveRange(params object[] entities);
    ValueTask<object?> FindAsync(Type entityType, object?[]? keyValues, CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Starts a transaction, or joins the one already started (nested: the outermost commit commits).</summary>
    Task StartTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
