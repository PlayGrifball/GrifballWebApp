using GrifballWebApp.Database.Models;
using Microsoft.AspNetCore.Identity;

namespace GrifballWebApp.Server.Preview;

/// <summary>
/// The accounts the post-deploy smoke suite (<c>grifballwebapp.client/smoke</c>) signs in with on a PR preview.
/// Only in preview mode, and only when <see cref="PasswordKey"/> is set, each of <see cref="UserNames"/> is made to
/// exist at startup with that password, the <see cref="Roles"/> and no lockout. Running it again (every pod start)
/// changes nothing but what drifted, so a rotated password or a smoke run that locked an account out is put right by a
/// restart. Outside preview mode it does nothing, whatever the configuration says.
/// </summary>
public static class PreviewSmokeUsers
{
    public const string PasswordKey = "Preview:SmokeUserPassword";

    /// <summary>Two accounts, so the team builder can be driven from two browser sessions as two different users.</summary>
    public static readonly IReadOnlyList<string> UserNames = ["smoke-test", "smoke-test-2"];

    public static readonly IReadOnlyList<string> Roles = ["Sysadmin", "Commissioner"];

    /// <summary>
    /// Makes sure the smoke users exist as described above. Returns how many were created or changed, or <c>null</c>
    /// when it did nothing (preview mode off, or no password configured). A user that can't be set up (a password the
    /// identity rules refuse, say) is logged as an error and skipped: the preview itself still starts.
    /// </summary>
    public static async Task<int?> EnsureAsync(IConfiguration configuration, UserManager<User> userManager, ILogger logger)
    {
        if (!PreviewMode.IsEnabled(configuration))
            return null;

        var password = configuration[PasswordKey];
        if (string.IsNullOrEmpty(password))
        {
            logger.LogInformation("{Key} is not set: no smoke-test users", PasswordKey);
            return null;
        }

        var changed = 0;
        foreach (var userName in UserNames)
        {
            try
            {
                if (await EnsureUserAsync(userManager, userName, password, logger))
                    changed++;
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError(ex, "Preview smoke-test user {UserName} is not ready", userName);
            }
        }

        logger.LogInformation("Preview smoke-test users ready ({Changed} created or updated)", changed);
        return changed;
    }

    private static async Task<bool> EnsureUserAsync(UserManager<User> userManager, string userName, string password, ILogger logger)
    {
        var changed = false;
        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            user = new User { UserName = userName, DisplayName = userName };
            Check(await userManager.CreateAsync(user, password), "create", userName);
            changed = true;
        }
        else if (!await userManager.CheckPasswordAsync(user, password))
        {
            // The secret was rotated (or the account had no password): set the configured one.
            if (await userManager.HasPasswordAsync(user))
                Check(await userManager.RemovePasswordAsync(user), "remove the old password of", userName);
            Check(await userManager.AddPasswordAsync(user, password), "set the password of", userName);
            changed = true;
        }

        var missingRoles = new List<string>();
        foreach (var role in Roles)
        {
            if (!await userManager.IsInRoleAsync(user, role))
                missingRoles.Add(role);
        }
        if (missingRoles.Count > 0)
        {
            Check(await userManager.AddToRolesAsync(user, missingRoles), "add roles to", userName);
            changed = true;
        }

        // A smoke run with a wrong password (login locks out on failure) must not keep the account out until the
        // lockout expires.
        if (user.LockoutEnd is not null || user.AccessFailedCount != 0)
        {
            Check(await userManager.SetLockoutEndDateAsync(user, null), "unlock", userName);
            Check(await userManager.ResetAccessFailedCountAsync(user), "reset failed logins of", userName);
            changed = true;
        }

        if (changed)
            logger.LogInformation("Preview smoke-test user {UserName} created or updated", userName);
        return changed;
    }

    private static void Check(IdentityResult result, string action, string userName)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Could not {action} preview smoke-test user {userName}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
    }
}
