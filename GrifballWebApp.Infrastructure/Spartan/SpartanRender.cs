using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GrifballWebApp.Application.Spartan;
using GrifballWebApp.Domain.Players;
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
}

public sealed class SpartanRenderReport
{
    public int Done { get; set; }
    public int Failed { get; set; }
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
    TimeProvider time)
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
            .Where(r => r.State == UserSpartanRenderState.Requested || (r.State == UserSpartanRenderState.Rendering && r.RequestedAt < expired))
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
            if (await RenderAsync(render, catalog, ct))
                report.Done++;
            else
                report.Failed++;
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

    /// <summary>One user's render. True when its files are stored (or already were).</summary>
    private async Task<bool> RenderAsync(UserSpartanRender render, Application.Spartan.SpartanCatalog catalog, CancellationToken ct)
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
                render.Error = null;
                await context.SaveChangesAsync(ct);
                return true;
            }

            render.State = UserSpartanRenderState.Rendering;
            await context.SaveChangesAsync(ct);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(_options.Timeout);
            var files = await MakeFilesAsync(choice, stance, timeout.Token);

            var old = await context.UserSpartanFiles.Where(f => f.UserId == render.UserId).ToListAsync(ct);
            context.UserSpartanFiles.RemoveRange(old);
            foreach (var (name, file) in files)
                context.UserSpartanFiles.Add(new UserSpartanFile { UserId = render.UserId, Name = name, ContentType = file.ContentType, Content = file.Content });

            // A choice made while this rendered asks again: the next run renders it
            var askedSince = await context.UserSpartanRenders.AsNoTracking()
                .Where(r => r.UserId == render.UserId).Select(r => r.RequestedAt).FirstAsync(ct);
            render.State = askedSince > asked ? UserSpartanRenderState.Requested : UserSpartanRenderState.Done;
            render.RenderedAt = time.GetUtcNow().UtcDateTime;
            render.LoadoutKey = key;
            render.Error = null;
            await context.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception e) when (!ct.IsCancellationRequested && e is not DbUpdateException)
        {
            return await FailAsync(render, e is OperationCanceledException ? "The render took too long" : e.Message, ct);
        }
    }

    private async Task<bool> FailAsync(UserSpartanRender render, string error, CancellationToken ct)
    {
        render.State = UserSpartanRenderState.Failed;
        render.Error = error.Length > 500 ? error[..500] : error;
        await context.SaveChangesAsync(ct);
        return false;
    }

    private async Task<(SpartanChoice? Choice, int? Stance, string? Problem)> WhatToRenderAsync(int userId, Application.Spartan.SpartanCatalog catalog, CancellationToken ct)
    {
        var xuid = await context.Users.Where(u => u.Id == userId).Select(u => u.XboxUserID).FirstOrDefaultAsync(ct);
        var armor = xuid is long id ? await context.XboxUserArmor.AsNoTracking().FirstOrDefaultAsync(a => a.XboxUserID == id && a.Status == XboxUserArmorStatus.Found && a.CoreId != null, ct) : null;
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

    internal static JobRunReport Report(SpartanRenderReport report, ILogger logger)
    {
        var summary = report.Disabled ? "The Spartan render job is turned off (SpartanRender:Enabled)"
            : report.CatalogUnavailable ? "The Spartan render service is not available; the renders wait"
            : report.Waiting == 0 ? "No Spartan render is waiting"
            : $"{report.Done} rendered, {report.Failed} failed";
        if (report.CatalogUnavailable)
            logger.LogWarning("Spartan render job: the render service's catalog could not be read");
        var result = report.Disabled || report.Waiting == 0 ? JobRunReport.Skipped(summary)
            : report.CatalogUnavailable || (report.Failed > 0 && report.Done == 0) ? JobRunReport.Failed(summary)
            : JobRunReport.Succeeded(summary);
        return result.With("waiting", report.Waiting).With("done", report.Done).With("failed", report.Failed).With("marked", report.Marked);
    }
}
