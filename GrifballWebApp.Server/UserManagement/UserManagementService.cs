﻿using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Surprenant.Grunt.Core;
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
    public string? Region { get; set; }
    public string? DisplayName { get; set; }
    public string? Gamertag { get; set; }
    public List<RoleDto> Roles { get; set; }
}

public class RoleDto
{
    public string RoleName { get; set; }
    public bool HasRole { get; set; }
}

public class UserManagementService
{
    private readonly GrifballContext _context;
    private readonly HaloInfiniteClientFactory _haloInfiniteClientFactory;
    private readonly UserManager<User> _userManager;
    public UserManagementService(GrifballContext grifballContext, HaloInfiniteClientFactory haloInfiniteClientFactory, UserManager<User> userManager)
    {
        _context = grifballContext;
        _haloInfiniteClientFactory = haloInfiniteClientFactory;
        _userManager = userManager;
    }

    public Task<List<UserResponseDto>> GetUsers(CancellationToken ct)
    {
        return _context.Users
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
                Roles = _context.Roles.AsSplitQuery().AsNoTracking().Select(r => new RoleDto()
                {
                    RoleName = r.Name,
                    HasRole = r.UserRoles.Any(ur => ur.UserId == user.Id),
                }).ToList(),
            })
            .ToListAsync(ct);
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

        var xboxUser = await _context.XboxUsers
            .Include(x => x.User)
            .Where(x => x.Gamertag == createUserRequest.Gamertag).FirstOrDefaultAsync(ct);

        if (xboxUser is null)
        {
            var client = await _haloInfiniteClientFactory.CreateAsync();
            var xboxUserResult = await client.UserByGamertag(createUserRequest.Gamertag);
            if (xboxUserResult.Result is null || string.IsNullOrEmpty(xboxUserResult.Result.xuid))
            {
                return $"Failed to get xbox user with gamertag {createUserRequest.Gamertag}";
            }
            var parsed = long.TryParse(xboxUserResult.Result.xuid, out var xboxUserID);

            if (parsed is false)
                return "Contact Sysadmin";

            xboxUser = new Database.Models.XboxUser()
            {
                XboxUserID = xboxUserID,
                Gamertag = xboxUserResult.Result.gamertag,
            };
            await _context.XboxUsers.AddAsync(xboxUser);
        }

        if (xboxUser.User is not null)
            return $"Gamertag is already used by {xboxUser.User.UserName ?? xboxUser.User.DisplayName}";

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
                var xboxUser = await _context.XboxUsers
                .Include(x => x.User)
                .Where(x => x.Gamertag == editUser.Gamertag).FirstOrDefaultAsync();

                if (xboxUser is null)
                {
                    var client = await _haloInfiniteClientFactory.CreateAsync();
                    var xboxUserResult = await client.UserByGamertag(editUser.Gamertag);
                    if (xboxUserResult.Result is null || string.IsNullOrEmpty(xboxUserResult.Result.xuid))
                    {
                        return $"Failed to get xbox user with gamertag {editUser.Gamertag}";
                    }
                    var parsed = long.TryParse(xboxUserResult.Result.xuid, out var xboxUserID);

                    if (parsed is false)
                        return "Contact Sysadmin";

                    xboxUser = new Database.Models.XboxUser()
                    {
                        XboxUserID = xboxUserID,
                        Gamertag = xboxUserResult.Result.gamertag,
                    };
                    await _context.XboxUsers.AddAsync(xboxUser);
                }

                if (xboxUser.User is not null)
                    return $"Gamertag is already used by {xboxUser.User.UserName ?? xboxUser.User.DisplayName}";

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
}
