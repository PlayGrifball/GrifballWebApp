using DiscordInterface.Generated;
using DiscordInterfaces;
using NetCord.Rest;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// Substitute button/string-menu contexts and the one way their handlers' tests check a reply, shared by
/// MatchmakingButtonHandlerUnitTests, SignupButtonHandlerUnitTests, MatchmakingStringMenuHandlerUnitTests and
/// DraftStringMenuHandlerUnitTests (a NetCord callback-shape change then needs fixing in one place).
/// </summary>
internal static class DiscordComponentContexts
{
    internal static IDiscordButtonInteractionContext ButtonContext(ulong discordUserId)
    {
        var context = Substitute.For<IDiscordButtonInteractionContext>();
        context.User.Id.Returns(discordUserId);
        return context;
    }

    internal static IDiscordStringMenuInteractionContext StringMenuContext(ulong discordUserId, params string[] selectedValues)
    {
        var context = Substitute.For<IDiscordStringMenuInteractionContext>();
        context.User.Id.Returns(discordUserId);
        context.SelectedValues.Returns(selectedValues);
        return context;
    }

    /// <summary>The context's interaction was replied or modified to with exactly <paramref name="expected"/>.</summary>
    internal static async Task AssertSent(IDiscordInteractionContext context, string expected)
        => await context.Interaction.Received(1).SendResponseAsync(Arg.Is<InteractionCallbackProperties>(p =>
            ((InteractionCallbackProperties<InteractionMessageProperties>)p).Data.Content == expected));
}
