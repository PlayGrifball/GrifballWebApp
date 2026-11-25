using GrifballWebApp.Domain.Players;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Profile;

public class SetProfilePictureRequest
{
    public ProfilePictureChoice ProfilePicture { get; set; }
}

/// <summary>A user choosing which of their Xbox images (their Halo emblem or gamer picture) stands for them on the site.</summary>
public class ProfilePictureService
{
    private readonly IApplicationDbContext _context;

    public ProfilePictureService(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>Sets the image the user shows; false when there is no such user.</summary>
    public async Task<bool> Set(int userId, ProfilePictureChoice choice, CancellationToken ct = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return false;
        user.ChooseProfilePicture(choice);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
