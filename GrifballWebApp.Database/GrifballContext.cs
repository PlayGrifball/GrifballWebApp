using GrifballWebApp.Database.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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

    public virtual DbSet<DiscordUser> DiscordUsers { get; set; }
    public virtual DbSet<DiscordMessage> DiscordMessages { get; set; }
    public virtual DbSet<AvailabilityOption> Availability { get; set; }
    public virtual DbSet<GameVersion> GameVersions { get; set; }
    public virtual DbSet<Match> Matches { get; set; }
    public virtual DbSet<MatchBracketInfo> MatchBracketInfo { get; set; }
    public virtual DbSet<MatchLink> MatchLinks { get; set; }
    public virtual DbSet<MatchParticipant> MatchParticipants { get; set; }
    public virtual DbSet<MatchTeam> MatchTeams { get; set; }
    public virtual DbSet<Medal> Medals { get; set; }
    public virtual DbSet<MedalDifficulty> MedalDifficulties { get; set; }
    public virtual DbSet<MedalEarned> MedalEarned { get; set; }
    public virtual DbSet<MedalType> MedalTypes { get; set; }
    public virtual DbSet<Region> Regions { get; set; }
    public virtual DbSet<Season> Seasons { get; set; }
    public virtual DbSet<SeasonAvailability> SeasonAvailability { get; set; }
    public virtual DbSet<SeasonMatch> SeasonMatches { get; set; }
    public virtual DbSet<SeasonSignup> SeasonSignups { get; set; }
    public virtual DbSet<SignupAvailability> SignupAvailability { get; set; }
    public virtual DbSet<Team> Teams { get; set; }
    public virtual DbSet<TeamAvailability> TeamAvailability { get; set; }
    public virtual DbSet<TeamPlayer> TeamPlayers { get; set; }
    public virtual DbSet<UserExperience> UserExperiences { get; set; }
    public virtual DbSet<XboxUser> XboxUsers { get; set; }
    public virtual DbSet<Rank> Ranks { get; set; }
    public virtual DbSet<QueuedPlayer> QueuedPlayer { get; set; }
    public virtual DbSet<MatchedPlayer> MatchedPlayers { get; set; }
    public virtual DbSet<MatchedTeam> MatchedTeams { get; set; }
    public virtual DbSet<MatchedMatch> MatchedMatches { get; set; }
    public virtual DbSet<MatchedWinnerVote> MatchedWinnerVotes { get; set; }
    public virtual DbSet<MatchedKickVote> MatchedKickVotes { get; set; }
    public virtual DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new Configuration.DiscordUserConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.DiscordMessageConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.AvailabilityOptionConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.GameVersionConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchBracketInfoConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchLinkConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchParticipantConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchTeamConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MedalConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MedalDifficultyConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MedalEarnedConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MedalTypeConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.RegionConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.RoleConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.SeasonConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.SeasonAvailabilityConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.SeasonMatchConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.SeasonSignupConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.SignupAvailabilityConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.TeamConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.TeamAvailabilityConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.TeamPlayerConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.UserConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.UserExperienceConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.UserRoleConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.UserLoginConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.UserClaimConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.XboxUserConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.RankConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.QueuedPlayerConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchedPlayerConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchedTeamConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchedMatchConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchedWinnerVoteConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchedKickVoteConfiguration());

        modelBuilder.Entity<IdentityRoleClaim<int>>(b => b.ToTable("RoleClaims", "Auth", tb => tb.IsTemporal()));
        modelBuilder.Entity<IdentityUserToken<int>>(b =>
        {
            b.ToTable("UserTokens", "Auth", tb => tb.IsTemporal());
            b.HasKey(e => new { e.UserId, e.LoginProvider, e.Name });
        });

        modelBuilder.Entity<DataProtectionKey>(b => b.ToTable("DataProtectionKeys", "Auth", tb => tb.IsTemporal()));
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
}
