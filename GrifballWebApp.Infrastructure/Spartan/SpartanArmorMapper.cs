using GrifballWebApp.Application.Spartan;
using GrifballWebApp.Domain.Players;
using Microsoft.Extensions.Caching.Memory;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using Surprenant.Grunt.Models.HaloInfinite;

namespace GrifballWebApp.Infrastructure.Spartan;

/// <summary>An equipped set as the render service names it, and the stance of the equipped action pose and the weapon it holds (null when it has none).</summary>
public sealed record MappedSpartan(SpartanChoice Choice, int? Stance, int? WeaponTagId = null);

/// <summary>An action pose: its stance and the weaponTagId of the weapon it holds (each null when it has none).</summary>
public sealed record SpartanPose(int? Stance, int? WeaponTagId);

/// <summary>Halo did not give an answer to use (a passing problem); <see cref="Wait"/> is how long it asked to be left alone.</summary>
public sealed class HaloAnswerException(string message, TimeSpan? wait = null) : HttpRequestException(message)
{
    public TimeSpan? Wait { get; } = wait;
}

/// <summary>
/// Turns what an Xbox account has equipped (Halo's item paths) into a Spartan the render service can render: an item's title
/// is the name of its piece, a coating's style id is its coating, an action pose's stance id is its stance. Each item is asked
/// for once a day, one call after another (the gateway's customization budget is small).
/// </summary>
public sealed class SpartanArmorMapper(IHaloInfiniteClient halo, IMemoryCache cache)
{
    public static readonly TimeSpan ItemLifetime = TimeSpan.FromDays(1);
    private static readonly TimeSpan Gap = TimeSpan.FromMilliseconds(500);

    /// <summary>Waits between two calls to Halo; replaceable in tests.</summary>
    public Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = (delay, ct) => Task.Delay(delay, ct);

    private sealed record Item(string? Title, int? StyleId, int? StanceId, int? WeaponTagId = null);

    private sealed record Slot(string? Path, params string[] Hints);

    /// <summary>The Spartan of the armor row, or null when its core is not one the render service has (yet). Throws when Halo failed.</summary>
    public async Task<MappedSpartan?> Map(XboxUserArmor armor, Application.Spartan.SpartanCatalog catalog, CancellationToken ct)
    {
        var coreTitle = (await ItemOf(armor.CorePath, ct))?.Title;
        var core = coreTitle is null ? null : catalog.Cores.FirstOrDefault(c => string.Equals(c.Name, coreTitle, StringComparison.OrdinalIgnoreCase));
        if (core is null)
            return null;

        var pieces = new Dictionary<string, string>(StringComparer.Ordinal);
        Slot[] slots =
        [
            new(armor.HelmetPath, "helmet"),
            new(armor.GlovePath, "glove"),
            new(armor.KneePadPath, "knee"),
            new(armor.LeftShoulderPadPath, "shoulder", "left"),
            new(armor.RightShoulderPadPath, "shoulder", "right"),
            new(armor.HelmetAttachmentPath, "helmet", "attach"),
            new(armor.ChestAttachmentPath, "chest"),
            new(armor.HipAttachmentPath, "hip"),
            new(armor.WristAttachmentPath, "wrist"),
        ];
        foreach (var slot in slots)
        {
            var title = (await ItemOf(slot.Path, ct))?.Title;
            if (title is null)
                continue;
            foreach (var key in RegionsOf(core, title, slot.Hints))
                pieces.TryAdd(key, CatalogNameOf(core, key, title));
        }

        string? coating = null;
        if ((await ItemOf(armor.CoatingPath, ct))?.StyleId is int style && catalog.Coatings.Contains(style.ToString(), StringComparer.Ordinal))
            coating = style.ToString();
        string? visor = null;
        if ((await ItemOf(armor.VisorPath, ct))?.Title is { } visorTitle)
            visor = catalog.Visors.FirstOrDefault(v => string.Equals(v, visorTitle, StringComparison.OrdinalIgnoreCase));
        var pose = await PoseOf(armor, ct);

        return new MappedSpartan(new SpartanChoice(core.Name, pieces, null, coating, visor), pose.Stance, pose.WeaponTagId);
    }

    /// <summary>The stance of the armor row's action pose and the weapon it holds. Throws when Halo failed.</summary>
    public async Task<SpartanPose> PoseOf(XboxUserArmor armor, CancellationToken ct)
        => await ItemOf(armor.ActionPosePath, ct) is { } pose ? new(pose.StanceId, pose.WeaponTagId) : new(null, null);

    /// <summary>The coating (its style id) the account has equipped on a weapon type (a weapon core type, e.g. WeaponCommando), or null. Throws when Halo failed.</summary>
    public async Task<string?> WeaponCoatingOf(XboxUserArmor armor, string coreType, CancellationToken ct)
        => armor.WeaponCoatingPaths.TryGetValue(coreType, out var path) && (await ItemOf(path, ct))?.StyleId is int style ? style.ToString() : null;

    private static string CatalogNameOf(SpartanCatalogCore core, string regionKey, string title)
        => core.Regions.First(r => r.Key == regionKey).Pieces.First(p => string.Equals(p.Name, title, StringComparison.OrdinalIgnoreCase)).Name ?? title;

    /// <summary>The keys of the core's regions that have a piece named <paramref name="title"/>, narrowed by what the slot is called when several do.</summary>
    internal static IReadOnlyList<string> RegionsOf(SpartanCatalogCore core, string title, string[] hints)
    {
        var candidates = core.Regions
            .Where(r => r.Kind == "REGION" && r.Pieces.Any(p => string.Equals(p.Name, title, StringComparison.OrdinalIgnoreCase)))
            .Select(r => r.Key)
            .ToList();
        if (candidates.Count <= 1)
            return candidates;
        for (var count = hints.Length; count > 0; count--)
        {
            var wanted = hints.Take(count).ToList();
            var narrowed = candidates.Where(k => wanted.All(h => k.Contains(h, StringComparison.OrdinalIgnoreCase))).ToList();
            if (narrowed.Count > 0)
                return narrowed;
        }
        return [];
    }

    private async Task<Item?> ItemOf(string? path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        path = path.Trim().TrimStart('/');
        if (cache.TryGetValue("spartan-item:" + path, out Item? known))
            return known;
        await Delay(Gap, ct);
        var response = await halo.GameCmsGetItem(path, string.Empty);
        if (response.Error is { Code: 429 } limited)
            throw new HaloAnswerException($"Halo is throttling (wait {WaitOf(limited)})", WaitOf(limited));
        if (response.Result is null && response.Error is not { Code: 404 })
            throw new HaloAnswerException(response.Error is { Code: not 0 } e ? $"Halo answered {e.Code} for {path}" : $"Halo gave no answer for {path}");
        var item = response.Result is { } r ? ItemOfResult(r) : null;
        cache.Set("spartan-item:" + path, item, ItemLifetime);
        return item;
    }

    private static TimeSpan WaitOf(HaloApiErrorContainer limited)
        => limited.RetryAfter is { } wait && wait > TimeSpan.Zero ? wait : TimeSpan.FromMinutes(1);

    private static Item ItemOfResult(InGameItem r)
    {
        var title = r.CommonData?.Title?.Value;
        return new Item(
            string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            r.StyleId?.MIdentifier,
            r.StanceId is { MIdentifier: not 0 } stance ? stance.MIdentifier : null,
            r.WeaponTagId is not (null or 0) ? r.WeaponTagId : null);
    }
}
