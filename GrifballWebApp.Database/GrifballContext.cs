using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GrifballWebApp.Database;

public class GrifballContext :
    IdentityDbContext<User, Role, int, UserClaim, UserRole, UserLogin, IdentityRoleClaim<int>, IdentityUserToken<int>>,
    IDataProtectionKeyContext
{
    public GrifballContext() : base()
    {
    }

    public GrifballContext(DbContextOptions<GrifballContext> options) : base(options)
    {
    }

    // Only the aggregate roots have a DbSet: their parts are mapped through the roots' navigations and configurations,
    // and reached through the roots.
    public virtual DbSet<DiscordUser> DiscordUsers { get; set; }
    public virtual DbSet<DiscordMessage> DiscordMessages { get; set; }
    public virtual DbSet<AvailabilityOption> Availability { get; set; }
    public virtual DbSet<GameVersion> GameVersions { get; set; }
    public virtual DbSet<Match> Matches { get; set; }
    public virtual DbSet<RecordedGame> RecordedGames { get; set; }
    public virtual DbSet<Medal> Medals { get; set; }
    public virtual DbSet<MedalDifficulty> MedalDifficulties { get; set; }
    public virtual DbSet<MedalType> MedalTypes { get; set; }
    public virtual DbSet<Region> Regions { get; set; }
    public virtual DbSet<League> Leagues { get; set; }
    public virtual DbSet<Season> Seasons { get; set; }
    public virtual DbSet<PlayerSeasonStat> PlayerSeasonStats { get; set; }
    public virtual DbSet<GamertagClaim> GamertagClaims { get; set; }
    public virtual DbSet<HistoricalImportJob> HistoricalImportJobs { get; set; }
    public virtual DbSet<XuidLookup> XuidLookups { get; set; }
    public virtual DbSet<HistoricalSource> HistoricalSources { get; set; }
    public virtual DbSet<HistoricalImportSettings> HistoricalImportSettings { get; set; }
    public virtual DbSet<UserMerge> UserMerges { get; set; }
    public virtual DbSet<DuplicateDismissal> DuplicateDismissals { get; set; }
    public virtual DbSet<Vod> Vods { get; set; }
    public virtual DbSet<SeasonMatch> SeasonMatches { get; set; }
    public virtual DbSet<SeasonSignup> SeasonSignups { get; set; }
    public virtual DbSet<Team> Teams { get; set; }
    public virtual DbSet<XboxUser> XboxUsers { get; set; }
    public virtual DbSet<Rank> Ranks { get; set; }
    public virtual DbSet<QueuedPlayer> QueuedPlayer { get; set; }
    public virtual DbSet<MatchedMatch> MatchedMatches { get; set; }
    public virtual DbSet<MatchmakingSeason> MatchmakingSeasons { get; set; }
    public virtual DbSet<MatchmakingPlayerStat> MatchmakingPlayerStats { get; set; }
    public virtual DbSet<MatchReschedule> MatchReschedules { get; set; }
    public virtual DbSet<PasswordResetLink> PasswordResetLinks { get; set; }
    public virtual DbSet<ScheduleRecommendation> ScheduleRecommendations { get; set; }
    public virtual DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
    public virtual DbSet<OutboxMessage> Outbox { get; set; }

    public const string CaseInsensitiveCollation = "case_insensitive";

    /// <summary>SQLite's built-in case-insensitive collation.</summary>
    public const string SqliteCaseInsensitiveCollation = "NOCASE";

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // DateTimes are UTC (UtcDateTimeConverter). On Postgres they are timestamptz, Npgsql's default for
        // DateTime, which holds an instant rather than a bare date and time.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();

        // Postgres: text compared and sorted as SQL Server's default collation (SQL_Latin1_General_CP1_CI_AS)
        // does: case-insensitive, accent-sensitive ("Noah" = "noah", "Noé" <> "noe"), sorted as people read it
        // (alpha, Bravo, charlie) rather than by the server's locale (on Alpine, byte order). The app relies
        // on it: gamertags, team names and searches match whatever the case. Nothing secret depends on case
        // in a query: passwords and tokens are checked in .NET, and reset links are hex. The collation is
        // created in OnModelCreating.
        if (Database.IsNpgsql())
        {
            configurationBuilder.Properties<string>().UseCollation(CaseInsensitiveCollation);
        }

        // SQLite: text compared as SQL Server's collation does, case-insensitive, by its own NOCASE (which folds
        // ASCII letters only: "É" and "é" differ). It stores TimeSpan and decimal as text, which it can't sum,
        // sort or compare in a query, so it gets them as numbers: a TimeSpan as its ticks, a decimal as a double.
        if (Database.IsSqlite())
        {
            configurationBuilder.Properties<string>().UseCollation(SqliteCaseInsensitiveCollation);
            configurationBuilder.Properties<TimeSpan>().HaveConversion<TimeSpanToTicksConverter>();
            configurationBuilder.Properties<decimal>().HaveConversion<double>();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ICU's root locale, ignoring case only (strength level 2). Nondeterministic: strings that differ only
        // in case compare equal. PostgreSQL 18 allows LIKE and strpos (EF's Contains) on such a collation.
        if (Database.IsNpgsql())
            modelBuilder.HasCollation(CaseInsensitiveCollation, locale: "und-u-ks-level2", provider: "icu", deterministic: false);

        modelBuilder.ApplyConfiguration(new DiscordUserConfiguration());
        modelBuilder.ApplyConfiguration(new DiscordMessageConfiguration());
        modelBuilder.ApplyConfiguration(new AvailabilityOptionConfiguration());
        modelBuilder.ApplyConfiguration(new GameVersionConfiguration());
        modelBuilder.ApplyConfiguration(new MatchConfiguration());
        modelBuilder.ApplyConfiguration(new MatchBracketInfoConfiguration());
        modelBuilder.ApplyConfiguration(new SeriesGameConfiguration());
        // A game is in one series at most: unique where set, in each database's own quoting
        modelBuilder.Entity<SeriesGame>().HasIndex(e => e.MatchID).HasFilter(Database.IsNpgsql() ? "\"MatchID\" IS NOT NULL" : "[MatchID] IS NOT NULL");
        modelBuilder.Entity<SeriesGame>().HasIndex(e => e.RecordedGameID).HasFilter(Database.IsNpgsql() ? "\"RecordedGameID\" IS NOT NULL" : "[RecordedGameID] IS NOT NULL");
        // A match has one pending reschedule request at most: its active one
        modelBuilder.Entity<MatchReschedule>().HasIndex(e => e.SeasonMatchID).IsUnique()
            .HasFilter(Database.IsNpgsql() ? "\"Status\" = 0" : "[Status] = 0");
        modelBuilder.ApplyConfiguration(new RecordedGameConfiguration());
        modelBuilder.ApplyConfiguration(new RecordedGameTeamConfiguration());
        modelBuilder.ApplyConfiguration(new RecordedGameParticipantConfiguration());
        modelBuilder.ApplyConfiguration(new MatchParticipantConfiguration());
        modelBuilder.ApplyConfiguration(new MatchTeamConfiguration());
        modelBuilder.ApplyConfiguration(new MedalConfiguration());
        modelBuilder.ApplyConfiguration(new MedalDifficultyConfiguration());
        modelBuilder.ApplyConfiguration(new MedalEarnedConfiguration());
        modelBuilder.ApplyConfiguration(new MedalTypeConfiguration());
        modelBuilder.ApplyConfiguration(new RegionConfiguration());
        modelBuilder.ApplyConfiguration(new LeagueConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new SeasonConfiguration());
        // A historical season's archive key is unique where set. Written out for both databases in their own quoting: SQL
        // Server needs the filter (its unique indexes count NULLs as equal), Postgres gets the same partial index.
        modelBuilder.Entity<Season>().HasIndex(e => e.HistoricalSourceID)
            .HasFilter(Database.IsNpgsql() ? "\"HistoricalSourceID\" IS NOT NULL" : "[HistoricalSourceID] IS NOT NULL");
        modelBuilder.ApplyConfiguration(new PlayerSeasonStatConfiguration());
        modelBuilder.ApplyConfiguration(new PlayerChampionshipConfiguration());
        modelBuilder.ApplyConfiguration(new XboxGamertagHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new GamertagClaimConfiguration());
        // Claims about an Xbox account are one per kind, account and gamertag; claims about a user (a user's own, a Discord
        // match) one per kind, user and gamertag
        modelBuilder.Entity<GamertagClaim>().HasIndex(e => new { e.Kind, e.XboxUserID, e.NormalizedGamertag }).IsUnique()
            .HasFilter(Database.IsNpgsql() ? "\"Status\" IN (1, 2) AND \"ClaimantUserID\" IS NULL" : "[Status] IN (1, 2) AND [ClaimantUserID] IS NULL");
        modelBuilder.Entity<GamertagClaim>().HasIndex(e => new { e.Kind, e.ClaimantUserID, e.NormalizedGamertag }).IsUnique()
            .HasFilter(Database.IsNpgsql() ? "\"Status\" IN (1, 2) AND \"ClaimantUserID\" IS NOT NULL" : "[Status] IN (1, 2) AND [ClaimantUserID] IS NOT NULL");
        modelBuilder.ApplyConfiguration(new HistoricalImportJobConfiguration());
        modelBuilder.ApplyConfiguration(new XuidLookupConfiguration());
        modelBuilder.ApplyConfiguration(new HistoricalSourceConfiguration());
        modelBuilder.ApplyConfiguration(new HistoricalSourceSnapshotConfiguration());
        modelBuilder.ApplyConfiguration(new HistoricalImportSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new HistoricalSeasonReviewConfiguration());
        modelBuilder.ApplyConfiguration(new UserMergeConfiguration());
        modelBuilder.ApplyConfiguration(new DuplicateDismissalConfiguration());
        modelBuilder.ApplyConfiguration(new VodConfiguration());
        modelBuilder.ApplyConfiguration(new VodTeamConfiguration());
        modelBuilder.ApplyConfiguration(new SeasonAvailabilityConfiguration());
        modelBuilder.ApplyConfiguration(new SeasonMatchConfiguration());
        modelBuilder.ApplyConfiguration(new SeasonSignupConfiguration());
        modelBuilder.ApplyConfiguration(new SignupAvailabilityConfiguration());
        modelBuilder.ApplyConfiguration(new TeamConfiguration());
        modelBuilder.ApplyConfiguration(new TeamAvailabilityConfiguration());
        modelBuilder.ApplyConfiguration(new TeamPlayerConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new UserExperienceConfiguration());
        modelBuilder.ApplyConfiguration(new UserRoleConfiguration());
        modelBuilder.ApplyConfiguration(new UserLoginConfiguration());
        modelBuilder.ApplyConfiguration(new UserClaimConfiguration());
        modelBuilder.ApplyConfiguration(new XboxUserConfiguration());
        modelBuilder.ApplyConfiguration(new RankConfiguration());
        modelBuilder.ApplyConfiguration(new QueuedPlayerConfiguration());
        modelBuilder.ApplyConfiguration(new MatchedPlayerConfiguration());
        modelBuilder.ApplyConfiguration(new MatchedTeamConfiguration());
        modelBuilder.ApplyConfiguration(new MatchedMatchConfiguration());
        modelBuilder.ApplyConfiguration(new MatchmakingSeasonConfiguration());
        modelBuilder.ApplyConfiguration(new MatchmakingPlayerStatConfiguration());
        modelBuilder.ApplyConfiguration(new MatchedWinnerVoteConfiguration());
        modelBuilder.ApplyConfiguration(new MatchedKickVoteConfiguration());
        modelBuilder.ApplyConfiguration(new MatchRescheduleConfiguration());
        modelBuilder.ApplyConfiguration(new PasswordResetLinkConfiguration());
        modelBuilder.ApplyConfiguration(new ScheduleRecommendationConfiguration());
        modelBuilder.ApplyConfiguration(new ScheduleRecommendationMatchConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        modelBuilder.Entity<IdentityRoleClaim<int>>(b => b.ToTable("RoleClaims", "Auth", tb => tb.IsTemporal()));
        modelBuilder.Entity<IdentityUserToken<int>>(b =>
        {
            b.ToTable("UserTokens", "Auth", tb => tb.IsTemporal());
            b.HasKey(e => new { e.UserId, e.LoginProvider, e.Name });
        });

        modelBuilder.Entity<DataProtectionKey>(b => b.ToTable("DataProtectionKeys", "Auth", tb => tb.IsTemporal()));

        // Every table is temporal (SQL Server keeps a history of each change). Postgres has no temporal
        // tables, nor has SQLite: their providers ignore the setting, which is dropped here so their migrations
        // don't carry it. Triggers keep the same history there instead, of the same tables (PostgresHistory,
        // SqliteHistory).
        var temporal = modelBuilder.Model.GetEntityTypes().Where(e => e.IsTemporal()).ToList();
        if (!Database.IsSqlServer())
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                entityType.RemoveAnnotation("SqlServer:IsTemporal");
        }

        if (Database.IsNpgsql())
        {
            QuoteCheckConstraintColumns(modelBuilder);
            modelBuilder.AddHistoryTables(temporal);
        }

        if (Database.IsSqlite())
            modelBuilder.AddSqliteHistoryTables(temporal);
    }

    /// <summary>
    /// Check constraints are written with bare column names, as SQL Server takes them. Postgres folds bare
    /// names to lower case, and the columns are mixed case, so on Postgres each column name in them is
    /// quoted.
    /// </summary>
    private static void QuoteCheckConstraintColumns(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var columns = entityType.GetProperties().Select(p => p.GetColumnName()).ToArray();
            foreach (var check in entityType.GetCheckConstraints().ToList())
            {
                var sql = check.Sql;
                foreach (var column in columns)
                    sql = System.Text.RegularExpressions.Regex.Replace(sql, $@"(?<![""\w]){column}(?![""\w])", $@"""{column}""");
                entityType.RemoveCheckConstraint(check.ModelName);
                entityType.AddCheckConstraint(check.ModelName, sql).Name = check.Name;
            }
        }
    }

    private IDbContextTransaction? transaction;
    private byte transactionLayer;

    public virtual async Task StartTransactionAsync(CancellationToken cancellationToken = default)
    {
        PreventRollover();
        transactionLayer++;

        if (transactionLayer == 1 && transaction is null)
        {
            transaction = await Database.BeginTransactionAsync(cancellationToken);
        }
    }

    private void PreventRollover()
    {
        if (transactionLayer == byte.MaxValue)
        {
            throw new Exception("There is a maximum of 255 transaction layers and somehow you have managed to use them all. Frankly, I am impressed and disgusted.");
        }
    }

    public virtual async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        PreventNoTransactionOnCommit();
        PreventOuterCommitOnInnerRollback();
        transactionLayer--;

        if (transaction is not null && transactionLayer == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            transaction = null;
        }
    }

    private void PreventNoTransactionOnCommit()
    {
        if (transaction is null && transactionLayer == 0)
        {
            throw new InvalidOperationException("No transaction to commit.");
        }
    }

    private void PreventOuterCommitOnInnerRollback()
    {
        if (transaction is null && transactionLayer > 1)
        {
            throw new Exception("An inner transaction has rolled back and this transaction can no longer be committed.");
        }
    }

    public virtual async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        PreventNoTransactionOnRollback();
        await transaction!.RollbackAsync(cancellationToken);
        transaction = null;
    }

    private void PreventNoTransactionOnRollback()
    {
        if (transaction is null)
        {
            throw new InvalidOperationException("No transaction to rollback.");
        }
    }

    /// <summary>The games of every season match's series, to read (a series game is changed only through its match).</summary>
    public IQueryable<SeriesGame> SeasonSeriesGames => SeasonMatches.SelectMany(m => m.SeriesGames);

    /// <summary>The players' lines of every recorded game, to read (a line is made and changed only through its game).</summary>
    public IQueryable<RecordedGameParticipant> RecordedGameLines => RecordedGames.SelectMany(g => g.Participants);

    /// <summary>Deletes a recorded game with its sides and players' lines (all loaded).</summary>
    public void RemoveRecordedGame(RecordedGame game)
    {
        RemoveRange(game.Participants);
        RemoveRange(game.Teams);
        RecordedGames.Remove(game);
    }

    /// <summary>Deletes a game taken out of its series: the match takes it out first (<see cref="SeasonMatch.RemoveGame"/>,
    /// which refuses a decided series).</summary>
    public void RemoveSeriesGame(SeasonMatch match, SeriesGame game)
    {
        match.RemoveGame(game);
        Remove(game);
    }

    /// <summary>Deletes all of a series' games: the match takes them out first (<see cref="SeasonMatch.ClearSeries"/>).
    /// Returns them, for the caller to free or delete what they point to.</summary>
    public IReadOnlyList<SeriesGame> ClearSeries(SeasonMatch match)
    {
        var games = match.ClearSeries();
        RemoveRange(games);
        return games;
    }

    /// <summary>Deletes a game a re-import no longer has in its imported series, which the match works out again first
    /// (<see cref="SeasonMatch.RemoveImportedGame"/>). Returns whether the series has games left.</summary>
    public bool RemoveImportedSeriesGame(SeasonMatch match, SeriesGame game, bool homeWon, bool awayWon)
    {
        var left = match.RemoveImportedGame(game, homeWon, awayWon);
        Remove(game);
        return left;
    }

    /// <summary>The teams of every Halo Infinite match, to read (a team is made only through its match).</summary>
    public IQueryable<MatchTeam> HaloMatchTeams => Matches.SelectMany(m => m.MatchTeams);

    /// <summary>The players of every Halo Infinite match, to read (a player is made only through their match).</summary>
    public IQueryable<MatchParticipant> HaloMatchParticipants => HaloMatchTeams.SelectMany(t => t.MatchParticipants);
}
