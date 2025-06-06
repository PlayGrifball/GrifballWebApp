using NetCord;
using NetCord.Rest;
using NetCord.Services;

namespace DiscordInterfaces;

public static class Ext
{
    public static IDiscordInteractionContext ToDiscordContext(this IInteractionContext interactionContext)
    {
        return new DiscordInteractionContext(interactionContext);
    }
}

internal class DiscordInteractionContext : IDiscordInteractionContext
{
    private readonly IDiscordInteraction _discordInteraction;
    public DiscordInteractionContext(IInteractionContext interactionContext)
    {
        _discordInteraction = new DiscordInteraction(interactionContext.Interaction);
    }
    public IDiscordInteraction Interaction => _discordInteraction;
}

public interface IDiscordInteractionContext
{
    IDiscordInteraction Interaction { get; }
}

internal class DiscordInteraction : IDiscordInteraction
{
    private readonly Interaction _interaction;
    private readonly IUser _user;
    public DiscordInteraction(Interaction interaction)
    {
        _interaction = interaction;
        _user = new DiscordUser(interaction.User);
    }
    public ulong ApplicationId => _interaction.ApplicationId;
    public IUser User => _user;
    public string Token => _interaction.Token;
    public Permissions AppPermissions => _interaction.AppPermissions;

    public Task SendResponseAsync(InteractionCallback callback, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        return _interaction.SendResponseAsync(callback, properties, cancellationToken);
    }

    public Task<RestMessage> GetResponseAsync(RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        return _interaction.GetResponseAsync(properties, cancellationToken);
    }

    public Task<RestMessage> ModifyResponseAsync(Action<MessageOptions> action, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        return _interaction.ModifyResponseAsync(action, properties, cancellationToken);
    }

    public Task DeleteResponseAsync(RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        return _interaction.DeleteResponseAsync(properties, cancellationToken);
    }

    public Task<RestMessage> SendFollowupMessageAsync(InteractionMessageProperties message, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        return _interaction.SendFollowupMessageAsync(message, properties, cancellationToken);
    }

    public Task<RestMessage> GetFollowupMessageAsync(ulong messageId, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        return _interaction.GetFollowupMessageAsync(messageId, properties, cancellationToken);
    }

    public Task<RestMessage> ModifyFollowupMessageAsync(ulong messageId, Action<MessageOptions> action, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        return _interaction.ModifyFollowupMessageAsync(messageId, action, properties, cancellationToken);
    }

    public Task DeleteFollowupMessageAsync(ulong messageId, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        return _interaction.DeleteFollowupMessageAsync(messageId, properties, cancellationToken);
    }
}

public interface IDiscordInteraction
{
    ulong ApplicationId { get; }
    IUser User { get; }
    string Token { get; }
    Permissions AppPermissions { get; }
    Task SendResponseAsync(InteractionCallback callback, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken));
    Task<RestMessage> GetResponseAsync(RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken));
    Task<RestMessage> ModifyResponseAsync(Action<MessageOptions> action, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken));
    Task DeleteResponseAsync(RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken));
    Task<RestMessage> SendFollowupMessageAsync(InteractionMessageProperties message, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken));
    Task<RestMessage> GetFollowupMessageAsync(ulong messageId, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken));
    Task<RestMessage> ModifyFollowupMessageAsync(ulong messageId, Action<MessageOptions> action, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken));
    Task DeleteFollowupMessageAsync(ulong messageId, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken));
}

internal class DiscordUser : IUser
{
    private readonly User _user;

    public DiscordUser(User user)
    {
        _user = user;
    }

    public ulong Id => _user.Id;
    public string Username => _user.Username;
    public ushort Discriminator => _user.Discriminator;
    public string? GlobalName => _user.GlobalName;
    public string? AvatarHash => _user.AvatarHash;
    public bool IsBot => _user.IsBot;
    public bool? IsSystemUser => _user.IsSystemUser;
    public bool? MfaEnabled => _user.MfaEnabled;
    public string? BannerHash => _user.BannerHash;
    public Color? AccentColor => _user.AccentColor;
    public string? Locale => _user.Locale;
    public bool? Verified => _user.Verified;
    public string? Email => _user.Email;
    public UserFlags? Flags => _user.Flags;
    public PremiumType? PremiumType => _user.PremiumType;
    public UserFlags? PublicFlags => _user.PublicFlags;
}

public interface IUser
{
    ulong Id { get; }
    string Username { get; }
    ushort Discriminator { get; }
    string? GlobalName { get; }
    string? AvatarHash { get; }
    bool IsBot { get; }
    bool? IsSystemUser { get; }
    bool? MfaEnabled { get; }
    string? BannerHash { get; }
    Color? AccentColor { get; }
    string? Locale { get; }
    bool? Verified { get; }
    string? Email { get; }
    UserFlags? Flags { get; }
    PremiumType? PremiumType { get; }
    UserFlags? PublicFlags { get; }
    // AvatarDecorationData
}