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
    XboxArmorThrottle throttle)
{
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
        foreach (var render in due)
        {
            switch (await RenderAsync(render, catalog, ct))
            {
                case Outcome.Done: report.Done++; break;
                case Outcome.Retry: report.Retrying++; break;
                default: report.Failed++; break;
            }
        }
        return report;
    }

    /// <summary>A render of the equipped set is asked for again when the armor job saw the set change since it.</summary>
    private async Task<int> AskAgainForChangedSetsAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var stale = await context.UserSpartanRenders
            .Where(r => r.State == UserSpartanRenderState.Done && r.RenderedAt != null)
            .Where(r => !context.UserSpartans.Any(s => s.UserId == r.UserId))
            .Where(r => context.Users.Any(u => u.Id == r.UserId
                && context.XboxUserArmor.Any(a => a.XboxUserID == u.XboxUserID && a.ChangedAt > r.RenderedAt)))
            .ToListAsync(ct);
        foreach (var render in stale)
        {
            render.State = UserSpartanRenderState.Requested;
            render.RequestedAt = now;
        }
        if (stale.Count > 0)
            await context.SaveChangesAsync(ct);
        return stale.Count;
    }

    private enum Outcome { Done, Failed, Retry }

    /// <summary>One user's render: done when its files are stored (or already were).</summary>
    private async Task<Outcome> RenderAsync(UserSpartanRender render, Application.Spartan.SpartanCatalog catalog, CancellationToken ct)
    {
        var asked = render.RequestedAt;
        try
        {
            var (choice, stance, problem) = await WhatToRenderAsync(render.UserId, catalog, ct);
            if (choice is null)
                return await FailAsync(render, problem ?? "There is no Spartan to render", ct);

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
            await context.UserSpartanFiles.Where(f => f.UserId == render.UserId).ExecuteDeleteAsync(ct);
            foreach (var tracked in context.ChangeTracker.Entries<UserSpartanFile>().Where(e => e.Entity.UserId == render.UserId).ToList())
                tracked.State = EntityState.Detached;
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
        catch (HttpRequestException e) when (!ct.IsCancellationRequested)
        {
            return await RetryLaterAsync(render, e.Message, ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested && e is not DbUpdateException)
        {
            return await FailAsync(render, e is OperationCanceledException ? "The render took too long" : e.Message, ct);
        }
    }

    // A passing problem is not the user's to fix: the render waits its turn again instead of being marked failed
    private async Task<Outcome> RetryLaterAsync(UserSpartanRender render, string error, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var wait = throttle.Remaining(now) is { } remaining && remaining > _options.RetryAfter ? remaining : _options.RetryAfter;
        render.State = UserSpartanRenderState.Requested;
        render.RequestedAt = now.UtcDateTime + wait;
        render.Error = error.Length > 500 ? error[..500] : error;
        await context.SaveChangesAsync(ct);
        return Outcome.Retry;
    }

    private async Task<Outcome> FailAsync(UserSpartanRender render, string error, CancellationToken ct)
    {
        render.State = UserSpartanRenderState.Failed;
        render.Error = error.Length > 500 ? error[..500] : error;
        await context.SaveChangesAsync(ct);
        return Outcome.Failed;
    }

    private async Task<(SpartanChoice? Choice, int? Stance, string? Problem)> WhatToRenderAsync(int userId, Application.Spartan.SpartanCatalog catalog, CancellationToken ct)
    {
        var xuid = await context.Users.Where(u => u.Id == userId).Select(u => u.XboxUserID).FirstOrDefaultAsync(ct);
        var armor = xuid is long id ? await context.XboxUserArmor.AsNoTracking().FirstOrDefaultAsync(a => a.XboxUserID == id && a.Status != XboxUserArmorStatus.NotFound && a.CoreId != null, ct) : null;
        var json = await context.UserSpartans.Where(s => s.UserId == userId).Select(s => s.ChoiceJson).FirstOrDefaultAsync(ct);

        var equipped = armor is null ? null : await mapper.Map(armor, catalog, ct);
        if (json is null)
        {
            return equipped is null
                ? (null, null, armor is null ? "No equipped Spartan is known for this account yet" : "The render service does not have the equipped core yet")
                : (equipped.Choice, equipped.Stance, null);
        }
        var choice = JsonSerializer.Deserialize<SpartanChoice>(json, SpartanService.Json)!;
        var problems = SpartanChoiceValidator.Validate(choice, catalog);
        return problems.Count > 0 ? (null, null, string.Join("; ", problems)) : (choice, equipped?.Stance, null);
    }

    private async Task<Dictionary<string, SpartanFileDto>> MakeFilesAsync(SpartanChoice choice, int? stance, CancellationToken ct)
    {
        var armor = choice.Armor?.ToDictionary(p => p.Key, p => p.Value) ?? [];
        var parts = (choice.Parts ?? []).Select(p => new SpartanPart(p.Region, p.Permutation)).ToList();
        var files = new Dictionary<string, SpartanFileDto>();
        if (stance is int id)
        {
            var set = await client.SubmitSetAsync(new SpartanSetRequest { Core = choice.Core, Armor = armor, Parts = parts, Coating = choice.Coating, Stance = id }, ct);
            set = await client.WaitAsync(set, TimeSpan.FromSeconds(5), ct);
            if (set.IsFailed)
                throw new InvalidOperationException("The render service could not render this Spartan");
            foreach (var item in set.Items)
                files[item.Name] = new SpartanFileDto(ContentTypeOf(item.Output), await client.GetFileAsync(item, ct));
            return files;
        }
        foreach (var (name, output, framing) in new[] { ("neutral-glb", "glb", "body"), ("neutral-body", "png", "body"), ("neutral-head", "png", "head") })
        {
            var bytes = await client.RenderAsync(new SpartanLoadout
            {
                Core = choice.Core, Armor = armor, Parts = parts, Coating = choice.Coating, Output = output, Framing = framing,
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
        return report.Retrying > 0 ? $"{summary}, {report.Retrying} to try again" : summary;
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
