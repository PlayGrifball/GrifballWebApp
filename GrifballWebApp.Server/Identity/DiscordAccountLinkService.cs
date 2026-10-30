using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Identity;

/// <summary>
/// Ties "Sign in with Discord" to the bot's Discord link (<see cref="User.DiscordUserID"/>, a <see cref="DiscordUser"/> row).
/// The two record the same Discord account: the login's provider key is the Discord user id the bot knows the player by.
/// </summary>
public interface IDiscordAccountLinkService
{
    /// <summary>The user the bot has already linked to this Discord login's id, or null (also for another provider).</summary>
    Task<User?> FindBotLinkedUserAsync(string loginProvider, string providerKey, CancellationToken ct = default);

    /// <summary>
    /// Links the user to the Discord account of this login: sets <see cref="User.DiscordUserID"/>, creating the
    /// <see cref="DiscordUser"/> row when the bot has not seen this id yet, and refreshes its username from the sign-in.
    /// Never moves a link: a user already linked to another Discord id, or an id already linked to another user, is left alone.
    /// Throws when the save fails (e.g. a concurrent sign-in created the same row); see <see cref="TryLinkAsync"/>.
    /// </summary>
    Task<DiscordLinkResult> LinkAsync(User user, string loginProvider, string providerKey, string? discordUsername, CancellationToken ct = default);

    /// <summary><see cref="LinkAsync"/> for a backfill that must not stop a sign-in: a failed save is logged and undone.</summary>
    Task<DiscordLinkResult> TryLinkAsync(User user, string loginProvider, string providerKey, string? discordUsername, CancellationToken ct = default);

    /// <summary>Runs <paramref name="work"/> in one database transaction: committed when it returns true, rolled back otherwise.</summary>
    Task<bool> InTransactionAsync(Func<Task<bool>> work, CancellationToken ct = default);
}

public enum DiscordLinkResult
{
    /// <summary>The login is not a Discord login with a numeric id.</summary>
    NotDiscord,
    Linked,
    /// <summary>The user was already linked to this id (its username may have been refreshed).</summary>
    AlreadyLinked,
    /// <summary>The user is linked to a different Discord id; nothing changed.</summary>
    UserLinkedToOtherDiscord,
    /// <summary>Another user holds this Discord id; nothing changed.</summary>
    DiscordLinkedToAnotherUser,
    /// <summary>The save failed (<see cref="IDiscordAccountLinkService.TryLinkAsync"/> only); nothing changed.</summary>
    Failed,
}

/// <inheritdoc/>
public class DiscordAccountLinkService : IDiscordAccountLinkService
{
    public const string DiscordProvider = "Discord";

    private readonly GrifballContext _context;
    private readonly ILogger<DiscordAccountLinkService> _logger;

    public DiscordAccountLinkService(GrifballContext context, ILogger<DiscordAccountLinkService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public static bool TryGetDiscordUserId(string loginProvider, string providerKey, out long discordUserId)
    {
        discordUserId = 0;
        return loginProvider == DiscordProvider && long.TryParse(providerKey, out discordUserId);
    }

    public async Task<User?> FindBotLinkedUserAsync(string loginProvider, string providerKey, CancellationToken ct = default)
    {
        if (!TryGetDiscordUserId(loginProvider, providerKey, out var discordUserId))
            return null;
        return await _context.Users.FirstOrDefaultAsync(u => u.DiscordUserID == discordUserId, ct);
    }

    public async Task<DiscordLinkResult> LinkAsync(User user, string loginProvider, string providerKey, string? discordUsername, CancellationToken ct = default)
    {
        if (!TryGetDiscordUserId(loginProvider, providerKey, out var discordUserId))
            return DiscordLinkResult.NotDiscord;

        if (user.DiscordUserID is long current && current != discordUserId)
            return DiscordLinkResult.UserLinkedToOtherDiscord;

        var alreadyLinked = user.DiscordUserID == discordUserId;
        if (!alreadyLinked && await _context.Users.AnyAsync(u => u.DiscordUserID == discordUserId && u.Id != user.Id, ct))
            return DiscordLinkResult.DiscordLinkedToAnotherUser;

        // Like the bot (MessageCreateHandler, DiscordSetGamertag): the Discord username, which the sign-in's name claim carries.
        var discordUser = await _context.DiscordUsers.FirstOrDefaultAsync(d => d.DiscordUserID == discordUserId, ct);
        if (discordUser is null)
        {
            _context.DiscordUsers.Add(new DiscordUser
            {
                DiscordUserID = discordUserId,
                DiscordUsername = string.IsNullOrEmpty(discordUsername) ? discordUserId.ToString() : discordUsername,
            });
        }
        else if (!string.IsNullOrEmpty(discordUsername) && discordUser.DiscordUsername != discordUsername)
        {
            // The bot only adds rows, never renames them; a sign-in is a fresh word from Discord.
            discordUser.DiscordUsername = discordUsername;
        }

        user.DiscordUserID = discordUserId;
        await _context.SaveChangesAsync(ct);
        return alreadyLinked ? DiscordLinkResult.AlreadyLinked : DiscordLinkResult.Linked;
    }

    public async Task<DiscordLinkResult> TryLinkAsync(User user, string loginProvider, string providerKey, string? discordUsername, CancellationToken ct = default)
    {
        try
        {
            return await LinkAsync(user, loginProvider, providerKey, discordUsername, ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Could not link user {UserId} to Discord user {DiscordUserId}", user.Id, providerKey);
            // Undo the pending changes so the next save (e.g. the sign-in itself) does not retry them.
            foreach (var entry in _context.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified).ToList())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.State = EntityState.Detached;
                }
                else
                {
                    entry.CurrentValues.SetValues(entry.OriginalValues);
                    entry.State = EntityState.Unchanged;
                }
            }
            return DiscordLinkResult.Failed;
        }
    }

    public async Task<bool> InTransactionAsync(Func<Task<bool>> work, CancellationToken ct = default)
    {
        await _context.StartTransactionAsync(ct);
        bool ok;
        try
        {
            ok = await work();
        }
        catch
        {
            await _context.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }

        if (ok)
            await _context.CommitTransactionAsync(ct);
        else
            await _context.RollbackTransactionAsync(ct);
        return ok;
    }
}
