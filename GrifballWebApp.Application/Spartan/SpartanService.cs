using System.Text.Json;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Spartan;

/// <summary>What a user's Spartan stands at: their own choice if they made one, and the render of it.</summary>
public sealed record SpartanProfileDto(
    SpartanChoice? Choice,
    bool HasEquippedSet,
    string? RenderState,
    DateTime? RenderedAt,
    string? RenderError,
    IReadOnlyList<string> Files);

public enum SetSpartanOutcome { Saved, Invalid, CatalogUnavailable, NoSuchUser }

public sealed record SetSpartanResult(SetSpartanOutcome Outcome, IReadOnlyList<string> Problems);

/// <summary>A rendered file of a user's Spartan.</summary>
public sealed record SpartanFileDto(string ContentType, byte[] Content);

/// <summary>
/// A user's Spartan on their profile: the one they have equipped in Halo Infinite unless they choose another (checked against
/// the render service's catalog), and its render, which the render job makes.
/// </summary>
public class SpartanService(IApplicationDbContext context, ISpartanCatalogSource catalogs, TimeProvider time)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The names a render's files can have.</summary>
    public static readonly IReadOnlyList<string> FileNames =
        ["glb", "stance-body", "stance-head", "neutral-body", "neutral-head"];

    public Task<SpartanCatalog?> Catalog(CancellationToken ct = default) => catalogs.Get(ct);

    public async Task<SpartanProfileDto?> Get(int userId, CancellationToken ct = default)
    {
        var user = await context.Users.Where(u => u.Id == userId).Select(u => new { u.XboxUserID }).FirstOrDefaultAsync(ct);
        if (user is null)
            return null;
        var json = await context.UserSpartans.Where(s => s.UserId == userId).Select(s => s.ChoiceJson).FirstOrDefaultAsync(ct);
        var hasEquipped = user.XboxUserID is long xuid
            && await context.XboxUserArmor.AnyAsync(a => a.XboxUserID == xuid && a.Status != XboxUserArmorStatus.NotFound && a.CoreId != null, ct);
        var render = await context.UserSpartanRenders.Where(r => r.UserId == userId)
            .Select(r => new { r.State, r.RenderedAt, r.Error, Files = r.Files.Select(f => f.Name).ToList() })
            .FirstOrDefaultAsync(ct);
        return new SpartanProfileDto(
            json is null ? null : JsonSerializer.Deserialize<SpartanChoice>(json, Json),
            hasEquipped,
            render?.State.ToString(),
            render?.RenderedAt,
            render?.Error,
            FileNames.Where(n => render?.Files.Contains(n) == true).ToList());
    }

    /// <summary>Saves the user's own Spartan if the render service can render it, and asks for its render.</summary>
    public async Task<SetSpartanResult> Set(int userId, SpartanChoice choice, CancellationToken ct = default)
    {
        if (!await context.Users.AnyAsync(u => u.Id == userId, ct))
            return new(SetSpartanOutcome.NoSuchUser, []);
        var catalog = await catalogs.Get(ct);
        if (catalog is null)
            return new(SetSpartanOutcome.CatalogUnavailable, []);
        var problems = SpartanChoiceValidator.Validate(choice, catalog);
        if (problems.Count > 0)
            return new(SetSpartanOutcome.Invalid, problems);
        var json = JsonSerializer.Serialize(choice, Json);
        if (json.Length > Domain.Players.UserSpartan.MaxChoiceLength)
            return new(SetSpartanOutcome.Invalid, ["That Spartan is too long to keep"]);

        var now = time.GetUtcNow().UtcDateTime;
        var row = await context.UserSpartans.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (row is null)
            context.UserSpartans.Add(new Domain.Players.UserSpartan { UserId = userId, ChoiceJson = json, UpdatedAt = now });
        else
        {
            row.ChoiceJson = json;
            row.UpdatedAt = now;
        }
        await AskForRender(userId, now, ct);
        await context.SaveChangesAsync(ct);
        return new(SetSpartanOutcome.Saved, []);
    }

    /// <summary>Goes back to the equipped set; false when there is no such user.</summary>
    public async Task<bool> Clear(int userId, CancellationToken ct = default)
    {
        if (!await context.Users.AnyAsync(u => u.Id == userId, ct))
            return false;
        var row = await context.UserSpartans.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (row is not null)
            context.UserSpartans.Remove(row);
        if (await context.UserSpartanRenders.AnyAsync(r => r.UserId == userId, ct))
            await AskForRender(userId, time.GetUtcNow().UtcDateTime, ct);
        await context.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Asks for the render of the user's Spartan (again); false when there is no such user.</summary>
    public async Task<bool> RequestRender(int userId, CancellationToken ct = default)
    {
        if (!await context.Users.AnyAsync(u => u.Id == userId, ct))
            return false;
        await AskForRender(userId, time.GetUtcNow().UtcDateTime, ct);
        await context.SaveChangesAsync(ct);
        return true;
    }

    public Task<SpartanFileDto?> File(int userId, string name, CancellationToken ct = default)
        => context.UserSpartanFiles.Where(f => f.UserId == userId && f.Name == name)
            .Select(f => new SpartanFileDto(f.ContentType, f.Content)).FirstOrDefaultAsync(ct);

    private async Task AskForRender(int userId, DateTime now, CancellationToken ct)
    {
        var render = await context.UserSpartanRenders.FirstOrDefaultAsync(r => r.UserId == userId, ct);
        if (render is null)
        {
            context.UserSpartanRenders.Add(new UserSpartanRender { UserId = userId, State = UserSpartanRenderState.Requested, RequestedAt = now });
            return;
        }
        render.State = UserSpartanRenderState.Requested;
        render.RequestedAt = now;
        render.Error = null;
    }
}
