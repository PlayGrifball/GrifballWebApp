using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GrifballWebApp.Application.Spartan;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure.HaloInfinite;
using GrifballWebApp.Infrastructure.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlayGrifball.InfiniteSpartanRender.Client;
using Quartz;

namespace GrifballWebApp.Infrastructure.Spartan;

public class SpartanRenderOptions
{
    /// <summary>Whether the render job renders anything.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Spartans one run renders (a render takes minutes).</summary>
    public int UsersPerRun { get; set; } = 2;
    /// <summary>A render that takes longer than this fails.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);
    /// <summary>A render the job began but never finished (a restart) is asked for again after this long.</summary>
    public TimeSpan RenderingExpiresAfter { get; set; } = TimeSpan.FromHours(2);
    /// <summary>A render that hit a passing problem (Halo or the render service not answering) is tried again after this long.</summary>
    public TimeSpan RetryAfter { get; set; } = TimeSpan.FromMinutes(10);
}

public sealed class SpartanRenderReport
{
    public int Done { get; set; }
    public int Failed { get; set; }
    public int Retrying { get; set; }
    public int Marked { get; set; }
    public bool Disabled { get; set; }
    public bool CatalogUnavailable { get; set; }
    public int Waiting { get; set; }
    /// <summary>One line per user a run rendered for: whose render it was and how it went.</summary>
    public List<string> Lines { get; } = [];
}

/// <summary>
/// Makes the renders users asked for (their choice made, their equipped set changed, or their own request): the Spartan is the
/// user's choice or else the set they have equipped, rendered by the Spartan render service in the stance they have equipped and
/// in the neutral one, and the files are kept (<see cref="UserSpartanFile"/>).
/// </summary>
public sealed class SpartanRenderSource(
    GrifballContext context,
    SpartanArmorMapper mapper,
    ISpartanCatalogSource catalogs,
    InfiniteSpartanRenderClient client,
    IOptions<SpartanRenderOptions> options,
    TimeProvider time,
    XboxArmorThrottle throttle,
    ILogger<SpartanRenderSource> logger)
{
    /// <summary>Users who show their equipped set and have no render row yet are queued this many at a time.</summary>
    private const int NewRendersPerRun = 100;

    private readonly SpartanRenderOptions _options = options.Value;

    public async Task<SpartanRenderReport> RunAsync(CancellationToken ct = default)
    {
        var report = new SpartanRenderReport();
        if (!_options.Enabled)
        {
            report.Disabled = true;
            return report;
        }
        var now = time.GetUtcNow().UtcDateTime;
        report.Marked = await AskAgainForChangedSetsAsync(ct);

        var expired = now - _options.RenderingExpiresAfter;
        var due = await context.UserSpartanRenders
            .Where(r => (r.State == UserSpartanRenderState.Requested && r.RequestedAt <= now) || (r.State == UserSpartanRenderState.Rendering && r.RequestedAt < expired))
            .OrderBy(r => r.RequestedAt)
            .Take(Math.Max(1, _options.UsersPerRun))
            .ToListAsync(ct);
        report.Waiting = due.Count;
        if (due.Count == 0)
            return report;

        var catalog = await catalogs.Get(ct);
        if (catalog is null)
        {
            report.CatalogUnavailable = true;
            return report;
        }
        var dueIds = due.Select(r => r.UserId).ToList();
        var names = await context.Users.Where(u => dueIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        foreach (var render in due)
        {
            _lastError = null;
            var outcome = await RenderAsync(render, catalog, ct);
            var error = Shorten(_lastError ?? "no reason given", LineErrorLength);
            switch (outcome)
            {
                case Outcome.Done: report.Done++; break;
                case Outcome.Retry: report.Retrying++; break;
                default: report.Failed++; break;
            }
            var who = $"{names.GetValueOrDefault(render.UserId) ?? "User"} (#{render.UserId})";
            report.Lines.Add(outcome switch
            {
                Outcome.Done => $"{who}: rendered",
                Outcome.Retry => $"{who}: will try again, {error}",
                _ => $"{who}: failed, {error}",
            });
        }
        return report;
    }

    private const int LineErrorLength = 120;

    /// <summary>The text of the last failure or retry a render recorded, read for the run's report (the row can be stale after a re-request).</summary>
    private string? _lastError;

    internal static string Shorten(string text, int length) => text.Length <= length ? text : text[..(length - 1)] + "…";

    /// <summary>
    /// Renders are asked for the users who show their equipped set and have none yet, and again when the armor job saw the set
    /// change since the last one (a failed render too: its problem may have been the set).
    /// </summary>
    private async Task<int> AskAgainForChangedSetsAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var missing = await context.Users
            .Where(u => u.XboxUserID != null && !context.UserSpartanRenders.Any(r => r.UserId == u.Id)
                && context.XboxUserArmor.Any(a => a.XboxUserID == u.XboxUserID && a.Status != XboxUserArmorStatus.NotFound && a.CoreId != null))
            .OrderBy(u => u.Id)
            .Select(u => u.Id)
            .Take(NewRendersPerRun)
            .ToListAsync(ct);
        foreach (var userId in missing)
            context.UserSpartanRenders.Add(new UserSpartanRender { UserId = userId, State = UserSpartanRenderState.Requested, RequestedAt = now });

        var stale = await context.UserSpartanRenders
            .Where(r => !context.UserSpartans.Any(s => s.UserId == r.UserId))
            .Where(r => (r.State == UserSpartanRenderState.Done && r.RenderedAt != null
                    && context.Users.Any(u => u.Id == r.UserId && context.XboxUserArmor.Any(a => a.XboxUserID == u.XboxUserID && a.ChangedAt > r.RenderedAt)))
                || (r.State == UserSpartanRenderState.Failed
                    && context.Users.Any(u => u.Id == r.UserId && context.XboxUserArmor.Any(a => a.XboxUserID == u.XboxUserID
                        && a.Status != XboxUserArmorStatus.NotFound && a.CoreId != null && a.ChangedAt > r.RequestedAt))))
            .ToListAsync(ct);
        foreach (var render in stale)
        {
            render.State = UserSpartanRenderState.Requested;
            render.RequestedAt = now;
        }
        if (missing.Count + stale.Count > 0)
            await context.SaveChangesAsync(ct);
        return missing.Count + stale.Count;
    }

    private enum Outcome { Done, Failed, Retry }

    /// <summary>One user's render: done when its files are stored (or already were).</summary>
    private async Task<Outcome> RenderAsync(UserSpartanRender render, Application.Spartan.SpartanCatalog catalog, CancellationToken ct)
    {
        var asked = render.RequestedAt;
        try
        {
            return await MakeAsync(render, asked, catalog, ct);
        }
        catch (HttpRequestException e) when (!ct.IsCancellationRequested)
        {
            Throttle((e as HaloAnswerException)?.Wait);
            return await RetryLaterAsync(render, asked, e.Message, ct);
        }
        catch (SpartanRenderException e) when (!ct.IsCancellationRequested && (int)e.StatusCode is 429 or >= 500)
        {
            Throttle(e.RetryAfter);
            logger.LogWarning(e, "The render service could not render for user {UserId} just now", render.UserId);
            return await RetryLaterAsync(render, asked, "The render service is not answering", ct);
        }
        catch (DbUpdateException e)
        {
            logger.LogWarning(e, "The files of user {UserId}'s render could not be saved", render.UserId);
            // What did not save must not ride along with the next user's save, and this render must not stay Rendering
            foreach (var entry in context.ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList())
                entry.State = EntityState.Detached;
            return await RetryLaterAsync(render, asked, "The render could not be saved", ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(e, "The render for user {UserId} failed", render.UserId);
            return await FailAsync(render, asked, FailureText(e), ct);
        }
    }

    private void Throttle(TimeSpan? wait)
    {
        if (wait is { } w)
            throttle.Wait(time.GetUtcNow(), w);
    }

    private static string FailureText(Exception e) => e switch
    {
        OperationCanceledException => "The render took too long",
        SpartanRenderException or InvalidOperationException => e.Message,
        _ => "The render failed",
    };

    private async Task<Outcome> MakeAsync(UserSpartanRender render, DateTime asked, Application.Spartan.SpartanCatalog catalog, CancellationToken ct)
    {
        var (choice, stance, problem, passing) = await WhatToRenderAsync(render.UserId, catalog, ct);
        if (choice is null)
            return await NothingToRenderAsync(render, asked, problem ?? "There is no Spartan to render", passing, ct);

        var key = KeyOf(choice, stance);
        var have = await context.UserSpartanFiles.CountAsync(f => f.UserId == render.UserId, ct);
        if (render.LoadoutKey == key && have == (stance is null ? 3 : 6))
        {
            render.State = UserSpartanRenderState.Done;
            render.RenderedAt = time.GetUtcNow().UtcDateTime;
            render.Error = null;
            await context.SaveChangesAsync(ct);
            return Outcome.Done;
        }

        render.State = UserSpartanRenderState.Rendering;
        await context.SaveChangesAsync(ct);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_options.Timeout);
        var files = await MakeFilesAsync(choice, stance, timeout.Token);

        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        await DeleteFilesAsync(render.UserId, ct);
        foreach (var (name, file) in files)
            context.UserSpartanFiles.Add(new UserSpartanFile { UserId = render.UserId, Name = name, ContentType = file.ContentType, Content = file.Content });
        await context.SaveChangesAsync(ct);

        // A choice made while this rendered asks again: the update only finishes the request this render began from
        var renderedAt = time.GetUtcNow().UtcDateTime;
        await context.UserSpartanRenders.Where(r => r.UserId == render.UserId).ExecuteUpdateAsync(u => u
            .SetProperty(r => r.State, r => r.RequestedAt == asked ? UserSpartanRenderState.Done : UserSpartanRenderState.Requested)
            .SetProperty(r => r.RenderedAt, renderedAt)
            .SetProperty(r => r.LoadoutKey, key)
            .SetProperty(r => r.Error, (string?)null), ct);
        await transaction.CommitAsync(ct);
        await context.Entry(render).ReloadAsync(ct);
        return Outcome.Done;
    }

    private async Task<Outcome> NothingToRenderAsync(UserSpartanRender render, DateTime asked, string problem, bool passing, CancellationToken ct)
    {
        if (passing)
            return await RetryLaterAsync(render, asked, problem, ct);
        // Without a choice or a known set the pictures shown would be an old Spartan's
        if (!await context.UserSpartans.AnyAsync(s => s.UserId == render.UserId, ct))
            await DeleteFilesAsync(render.UserId, ct);
        return await FailAsync(render, asked, problem, ct);
    }

    private async Task DeleteFilesAsync(int userId, CancellationToken ct)
    {
        await context.UserSpartanFiles.Where(f => f.UserId == userId).ExecuteDeleteAsync(ct);
        foreach (var tracked in context.ChangeTracker.Entries<UserSpartanFile>().Where(e => e.Entity.UserId == userId).ToList())
            tracked.State = EntityState.Detached;
    }

    // A passing problem is not the user's to fix: the render waits its turn again instead of being marked failed. Like Fail, it
    // only settles the request this render began from: one made while it ran stands.
    private async Task<Outcome> RetryLaterAsync(UserSpartanRender render, DateTime asked, string error, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var until = now.UtcDateTime + (throttle.Remaining(now) is { } remaining && remaining > _options.RetryAfter ? remaining : _options.RetryAfter);
        var text = Cut(error);
        _lastError = text;
        await context.UserSpartanRenders.Where(r => r.UserId == render.UserId && r.RequestedAt == asked)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.State, UserSpartanRenderState.Requested)
                .SetProperty(r => r.RequestedAt, until)
                .SetProperty(r => r.Error, text), ct);
        await ReloadAsync(render, ct);
        return Outcome.Retry;
    }

    private async Task<Outcome> FailAsync(UserSpartanRender render, DateTime asked, string error, CancellationToken ct)
    {
        var text = Cut(error);
        _lastError = text;
        await context.UserSpartanRenders.Where(r => r.UserId == render.UserId && r.RequestedAt == asked)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.State, UserSpartanRenderState.Failed)
                .SetProperty(r => r.Error, text), ct);
        await ReloadAsync(render, ct);
        return Outcome.Failed;
    }

    private static string Cut(string error) => error.Length > 500 ? error[..500] : error;

    private async Task ReloadAsync(UserSpartanRender render, CancellationToken ct)
    {
        if (context.Entry(render).State != EntityState.Detached)
            await context.Entry(render).ReloadAsync(ct);
    }

    private sealed record Plan(SpartanChoice? Choice, int? Stance, string? Problem = null, bool Passing = false);

    private async Task<Plan> WhatToRenderAsync(int userId, Application.Spartan.SpartanCatalog catalog, CancellationToken ct)
    {
        var xuid = await context.Users.Where(u => u.Id == userId).Select(u => u.XboxUserID).FirstOrDefaultAsync(ct);
        var armor = xuid is long id ? await context.XboxUserArmor.AsNoTracking().FirstOrDefaultAsync(a => a.XboxUserID == id && a.Status != XboxUserArmorStatus.NotFound && a.CoreId != null, ct) : null;
        var json = await context.UserSpartans.Where(s => s.UserId == userId).Select(s => s.ChoiceJson).FirstOrDefaultAsync(ct);

        if (json is null)
        {
            if (armor is null)
                return new(null, null, "No equipped Spartan is known for this account yet");
            var equipped = await mapper.Map(armor, catalog, ct);
            return equipped is null
                ? new(null, null, "The render service does not have the equipped core yet", Passing: true)
                : new(equipped.Choice, equipped.Stance);
        }
        var choice = JsonSerializer.Deserialize<SpartanChoice>(json, SpartanService.Json)!;
        var problems = SpartanChoiceValidator.Validate(choice, catalog);
        return problems.Count > 0 ? new(null, null, string.Join("; ", problems)) : new(choice, await StanceOfAsync(armor, ct));
    }

    // The user's own Spartan needs nothing from Halo but the stance they have equipped: without it, only the neutral pose is made
    private async Task<int?> StanceOfAsync(XboxUserArmor? armor, CancellationToken ct)
    {
        if (armor is null)
            return null;
        try
        {
            return await mapper.StanceOf(armor, ct);
        }
        catch (HttpRequestException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task<Dictionary<string, SpartanFileDto>> MakeFilesAsync(SpartanChoice choice, int? stance, CancellationToken ct)
    {
        var armor = choice.Armor?.ToDictionary(p => p.Key, p => p.Value) ?? [];
        var parts = (choice.Parts ?? []).Select(p => new SpartanPart(p.Region, p.Permutation)).ToList();
        var files = new Dictionary<string, SpartanFileDto>();
        if (stance is int id)
        {
            var set = await client.SubmitSetAsync(new SpartanSetRequest { Core = choice.Core, Armor = armor, Parts = parts, Coating = choice.Coating, Visor = choice.Visor, Stance = id }, ct);
            set = await client.WaitAsync(set, TimeSpan.FromSeconds(5), ct);
            if (set.IsFailed)
                throw new InvalidOperationException("The render service could not render this Spartan");
            foreach (var item in set.Items)
                files[item.Name] = new SpartanFileDto(ContentTypeOf(item.Output), await client.GetFileAsync(item, ct));
            return files;
        }
        foreach (var (name, output, framing) in new[] { ("glb", "glb", "body"), ("neutral-body", "png", "body"), ("neutral-head", "png", "head") })
        {
            var bytes = await client.RenderAsync(new SpartanLoadout
            {
                Core = choice.Core, Armor = armor, Parts = parts, Coating = choice.Coating, Visor = choice.Visor, Output = output, Framing = framing,
            }, TimeSpan.FromSeconds(5), ct);
            files[name] = new SpartanFileDto(ContentTypeOf(output), bytes);
        }
        return files;
    }

    private static string ContentTypeOf(string output) => output == "glb" ? "model/gltf-binary" : "image/png";

    internal static string KeyOf(SpartanChoice choice, int? stance)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            choice.Core,
            Armor = (choice.Armor ?? new Dictionary<string, string>()).OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"),
            Parts = (choice.Parts ?? []).OrderBy(p => p.Region).Select(p => $"{p.Region}:{p.Permutation}"),
            choice.Coating,
            choice.Visor,
            Stance = stance,
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

/// <summary>
/// Renders the Spartans users asked for (<see cref="SpartanRenderSource.RunAsync"/>): every two minutes a run takes the oldest
/// asked for and has the render service make them. When none is waiting, a run is one query.
/// </summary>
[DisallowConcurrentExecution]
public sealed class SpartanRenderJob(SpartanRenderSource renders, ILogger<SpartanRenderJob> logger) : IJob
{
    public static readonly JobKey Key = new("spartan-render", RecurringJobs.Group);

    public static RecurringJob Definition => new()
    {
        Key = Key,
        Description = "Renders the Spartans users asked for (their own choice or their equipped set) with the Spartan render service and keeps the pictures and models",
        DefaultSchedule = (trigger, now) => trigger
            .StartAt(now + TimeSpan.FromMinutes(5))
            .WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromMinutes(2)).RepeatForever().WithMisfireInstruction(SimpleTriggerMisfireInstruction.NowWithExistingCount)),
    };

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var report = await renders.RunAsync(cancellationToken);
        context.Result = Report(report, logger);
    }

    private static string RunSummary(SpartanRenderReport report)
    {
        var summary = $"{report.Done} rendered, {report.Failed} failed";
        if (report.Retrying > 0)
            summary += $", {report.Retrying} to try again";
        return report.Lines.Count == 0 ? summary : SpartanRenderSource.Shorten(summary + ". " + string.Join("; ", report.Lines), 1000);
    }

    internal static JobRunReport Report(SpartanRenderReport report, ILogger logger)
    {
        var summary = report.Disabled ? "The Spartan render job is turned off (SpartanRender:Enabled)"
            : report.CatalogUnavailable ? "The Spartan render service is not available; the renders wait"
            : report.Waiting == 0 ? "No Spartan render is waiting"
            : RunSummary(report);
        if (report.CatalogUnavailable)
            logger.LogWarning("Spartan render job: the render service's catalog could not be read");
        var result = report.Disabled || report.Waiting == 0 ? JobRunReport.Skipped(summary)
            : report.CatalogUnavailable || (report.Failed + report.Retrying > 0 && report.Done == 0) ? JobRunReport.Failed(summary)
            : JobRunReport.Succeeded(summary);
        return result.With("waiting", report.Waiting).With("done", report.Done).With("failed", report.Failed).With("retrying", report.Retrying).With("marked", report.Marked);
    }
}
