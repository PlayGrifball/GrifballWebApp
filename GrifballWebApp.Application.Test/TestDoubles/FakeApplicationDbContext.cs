using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GrifballWebApp.Application.Test.TestDoubles;

/// <summary>
/// IApplicationDbContext, real but disposable: a SQLite in-memory database, with the same model
/// GrifballWebApp.Infrastructure's GrifballContext builds (its entity configurations live in GrifballWebApp.Domain,
/// beside the models, so this layer's tests reach them too). It skips what only matters for the real databases
/// (Postgres' collation, SQL Server's temporal history, the SQLite history triggers): a unit test's database is
/// thrown away with the test. Its model setup is a copy of GrifballContext's: GrifballWebApp.Integration.Test's
/// FakeApplicationDbContextModelTests compares the two schemas table by table, and fails when they drift apart.
/// </summary>
internal sealed class FakeApplicationDbContext :
    IdentityDbContext<User, Role, int, UserClaim, UserRole, UserLogin, IdentityRoleClaim<int>, IdentityUserToken<int>>,
    IApplicationDbContext
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection? ownedConnection;

    public FakeApplicationDbContext(DbContextOptions<FakeApplicationDbContext> options) : base(options)
    {
    }

    /// <summary>A throwaway SQLite in-memory database, its own connection kept open for the context's lifetime
    /// (closing it drops the database) and disposed with it. The schema (and the model's HasData rows) is a copy of
    /// <see cref="Template"/>'s: made once a test run, copied page by page, rather than ~80 tables' DDL a test.</summary>
    public FakeApplicationDbContext(IEnumerable<IInterceptor>? interceptors = null) : this(BuildOptions(out var connection, interceptors ?? []))
    {
        ownedConnection = connection;
    }

    /// <summary>The schema every fake context starts from, made once (EnsureCreated) on a connection of its own that stays
    /// open for the test run. Only read from, by one copy at a time: a SQLite connection isn't safe to share across threads.</summary>
    private static readonly Lazy<Microsoft.Data.Sqlite.SqliteConnection> Template = new(() =>
    {
        var template = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        template.Open();
        using var context = new FakeApplicationDbContext(new DbContextOptionsBuilder<FakeApplicationDbContext>().UseSqlite(template).Options);
        context.Database.EnsureCreated();
        return template;
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lock TemplateLock = new();

    private static DbContextOptions<FakeApplicationDbContext> BuildOptions(out Microsoft.Data.Sqlite.SqliteConnection connection, IEnumerable<IInterceptor> interceptors)
    {
        connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        lock (TemplateLock)
            Template.Value.BackupDatabase(connection);
        return new DbContextOptionsBuilder<FakeApplicationDbContext>().UseSqlite(connection).AddInterceptors(interceptors).Options;
    }

    /// <summary>
    /// Another context on this one's database (its connection, which it doesn't own: disposing it leaves the database be),
    /// running <paramref name="interceptors"/>: the other request of a race, or the service's own context with a hook in it
    /// (<see cref="BeforeFirstSave"/>, <see cref="BeforeFirstTransaction"/>, <see cref="RefusingSaves"/>).
    /// </summary>
    public FakeApplicationDbContext NewContextLike(params IInterceptor[] interceptors)
        => new(new DbContextOptionsBuilder<FakeApplicationDbContext>().UseSqlite(Database.GetDbConnection()).AddInterceptors(interceptors).Options);

    public override void Dispose()
    {
        base.Dispose();
        ownedConnection?.Dispose();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (ownedConnection is not null)
            await ownedConnection.DisposeAsync();
    }

    public DbSet<DiscordUser> DiscordUsers { get; set; } = null!;
    public DbSet<DiscordMessage> DiscordMessages { get; set; } = null!;
    public DbSet<AvailabilityOption> Availability { get; set; } = null!;
    public DbSet<GameVersion> GameVersions { get; set; } = null!;
    public DbSet<Match> Matches { get; set; } = null!;
    public DbSet<RecordedGame> RecordedGames { get; set; } = null!;
    public DbSet<Medal> Medals { get; set; } = null!;
    public DbSet<MedalDifficulty> MedalDifficulties { get; set; } = null!;
    public DbSet<MedalType> MedalTypes { get; set; } = null!;
    public DbSet<Region> Regions { get; set; } = null!;
    public DbSet<League> Leagues { get; set; } = null!;
    public DbSet<Season> Seasons { get; set; } = null!;
    public DbSet<PlayerSeasonStat> PlayerSeasonStats { get; set; } = null!;
    public DbSet<GamertagClaim> GamertagClaims { get; set; } = null!;
    public DbSet<HistoricalImportJob> HistoricalImportJobs { get; set; } = null!;
    public DbSet<XuidLookup> XuidLookups { get; set; } = null!;
    public DbSet<HistoricalSource> HistoricalSources { get; set; } = null!;
    public DbSet<HistoricalImportSettings> HistoricalImportSettings { get; set; } = null!;
    public DbSet<UserMerge> UserMerges { get; set; } = null!;
    public DbSet<DuplicateDismissal> DuplicateDismissals { get; set; } = null!;
    public DbSet<Vod> Vods { get; set; } = null!;
    public DbSet<SeasonMatch> SeasonMatches { get; set; } = null!;
    public DbSet<SeasonSignup> SeasonSignups { get; set; } = null!;
    public DbSet<Team> Teams { get; set; } = null!;
    public DbSet<XboxUser> XboxUsers { get; set; } = null!;
    public DbSet<Rank> Ranks { get; set; } = null!;
    public DbSet<QueuedPlayer> QueuedPlayer { get; set; } = null!;
    public DbSet<MatchedMatch> MatchedMatches { get; set; } = null!;
    public DbSet<MatchmakingSeason> MatchmakingSeasons { get; set; } = null!;
    public DbSet<MatchmakingPlayerStat> MatchmakingPlayerStats { get; set; } = null!;
    public DbSet<MatchReschedule> MatchReschedules { get; set; } = null!;
    public DbSet<PasswordResetLink> PasswordResetLinks { get; set; } = null!;
    public DbSet<ScheduleRecommendation> ScheduleRecommendations { get; set; } = null!;
    public DbSet<OutboxMessage> Outbox { get; set; } = null!;

    /// <summary>SQLite's built-in case-insensitive collation (GrifballContext.SqliteCaseInsensitiveCollation).</summary>
    private const string SqliteCaseInsensitiveCollation = "NOCASE";

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<string>().UseCollation(SqliteCaseInsensitiveCollation);
        configurationBuilder.Properties<TimeSpan>().HaveConversion<TimeSpanToTicksConverter>();
        configurationBuilder.Properties<decimal>().HaveConversion<double>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new DiscordUserConfiguration());
        builder.ApplyConfiguration(new DiscordMessageConfiguration());
        builder.ApplyConfiguration(new AvailabilityOptionConfiguration());
        builder.ApplyConfiguration(new GameVersionConfiguration());
        builder.ApplyConfiguration(new MatchConfiguration());
        builder.ApplyConfiguration(new MatchBracketInfoConfiguration());
        builder.ApplyConfiguration(new SeriesGameConfiguration());
        builder.Entity<SeriesGame>().HasIndex(e => e.MatchID).HasFilter("[MatchID] IS NOT NULL");
        builder.Entity<SeriesGame>().HasIndex(e => e.RecordedGameID).HasFilter("[RecordedGameID] IS NOT NULL");
        builder.Entity<MatchReschedule>().HasIndex(e => e.SeasonMatchID).IsUnique().HasFilter("[Status] = 0");
        builder.ApplyConfiguration(new RecordedGameConfiguration());
        builder.ApplyConfiguration(new RecordedGameTeamConfiguration());
        builder.ApplyConfiguration(new RecordedGameParticipantConfiguration());
        builder.ApplyConfiguration(new MatchParticipantConfiguration());
        builder.ApplyConfiguration(new MatchTeamConfiguration());
        builder.ApplyConfiguration(new MedalConfiguration());
        builder.ApplyConfiguration(new MedalDifficultyConfiguration());
        builder.ApplyConfiguration(new MedalEarnedConfiguration());
        builder.ApplyConfiguration(new MedalTypeConfiguration());
        builder.ApplyConfiguration(new RegionConfiguration());
        builder.ApplyConfiguration(new LeagueConfiguration());
        builder.ApplyConfiguration(new RoleConfiguration());
        builder.ApplyConfiguration(new SeasonConfiguration());
        builder.Entity<Season>().HasIndex(e => e.HistoricalSourceID).HasFilter("[HistoricalSourceID] IS NOT NULL");
        builder.ApplyConfiguration(new PlayerSeasonStatConfiguration());
        builder.ApplyConfiguration(new PlayerChampionshipConfiguration());
        builder.ApplyConfiguration(new XboxGamertagHistoryConfiguration());
        builder.ApplyConfiguration(new GamertagClaimConfiguration());
        builder.Entity<GamertagClaim>().HasIndex(e => new { e.Kind, e.XboxUserID, e.NormalizedGamertag }).IsUnique()
            .HasFilter("[Status] IN (1, 2) AND [ClaimantUserID] IS NULL");
        builder.Entity<GamertagClaim>().HasIndex(e => new { e.Kind, e.ClaimantUserID, e.NormalizedGamertag }).IsUnique()
            .HasFilter("[Status] IN (1, 2) AND [ClaimantUserID] IS NOT NULL");
        builder.ApplyConfiguration(new HistoricalImportJobConfiguration());
        builder.ApplyConfiguration(new XuidLookupConfiguration());
        builder.ApplyConfiguration(new HistoricalSourceConfiguration());
        builder.ApplyConfiguration(new HistoricalSourceSnapshotConfiguration());
        builder.ApplyConfiguration(new HistoricalImportSettingsConfiguration());
        builder.ApplyConfiguration(new HistoricalSeasonReviewConfiguration());
        builder.ApplyConfiguration(new UserMergeConfiguration());
        builder.ApplyConfiguration(new DuplicateDismissalConfiguration());
        builder.ApplyConfiguration(new VodConfiguration());
        builder.ApplyConfiguration(new VodTeamConfiguration());
        builder.ApplyConfiguration(new SeasonAvailabilityConfiguration());
        builder.ApplyConfiguration(new SeasonMatchConfiguration());
        builder.ApplyConfiguration(new SeasonSignupConfiguration());
        builder.ApplyConfiguration(new SignupAvailabilityConfiguration());
        builder.ApplyConfiguration(new TeamConfiguration());
        builder.ApplyConfiguration(new TeamAvailabilityConfiguration());
        builder.ApplyConfiguration(new TeamPlayerConfiguration());
        builder.ApplyConfiguration(new UserConfiguration());
        builder.ApplyConfiguration(new UserExperienceConfiguration());
        builder.ApplyConfiguration(new UserRoleConfiguration());
        builder.ApplyConfiguration(new UserLoginConfiguration());
        builder.ApplyConfiguration(new UserClaimConfiguration());
        builder.ApplyConfiguration(new XboxUserConfiguration());
        builder.ApplyConfiguration(new RankConfiguration());
        builder.ApplyConfiguration(new QueuedPlayerConfiguration());
        builder.ApplyConfiguration(new MatchedPlayerConfiguration());
        builder.ApplyConfiguration(new MatchedTeamConfiguration());
        builder.ApplyConfiguration(new MatchedMatchConfiguration());
        builder.ApplyConfiguration(new MatchmakingSeasonConfiguration());
        builder.ApplyConfiguration(new MatchmakingPlayerStatConfiguration());
        builder.ApplyConfiguration(new MatchedWinnerVoteConfiguration());
        builder.ApplyConfiguration(new MatchedKickVoteConfiguration());
        builder.ApplyConfiguration(new MatchRescheduleConfiguration());
        builder.ApplyConfiguration(new PasswordResetLinkConfiguration());
        builder.ApplyConfiguration(new ScheduleRecommendationConfiguration());
        builder.ApplyConfiguration(new ScheduleRecommendationMatchConfiguration());
        builder.ApplyConfiguration(new OutboxMessageConfiguration());

        // ASP.NET Identity's other two tables, where GrifballContext puts them (it marks them temporal: SQL Server only)
        builder.Entity<IdentityRoleClaim<int>>(b => b.ToTable("RoleClaims", "Auth"));
        builder.Entity<IdentityUserToken<int>>(b =>
        {
            b.ToTable("UserTokens", "Auth");
            b.HasKey(e => new { e.UserId, e.LoginProvider, e.Name });
        });
    }

    /// <summary>GrifballContext.UtcDateTimeConverter, copied: a DateTime with Kind Local can't come from this
    /// layer's own tests, which build their fixtures with DateTime.UtcNow or a fixed UTC instant.</summary>
    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => ToUtc(v),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
        private static DateTime ToUtc(DateTime v)
        {
            if (v.Kind == DateTimeKind.Local)
                throw new InvalidOperationException($"A DateTime with Kind Local ({v:O}) can't be stored: the database holds UTC.");
            return DateTime.SpecifyKind(v, DateTimeKind.Utc);
        }
    }

    public IQueryable<SeriesGame> SeasonSeriesGames => SeasonMatches.SelectMany(m => m.SeriesGames);

    public IQueryable<RecordedGameParticipant> RecordedGameLines => RecordedGames.SelectMany(g => g.Participants);

    public void RemoveRecordedGame(RecordedGame game)
    {
        RemoveRange(game.Participants);
        RemoveRange(game.Teams);
        RecordedGames.Remove(game);
    }

    public void RemoveSeriesGame(SeasonMatch match, SeriesGame game)
    {
        match.RemoveGame(game);
        Remove(game);
    }

    public IReadOnlyList<SeriesGame> ClearSeries(SeasonMatch match)
    {
        var games = match.ClearSeries();
        RemoveRange(games);
        return games;
    }

    public bool RemoveImportedSeriesGame(SeasonMatch match, SeriesGame game, bool homeWon, bool awayWon)
    {
        var left = match.RemoveImportedGame(game, homeWon, awayWon);
        Remove(game);
        return left;
    }

    public void RemoveArchivedMatch(MatchedMatch match) => MatchedMatchRemoval.RemoveArchivedMatch(this, match);

    public IQueryable<MatchTeam> HaloMatchTeams => Matches.SelectMany(m => m.MatchTeams);

    public IQueryable<MatchParticipant> HaloMatchParticipants => HaloMatchTeams.SelectMany(t => t.MatchParticipants);

    private IDbContextTransaction? transaction;
    private byte transactionLayer;

    public async Task StartTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (transactionLayer == byte.MaxValue)
            throw new InvalidOperationException("Too many nested transactions.");
        transactionLayer++;

        if (transactionLayer == 1 && transaction is null)
            transaction = await Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (transaction is null && transactionLayer == 0)
            throw new InvalidOperationException("No transaction to commit.");
        if (transaction is null && transactionLayer > 1)
            throw new Exception("An inner transaction has rolled back and this transaction can no longer be committed.");
        transactionLayer--;

        if (transaction is not null && transactionLayer == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (transaction is null)
            throw new InvalidOperationException("No transaction to rollback.");
        await transaction.RollbackAsync(cancellationToken);
        transaction = null;
    }
}
