using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.XboxAccounts;

/// <summary>An Xbox account's stored image: its bytes and type, and when it was last checked (for the response's validators).</summary>
public sealed record XboxImageContent(byte[] Content, string ContentType, DateTime CheckedAt);

/// <summary>Reads the images the image jobs keep for Xbox accounts (gamer pictures and Halo emblems), so pages show them from the site.</summary>
public class XboxImageService
{
    private readonly IApplicationDbContext _context;

    public XboxImageService(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>The Xbox account a site user is tied to; null for a user with none (or no such user).</summary>
    public virtual async Task<long?> GetXboxUserIdOfAsync(int userID, CancellationToken ct = default)
        => await _context.Users.AsNoTracking().Where(u => u.Id == userID).Select(u => u.XboxUserID).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Which stored image stands for the account: the one its user chose (the emblem when it has no user, or they chose
    /// nothing), else the other when that one is not stored; null when neither is.
    /// </summary>
    public virtual async Task<XboxUserImageKind?> GetShownKindAsync(long xboxUserID, ProfilePictureChoice? choice = null, CancellationToken ct = default)
    {
        choice ??= await _context.Users.AsNoTracking().Where(u => u.XboxUserID == xboxUserID).Select(u => (ProfilePictureChoice?)u.ProfilePicture).FirstOrDefaultAsync(ct);
        var kinds = choice == ProfilePictureChoice.GamerPicture
            ? new[] { XboxUserImageKind.GamerPicture, XboxUserImageKind.HaloEmblem }
            : [XboxUserImageKind.HaloEmblem, XboxUserImageKind.GamerPicture];
        var stored = await _context.XboxUserImages.AsNoTracking()
            .Where(i => i.XboxUserID == xboxUserID && i.Status == XboxUserImageStatus.Found && i.Content != null)
            .Select(i => i.Kind).ToListAsync(ct);
        return kinds.Cast<XboxUserImageKind?>().FirstOrDefault(k => stored.Contains(k!.Value));
    }

    /// <summary>The Xbox account a site user is tied to and the image they chose to show; null for a user with no account (or no such user).</summary>
    public virtual async Task<(long XboxUserID, ProfilePictureChoice Choice)?> GetAccountAndChoiceOfAsync(int userID, CancellationToken ct = default)
    {
        var user = await _context.Users.AsNoTracking().Where(u => u.Id == userID && u.XboxUserID != null)
            .Select(u => new { XboxUserID = u.XboxUserID!.Value, u.ProfilePicture }).FirstOrDefaultAsync(ct);
        return user is null ? null : (user.XboxUserID, user.ProfilePicture);
    }

    /// <summary>When the account's stored image of that kind was last checked (its version); null when none is stored. Reads no bytes.</summary>
    public virtual async Task<DateTime?> GetCheckedAtAsync(long xboxUserID, XboxUserImageKind kind, CancellationToken ct = default)
        => await _context.XboxUserImages.AsNoTracking()
            .Where(i => i.XboxUserID == xboxUserID && i.Kind == kind && i.Status == XboxUserImageStatus.Found && i.Content != null)
            .Select(i => (DateTime?)i.CheckedAt)
            .FirstOrDefaultAsync(ct);

    /// <summary>The account's stored image of that kind; null when none is stored (not fetched yet, or Xbox has none).</summary>
    public virtual async Task<XboxImageContent?> GetAsync(long xboxUserID, XboxUserImageKind kind, CancellationToken ct = default)
    {
        var image = await _context.XboxUserImages.AsNoTracking()
            .Where(i => i.XboxUserID == xboxUserID && i.Kind == kind && i.Status == XboxUserImageStatus.Found && i.Content != null)
            .Select(i => new { i.Content, i.ContentType, i.CheckedAt })
            .FirstOrDefaultAsync(ct);
        return image is null ? null : new XboxImageContent(image.Content!, image.ContentType ?? "application/octet-stream", image.CheckedAt);
    }
}
