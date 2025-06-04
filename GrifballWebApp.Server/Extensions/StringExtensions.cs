using GrifballWebApp.Database.Models;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server.Extensions;

public static class StringExtensions
{
    public static string RemoveXUIDWrapper(this string s)
    {
        return s.Replace("xuid(", "").Replace(")", "");
    }

    public static string AddXUIDWrapper(this string s)
    {
        return $"xuid({s})";
    }

    public static string? ToDisplayName(this MatchedPlayer player)
    {
        return player.User.ToDisplayName();
    }

    public static string? ToDisplayName(this QueuedPlayer player)
    {
        return player.User.ToDisplayName();
    }

    public static string? ToDisplayName(this User user)
    {
        return user.XboxUser?.Gamertag ?? user.DiscordUser?.DiscordUsername ?? user.DisplayName;
    }

    public static async Task TempResponse(this IInteractionContext context, string message)
    {
        await context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
        {
            Content = message,
            Flags = NetCord.MessageFlags.Ephemeral,
        }));
        await Task.Delay(5000);
        await context.Interaction.DeleteResponseAsync();
    }


    public static async Task ModifyTempResponse(this IInteractionContext context, string message)
    {
        await context.Interaction.ModifyResponseAsync(x => x.WithContent(message).WithFlags(NetCord.MessageFlags.Ephemeral));
        await Task.Delay(5000);
        await context.Interaction.DeleteResponseAsync();
    }
}
