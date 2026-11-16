using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Application.Chat;

/// <summary>
/// What the chat keeps of a user (in scopes of its own, as a reply outlives its host's): the model they chose (<see cref="ChatChoice"/>)
/// and their spend on paid models this month (<see cref="ChatMonth"/>).
/// </summary>
public sealed class ChatAccounts(IServiceScopeFactory scopes, TimeProvider time, ILogger<ChatAccounts> log)
{
    /// <summary>How many times a reply's spend is saved again when another reply changed the month's row meanwhile.</summary>
    public const int SaveAttempts = 3;

    /// <summary>The model <paramref name="userId"/> chose (its ID), or null for none.</summary>
    public async Task<string?> ChoiceAsync(int userId, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return await context.ChatChoices.AsNoTracking().Where(c => c.UserID == userId).Select(c => c.Model).SingleOrDefaultAsync(ct);
    }

    /// <summary><paramref name="userId"/> chose <paramref name="model"/> (its ID): their questions go to it until they choose another.</summary>
    public async Task ChooseAsync(int userId, string model, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var choice = await context.ChatChoices.SingleOrDefaultAsync(c => c.UserID == userId, ct);
        if (choice is null)
            context.ChatChoices.Add(ChatChoice.Make(userId, model));
        else
            choice.Choose(model);
        await context.SaveChangesAsync(ct);
    }

    /// <summary><paramref name="userId"/>'s month (UTC) so far, read only: nothing spent yet when they have no row for it.</summary>
    public async Task<ChatMonth> MonthAsync(int userId, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var month = ChatMonth.MonthOf(time.GetUtcNow().UtcDateTime);
        return await context.ChatMonths.AsNoTracking().SingleOrDefaultAsync(m => m.UserID == userId && m.Month == month, ct)
            ?? ChatMonth.Begin(userId, month);
    }

    /// <summary>
    /// A paid reply's cost and tokens added to <paramref name="userId"/>'s month: saved again (from a fresh read) when another reply
    /// changed the row, or made it, meanwhile, up to <see cref="SaveAttempts"/> times.
    /// </summary>
    public async Task RecordAsync(int userId, long costMicros, long inputTokens, long outputTokens, CancellationToken ct = default)
    {
        var month = ChatMonth.MonthOf(time.GetUtcNow().UtcDateTime);
        for (var attempt = 1; attempt <= SaveAttempts; attempt++)
        {
            using var scope = scopes.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var row = await context.ChatMonths.SingleOrDefaultAsync(m => m.UserID == userId && m.Month == month, ct);
            if (row is null)
            {
                row = ChatMonth.Begin(userId, month);
                context.ChatMonths.Add(row);
            }
            row.Record(costMicros, inputTokens, outputTokens);
            try
            {
                await context.SaveChangesAsync(ct);
                return;
            }
            // Another reply's save came first (its Version, or the month's first row): add to what it saved
            catch (DbUpdateException) when (attempt < SaveAttempts)
            {
            }
        }
    }

    /// <summary>How long a change waits for the person's answer before it is dropped.</summary>
    public static readonly TimeSpan WaitingFor = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Where <paramref name="asker"/>'s waiting change on <paramref name="host"/> is kept (<see cref="IChatWaiting"/>): each host's its own, so a
    /// yes in a DM answers only what the DM showed.
    /// </summary>
    public static string WaitingKey(ChatHost host, string asker) => $"chat:waiting:{host.Name}:{asker}";

    /// <summary><paramref name="action"/> waits for <paramref name="asker"/>'s answer on <paramref name="host"/>, for <see cref="WaitingFor"/>; it replaces any before it.</summary>
    public async Task SaveWaitingAsync(ChatHost host, string asker, ChatAction action, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        await Waiting(scope).SaveAsync(WaitingKey(host, asker), action, WaitingFor, ct);
    }

    /// <summary>The change waiting for <paramref name="asker"/>'s answer on <paramref name="host"/>, left waiting; or null.</summary>
    public async Task<ChatAction?> WaitingAsync(ChatHost host, string asker, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        return await Waiting(scope).PeekAsync(WaitingKey(host, asker), ct);
    }

    /// <summary>The change waiting for <paramref name="asker"/>'s answer on <paramref name="host"/>, taken (only one answer gets it); or null.</summary>
    public async Task<ChatAction?> TakeWaitingAsync(ChatHost host, string asker, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        return await Waiting(scope).TakeAsync(WaitingKey(host, asker), ct);
    }

    /// <summary>Nothing waits for <paramref name="asker"/>'s answer on <paramref name="host"/> any more.</summary>
    public async Task ForgetWaitingAsync(ChatHost host, string asker, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        await Waiting(scope).ForgetAsync(WaitingKey(host, asker), ct);
    }

    private static IChatWaiting Waiting(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<IChatWaiting>();

    /// <summary>
    /// <see cref="RecordAsync"/>, its failure logged rather than thrown: the reply it is for has been given already, whole or not, and
    /// must stand as it was.
    /// </summary>
    public async Task TryRecordAsync(int userId, long costMicros, long inputTokens, long outputTokens)
    {
        try
        {
            await RecordAsync(userId, costMicros, inputTokens, outputTokens);
        }
        catch (Exception e)
        {
            ChatLog.SpendNotSaved(log, e, userId, costMicros);
        }
    }
}
