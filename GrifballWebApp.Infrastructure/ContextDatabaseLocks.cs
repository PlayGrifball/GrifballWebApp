using GrifballWebApp.Application.Database;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Infrastructure;

/// <summary>The use cases' database locks (<see cref="IDatabaseLocks"/>), on <paramref name="context"/>'s connection and transaction.</summary>
public sealed class ContextDatabaseLocks(GrifballContext context) : IDatabaseLocks
{
    public Task LockSeasonRowAsync(int seasonID, CancellationToken ct = default)
    {
        if (context.Database.IsSqlite())
            return Task.CompletedTask;
        return context.Database.IsNpgsql()
            ? context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Event\".\"Seasons\" WHERE \"SeasonID\" = {seasonID} FOR UPDATE", ct)
            : context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM [Event].[Seasons] WITH (UPDLOCK, HOLDLOCK) WHERE [SeasonID] = {seasonID}", ct);
    }
}
