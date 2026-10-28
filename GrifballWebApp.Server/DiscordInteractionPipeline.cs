using GrifballWebApp.Database;
using GrifballWebApp.Database.Services;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server;

/// <summary>
/// Discord equivalent of <see cref="SignalR.UserContextHubFilter"/> and <see cref="SignalR.ExceptionLogHubFilter"/>.
/// Hooked into NetCord's pre-execution and result handlers so every slash command, button, string menu and modal
/// runs with the current user set for audit columns and gets a generic reply when it throws.
/// </summary>
public static class DiscordInteractionPipeline
{
    public const string GenericErrorMessage = "Something went wrong. Please contact a sysadmin.";

    // Discord error code for responding to an interaction that was already responded to or deferred
    private const int InteractionAlreadyAcknowledged = 40060;

    public static void Use<TInteraction, TContext>(ApplicationCommandServiceOptions<TInteraction, TContext> options)
        where TInteraction : ApplicationCommandInteraction
        where TContext : IApplicationCommandContext
    {
        var handler = new ApplicationCommandPipeline<TContext>();
        options.PreExecutionHandler = handler;
        options.ResultHandler = handler;
    }

    public static void Use<TInteraction, TContext>(ComponentInteractionServiceOptions<TInteraction, TContext> options)
        where TInteraction : ComponentInteraction
        where TContext : IComponentInteractionContext
    {
        var handler = new ComponentInteractionPipeline<TContext>();
        options.PreExecutionHandler = handler;
        options.ResultHandler = handler;
    }

    /// <summary>
    /// Sets the current user in the interaction's DI scope (the one the module is resolved from) to the app user linked to the Discord user.
    /// An unlinked Discord user leaves it unset, so audit columns stay null.
    /// </summary>
    public static async ValueTask<PreExecutionResult> SetCurrentUserAsync(Interaction interaction, ILogger logger, IServiceProvider services)
    {
        await SetCurrentUserAsync(interaction.User.Id, logger, services);
        return PreExecutionResult.Continue;
    }

    /// <summary>
    /// Sets the current user in <paramref name="services"/> (a DI scope) to the app user linked to <paramref name="discordId"/>, if any.
    /// A failed lookup is logged, not thrown.
    /// </summary>
    public static async Task SetCurrentUserAsync(ulong discordId, ILogger logger, IServiceProvider services)
    {
        var discordUserId = (long)discordId;
        try
        {
            var context = services.GetRequiredService<GrifballContext>();
            var userId = await context.Users
                .AsNoTracking()
                .Where(x => x.DiscordUserID == discordUserId)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync();
            if (userId.HasValue)
                services.GetRequiredService<ICurrentUserService>().SetCurrentUserId(userId.Value);
        }
        catch (Exception ex)
        {
            // Still run the interaction (or answer the message): a failed lookup should cost the audit user, not the reply
            logger.LogError(ex, "Failed to look up the app user for Discord user {DiscordUserId}", discordUserId);
        }
    }

    /// <summary>
    /// Replaces NetCord's default result handler: exceptions are logged and the user gets a generic message
    /// instead of the raw exception message. Other failures (unknown command, bad arguments) reply with NetCord's message as before.
    /// </summary>
    public static async ValueTask HandleResultAsync(IExecutionResult result, Interaction interaction, string name, ILogger logger)
    {
        if (result is not IFailResult failResult)
            return;

        InteractionMessageProperties message;
        if (failResult is IExceptionResult exceptionResult)
        {
            logger.LogError(exceptionResult.Exception, "Discord interaction {InteractionName} failed for Discord user {DiscordUserId}", name, interaction.User.Id);
            message = new() { Content = GenericErrorMessage, Flags = MessageFlags.Ephemeral };
        }
        else
        {
            logger.LogDebug("Discord interaction {InteractionName} failed with '{Message}'", name, failResult.Message);
            message = new() { Content = failResult.Message };
        }

        try
        {
            await interaction.SendResponseAsync(InteractionCallback.Message(message));
        }
        catch (RestException ex) when (ex.Error?.Code == InteractionAlreadyAcknowledged)
        {
            // The module already responded or deferred before failing, so the initial response is used up
            await interaction.SendFollowupMessageAsync(message);
        }
    }
}

public sealed class ApplicationCommandPipeline<TContext> : IApplicationCommandPreExecutionHandler<TContext>, IApplicationCommandResultHandler<TContext>
    where TContext : IApplicationCommandContext
{
    public ValueTask<PreExecutionResult> HandleAsync(TContext context, GatewayClient? client, ILogger logger, IServiceProvider services)
        => DiscordInteractionPipeline.SetCurrentUserAsync(context.Interaction, logger, services);

    public ValueTask HandleResultAsync(IExecutionResult result, TContext context, GatewayClient? client, ILogger logger, IServiceProvider services)
        => DiscordInteractionPipeline.HandleResultAsync(result, context.Interaction, context.Interaction.Data.Name, logger);
}

public sealed class ComponentInteractionPipeline<TContext> : IComponentInteractionPreExecutionHandler<TContext>, IComponentInteractionResultHandler<TContext>
    where TContext : IComponentInteractionContext
{
    public ValueTask<PreExecutionResult> HandleAsync(TContext context, GatewayClient? client, ILogger logger, IServiceProvider services)
        => DiscordInteractionPipeline.SetCurrentUserAsync(context.Interaction, logger, services);

    public ValueTask HandleResultAsync(IExecutionResult result, TContext context, GatewayClient? client, ILogger logger, IServiceProvider services)
        => DiscordInteractionPipeline.HandleResultAsync(result, context.Interaction, context.Interaction.Data.CustomId, logger);
}
