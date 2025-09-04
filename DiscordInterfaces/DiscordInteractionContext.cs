using DiscordInterface.Generated;
using NetCord.Services;

namespace DiscordInterfaces;

public static class Ext
{
    public static IDiscordInteractionContext ToDiscordContext(this IInteractionContext interactionContext)
    {
        return new DiscordInteractionContext(interactionContext);
    }
}