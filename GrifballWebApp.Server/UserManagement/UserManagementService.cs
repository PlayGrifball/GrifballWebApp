using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.QueryableExtensions;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.Sorting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Surprenant.Grunt.Core;
using System.Security.Cryptography;
using System.Text;

namespace GrifballWebApp.Server.UserManagement;

public class UserResponseDto
{
    public int UserID { get; set; }
    public string UserName { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public bool LockoutEnabled { get; set; }
    public bool IsDummyUser { get; set; }
    public int AccessFailedCount { get; set; }
    public required string? Region { get; set; }
    public required string? DisplayName { get; set; }
    public required string? Gamertag { get; set; }
    public required string? Discord { get; set; }
    public required int ExternalAuthCount { get; set; }
    public List<RoleDto> Roles { get; set; }
}

public class RoleDto
{
    public string RoleName { get; set; }
    public bool HasRole { get; set; }
}

public interface IUserManagementService
{
    Task<PaginationResult<UserResponseDto>> GetUsers(PaginationFilter filter, string search, CancellationToken ct);
    Task<UserResponseDto?> GetUser(int userID, CancellationToken ct);
    Task<string?> CreateUser(CreateUserRequestDto createUserRequest, CancellationToken ct);
    Task<string?> EditUser(UserResponseDto editUser, CancellationToken ct);
    Task<(bool Success, string Message, string? Token, DateTime? ExpiresAt)> GeneratePasswordResetLink(string username, int createdByUserId, CancellationToken ct);
    Task<(bool Success, string Message)> UsePasswordResetLink(string token, string newPassword, CancellationToken ct);
    Task CleanupExpiredPasswordResetLinks(CancellationToken ct);
}

public class UserManagementService : IUserManagementService
{
    private readonly GrifballContext _context;
    private readonly UserManager<User> _userManager;
    private readonly IGetsertXboxUserService _getsertXboxUserService;
    public UserManagementService(GrifballContext grifballContext, UserManager<User> userManager, IGetsertXboxUserService getsertXboxUserService)
    {
        _context = grifballContext;
        _userManager = userManager;
        _getsertXboxUserService = getsertXboxUserService;
    }

    public async Task<PaginationResult<UserResponseDto>> GetUsers(PaginationFilter filter, string search, CancellationToken ct)
    {
        var query = _context.Users
            .AsNoTracking().AsSplitQuery()
            .Select(user => new UserResponseDto()
            {
                UserID = user.Id,
                UserName = user.UserName,
                LockoutEnd = user.LockoutEnd,
                LockoutEnabled = user.LockoutEnabled,
                IsDummyUser = user.IsDummyUser,
                AccessFailedCount = user.AccessFailedCount,
                Region = user.Region.RegionName,
                DisplayName = user.DisplayName,
                Gamertag = user.XboxUser.Gamertag,
                Discord = user.DiscordUser.DiscordUsername,
                ExternalAuthCount = user.UserLogins.Count,
                Roles = _context.Roles.AsSplitQuery().AsNoTracking().Select(r => new RoleDto()
                {
                    RoleName = r.Name,
                    HasRole = r.UserRoles.Any(ur => ur.UserId == user.Id),
                }).ToList(),
            });

        if (search != string.Empty)
        {
            query = query.Where(x => x.UserName.Contains(search) || x.DisplayName!.Contains(search) || x.Gamertag!.Contains(search));
        }

        if (filter.SortColumn is null)
        {
            query = query.OrderByDescending(x => x.UserID);
        }
        else
        {
            query = query.OrderBy(filter);
        }

        return await query.PaginationResult(filter, ct);
    }

    public Task<UserResponseDto?> GetUser(int userID, CancellationToken ct)
    {
        return _context.Users
            .AsNoTracking().AsSplitQuery()
            .Where(x => x.Id == userID)
            .Select(user => new UserResponseDto()
            {
                UserID = user.Id,
                UserName = user.UserName,
                LockoutEnd = user.LockoutEnd,
                LockoutEnabled = user.LockoutEnabled,
                IsDummyUser = user.IsDummyUser,
                AccessFailedCount = user.AccessFailedCount,
                Region = user.Region.RegionName,
                DisplayName = user.DisplayName,
                Gamertag = user.XboxUser.Gamertag,
                Discord = user.DiscordUser.DiscordUsername,
                ExternalAuthCount = user.UserLogins.Count,
                Roles = _context.Roles.AsSplitQuery().AsNoTracking().Select(r => new RoleDto()
                {
                    RoleName = r.Name,
                    HasRole = r.UserRoles.Any(ur => ur.UserId == user.Id),
                }).ToList(),
            }).FirstOrDefaultAsync(ct);
    }

    public async Task<string?> CreateUser(CreateUserRequestDto createUserRequest, CancellationToken ct)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);
        var userNameTaken = await _context.Users.AnyAsync(x => x.UserName == createUserRequest.UserName, ct);
        if (userNameTaken)
            return "User name is taken";

        var (xboxUser, msg) = await _getsertXboxUserService.GetsertXboxUserByGamertag(createUserRequest.Gamertag, ct);

        if (xboxUser is null)
            return msg ?? "Failed to get xbox user, unknown reason";

        if (xboxUser.User is not null)
        {
            //return $"Gamertag is already used by {xboxUser.User.UserName ?? xboxUser.User.DisplayName}";
            // Take gamertag
            xboxUser.User.XboxUserID = null;
        }
            

        var user = new User()
        {
            UserName = createUserRequest.UserName,
            XboxUser = xboxUser,
            DisplayName = createUserRequest.DisplayName,
        };

        var result = await _userManager.CreateAsync(user);

        if (result.Succeeded is false)
        {
            var sb = new StringBuilder();
            foreach(var error in result.Errors)
                sb.AppendLine(error.Description);
            return sb.ToString();
        }

        await transaction.CommitAsync(ct);
        return null;
    }

    public async Task<string?> EditUser(UserResponseDto editUser, CancellationToken ct)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        var user = await _context.Users
            .Include(x => x.XboxUser)
            .Where(user => user.Id == editUser.UserID).FirstOrDefaultAsync(ct);

        if (user is null)
            throw new Exception("User does not exist");

        user.DisplayName = editUser.DisplayName;
        user.IsDummyUser = editUser.IsDummyUser;

        if (user.XboxUser?.Gamertag.ToLower() != editUser.Gamertag?.ToLower())
        {
            if (string.IsNullOrEmpty(editUser.Gamertag))
            {
                user.XboxUserID = null;
            }
            else
            {
                var (xboxUser, msg) = await _getsertXboxUserService.GetsertXboxUserByGamertag(editUser.Gamertag, ct);

                if (xboxUser is null)
                    return msg ?? "Failed to get xbox user, unknown reason";

                if (xboxUser.User is not null)
                {
                    //return $"Gamertag is already used by {xboxUser.User.UserName ?? xboxUser.User.DisplayName}";
                    // Take gamertag...
                    xboxUser.User.XboxUserID = null;
                }
                    

                user.XboxUser = xboxUser;
            }
            
        }

        if (editUser.LockoutEnd != user.LockoutEnd)
        {
            var result = await _userManager.SetLockoutEndDateAsync(user, editUser.LockoutEnd);
            if (!result.Succeeded)
                return "Failed to set lockout end date";
        }

        if (editUser.LockoutEnabled != user.LockoutEnabled)
        {
            var result = await _userManager.SetLockoutEnabledAsync(user, editUser.LockoutEnabled);
            if (!result.Succeeded)
                return "Failed to set lockout enabled";
        }

        if (editUser.UserName != user.UserName)
        {
            var result = await _userManager.SetUserNameAsync(user, editUser.UserName);
            if (!result.Succeeded)
                return "Failed to set username";
        }

        var currentRoles = await _userManager.GetRolesAsync(user);

        var rolesToAdd = editUser.Roles.Where(x => x.HasRole && !currentRoles.Contains(x.RoleName)).Select(x => x.RoleName).ToList();
        var rolesToRemove = editUser.Roles.Where(x => !x.HasRole && currentRoles.Contains(x.RoleName)).Select(x => x.RoleName).ToList();

        if (rolesToAdd.Any())
        {
            var result = await _userManager.AddToRolesAsync(user, rolesToAdd);
            if (!result.Succeeded)
                return "Failed to add roles";
        }
            
        if (rolesToRemove.Any())
        {
            var result = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!result.Succeeded)
                return "Failed to remove roles";
        }

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return null;
    }

    public async Task<(bool Success, string Message, string? Token, DateTime? ExpiresAt)> GeneratePasswordResetLink(string username, int createdByUserId, CancellationToken ct)
    {
        var user = await _userManager.FindByNameAsync(username);
        if (user == null)
        {
            return (false, "User not found", null, null);
        }

        // Check if user has a password (username/password login)
        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return (false, "User does not have a password set (likely uses external login only)", null, null);
        }

        // Generate a cryptographically secure random token (64 characters)
        var tokenBytes = new byte[32]; // 32 bytes = 64 hex characters
        RandomNumberGenerator.Fill(tokenBytes);
        var token = Convert.ToHexString(tokenBytes).ToLower();
        var expiresAt = DateTime.UtcNow.AddMinutes(10);

        // Clean up any existing unused reset links for this user
        var existingLinks = await _context.PasswordResetLinks
            .Where(x => x.UserId == user.Id && !x.IsUsed)
            .ToListAsync(ct);

        _context.PasswordResetLinks.RemoveRange(existingLinks);

        // Create new reset link
        var resetLink = new PasswordResetLink
        {
            UserId = user.Id,
            Token = token,
            ExpiresAt = expiresAt,
            CreatedByID = createdByUserId,
            ModifiedByID = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        _context.PasswordResetLinks.Add(resetLink);
        await _context.SaveChangesAsync(ct);

        return (true, "Password reset link generated successfully", token, expiresAt);
    }

    public async Task<(bool Success, string Message)> UsePasswordResetLink(string token, string newPassword, CancellationToken ct)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var resetLink = await _context.PasswordResetLinks
            .Include(x => x.User)
            .Where(x => x.Token == token && !x.IsUsed)
            .FirstOrDefaultAsync(ct);

        if (resetLink == null)
        {
            return (false, "Invalid or already used reset link");
        }

        if (resetLink.ExpiresAt <= DateTime.UtcNow)
        {
            // Clean up expired link
            _context.PasswordResetLinks.Remove(resetLink);
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return (false, "Reset link has expired");
        }

        // Reset the password
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(resetLink.User);
        var result = await _userManager.ResetPasswordAsync(resetLink.User, resetToken, newPassword);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return (false, $"Failed to reset password: {errors}");
        }

        // Mark the reset link as used and clean up
        _context.PasswordResetLinks.Remove(resetLink);
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return (true, "Password reset successfully");
    }

    public async Task CleanupExpiredPasswordResetLinks(CancellationToken ct)
    {
        var expiredLinks = await _context.PasswordResetLinks
            .Where(x => x.ExpiresAt <= DateTime.UtcNow || x.IsUsed)
            .ToListAsync(ct);

        if (expiredLinks.Any())
        {
            _context.PasswordResetLinks.RemoveRange(expiredLinks);
            await _context.SaveChangesAsync(ct);
        }
    }
}
