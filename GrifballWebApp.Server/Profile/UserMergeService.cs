using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Server.Profile;

/// <summary>
/// Useful for when multiple accounts are mistakely made for the same person
/// </summary>
public interface IUserMergeService
{
    /// <summary>
    /// Useful for when multiple accounts are mistakely made for the same person
    /// </summary>
    /// <param name="mergeToId">The user to transfer owned entities to</param>
    /// <param name="mergeFromId">The user to take owned entities from, this account will be deleted</param>
    /// <param name="options">Options for the merge operation, such as whether to transfer Discord/Xbox IDs, team players, etc. Defaults to transfer all and delete merge from.</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task Merge(int mergeToId, int mergeFromId, MergeOptions? options = null, CancellationToken ct = default);
}

/// <inheritdoc/>
public class UserMergeService : IUserMergeService
{
    private readonly GrifballContext _context;
    public UserMergeService(GrifballContext context)
    {
        _context = context;
    }
    public async Task Merge(int mergeToId, int mergeFromId, MergeOptions? options = null, CancellationToken ct = default)
    {
        if (mergeToId == mergeFromId)
            throw new ArgumentException("Cannot merge a user into itself");

        options ??= new MergeOptions();

        // Look both users up before starting the transaction, so a missing user (for example a second click
        // after the first merge already deleted the source) fails cleanly without leaving a transaction open.
        var mergeTo = await _context.Users
            .Where(u => u.Id == mergeToId)
            .FirstOrDefaultAsync(ct) ?? throw new UserNotFoundException(mergeToId);

        var mergeFrom = await _context.Users
            .Where(u => u.Id == mergeFromId)
            .FirstOrDefaultAsync(ct) ?? throw new UserNotFoundException(mergeFromId);

        await _context.StartTransactionAsync(ct);

        // External logins (Auth.UserLogins) are what an external sign-in such as "Sign in with Discord" looks up.
        // They move in the same SaveChanges as the other transfers, before the source is deleted, so a failed
        // merge never loses them. The key is (LoginProvider, ProviderKey), so only UserId changes and the target
        // can hold any number of logins, including several for one provider.
        var logins = await _context.UserLogins.Where(l => l.UserId == mergeFromId).ToListAsync(ct);
        if (options.TransferLogins)
        {
            logins.ForEach(l => l.UserId = mergeToId);
        }

        if (options.TransferDiscord)
        {
            if (mergeFrom.DiscordUserID is not null)
            {
                mergeTo.DiscordUserID = mergeFrom.DiscordUserID;
                mergeFrom.DiscordUserID = null;
            }
            else if (mergeTo.DiscordUserID is null && options.TransferLogins)
            {
                // A login-only account (signed in with Discord, never linked by the bot) has no DiscordUserID.
                // Its Discord login key is the Discord user ID, so link the bot account from it, but only when the
                // target has no link of its own and that Discord user is known and not linked to anyone else.
                await LinkDiscordFromLogin(mergeTo, logins, ct);
            }
        }

        if (mergeFrom.XboxUserID is not null && options.TransferXbox)
        {
            mergeTo.XboxUserID = mergeFrom.XboxUserID;
            mergeFrom.XboxUserID = null;
        }

        if (options.TransferTeamPlayers)
        {
            var teamPlayers = await _context.TeamPlayers.Where(tp => tp.UserID == mergeFromId).ToListAsync(ct);
            teamPlayers.ForEach(tp => tp.UserID = mergeToId);
        }
        
        if (options.TransferExperiences)
        {
            var experience = await _context.UserExperiences.Where(x => x.UserID == mergeFromId).ToListAsync(ct);
            experience.ForEach(x => x.UserID = mergeToId);
        }
        
        if (options.TransferSignups)
        {
            var signups = await _context.SeasonSignups.Where(x => x.UserID == mergeFromId).ToListAsync(ct);
            signups.ForEach(x => x.UserID = mergeToId);
        }

        if (options.TransferSeasonStats)
        {
            var stats = await _context.PlayerSeasonStats.Where(x => x.UserID == mergeFromId).ToListAsync(ct);
            stats.ForEach(x => x.UserID = mergeToId);
        }

        if (options.TransferChampionships)
        {
            var championships = await _context.PlayerChampionships.Where(x => x.UserID == mergeFromId).ToListAsync(ct);
            championships.ForEach(x => x.UserID = mergeToId);
        }

        if (options.TransferAliases)
        {
            var aliases = await _context.PlayerAliases.Where(x => x.UserID == mergeFromId).ToListAsync(ct);
            foreach (var alias in aliases)
            {
                var existing = await _context.PlayerAliases.AnyAsync(a => a.UserID == mergeToId && a.NormalizedAlias == alias.NormalizedAlias, ct);
                if (!existing)
                {
                    alias.UserID = mergeToId;
                }
                else
                {
                    _context.PlayerAliases.Remove(alias);
                }
            }
        }

        // Delete just in case it doesnt cascade
        var rolesToDelete = await _context.UserRoles.Where(x => x.UserId == mergeFromId).ToArrayAsync(ct);
        _context.UserRoles.RemoveRange(rolesToDelete);
        //ExecuteDeleteAsync does not work in with in memory test
        //await _context.UserRoles.Where(x => x.UserId == existingUserID).ExecuteDeleteAsync(ct);

        // Transfer entities
        await _context.SaveChangesAsync(ct);

        // Delete user
        if (options.DeleteMergeFrom)
        {
            _context.Users.Remove(mergeFrom);
            await _context.SaveChangesAsync(ct);
        }

        await _context.CommitTransactionAsync(ct);
    }

    private async Task LinkDiscordFromLogin(User mergeTo, List<UserLogin> logins, CancellationToken ct)
    {
        var discordLogin = logins.FirstOrDefault(l => l.LoginProvider == DiscordProvider);
        if (discordLogin is null || !long.TryParse(discordLogin.ProviderKey, out var discordUserId))
            return;

        var known = await _context.DiscordUsers.AnyAsync(d => d.DiscordUserID == discordUserId, ct);
        var linkedElsewhere = await _context.Users.AnyAsync(u => u.DiscordUserID == discordUserId, ct);
        if (known && !linkedElsewhere)
            mergeTo.DiscordUserID = discordUserId;
    }

    private const string DiscordProvider = "Discord";
}

/// <summary>A user to merge does not exist, e.g. a second request after the first merge already deleted the source.</summary>
public class UserNotFoundException(int userId) : Exception("Did not find user " + userId)
{
    public int UserId { get; } = userId;
}

public class MergeOptions
{
    /// <summary>Moves the bot's Discord link (DiscordUserID) to the target. When the source has none, the target has none
    /// and <see cref="TransferLogins"/> is on, the link is taken from the source's Discord login if that Discord user is known.</summary>
    public bool TransferDiscord { get; set; } = true;
    /// <summary>Moves every external login (any provider, e.g. "Sign in with Discord") to the target, so it then signs in as the target.
    /// With this off and <see cref="DeleteMergeFrom"/> on, the source's logins are deleted with it.</summary>
    public bool TransferLogins { get; set; } = true;
    public bool TransferXbox { get; set; } = true;
    public bool TransferTeamPlayers { get; set; } = true;
    public bool TransferExperiences { get; set; } = true;
    public bool TransferSignups { get; set; } = true;
    public bool TransferSeasonStats { get; set; } = true;
    public bool TransferChampionships { get; set; } = true;
    public bool TransferAliases { get; set; } = true;
    public bool DeleteMergeFrom { get; set; } = true;
}
