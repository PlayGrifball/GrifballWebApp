using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;

namespace GrifballWebApp.Infrastructure.HaloInfinite;

public class XboxImagesOptions
{
    /// <summary>Whether the image jobs ask Xbox and Halo for anything. False leaves every account due until it is true again.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Accounts whose gamer picture one run of its job fetches, <see cref="XboxImageSource.UsersPerCall"/> to a Halo call.</summary>
    public int GamerPictureAccountsPerRun { get; set; } = 100;
    /// <summary>
    /// At most this many profile calls a minute. The gateway's profile budget (15 a minute, one count for every gamertag and user
    /// lookup, a trip locking all of them for minutes) is shared with the XUID lookups, so this stays well under it.
    /// </summary>
    public int ProfileCallsPerMinute { get; set; } = 3;
    /// <summary>Accounts whose emblem one run of its job fetches (one Halo call each).</summary>
    public int EmblemAccountsPerRun { get; set; } = 60;
    /// <summary>At most this many appearance calls a minute (the gateway's customization budget is 120).</summary>
    public int EmblemCallsPerMinute { get; set; } = 60;
    /// <summary>An image found, or an account without one, is checked again after this long (a picture or an emblem may change).</summary>
    public TimeSpan CheckAgainAfter { get; set; } = TimeSpan.FromDays(14);
    /// <summary>A check that failed (the API down, a bad image) is tried again after this long.</summary>
    public TimeSpan RetryFailedAfter { get; set; } = TimeSpan.FromHours(6);
}

/// <summary>What a run of an image job did.</summary>
public sealed class XboxImagesReport
{
    public int Checked { get; set; }
    public int Stored { get; set; }
    public int Unchanged { get; set; }
    public int NotFound { get; set; }
    public int Failed { get; set; }
    /// <summary>Set when Halo, or the gateway's budget for it, answered 429: the run stopped there, that account left due.</summary>
    public TimeSpan? ThrottledFor { get; set; }
    /// <summary>How many accounts were due when the run began (0: nothing to do).</summary>
    public int Due { get; set; }
    /// <summary>Set when XboxImages:Enabled is off: the run did nothing.</summary>
    public bool Disabled { get; set; }
}

/// <summary>The image a download gave.</summary>
public sealed record DownloadedImage(byte[] Content, string ContentType);

/// <summary>Downloads a gamer picture from the URL Halo's profile service gave.</summary>
public interface IXboxImageDownloader
{
    /// <summary>The image at <paramref name="url"/>; throws <see cref="InvalidOperationException"/> (its message says why) for anything but a small image over https.</summary>
    Task<DownloadedImage> DownloadAsync(string url, CancellationToken ct);
}

public sealed class XboxImageDownloader(HttpClient http) : IXboxImageDownloader
{
    public static void Configure(HttpClient client)
    {
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GrifballWebApp/1.0 (gamer pictures)");
    }

    public async Task<DownloadedImage> DownloadAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("The picture's address isn't an https URL");
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"The picture's address answered {(int)response.StatusCode}");
        var type = response.Content.Headers.ContentType?.MediaType;
        if (type is null || !type.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The picture's address didn't serve an image");
        if (response.Content.Headers.ContentLength > XboxUserImage.MaxContentBytes)
            throw new InvalidOperationException("The picture is too large");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > XboxUserImage.MaxContentBytes)
                throw new InvalidOperationException("The picture is too large");
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        var bytes = buffer.ToArray();
        return new DownloadedImage(bytes, XboxImageSource.SniffContentType(bytes) ?? throw new InvalidOperationException("The picture isn't a PNG, JPEG or WebP"));
    }
}

/// <summary>
/// Fetches the images kept for Xbox accounts (<see cref="XboxUserImage"/>): the gamer picture, from Halo's profile service
/// (<c>Users</c>, 20 accounts a call), and the Halo Infinite emblem, from the account's equipped appearance and the emblem
/// mapping. A batch takes the accounts due (no row yet, or checked longer ago than <see cref="XboxImagesOptions.CheckAgainAfter"/>, a
/// failure <see cref="XboxImagesOptions.RetryFailedAfter"/>), spaced to the options' calls a minute, and stops at a 429.
/// </summary>
public class XboxImageSource
{
    /// <summary>Accounts to a <c>Users</c> call (Grunt's HaloInput.MaxPlayersPerCall).</summary>
    public const int UsersPerCall = 20;

    private readonly GrifballContext _context;
    private readonly IHaloInfiniteClient _halo;
    private readonly IXboxImageDownloader _downloader;
    private readonly XboxImagesOptions _options;
    private readonly TimeProvider _time;

    public XboxImageSource(GrifballContext context, IHaloInfiniteClient halo, IXboxImageDownloader downloader, IOptions<XboxImagesOptions> options, TimeProvider time)
    {
        _context = context;
        _halo = halo;
        _downloader = downloader;
        _options = options.Value;
        _time = time;
    }

    /// <summary>Waits between two calls to Halo (the throttle); replaceable in tests.</summary>
    public Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = (delay, ct) => Task.Delay(delay, ct);

    private IQueryable<long> Due(XboxUserImageKind kind)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var again = now - _options.CheckAgainAfter;
        var retry = now - _options.RetryFailedAfter;
        return _context.XboxUsers
            .Where(x => !_context.XboxUserImages.Any(i => i.XboxUserID == x.XboxUserID && i.Kind == kind
                && (i.Status == XboxUserImageStatus.Failed || i.Error != null ? i.CheckedAt > retry : i.CheckedAt > again)))
            .Select(x => x.XboxUserID);
    }

    /// <summary>The accounts due, those never checked first, then the longest unchecked, so failures coming due again don't starve the rest.</summary>
    private IQueryable<long> DueInOrder(XboxUserImageKind kind)
        => Due(kind)
            .OrderBy(x => _context.XboxUserImages.Any(i => i.XboxUserID == x && i.Kind == kind) ? 1 : 0)
            .ThenBy(x => _context.XboxUserImages.Where(i => i.XboxUserID == x && i.Kind == kind).Select(i => i.CheckedAt).FirstOrDefault())
            .ThenBy(x => x);

    public Task<int> CountDueAsync(XboxUserImageKind kind, CancellationToken ct) => Due(kind).CountAsync(ct);

    /// <summary>The image row of the account (made, unsaved, when it has none), stamped as checked now.</summary>
    private async Task<XboxUserImage> RowAsync(long xuid, XboxUserImageKind kind, CancellationToken ct)
    {
        var row = await _context.XboxUserImages.FirstOrDefaultAsync(i => i.XboxUserID == xuid && i.Kind == kind, ct);
        if (row is null)
        {
            row = new XboxUserImage { XboxUserID = xuid, Kind = kind };
            _context.XboxUserImages.Add(row);
        }
        row.CheckedAt = _time.GetUtcNow().UtcDateTime;
        return row;
    }

    private static void Found(XboxUserImage row, string source, DownloadedImage image)
    {
        row.Status = XboxUserImageStatus.Found;
        row.Source = source;
        row.ContentType = image.ContentType;
        row.Content = image.Content;
        row.Error = null;
    }

    private static void NotFound(XboxUserImage row)
    {
        if (row.Content is not null)
        {
            // Halo not giving one this time doesn't take away the image already shown; try again soon
            row.Error = "Halo no longer gave one";
            return;
        }
        row.Status = XboxUserImageStatus.NotFound;
        row.Source = null;
        row.ContentType = null;
        row.Content = null;
        row.Error = null;
    }

    private static void Failed(XboxUserImage row, string error)
    {
        // The bytes it has stay: a failed check doesn't take away the image already shown
        row.Status = row.Content is null ? XboxUserImageStatus.Failed : XboxUserImageStatus.Found;
        if (row.Content is null)
            row.Source = null;
        row.Error = error.Length > 200 ? error[..200] : error;
    }

    private static TimeSpan? ThrottleOf<T>(HaloApiResultContainer<T, HaloApiErrorContainer> response)
        => response.Error is { Code: 429 } throttled
            ? throttled.RetryAfter is { } wait && wait > TimeSpan.Zero ? wait : TimeSpan.FromMinutes(1)
            : null;

    private static string Describe<T>(HaloApiResultContainer<T, HaloApiErrorContainer> response)
        => response.Error is { Code: not 0 } error ? $"Halo answered {error.Code}" : "Halo gave no answer";

    private static TimeSpan Gap(int callsPerMinute) => TimeSpan.FromMinutes(1) / Math.Max(1, callsPerMinute);

    // ---- gamer pictures ----

    /// <summary>One run of the gamer pictures: up to GamerPictureAccountsPerRun due accounts, in calls of <see cref="UsersPerCall"/>.</summary>
    public async Task<XboxImagesReport> RunGamerPicturesAsync(CancellationToken ct = default)
    {
        var report = new XboxImagesReport();
        if (!_options.Enabled)
        {
            report.Disabled = true;
            return report;
        }
        var due = await DueInOrder(XboxUserImageKind.GamerPicture).Take(Math.Max(1, _options.GamerPictureAccountsPerRun)).ToListAsync(ct);
        report.Due = await CountDueAsync(XboxUserImageKind.GamerPicture, ct);
        var gap = Gap(_options.ProfileCallsPerMinute);
        var first = true;
        foreach (var chunk in due.Chunk(UsersPerCall))
        {
            if (!first)
                await Delay(gap, ct);
            first = false;
            if (await GamerPictureCallAsync(chunk, report, ct))
                break;
        }
        return report;
    }

    /// <summary>One <c>Users</c> call for the accounts, and what it gave stored. Returns true when Halo throttled it (nothing stored).</summary>
    private async Task<bool> GamerPictureCallAsync(long[] xuids, XboxImagesReport report, CancellationToken ct)
    {
        HaloApiResultContainer<List<Surprenant.Grunt.Models.User>, HaloApiErrorContainer> response;
        string? callError = null;
        try
        {
            response = await _halo.Users(xuids.Select(x => x.ToString()));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            response = new(default!, new HaloApiErrorContainer());
            callError = ex.Message;
        }
        if (ThrottleOf(response) is { } wait)
        {
            report.ThrottledFor = wait;
            return true;
        }
        var users = response.Result;
        foreach (var xuid in xuids)
        {
            var row = await RowAsync(xuid, XboxUserImageKind.GamerPicture, ct);
            report.Checked++;
            if (users is null)
            {
                Failed(row, callError ?? Describe(response));
                report.Failed++;
                continue;
            }
            var url = PictureUrlOf(users.FirstOrDefault(u => u.xuid == xuid.ToString())?.gamerpic);
            if (url is null)
            {
                NotFound(row);
                report.NotFound++;
            }
            else
                await StorePictureAsync(row, url, report, ct);
        }
        await _context.SaveChangesAsync(ct);
        return false;
    }

    /// <summary>The picture to keep: the large one (the biggest the site shows), else the nearest size Halo gave.</summary>
    public static string? PictureUrlOf(Gamerpic? gamerpic)
        => new[] { gamerpic?.large, gamerpic?.xlarge, gamerpic?.medium, gamerpic?.small }.FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));

    private async Task StorePictureAsync(XboxUserImage row, string url, XboxImagesReport report, CancellationToken ct)
    {
        if (row.Status == XboxUserImageStatus.Found && row.Content is not null && row.Source == url)
        {
            row.Error = null;
            report.Unchanged++;
            return;
        }
        try
        {
            Found(row, url, await _downloader.DownloadAsync(url, ct));
            report.Stored++;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Failed(row, ex.Message);
            report.Failed++;
        }
    }

    // ---- Halo Infinite emblems ----

    /// <summary>One run of the emblems: up to EmblemAccountsPerRun due accounts, an appearance call each.</summary>
    public async Task<XboxImagesReport> RunEmblemsAsync(CancellationToken ct = default)
    {
        var report = new XboxImagesReport();
        if (!_options.Enabled)
        {
            report.Disabled = true;
            return report;
        }
        var due = await DueInOrder(XboxUserImageKind.HaloEmblem).Take(Math.Max(1, _options.EmblemAccountsPerRun)).ToListAsync(ct);
        report.Due = await CountDueAsync(XboxUserImageKind.HaloEmblem, ct);
        if (due.Count == 0)
            return report;

        var mapping = await _halo.GameCmsGetEmblemMapping();
        if (ThrottleOf(mapping) is { } mappingWait)
        {
            report.ThrottledFor = mappingWait;
            return report;
        }
        if (mapping.Result is null)
            throw new InvalidOperationException("The emblem mapping isn't available: " + Describe(mapping));

        var gap = Gap(_options.EmblemCallsPerMinute);
        var downloaded = new Dictionary<string, DownloadedImage?>();
        var first = true;
        foreach (var xuid in due)
        {
            if (!first)
                await Delay(gap, ct);
            first = false;
            if (await EmblemAsync(xuid, mapping.Result, downloaded, report, ct))
                break;
            await _context.SaveChangesAsync(ct);
        }
        return report;
    }

    /// <summary>The emblem of one account. Returns true when Halo throttled the run (nothing recorded for the account).</summary>
    private async Task<bool> EmblemAsync(long xuid, Dictionary<string, Dictionary<string, Surprenant.Grunt.Models.HaloInfinite.EmblemImageAssets>> mapping,
        Dictionary<string, DownloadedImage?> downloaded, XboxImagesReport report, CancellationToken ct)
    {
        HaloApiResultContainer<Surprenant.Grunt.Models.HaloInfinite.AppearanceCustomization, HaloApiErrorContainer> response;
        string? callError = null;
        try
        {
            response = await _halo.EconomyPlayerAppearanceCustomization($"xuid({xuid})");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            response = new(default!, new HaloApiErrorContainer());
            callError = ex.Message;
        }
        if (ThrottleOf(response) is { } wait)
        {
            report.ThrottledFor = wait;
            return true;
        }

        var row = await RowAsync(xuid, XboxUserImageKind.HaloEmblem, ct);
        report.Checked++;
        if (response.Result is null)
        {
            if (response.Error is { Code: 404 })
            {
                NotFound(row);
                report.NotFound++;
            }
            else
            {
                Failed(row, callError ?? Describe(response));
                report.Failed++;
            }
            return false;
        }

        var path = ImagePathOf(response.Result.Appearance?.Emblem, mapping);
        if (path is null)
        {
            NotFound(row);
            report.NotFound++;
        }
        else if (await StoreEmblemAsync(row, path, downloaded, report, ct))
        {
            report.Checked--;
            return true;
        }
        return false;
    }

    /// <summary>The emblem image's path: the emblem's file name without ".json" in the mapping, then its configuration.</summary>
    public static string? ImagePathOf(Surprenant.Grunt.Models.HaloInfinite.Emblem? emblem,
        Dictionary<string, Dictionary<string, Surprenant.Grunt.Models.HaloInfinite.EmblemImageAssets>> mapping)
    {
        if (string.IsNullOrWhiteSpace(emblem?.EmblemPath))
            return null;
        var id = Path.GetFileNameWithoutExtension(emblem.EmblemPath);
        return mapping.TryGetValue(id, out var configurations)
            && configurations.TryGetValue(emblem.ConfigurationId.ToString(), out var assets)
            && !string.IsNullOrWhiteSpace(assets.EmblemCmsPath)
            ? assets.EmblemCmsPath
            : null;
    }

    private async Task<bool> StoreEmblemAsync(XboxUserImage row, string path, Dictionary<string, DownloadedImage?> downloaded, XboxImagesReport report, CancellationToken ct)
    {
        if (row.Status == XboxUserImageStatus.Found && row.Content is not null && row.Source == path)
        {
            row.Error = null;
            report.Unchanged++;
            return false;
        }
        try
        {
            if (!downloaded.TryGetValue(path, out var image))
            {
                var response = await _halo.GameCmsWaypointGetImage(path);
                if (ThrottleOf(response) is { } wait)
                {
                    report.ThrottledFor = wait;
                    return true;
                }
                image = response.Result is { Length: > 0 and <= XboxUserImage.MaxContentBytes } bytes && SniffContentType(bytes) is { } type
                    ? new DownloadedImage(bytes, type)
                    : null;
                if (image is null && response.Result is null)
                    throw new InvalidOperationException("The emblem image: " + Describe(response));
                downloaded[path] = image;
            }
            if (image is null)
                throw new InvalidOperationException("The emblem image isn't a PNG, JPEG or WebP, or is too large");
            Found(row, path, image);
            report.Stored++;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Failed(row, ex.Message);
            report.Failed++;
        }
        return false;
    }

    /// <summary>The image type of the bytes by their first bytes (the gateway gives the image without its type); null for anything but PNG, JPEG or WebP.</summary>
    public static string? SniffContentType(byte[] bytes) => bytes switch
    {
        [0x89, 0x50, 0x4E, 0x47, ..] => "image/png",
        [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
        [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => "image/webp",
        _ => null,
    };
}
