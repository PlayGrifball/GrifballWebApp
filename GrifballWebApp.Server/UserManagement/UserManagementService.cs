using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.UserManagement;

public class UserResponseDto
{
    public int ID { get; set; }
    public string UserName { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public bool LockoutEnabled { get; set; }
    public int AccessFailedCount { get; set; }
    public string? Region { get; set; }
    public string DisplayName { get; set; }
    public string Gamertag { get; set; }
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
    public UserManagementService(GrifballContext grifballContext)
    {
        _context = grifballContext;
    }

    public Task<List<UserResponseDto>> GetUsers(CancellationToken ct)
    {
        return _context.Users
            .AsNoTracking().AsSplitQuery()
            .Select(x => new UserResponseDto()
            {
                ID = x.Id,
                UserName = x.UserName,
                LockoutEnd = x.LockoutEnd,
                LockoutEnabled = x.LockoutEnabled,
                AccessFailedCount = x.AccessFailedCount,
                Region = x.Region.RegionName,
                DisplayName = x.DisplayName,
                Gamertag = x.XboxUser.Gamertag,
                Roles = _context.Roles.AsSplitQuery().AsNoTracking().Select(r => new RoleDto()
                {
                    RoleName = r.Name,
                    HasRole = r.UserRoles.Any(ur => ur.UserId == x.Id),
                }).ToList(),
            }).ToListAsync(ct);
    }
}
