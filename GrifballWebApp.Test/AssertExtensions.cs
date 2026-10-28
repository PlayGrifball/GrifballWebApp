using DiscordInterface.Generated;
using NetCord.Rest;
using NSubstitute;

namespace GrifballWebApp.Test;
internal static class AssertExtensions
{
    internal static async Task AssertSendResponse(this IDiscordInteractionContext context, string message)
    {
        await context.Interaction.Received(1).SendResponseAsync(Arg.Is<InteractionCallbackProperties<InteractionMessageProperties>>(x => x.Data.Content == message), Arg.Any<bool>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }
}
