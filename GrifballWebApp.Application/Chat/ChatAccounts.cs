using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Application.Chat;

/// <summary>
/// What the chat keeps of a user (in scopes of its own, as a reply outlives its host's): the model they chose (<see cref="ChatChoice"/>)
/// and their spend on paid models this month (<see cref="ChatMonth"/>).
/// </summary>
public sealed class ChatAccounts(IServiceScopeFactory scopes, TimeProvider time)
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

    /// <summary><paramref name="userId"/>'s spend on paid models this month (UTC), in US dollars.</summary>
    public async Task<decimal> SpentAsync(int userId, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var month = ChatMonth.MonthOf(time.GetUtcNow().UtcDateTime);
        var spent = await context.ChatMonths.AsNoTracking().Where(m => m.UserID == userId && m.Month == month)
            .Select(m => (long?)m.CostMicros).SingleOrDefaultAsync(ct);
        return (spent ?? 0) / 1_000_000m;
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
}
