using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using Surprenant.Grunt.Models.HaloInfinite;

namespace GrifballWebApp.Infrastructure.HaloInfinite;

public class XboxArmorOptions
{
    /// <summary>Whether the armor job asks Halo for anything. False leaves every account due until it is true again.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Accounts one run of the job checks (one Halo customization call each).</summary>
    public int AccountsPerRun { get; set; } = 60;
    /// <summary>At most this many customization calls a minute (the gateway's customization budget is 120, shared with the emblems).</summary>
    public int CallsPerMinute { get; set; } = 30;
    /// <summary>An account's armor is read again after this long.</summary>
    public TimeSpan CheckAgainAfter { get; set; } = TimeSpan.FromDays(7);
    /// <summary>A check that failed (the API down) is tried again after this long.</summary>
    public TimeSpan RetryFailedAfter { get; set; } = TimeSpan.FromHours(6);
}

/// <summary>What a run of the armor job did.</summary>
public sealed class XboxArmorReport
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
    /// <summary>Set when XboxArmor:Enabled is off: the run did nothing.</summary>
    public bool Disabled { get; set; }
}

/// <summary>When Halo last told the armor job to wait (a 429), kept across runs so the next run does not ask again too soon.</summary>
public sealed class XboxArmorThrottle
{
    private readonly Lock _lock = new();
    private DateTimeOffset _until;

    public TimeSpan? Remaining(DateTimeOffset now)
    {
        lock (_lock)
            return _until > now ? _until - now : null;
    }

    public void Wait(DateTimeOffset now, TimeSpan wait)
    {
        lock (_lock)
            _until = now + wait;
    }
}

/// <summary>
/// Keeps what every Xbox account that has played a Halo Infinite match has equipped (<see cref="XboxUserArmor"/>), read from
/// Halo's public customization. Accounts are taken in priority order: those who played a season match or a Late League
/// match first, the one most recently active in one first; then those with only other Infinite matches, the most recently
/// active first.
/// </summary>
public sealed class XboxArmorSource(GrifballContext context, IHaloInfiniteClient halo, IOptions<XboxArmorOptions> options, TimeProvider time, XboxArmorThrottle throttle)
{
    private readonly XboxArmorOptions _options = options.Value;

    /// <summary>Waits between two calls to Halo (the throttle); replaceable in tests.</summary>
    public Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = (delay, ct) => Task.Delay(delay, ct);

    private IQueryable<XboxUser> Due()
    {
        var now = time.GetUtcNow().UtcDateTime;
        var again = now - _options.CheckAgainAfter;
        var retry = now - _options.RetryFailedAfter;
        return context.XboxUsers
            .Where(x => x.MatchParticipants.Any())
            .Where(x => !context.XboxUserArmor.Any(a => a.XboxUserID == x.XboxUserID
                && (a.Status == XboxUserArmorStatus.Failed ? a.CheckedAt > retry : a.CheckedAt > again)));
    }

    /// <summary>The accounts due, event players before the rest, the most recently active in an event (or, for the rest, in any match) first.</summary>
    private IQueryable<long> DueInOrder()
        => Due()
            .Select(x => new
            {
                x.XboxUserID,
                LastEvent = x.MatchParticipants
                    .Where(p => p.MatchTeam.Match.SeriesGame != null || p.MatchTeam.Match.MatchedMatch != null)
                    .Max(p => p.MatchTeam.Match.StartTime),
                Last = x.MatchParticipants.Max(p => p.MatchTeam.Match.StartTime),
            })
            .OrderBy(x => x.LastEvent == null ? 1 : 0)
            .ThenByDescending(x => x.LastEvent)
            .ThenByDescending(x => x.Last)
            .ThenBy(x => x.XboxUserID)
            .Select(x => x.XboxUserID);

    public Task<int> CountDueAsync(CancellationToken ct) => Due().CountAsync(ct);

    /// <summary>One run: up to AccountsPerRun due accounts, a customization call each, in priority order.</summary>
    public async Task<XboxArmorReport> RunAsync(CancellationToken ct = default)
    {
        var report = new XboxArmorReport();
        if (!_options.Enabled)
        {
            report.Disabled = true;
            return report;
        }
        report.Due = await CountDueAsync(ct);
        if (report.Due == 0)
            return report;
        if (throttle.Remaining(time.GetUtcNow()) is { } remaining)
        {
            report.ThrottledFor = remaining;
            return report;
        }
        var due = await DueInOrder().Take(Math.Max(1, _options.AccountsPerRun)).ToListAsync(ct);

        var gap = TimeSpan.FromMinutes(1) / Math.Max(1, _options.CallsPerMinute);
        var first = true;
        foreach (var xuid in due)
        {
            if (!first)
                await Delay(gap, ct);
            first = false;
            if (await CheckAsync(xuid, report, ct))
                break;
            await context.SaveChangesAsync(ct);
        }
        return report;
    }

    /// <summary>One account. Returns true when Halo throttled the run (nothing recorded for the account).</summary>
    private async Task<bool> CheckAsync(long xuid, XboxArmorReport report, CancellationToken ct)
    {
        HaloApiResultContainer<CustomizationData, HaloApiErrorContainer> response;
        string? callError = null;
        try
        {
            response = await halo.EconomyPlayerPublicCustomization($"xuid({xuid})");
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            response = new(default!, new HaloApiErrorContainer());
            callError = ex.Message;
        }
        if (response.Error is { Code: 429 } limited)
        {
            var wait = limited.RetryAfter is { } w && w > TimeSpan.Zero ? w : TimeSpan.FromMinutes(1);
            report.ThrottledFor = wait;
            throttle.Wait(time.GetUtcNow(), wait);
            return true;
        }

        var now = time.GetUtcNow().UtcDateTime;
        var row = await context.XboxUserArmor.FirstOrDefaultAsync(a => a.XboxUserID == xuid, ct);
        if (row is null)
        {
            row = new XboxUserArmor { XboxUserID = xuid };
            context.XboxUserArmor.Add(row);
        }
        row.CheckedAt = now;
        report.Checked++;

        if (response.Result is null)
        {
            if (response.Error is { Code: 404 })
            {
                MarkNotFound(row, now);
                report.NotFound++;
            }
            else
            {
                // The set it has stays: a failed check doesn't take away what is already known, it only comes due sooner
                row.Status = XboxUserArmorStatus.Failed;
                row.Error = Truncate(callError ?? (response.Error is { Code: not 0 } e ? $"Halo answered {e.Code}" : "Halo gave no answer"));
                report.Failed++;
            }
            return false;
        }

        if (Apply(row, response.Result, now))
            report.Stored++;
        else
            report.Unchanged++;
        return false;
    }

    /// <summary>A set that was there and is gone counts as a change, so its render is refreshed.</summary>
    private static void MarkNotFound(XboxUserArmor row, DateTime now)
    {
        if (row.CoreId is not null && row.Status != XboxUserArmorStatus.NotFound)
            row.ChangedAt = now;
        row.Status = XboxUserArmorStatus.NotFound;
        row.Error = null;
    }

    private static string Truncate(string error) => error.Length > 200 ? error[..200] : error;

    /// <summary>
    /// Copies the equipped set of <paramref name="data"/> into <paramref name="row"/> and marks it found. The equipped core's
    /// equipped theme gives the pieces; the appearance gives the stance, each weapon type's equipped theme its coating. Returns whether anything changed from what the row had.
    /// </summary>
    public static bool Apply(XboxUserArmor row, CustomizationData data, DateTime now)
    {
        var core = data.ArmorCores?.ArmorCores?.FirstOrDefault(c => c.IsEquipped);
        var theme = core?.Themes?.FirstOrDefault(t => t.IsEquipped);
        var body = data.SpartanBody;
        var pose = data.Appearance;

        var before = (row.CoreId, row.CorePath, row.ThemePath, row.CoatingPath, row.HelmetPath, row.VisorPath, row.GlovePath, row.HelmetAttachmentPath,
            row.ChestAttachmentPath, row.KneePadPath, row.LeftShoulderPadPath, row.RightShoulderPadPath, row.HipAttachmentPath, row.WristAttachmentPath,
            row.ArmorFxPath, row.MythicFxPath, row.ActionPosePath, row.StancePath, row.BodyType, row.LeftArm, row.RightArm, row.LeftLeg, row.RightLeg, row.WeaponCoatingsJson);

        row.CoreId = Clean(core?.CoreId);
        row.CorePath = Clean(core?.CorePath);
        row.ThemePath = Clean(theme?.ThemePath);
        row.CoatingPath = Clean(theme?.CoatingPath);
        row.HelmetPath = Clean(theme?.HelmetPath);
        row.VisorPath = Clean(theme?.VisorPath);
        row.GlovePath = Clean(theme?.GlovePath);
        row.HelmetAttachmentPath = Clean(theme?.HelmetAttachmentPath);
        row.ChestAttachmentPath = Clean(theme?.ChestAttachmentPath);
        row.KneePadPath = Clean(theme?.KneePadPath);
        row.LeftShoulderPadPath = Clean(theme?.LeftShoulderPadPath);
        row.RightShoulderPadPath = Clean(theme?.RightShoulderPadPath);
        row.HipAttachmentPath = Clean(theme?.HipAttachmentPath);
        row.WristAttachmentPath = Clean(theme?.WristAttachmentPath);
        row.ArmorFxPath = Clean(theme?.ArmorFxPath);
        row.MythicFxPath = Clean(theme?.MythicFxPath);
        row.ActionPosePath = Clean(pose?.ActionPosePath);
        row.StancePath = Clean(pose?.StancePath);
        row.BodyType = Clean(body?.BodyType);
        row.LeftArm = Clean(body?.LeftArm);
        row.RightArm = Clean(body?.RightArm);
        row.LeftLeg = Clean(body?.LeftLeg);
        row.RightLeg = Clean(body?.RightLeg);
        row.WeaponCoatingsJson = WeaponCoatingsOf(data);

        var after = (row.CoreId, row.CorePath, row.ThemePath, row.CoatingPath, row.HelmetPath, row.VisorPath, row.GlovePath, row.HelmetAttachmentPath,
            row.ChestAttachmentPath, row.KneePadPath, row.LeftShoulderPadPath, row.RightShoulderPadPath, row.HipAttachmentPath, row.WristAttachmentPath,
            row.ArmorFxPath, row.MythicFxPath, row.ActionPosePath, row.StancePath, row.BodyType, row.LeftArm, row.RightArm, row.LeftLeg, row.RightLeg, row.WeaponCoatingsJson);

        var changed = row.Status is not (XboxUserArmorStatus.Found or XboxUserArmorStatus.Failed) || before != after;
        row.Status = XboxUserArmorStatus.Found;
        row.Error = null;
        if (changed)
            row.ChangedAt = now;
        return changed;
    }

    // Per weapon type, the equipped core's equipped theme (a type's only core is often not marked equipped), as sorted JSON
    private static string? WeaponCoatingsOf(CustomizationData data)
    {
        var coatings = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var cores in (data.WeaponCores?.WeaponCores ?? []).Where(c => !string.IsNullOrWhiteSpace(c.CoreType)).GroupBy(c => c.CoreType))
        {
            var core = cores.FirstOrDefault(c => c.IsEquipped) ?? cores.First();
            if (Clean(core.Themes?.FirstOrDefault(t => t.IsEquipped)?.CoatingPath) is { } path)
                coatings[cores.Key] = path;
        }
        return coatings.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(coatings) : null;
    }

    private static string? Clean(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        path = path.Trim();
        return path.Length > 300 ? path[..300] : path;
    }
}

/// <summary>
/// Keeps what every Xbox account that has played Halo Infinite has equipped (<see cref="XboxArmorSource.RunAsync"/>): every
/// five minutes a run takes the accounts due, event players first and the most recently active first, and reads each one's
/// equipped armor and stance from Halo. When none is due, a run is one query.
/// </summary>
[DisallowConcurrentExecution]
public sealed partial class XboxArmorJob(XboxArmorSource armor, ILogger<XboxArmorJob> logger) : IJob
{
    public static readonly JobKey Key = new("xbox-armor", RecurringJobs.Group);

    public static RecurringJob Definition => new()
    {
        Key = Key,
        Description = "Fetches and keeps the armor and stance every Xbox account has equipped in Halo Infinite, event players first",
        DefaultSchedule = (trigger, now) => trigger
            .StartAt(now + TimeSpan.FromMinutes(4))
            .WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromMinutes(5)).RepeatForever().WithMisfireInstruction(SimpleTriggerMisfireInstruction.NowWithExistingCount)),
    };

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var report = await armor.RunAsync(cancellationToken);
        context.Result = Report(report, logger);
    }

    internal static JobRunReport Report(XboxArmorReport report, ILogger logger)
    {
        if (report.ThrottledFor is { } wait)
            LogThrottled(logger, wait);
        var summary = report.Disabled ? "The armor job is turned off (XboxArmor:Enabled)"
            : report.Due == 0 ? "Every Xbox account that played Halo Infinite has its armor checked"
            : $"{report.Checked} of {report.Due} accounts checked: {report.Stored} stored, {report.Unchanged} unchanged, {report.NotFound} without one, {report.Failed} failed"
              + (report.ThrottledFor is { } w ? $"; Halo is throttling, stopped (wait {w})" : "");
        var result = report.Disabled || report.Due == 0 ? JobRunReport.Skipped(summary)
            : report.Failed > 0 && report.Stored + report.Unchanged + report.NotFound == 0 ? JobRunReport.Failed(summary)
            : JobRunReport.Succeeded(summary);
        return result.With("due", report.Due).With("checked", report.Checked).With("stored", report.Stored)
            .With("unchanged", report.Unchanged).With("notFound", report.NotFound).With("failed", report.Failed);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Xbox armor job: Halo is throttling; stopped, next run in five minutes (asked to wait {Wait})")]
    private static partial void LogThrottled(ILogger logger, TimeSpan wait);
}
