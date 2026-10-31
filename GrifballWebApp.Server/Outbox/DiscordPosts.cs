using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using System.Net;
using System.Text.Json;

namespace GrifballWebApp.Server.Outbox;

/// <summary>What outbox handlers use to find what an earlier try (or an earlier message of the group) already did.</summary>
public static class DiscordPosts
{
    /// <summary>The bot's latest message in <paramref name="channelId"/> matching <paramref name="match"/>, among the last <paramref name="count"/>.</summary>
    public static async Task<IDiscordRestMessage?> FindBotPostAsync(this IDiscordRestClient discord, ulong channelId, Func<IDiscordRestMessage, bool> match, CancellationToken ct, int count = 50)
    {
        var me = await discord.GetCurrentUserAsync(null, ct);
        var messages = await discord.GetMessagesAsync(channelId, new PaginationProperties<ulong>()
        {
            BatchSize = count,
        }).Take(count).ToListAsync(ct);
        return messages
            .Where(x => x.Author.Id == me.Id)
            .FirstOrDefault(match);
    }

    /// <summary>The result of the group's latest sent message of <paramref name="kind"/>, if any.</summary>
    public static async Task<TResult?> SentResultAsync<TResult>(this GrifballContext context, string groupKey, string kind, CancellationToken ct)
        where TResult : class
    {
        var result = await context.Outbox
            .Where(x => x.GroupKey == groupKey && x.Kind == kind && x.Status == OutboxStatus.Sent && x.Result != null)
            .OrderByDescending(x => x.Sequence)
            .Select(x => x.Result)
            .FirstOrDefaultAsync(ct);
        return result is null ? null : JsonSerializer.Deserialize<TResult>(result, OutboxJson.Options);
    }

    public static bool IsNotFound(Exception ex) => ex is RestException { StatusCode: HttpStatusCode.NotFound };
}
