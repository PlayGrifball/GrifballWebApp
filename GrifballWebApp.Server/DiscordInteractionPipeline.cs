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
    /// Sets the current user to the app user linked to the Discord user, in the interaction's DI scope (the one the module is
    /// resolved from) and as the ambient user (<see cref="IAmbientCurrentUser"/>, for IDbContextFactory contexts and other scopes).
    /// An unlinked Discord user leaves it unset, so audit columns stay null. <see cref="EndCurrentUser"/> clears the ambient user.
    /// </summary>
    /// <remarks>
    /// Deliberately not async. NetCord 1.0.0-beta.28 (ApplicationCommandInteractionHandler and ComponentInteractionHandler,
    /// HandleInteractionAsyncCore) awaits the pre-execution handler, then the module, then the result handler, all from that one
    /// async method. An AsyncLocal assigned inside an async method is undone when the method returns, so a user assigned after the
    /// lookup's await would never reach the module. Begin runs synchronously here, still in HandleInteractionAsyncCore's own flow,
    /// so its slot encloses the module call; the async lookup then fills the slot in.
    /// </remarks>
    public static ValueTask<PreExecutionResult> SetCurrentUserAsync(Interaction interaction, ILogger logger, IServiceProvider services)
    {
        services.GetRequiredService<IAmbientCurrentUser>().Begin();
        return Continue(SetCurrentUserAsync(interaction.User.Id, logger, services));

        static async ValueTask<PreExecutionResult> Continue(Task setCurrentUser)
        {
            await setCurrentUser;
            return PreExecutionResult.Continue;
        }
    }

    /// <summary>
    /// Clears the ambient user once the module has run, so nothing the interaction left running (or a pooled execution
    /// context) keeps it. Called first, synchronously, by the result handlers.
    /// </summary>
    public static void EndCurrentUser(IServiceProvider services) => services.GetRequiredService<IAmbientCurrentUser>().End();

    /// <summary>
    /// Sets the current user in <paramref name="services"/> (a DI scope), and as the ambient user if a slot was begun, to the app
    /// user linked to <paramref name="discordId"/>, if any.
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
            {
                services.GetRequiredService<ICurrentUserService>().SetCurrentUserId(userId.Value);
                var ambientCurrentUser = services.GetRequiredService<IAmbientCurrentUser>();
                if (ambientCurrentUser.IsActive)
                    ambientCurrentUser.UserId = userId.Value;
            }
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
    {
        DiscordInteractionPipeline.EndCurrentUser(services);
        return DiscordInteractionPipeline.HandleResultAsync(result, context.Interaction, context.Interaction.Data.Name, logger);
    }
}

public sealed class ComponentInteractionPipeline<TContext> : IComponentInteractionPreExecutionHandler<TContext>, IComponentInteractionResultHandler<TContext>
    where TContext : IComponentInteractionContext
{
    public ValueTask<PreExecutionResult> HandleAsync(TContext context, GatewayClient? client, ILogger logger, IServiceProvider services)
        => DiscordInteractionPipeline.SetCurrentUserAsync(context.Interaction, logger, services);

    public ValueTask HandleResultAsync(IExecutionResult result, TContext context, GatewayClient? client, ILogger logger, IServiceProvider services)
    {
        DiscordInteractionPipeline.EndCurrentUser(services);
        return DiscordInteractionPipeline.HandleResultAsync(result, context.Interaction, context.Interaction.Data.CustomId, logger);
    }
}
