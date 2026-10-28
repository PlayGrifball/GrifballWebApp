using GrifballWebApp.Database.Models;
using Microsoft.AspNetCore.Identity;

namespace GrifballWebApp.Server.Identity;

/// <summary>Lists, links and unlinks a user's external logins (Auth.UserLogins), the rows an external sign-in looks up.</summary>
public interface IExternalLoginService
{
    /// <returns>The user's logins and whether they have a password, or null when the user does not exist.</returns>
    Task<LinkedLoginsDto?> GetLogins(int userId);

    /// <summary>Adds an external login to the user. Refuses one that already belongs to another user.</summary>
    Task<LinkLoginResult> Link(int userId, UserLoginInfo info);

    /// <summary>Removes one of the user's external logins, unless it is their last way to sign in.</summary>
    Task<UnlinkLoginResult> Unlink(int userId, string loginProvider, string providerKey);
}

public enum LinkLoginResult
{
    Linked,
    AlreadyLinked,
    LinkedToAnotherUser,
    UserNotFound,
    Failed,
}

public enum UnlinkLoginResult
{
    Removed,
    LoginNotFound,
    LastSignInMethod,
    UserNotFound,
    Failed,
}

public class ExternalLoginDto
{
    public required string LoginProvider { get; set; }
    public required string ProviderKey { get; set; }
    public required string ProviderDisplayName { get; set; }
}

public class ExternalLoginKeyDto
{
    public required string LoginProvider { get; set; }
    public required string ProviderKey { get; set; }
}

public class ExternalProviderDto
{
    public required string Name { get; set; }
    public required string DisplayName { get; set; }
}

public class LinkedLoginsDto
{
    public required bool HasPassword { get; set; }
    public required List<ExternalLoginDto> Logins { get; set; }
    /// <summary>The external providers this server is configured for. Filled in by the controller.</summary>
    public List<ExternalProviderDto> Providers { get; set; } = [];
}

/// <inheritdoc/>
public class ExternalLoginService : IExternalLoginService
{
    private readonly UserManager<User> _userManager;

    public ExternalLoginService(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task<LinkedLoginsDto?> GetLogins(int userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return null;

        var logins = await _userManager.GetLoginsAsync(user);
        return new LinkedLoginsDto
        {
            HasPassword = await _userManager.HasPasswordAsync(user),
            Logins = logins
                .OrderBy(l => l.LoginProvider).ThenBy(l => l.ProviderKey)
                .Select(l => new ExternalLoginDto
                {
                    LoginProvider = l.LoginProvider,
                    ProviderKey = l.ProviderKey,
                    ProviderDisplayName = l.ProviderDisplayName ?? l.LoginProvider,
                })
                .ToList(),
        };
    }

    public async Task<LinkLoginResult> Link(int userId, UserLoginInfo info)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return LinkLoginResult.UserNotFound;

        var owner = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (owner is not null)
            return owner.Id == user.Id ? LinkLoginResult.AlreadyLinked : LinkLoginResult.LinkedToAnotherUser;

        var result = await _userManager.AddLoginAsync(user, info);
        return result.Succeeded ? LinkLoginResult.Linked : LinkLoginResult.Failed;
    }

    public async Task<UnlinkLoginResult> Unlink(int userId, string loginProvider, string providerKey)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return UnlinkLoginResult.UserNotFound;

        var logins = await _userManager.GetLoginsAsync(user);
        if (!logins.Any(l => l.LoginProvider == loginProvider && l.ProviderKey == providerKey))
            return UnlinkLoginResult.LoginNotFound;

        // Never remove the last way to sign in: without a password, at least one other login must remain.
        if (logins.Count <= 1 && !await _userManager.HasPasswordAsync(user))
            return UnlinkLoginResult.LastSignInMethod;

        var result = await _userManager.RemoveLoginAsync(user, loginProvider, providerKey);
        return result.Succeeded ? UnlinkLoginResult.Removed : UnlinkLoginResult.Failed;
    }
}
