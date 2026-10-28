using DiscordInterface.Generated;
using NetCord.Gateway;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.ComponentInteractions;

namespace DiscordInterfaces;

public static class Ext
{
    public static IDiscordInteractionContext ToDiscordContext(this IInteractionContext interactionContext)
    {
        return new DiscordInteractionContext(interactionContext);
    }

    public static IDiscordStringMenuInteractionContext ToDiscordContext(this StringMenuInteractionContext interactionContext)
    {
        return new DiscordStringMenuInteractionContext(interactionContext);
    }

    public static IDiscordButtonInteractionContext ToDiscordContext(this ButtonInteractionContext interactionContext)
    {
        return new DiscordButtonInteractionContext(interactionContext);
    }

    public static IDiscordApplicationCommandContext ToDiscordContext(this ApplicationCommandContext interactionContext)
    {
        return new DiscordApplicationCommandContext(interactionContext);
    }
}

public partial interface IDiscordStringMenuInteractionContext : IDiscordInteractionContext
{
    GatewayClient Client { get; }

    IDiscordRestMessage Message { get; }

    IDiscordUser User { get; }

    IDiscordGuild? Guild { get; }

    IDiscordTextChannel Channel { get; }

    IReadOnlyList<string> SelectedValues { get; }
}

public partial class DiscordStringMenuInteractionContext : IDiscordStringMenuInteractionContext
{
    private readonly StringMenuInteractionContext _original;

    public GatewayClient Client => _original.Client;

    public IDiscordRestMessage Message => new DiscordRestMessage(_original.Message);

    public IDiscordUser User => new DiscordUser(_original.User);

    public IDiscordGuild? Guild => _original.Guild is null ? null : new DiscordGuild(_original.Guild);

    public IDiscordTextChannel Channel => new DiscordTextChannel(_original.Channel);

    public IReadOnlyList<string> SelectedValues => _original.SelectedValues;

    public IInteractionContext Original => _original;

    public IDiscordInteraction Interaction => new DiscordInteraction(_original.Interaction);

    public DiscordStringMenuInteractionContext(StringMenuInteractionContext original)
    {
        _original = original;
    }
}

public partial interface IDiscordButtonInteractionContext : IDiscordInteractionContext
{
    GatewayClient Client { get; }

    IDiscordRestMessage Message { get; }

    IDiscordUser User { get; }

    IDiscordGuild? Guild { get; }

    IDiscordTextChannel Channel { get; }
}

public partial class DiscordButtonInteractionContext : IDiscordButtonInteractionContext
{
    private readonly ButtonInteractionContext _original;

    public GatewayClient Client => _original.Client;

    public IDiscordRestMessage Message => new DiscordRestMessage(_original.Message);

    public IDiscordUser User => new DiscordUser(_original.User);

    public IDiscordGuild? Guild => _original.Guild is null ? null : new DiscordGuild(_original.Guild);

    public IDiscordTextChannel Channel => new DiscordTextChannel(_original.Channel);

    public IInteractionContext Original => _original;

    public IDiscordInteraction Interaction => new DiscordInteraction(_original.Interaction);

    public DiscordButtonInteractionContext(ButtonInteractionContext original)
    {
        _original = original;
    }
}

public partial interface IDiscordApplicationCommandContext : IDiscordInteractionContext
{
    GatewayClient Client { get; }

    IDiscordUser User { get; }

    IDiscordGuild? Guild { get; }

    IDiscordTextChannel Channel { get; }
}

public partial class DiscordApplicationCommandContext : IDiscordApplicationCommandContext
{
    private readonly ApplicationCommandContext _original;

    public GatewayClient Client => _original.Client;

    public IDiscordUser User => new DiscordUser(_original.User);

    public IDiscordGuild? Guild => _original.Guild is null ? null : new DiscordGuild(_original.Guild);

    public IDiscordTextChannel Channel => new DiscordTextChannel(_original.Channel);

    public IInteractionContext Original => _original;

    public IDiscordInteraction Interaction => new DiscordInteraction(_original.Interaction);

    public DiscordApplicationCommandContext(ApplicationCommandContext original)
    {
        _original = original;
    }
}
