using DiscordInterface.Generated;
using NetCord.Rest;

namespace GrifballWebApp.Server.Extensions;

/// <summary>The Discord interactions' replies: ephemeral, and temporary (deleted after <see cref="TempResponseLifetime"/>).</summary>
public static class InteractionResponseExtensions
{
    /// <summary>
    /// How long a temporary response (TempResponse, ModifyTempResponse) stays before it's deleted. The
    /// tests set it to nothing (SetUpFixture): each one that reached a temporary response waited it out.
    /// </summary>
    public static TimeSpan TempResponseLifetime { get; set; } = TimeSpan.FromSeconds(5);

    public static async Task EphemeralResponse(this IDiscordInteractionContext context, string message)
    {
        await context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
        {
            Content = message,
            Flags = NetCord.MessageFlags.Ephemeral,
        }));
    }

    public static async Task TempResponse(this IDiscordInteractionContext context, string message)
    {
        await context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
        {
            Content = message,
            Flags = NetCord.MessageFlags.Ephemeral,
        }));
        await Task.Delay(TempResponseLifetime);
        await context.Interaction.DeleteResponseAsync();
    }

    public static async Task ModifyTempResponse(this IDiscordInteractionContext context, string message)
    {
        await context.Interaction.ModifyResponseAsync(x => x.WithContent(message).WithFlags(NetCord.MessageFlags.Ephemeral));
        await Task.Delay(TempResponseLifetime);
        await context.Interaction.DeleteResponseAsync();
    }
}
