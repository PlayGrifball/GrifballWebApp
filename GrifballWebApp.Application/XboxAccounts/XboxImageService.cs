using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Players;
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

    /// <summary>When the account's stored image of that kind was last checked (its version); null when none is stored. Reads no bytes.</summary>
    public async Task<DateTime?> GetCheckedAtAsync(long xboxUserID, XboxUserImageKind kind, CancellationToken ct = default)
        => await _context.XboxUserImages.AsNoTracking()
            .Where(i => i.XboxUserID == xboxUserID && i.Kind == kind && i.Status == XboxUserImageStatus.Found && i.Content != null)
            .Select(i => (DateTime?)i.CheckedAt)
            .FirstOrDefaultAsync(ct);

    /// <summary>The account's stored image of that kind; null when none is stored (not fetched yet, or Xbox has none).</summary>
    public async Task<XboxImageContent?> GetAsync(long xboxUserID, XboxUserImageKind kind, CancellationToken ct = default)
    {
        var image = await _context.XboxUserImages.AsNoTracking()
            .Where(i => i.XboxUserID == xboxUserID && i.Kind == kind && i.Status == XboxUserImageStatus.Found && i.Content != null)
            .Select(i => new { i.Content, i.ContentType, i.CheckedAt })
            .FirstOrDefaultAsync(ct);
        return image is null ? null : new XboxImageContent(image.Content!, image.ContentType ?? "application/octet-stream", image.CheckedAt);
    }
}
