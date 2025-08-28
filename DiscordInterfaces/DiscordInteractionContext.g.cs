using DiscordInterfaceSourceGen;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Text.Json.Serialization.Metadata;

namespace DiscordInterfaceSourceGen;

public interface IDiscordInteractionContext
{
    NetCord.Services.IInteractionContext Original { get; }
    IDiscordInteraction Interaction { get; }
}


public interface IDiscordInteraction
{
    NetCord.Interaction Original { get; }
    ulong Id { get; }
    ulong ApplicationId { get; }
    ulong? GuildId { get; }
    IDiscordInteractionGuildReference GuildReference { get; }
    IDiscordGuild Guild { get; }
    IDiscordTextChannel Channel { get; }
    IDiscordUser User { get; }
    string Token { get; }
    NetCord.Permissions AppPermissions { get; }
    string UserLocale { get; }
    string GuildLocale { get; }
    IReadOnlyList<IDiscordEntitlement> Entitlements { get; }
    IReadOnlyDictionary<NetCord.ApplicationIntegrationType, ulong> AuthorizingIntegrationOwners { get; }
    NetCord.InteractionContextType Context { get; }
    IDiscordInteractionData Data { get; }
    System.DateTimeOffset CreatedAt { get; }
    Task SendResponseAsync(IDiscordInteractionCallback callback, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> GetResponseAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> ModifyResponseAsync(Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteResponseAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> SendFollowupMessageAsync(IDiscordInteractionMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> GetFollowupMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> ModifyFollowupMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteFollowupMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordInteractionGuildReference
{
    NetCord.InteractionGuildReference Original { get; }
    ulong Id { get; }
    IReadOnlyList<string> Features { get; }
    string Locale { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordGuild
{
    NetCord.Gateway.Guild Original { get; }
    System.DateTimeOffset JoinedAt { get; }
    bool IsLarge { get; }
    bool IsUnavailable { get; }
    int UserCount { get; }
    ImmutableDictionary<ulong, IDiscordVoiceState> VoiceStates { get; }
    ImmutableDictionary<ulong, IDiscordGuildUser> Users { get; }
    ImmutableDictionary<ulong, IDiscordGuildChannel> Channels { get; }
    ImmutableDictionary<ulong, IDiscordGuildThread> ActiveThreads { get; }
    ImmutableDictionary<ulong, IDiscordPresence> Presences { get; }
    ImmutableDictionary<ulong, IDiscordStageInstance> StageInstances { get; }
    ImmutableDictionary<ulong, IDiscordGuildScheduledEvent> ScheduledEvents { get; }
    bool IsOwner { get; }
    ulong Id { get; }
    string Name { get; }
    bool HasIcon { get; }
    string IconHash { get; }
    bool HasSplash { get; }
    string SplashHash { get; }
    bool HasDiscoverySplash { get; }
    string DiscoverySplashHash { get; }
    ulong OwnerId { get; }
    NetCord.Permissions? Permissions { get; }
    ulong? AfkChannelId { get; }
    int AfkTimeout { get; }
    bool? WidgetEnabled { get; }
    ulong? WidgetChannelId { get; }
    NetCord.VerificationLevel VerificationLevel { get; }
    NetCord.DefaultMessageNotificationLevel DefaultMessageNotificationLevel { get; }
    NetCord.ContentFilter ContentFilter { get; }
    ImmutableDictionary<ulong, IDiscordRole> Roles { get; }
    ImmutableDictionary<ulong, IDiscordGuildEmoji> Emojis { get; }
    IReadOnlyList<string> Features { get; }
    NetCord.MfaLevel MfaLevel { get; }
    ulong? ApplicationId { get; }
    ulong? SystemChannelId { get; }
    NetCord.Rest.SystemChannelFlags SystemChannelFlags { get; }
    ulong? RulesChannelId { get; }
    int? MaxPresences { get; }
    int? MaxUsers { get; }
    string VanityUrlCode { get; }
    string Description { get; }
    bool HasBanner { get; }
    string BannerHash { get; }
    int PremiumTier { get; }
    int? PremiumSubscriptionCount { get; }
    string PreferredLocale { get; }
    ulong? PublicUpdatesChannelId { get; }
    int? MaxVideoChannelUsers { get; }
    int? MaxStageVideoChannelUsers { get; }
    int? ApproximateUserCount { get; }
    int? ApproximatePresenceCount { get; }
    IDiscordGuildWelcomeScreen WelcomeScreen { get; }
    NetCord.NsfwLevel NsfwLevel { get; }
    ImmutableDictionary<ulong, IDiscordGuildSticker> Stickers { get; }
    bool PremiumProgressBarEnabled { get; }
    ulong? SafetyAlertsChannelId { get; }
    IDiscordRole EveryoneRole { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordGuild With(Action<IDiscordGuild> action);
    int Compare(IDiscordPartialGuildUser x, IDiscordPartialGuildUser y);
    IDiscordImageUrl GetIconUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl GetSplashUrl(NetCord.ImageFormat format);
    IDiscordImageUrl GetDiscoverySplashUrl(NetCord.ImageFormat format);
    IDiscordImageUrl GetBannerUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl GetWidgetUrl(NetCord.GuildWidgetStyle? style = default, string? hostname = null, NetCord.ApiVersion? version = default);
    IAsyncEnumerable<IDiscordRestAuditLogEntry> GetAuditLogAsync(IDiscordGuildAuditLogPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IReadOnlyList<IDiscordAutoModerationRule>> GetAutoModerationRulesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordAutoModerationRule> GetAutoModerationRuleAsync(ulong autoModerationRuleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordAutoModerationRule> CreateAutoModerationRuleAsync(IDiscordAutoModerationRuleProperties autoModerationRuleProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordAutoModerationRule> ModifyAutoModerationRuleAsync(ulong autoModerationRuleId, Action<IDiscordAutoModerationRuleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAutoModerationRuleAsync(ulong autoModerationRuleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGuildEmoji>> GetEmojisAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildEmoji> GetEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildEmoji> CreateEmojiAsync(IDiscordGuildEmojiProperties guildEmojiProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildEmoji> ModifyEmojiAsync(ulong emojiId, Action<IDiscordGuildEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestGuild> GetAsync(bool withCounts = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildPreview> GetPreviewAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestGuild> ModifyAsync(Action<IDiscordGuildOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGuildChannel>> GetChannelsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildChannel> CreateChannelAsync(IDiscordGuildChannelProperties channelProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task ModifyChannelPositionsAsync(IEnumerable<IDiscordGuildChannelPositionProperties> positions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGuildThread>> GetActiveThreadsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildUser> GetUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordGuildUser> GetUsersAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IReadOnlyList<IDiscordGuildUser>> FindUserAsync(string name, int limit, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildUser> AddUserAsync(ulong userId, IDiscordGuildUserProperties userProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildUser> ModifyUserAsync(ulong userId, Action<IDiscordGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildUser> ModifyCurrentUserAsync(Action<IDiscordCurrentGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task AddUserRoleAsync(ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task RemoveUserRoleAsync(ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task KickUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordGuildBan> GetBansAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordGuildBan> GetBanAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task BanUserAsync(ulong userId, int deleteMessageSeconds = 0, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildBulkBan> BanUsersAsync(IEnumerable<ulong> userIds, int deleteMessageSeconds = 0, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task UnbanUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordRole>> GetRolesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRole> GetRoleAsync(ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRole> CreateRoleAsync(IDiscordRoleProperties guildRoleProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordRole>> ModifyRolePositionsAsync(IEnumerable<IDiscordRolePositionProperties> positions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRole> ModifyRoleAsync(ulong roleId, Action<IDiscordRoleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteRoleAsync(ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<NetCord.MfaLevel> ModifyMfaLevelAsync(NetCord.MfaLevel mfaLevel, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<int> GetPruneCountAsync(int days, IEnumerable<ulong>? roles = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<int?> PruneAsync(IDiscordGuildPruneProperties pruneProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IEnumerable<IDiscordVoiceRegion>> GetVoiceRegionsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IEnumerable<IDiscordRestInvite>> GetInvitesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordIntegration>> GetIntegrationsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteIntegrationAsync(ulong integrationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildWidgetSettings> GetWidgetSettingsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildWidgetSettings> ModifyWidgetSettingsAsync(Action<IDiscordGuildWidgetSettingsOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildWidget> GetWidgetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildVanityInvite> GetVanityInviteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildWelcomeScreen> GetWelcomeScreenAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildWelcomeScreen> ModifyWelcomeScreenAsync(Action<IDiscordGuildWelcomeScreenOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildOnboarding> GetOnboardingAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildOnboarding> ModifyOnboardingAsync(Action<IDiscordGuildOnboardingOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGuildScheduledEvent>> GetScheduledEventsAsync(bool withUserCount = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildScheduledEvent> CreateScheduledEventAsync(IDiscordGuildScheduledEventProperties guildScheduledEventProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildScheduledEvent> GetScheduledEventAsync(ulong scheduledEventId, bool withUserCount = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildScheduledEvent> ModifyScheduledEventAsync(ulong scheduledEventId, Action<IDiscordGuildScheduledEventOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteScheduledEventAsync(ulong scheduledEventId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordGuildScheduledEventUser> GetScheduledEventUsersAsync(ulong scheduledEventId, IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IEnumerable<IDiscordGuildTemplate>> GetTemplatesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildTemplate> CreateTemplateAsync(IDiscordGuildTemplateProperties guildTemplateProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildTemplate> SyncTemplateAsync(string templateCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildTemplate> ModifyTemplateAsync(string templateCode, Action<IDiscordGuildTemplateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildTemplate> DeleteTemplateAsync(string templateCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGuildApplicationCommand>> GetApplicationCommandsAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildApplicationCommand> CreateApplicationCommandAsync(ulong applicationId, IDiscordApplicationCommandProperties applicationCommandProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildApplicationCommand> GetApplicationCommandAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildApplicationCommand> ModifyApplicationCommandAsync(ulong applicationId, ulong commandId, Action<IDiscordApplicationCommandOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteApplicationCommandAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGuildApplicationCommand>> BulkOverwriteApplicationCommandsAsync(ulong applicationId, IEnumerable<IDiscordApplicationCommandProperties> commands, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordApplicationCommandGuildPermissions>> GetApplicationCommandsPermissionsAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationCommandGuildPermissions> GetApplicationCommandPermissionsAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationCommandGuildPermissions> OverwriteApplicationCommandPermissionsAsync(ulong applicationId, ulong commandId, IEnumerable<IDiscordApplicationCommandGuildPermissionProperties> newPermissions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGuildSticker>> GetStickersAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildSticker> GetStickerAsync(ulong stickerId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildSticker> CreateStickerAsync(IDiscordGuildStickerProperties sticker, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildSticker> ModifyStickerAsync(ulong stickerId, Action<IDiscordGuildStickerOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteStickerAsync(ulong stickerId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordGuildUserInfo> SearchUsersAsync(IDiscordGuildUsersSearchPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordGuildUser> GetCurrentUserGuildUserAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task LeaveAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordVoiceState> GetCurrentUserVoiceStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordVoiceState> GetUserVoiceStateAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task ModifyCurrentUserVoiceStateAsync(Action<IDiscordCurrentUserVoiceStateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task ModifyUserVoiceStateAsync(ulong channelId, ulong userId, Action<IDiscordVoiceStateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordWebhook>> GetWebhooksAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordTextChannel
{
    NetCord.TextChannel Original { get; }
    ulong? LastMessageId { get; }
    System.DateTimeOffset? LastPin { get; }
    ulong Id { get; }
    NetCord.ChannelFlags Flags { get; }
    System.DateTimeOffset CreatedAt { get; }
    Task<IDiscordTextChannel> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordTextChannel> DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordRestMessage> GetMessagesAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IReadOnlyList<IDiscordRestMessage>> GetMessagesAroundAsync(ulong messageId, int? limit = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> SendMessageAsync(IDiscordMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task AddMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordUser> GetMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordMessageReactionsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessagesAsync(IEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessagesAsync(IAsyncEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task TriggerTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDisposable> EnterTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordRestMessage>> GetPinnedMessagesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task PinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task UnpinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordUser> GetMessagePollAnswerVotersAsync(ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordRestMessage> EndMessagePollAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordUser
{
    NetCord.User Original { get; }
    ulong Id { get; }
    string Username { get; }
    ushort Discriminator { get; }
    string GlobalName { get; }
    string AvatarHash { get; }
    bool IsBot { get; }
    bool? IsSystemUser { get; }
    bool? MfaEnabled { get; }
    string BannerHash { get; }
    NetCord.Color? AccentColor { get; }
    string Locale { get; }
    bool? Verified { get; }
    string Email { get; }
    NetCord.UserFlags? Flags { get; }
    NetCord.PremiumType? PremiumType { get; }
    NetCord.UserFlags? PublicFlags { get; }
    IDiscordAvatarDecorationData AvatarDecorationData { get; }
    bool HasAvatar { get; }
    bool HasBanner { get; }
    bool HasAvatarDecoration { get; }
    IDiscordImageUrl DefaultAvatarUrl { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordImageUrl GetAvatarUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl GetBannerUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl GetAvatarDecorationUrl();
    Task<IDiscordUser> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordDMChannel> GetDMChannelAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordEntitlement
{
    NetCord.Entitlement Original { get; }
    ulong Id { get; }
    ulong SkuId { get; }
    ulong ApplicationId { get; }
    ulong? UserId { get; }
    NetCord.EntitlementType Type { get; }
    bool Deleted { get; }
    System.DateTimeOffset? StartsAt { get; }
    System.DateTimeOffset? EndsAt { get; }
    ulong? GuildId { get; }
    bool? Consumed { get; }
    System.DateTimeOffset CreatedAt { get; }
    Task<IDiscordEntitlement> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task ConsumeAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordInteractionData
{
    NetCord.InteractionData Original { get; }
}


public interface IDiscordInteractionCallback
{
    NetCord.Rest.InteractionCallback Original { get; }
    NetCord.Rest.InteractionCallbackType Type { get; }
    static IDiscordInteractionCallback Pong { get; }
    static IDiscordInteractionCallback DeferredModifyMessage { get; }
    HttpContent Serialize();
}


public interface IDiscordRestRequestProperties
{
    NetCord.Rest.RestRequestProperties Original { get; }
    NetCord.Rest.RestRateLimitHandling? RateLimitHandling { get; }
    string AuditLogReason { get; }
    string ErrorLocalization { get; }
    IDiscordRestRequestProperties WithRateLimitHandling(NetCord.Rest.RestRateLimitHandling? rateLimitHandling);
    IDiscordRestRequestProperties WithAuditLogReason(string auditLogReason);
    IDiscordRestRequestProperties WithErrorLocalization(string errorLocalization);
}


public interface IDiscordRestMessage
{
    NetCord.Rest.RestMessage Original { get; }
    ulong Id { get; }
    ulong ChannelId { get; }
    IDiscordUser Author { get; }
    string Content { get; }
    System.DateTimeOffset? EditedAt { get; }
    bool IsTts { get; }
    bool MentionEveryone { get; }
    IReadOnlyList<IDiscordUser> MentionedUsers { get; }
    IReadOnlyList<ulong> MentionedRoleIds { get; }
    IReadOnlyList<IDiscordGuildChannelMention> MentionedChannels { get; }
    IReadOnlyList<IDiscordAttachment> Attachments { get; }
    IReadOnlyList<IDiscordEmbed> Embeds { get; }
    IReadOnlyList<IDiscordMessageReaction> Reactions { get; }
    string Nonce { get; }
    bool IsPinned { get; }
    ulong? WebhookId { get; }
    NetCord.MessageType Type { get; }
    IDiscordMessageActivity Activity { get; }
    IDiscordApplication Application { get; }
    ulong? ApplicationId { get; }
    NetCord.MessageFlags Flags { get; }
    IDiscordMessageReference MessageReference { get; }
    IReadOnlyList<IDiscordMessageSnapshot> MessageSnapshots { get; }
    IDiscordRestMessage ReferencedMessage { get; }
    IDiscordMessageInteractionMetadata InteractionMetadata { get; }
    IDiscordMessageInteraction Interaction { get; }
    IDiscordGuildThread StartedThread { get; }
    IReadOnlyList<IDiscordComponent> Components { get; }
    IReadOnlyList<IDiscordMessageSticker> Stickers { get; }
    int? Position { get; }
    IDiscordRoleSubscriptionData RoleSubscriptionData { get; }
    IDiscordInteractionResolvedData ResolvedData { get; }
    IDiscordMessagePoll Poll { get; }
    IDiscordMessageCall Call { get; }
    System.DateTimeOffset CreatedAt { get; }
    Task<IDiscordRestMessage> ReplyAsync(IDiscordReplyMessageProperties replyMessage, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> SendAsync(IDiscordMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> CrosspostAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task AddReactionAsync(IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteReactionAsync(IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteReactionAsync(IDiscordReactionEmojiProperties emoji, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordUser> GetReactionsAsync(IDiscordReactionEmojiProperties emoji, IDiscordMessageReactionsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task DeleteAllReactionsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAllReactionsAsync(IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> ModifyAsync(Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task PinAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task UnpinAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildThread> CreateGuildThreadAsync(IDiscordGuildThreadFromMessageProperties threadFromMessageProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordUser> GetPollAnswerVotersAsync(int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordRestMessage> EndPollAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordMessageOptions
{
    NetCord.Rest.MessageOptions Original { get; }
    string Content { get; }
    IEnumerable<IDiscordEmbedProperties> Embeds { get; }
    NetCord.MessageFlags? Flags { get; }
    IDiscordAllowedMentionsProperties AllowedMentions { get; }
    IEnumerable<IDiscordComponentProperties> Components { get; }
    IEnumerable<IDiscordAttachmentProperties> Attachments { get; }
    HttpContent Serialize();
    IDiscordMessageOptions WithContent(string content);
    IDiscordMessageOptions WithEmbeds(IEnumerable<IDiscordEmbedProperties> embeds);
    IDiscordMessageOptions AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds);
    IDiscordMessageOptions AddEmbeds(IDiscordEmbedProperties[] embeds);
    IDiscordMessageOptions WithFlags(NetCord.MessageFlags? flags);
    IDiscordMessageOptions WithAllowedMentions(IDiscordAllowedMentionsProperties allowedMentions);
    IDiscordMessageOptions WithComponents(IEnumerable<IDiscordComponentProperties> components);
    IDiscordMessageOptions AddComponents(IEnumerable<IDiscordComponentProperties> components);
    IDiscordMessageOptions AddComponents(IDiscordComponentProperties[] components);
    IDiscordMessageOptions WithAttachments(IEnumerable<IDiscordAttachmentProperties> attachments);
    IDiscordMessageOptions AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments);
    IDiscordMessageOptions AddAttachments(IDiscordAttachmentProperties[] attachments);
}


public interface IDiscordInteractionMessageProperties
{
    NetCord.Rest.InteractionMessageProperties Original { get; }
    bool Tts { get; }
    string Content { get; }
    IEnumerable<IDiscordEmbedProperties> Embeds { get; }
    IDiscordAllowedMentionsProperties AllowedMentions { get; }
    NetCord.MessageFlags? Flags { get; }
    IEnumerable<IDiscordComponentProperties> Components { get; }
    IEnumerable<IDiscordAttachmentProperties> Attachments { get; }
    IDiscordMessagePollProperties Poll { get; }
    HttpContent Serialize();
    IDiscordInteractionMessageProperties WithTts(bool tts = true);
    IDiscordInteractionMessageProperties WithContent(string content);
    IDiscordInteractionMessageProperties WithEmbeds(IEnumerable<IDiscordEmbedProperties> embeds);
    IDiscordInteractionMessageProperties AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds);
    IDiscordInteractionMessageProperties AddEmbeds(IDiscordEmbedProperties[] embeds);
    IDiscordInteractionMessageProperties WithAllowedMentions(IDiscordAllowedMentionsProperties allowedMentions);
    IDiscordInteractionMessageProperties WithFlags(NetCord.MessageFlags? flags);
    IDiscordInteractionMessageProperties WithComponents(IEnumerable<IDiscordComponentProperties> components);
    IDiscordInteractionMessageProperties AddComponents(IEnumerable<IDiscordComponentProperties> components);
    IDiscordInteractionMessageProperties AddComponents(IDiscordComponentProperties[] components);
    IDiscordInteractionMessageProperties WithAttachments(IEnumerable<IDiscordAttachmentProperties> attachments);
    IDiscordInteractionMessageProperties AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments);
    IDiscordInteractionMessageProperties AddAttachments(IDiscordAttachmentProperties[] attachments);
    IDiscordInteractionMessageProperties WithPoll(IDiscordMessagePollProperties poll);
}


public interface IDiscordVoiceState
{
    NetCord.Gateway.VoiceState Original { get; }
    ulong GuildId { get; }
    ulong? ChannelId { get; }
    ulong UserId { get; }
    IDiscordGuildUser User { get; }
    string SessionId { get; }
    bool IsDeafened { get; }
    bool IsMuted { get; }
    bool IsSelfDeafened { get; }
    bool IsSelfMuted { get; }
    bool? SelfStreamExists { get; }
    bool SelfVideoExists { get; }
    bool Suppressed { get; }
    System.DateTimeOffset? RequestToSpeakTimestamp { get; }
}


public interface IDiscordGuildUser
{
    NetCord.GuildUser Original { get; }
    ulong GuildId { get; }
    string Nickname { get; }
    string GuildAvatarHash { get; }
    string GuildBannerHash { get; }
    IReadOnlyList<ulong> RoleIds { get; }
    ulong? HoistedRoleId { get; }
    System.DateTimeOffset JoinedAt { get; }
    System.DateTimeOffset? GuildBoostStart { get; }
    bool Deafened { get; }
    bool Muted { get; }
    NetCord.GuildUserFlags GuildFlags { get; }
    bool? IsPending { get; }
    System.DateTimeOffset? TimeOutUntil { get; }
    IDiscordAvatarDecorationData GuildAvatarDecorationData { get; }
    bool HasGuildAvatar { get; }
    bool HasGuildBanner { get; }
    bool HasGuildAvatarDecoration { get; }
    ulong Id { get; }
    string Username { get; }
    ushort Discriminator { get; }
    string GlobalName { get; }
    string AvatarHash { get; }
    bool IsBot { get; }
    bool? IsSystemUser { get; }
    bool? MfaEnabled { get; }
    string BannerHash { get; }
    NetCord.Color? AccentColor { get; }
    string Locale { get; }
    bool? Verified { get; }
    string Email { get; }
    NetCord.UserFlags? Flags { get; }
    NetCord.PremiumType? PremiumType { get; }
    NetCord.UserFlags? PublicFlags { get; }
    IDiscordAvatarDecorationData AvatarDecorationData { get; }
    bool HasAvatar { get; }
    bool HasBanner { get; }
    bool HasAvatarDecoration { get; }
    IDiscordImageUrl DefaultAvatarUrl { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordImageUrl GetGuildAvatarUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl GetGuildBannerUrl(NetCord.ImageFormat? format = default);
    Task<IDiscordGuildUser> TimeOutAsync(System.DateTimeOffset until, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordGuildUserInfo> GetInfoAsync(IDiscordRestRequestProperties? properties = null);
    Task<IDiscordGuildUser> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildUser> ModifyAsync(Action<IDiscordGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task AddRoleAsync(ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task RemoveRoleAsync(ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task KickAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task BanAsync(int deleteMessageSeconds = 0, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task UnbanAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordVoiceState> GetVoiceStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task ModifyVoiceStateAsync(ulong channelId, Action<IDiscordVoiceStateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IDiscordImageUrl GetGuildAvatarDecorationUrl();
    IDiscordImageUrl GetAvatarUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl GetBannerUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl GetAvatarDecorationUrl();
    Task<IDiscordDMChannel> GetDMChannelAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordGuildChannel
{
    NetCord.IGuildChannel Original { get; }
    ulong GuildId { get; }
    int? Position { get; }
    IReadOnlyDictionary<ulong, IDiscordPermissionOverwrite> PermissionOverwrites { get; }
    Task<IDiscordGuildChannel> ModifyAsync(Action<IDiscordGuildChannelOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task ModifyPermissionsAsync(IDiscordPermissionOverwriteProperties permissionOverwrite, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IEnumerable<IDiscordRestInvite>> GetInvitesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestInvite> CreateInviteAsync(IDiscordInviteProperties? inviteProperties = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeletePermissionAsync(ulong overwriteId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordGuildThread
{
    NetCord.GuildThread Original { get; }
    ulong OwnerId { get; }
    int MessageCount { get; }
    int UserCount { get; }
    IDiscordGuildThreadMetadata Metadata { get; }
    IDiscordThreadCurrentUser CurrentUser { get; }
    int TotalMessageSent { get; }
    ulong GuildId { get; }
    int? Position { get; }
    IReadOnlyDictionary<ulong, IDiscordPermissionOverwrite> PermissionOverwrites { get; }
    string Name { get; }
    string Topic { get; }
    bool Nsfw { get; }
    int Slowmode { get; }
    ulong? ParentId { get; }
    NetCord.ThreadArchiveDuration? DefaultAutoArchiveDuration { get; }
    int DefaultThreadSlowmode { get; }
    ulong? LastMessageId { get; }
    System.DateTimeOffset? LastPin { get; }
    ulong Id { get; }
    NetCord.ChannelFlags Flags { get; }
    System.DateTimeOffset CreatedAt { get; }
    Task<IDiscordGuildThread> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildThread> ModifyAsync(Action<IDiscordGuildChannelOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildThread> DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task JoinAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task AddUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task LeaveAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordThreadUser> GetUserAsync(ulong userId, bool withGuildUser = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordThreadUser> GetUsersAsync(IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task ModifyPermissionsAsync(IDiscordPermissionOverwriteProperties permissionOverwrite, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IEnumerable<IDiscordRestInvite>> GetInvitesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestInvite> CreateInviteAsync(IDiscordInviteProperties? inviteProperties = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeletePermissionAsync(ulong overwriteId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildThread> CreateGuildThreadAsync(ulong messageId, IDiscordGuildThreadFromMessageProperties threadFromMessageProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildThread> CreateGuildThreadAsync(IDiscordGuildThreadProperties threadProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordGuildThread> GetPublicArchivedGuildThreadsAsync(IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    IAsyncEnumerable<IDiscordGuildThread> GetPrivateArchivedGuildThreadsAsync(IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    IAsyncEnumerable<IDiscordGuildThread> GetJoinedPrivateArchivedGuildThreadsAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordIncomingWebhook> CreateWebhookAsync(IDiscordWebhookProperties webhookProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordWebhook>> GetChannelWebhooksAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordRestMessage> GetMessagesAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IReadOnlyList<IDiscordRestMessage>> GetMessagesAroundAsync(ulong messageId, int? limit = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> SendMessageAsync(IDiscordMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task AddMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordUser> GetMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordMessageReactionsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessagesAsync(IEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessagesAsync(IAsyncEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task TriggerTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDisposable> EnterTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordRestMessage>> GetPinnedMessagesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task PinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task UnpinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordUser> GetMessagePollAnswerVotersAsync(ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordRestMessage> EndMessagePollAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordPresence
{
    NetCord.Gateway.Presence Original { get; }
    IDiscordUser User { get; }
    ulong GuildId { get; }
    NetCord.UserStatusType Status { get; }
    IReadOnlyList<IDiscordUserActivity> Activities { get; }
    IReadOnlyDictionary<NetCord.Gateway.Platform, NetCord.UserStatusType> Platform { get; }
}


public interface IDiscordStageInstance
{
    NetCord.StageInstance Original { get; }
    ulong Id { get; }
    ulong GuildId { get; }
    ulong ChannelId { get; }
    string Topic { get; }
    NetCord.StageInstancePrivacyLevel PrivacyLevel { get; }
    bool DiscoverableDisabled { get; }
    System.DateTimeOffset CreatedAt { get; }
    Task<IDiscordStageInstance> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordStageInstance> ModifyAsync(Action<IDiscordStageInstanceOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordGuildScheduledEvent
{
    NetCord.GuildScheduledEvent Original { get; }
    ulong Id { get; }
    ulong GuildId { get; }
    ulong? ChannelId { get; }
    ulong? CreatorId { get; }
    string Name { get; }
    string Description { get; }
    System.DateTimeOffset ScheduledStartTime { get; }
    System.DateTimeOffset? ScheduledEndTime { get; }
    NetCord.GuildScheduledEventPrivacyLevel PrivacyLevel { get; }
    NetCord.GuildScheduledEventStatus Status { get; }
    NetCord.GuildScheduledEventEntityType EntityType { get; }
    ulong? EntityId { get; }
    string Location { get; }
    IDiscordUser Creator { get; }
    int? UserCount { get; }
    string CoverImageHash { get; }
    IDiscordGuildScheduledEventRecurrenceRule RecurrenceRule { get; }
    bool HasCoverImage { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordImageUrl GetCoverImageUrl(NetCord.ImageFormat format);
    Task<IDiscordGuildScheduledEvent> GetAsync(bool withUserCount = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildScheduledEvent> ModifyAsync(Action<IDiscordGuildScheduledEventOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordGuildScheduledEventUser> GetUsersAsync(IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
}


public interface IDiscordRole
{
    NetCord.Role Original { get; }
    ulong Id { get; }
    string Name { get; }
    NetCord.Color Color { get; }
    bool Hoist { get; }
    string IconHash { get; }
    string UnicodeEmoji { get; }
    int Position { get; }
    NetCord.Permissions Permissions { get; }
    bool Managed { get; }
    bool Mentionable { get; }
    IDiscordRoleTags Tags { get; }
    NetCord.RoleFlags Flags { get; }
    ulong GuildId { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordImageUrl GetIconUrl(NetCord.ImageFormat format);
    int CompareTo(IDiscordRole other);
    Task<IDiscordRole> ModifyAsync(Action<IDiscordRoleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordGuildEmoji
{
    NetCord.GuildEmoji Original { get; }
    IReadOnlyList<ulong> AllowedRoles { get; }
    ulong GuildId { get; }
    ulong Id { get; }
    IDiscordUser Creator { get; }
    bool? RequireColons { get; }
    bool? Managed { get; }
    bool? Available { get; }
    string Name { get; }
    bool Animated { get; }
    Task<IDiscordGuildEmoji> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildEmoji> ModifyAsync(Action<IDiscordGuildEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IDiscordImageUrl GetImageUrl(NetCord.ImageFormat format);
}


public interface IDiscordGuildWelcomeScreen
{
    NetCord.GuildWelcomeScreen Original { get; }
    string Description { get; }
    ImmutableDictionary<ulong, IDiscordGuildWelcomeScreenChannel> WelcomeChannels { get; }
}


public interface IDiscordGuildSticker
{
    NetCord.GuildSticker Original { get; }
    bool? Available { get; }
    ulong GuildId { get; }
    IDiscordUser Creator { get; }
    ulong Id { get; }
    string Name { get; }
    string Description { get; }
    IReadOnlyList<string> Tags { get; }
    NetCord.StickerFormat Format { get; }
    System.DateTimeOffset CreatedAt { get; }
    Task<IDiscordGuildSticker> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildSticker> ModifyAsync(Action<IDiscordGuildStickerOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IDiscordImageUrl GetImageUrl(NetCord.ImageFormat format);
}


public interface IDiscordPartialGuildUser
{
    NetCord.PartialGuildUser Original { get; }
    string Nickname { get; }
    string GuildAvatarHash { get; }
    string GuildBannerHash { get; }
    IReadOnlyList<ulong> RoleIds { get; }
    ulong? HoistedRoleId { get; }
    System.DateTimeOffset JoinedAt { get; }
    System.DateTimeOffset? GuildBoostStart { get; }
    bool Deafened { get; }
    bool Muted { get; }
    NetCord.GuildUserFlags GuildFlags { get; }
    bool? IsPending { get; }
    System.DateTimeOffset? TimeOutUntil { get; }
    IDiscordAvatarDecorationData GuildAvatarDecorationData { get; }
    bool HasGuildAvatar { get; }
    bool HasGuildBanner { get; }
    bool HasGuildAvatarDecoration { get; }
    ulong Id { get; }
    string Username { get; }
    ushort Discriminator { get; }
    string GlobalName { get; }
    string AvatarHash { get; }
    bool IsBot { get; }
    bool? IsSystemUser { get; }
    bool? MfaEnabled { get; }
    string BannerHash { get; }
    NetCord.Color? AccentColor { get; }
    string Locale { get; }
    bool? Verified { get; }
    string Email { get; }
    NetCord.UserFlags? Flags { get; }
    NetCord.PremiumType? PremiumType { get; }
    NetCord.UserFlags? PublicFlags { get; }
    IDiscordAvatarDecorationData AvatarDecorationData { get; }
    bool HasAvatar { get; }
    bool HasBanner { get; }
    bool HasAvatarDecoration { get; }
    IDiscordImageUrl DefaultAvatarUrl { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordImageUrl GetGuildAvatarDecorationUrl();
    IDiscordImageUrl GetAvatarUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl GetBannerUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl GetAvatarDecorationUrl();
    Task<IDiscordUser> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordDMChannel> GetDMChannelAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordImageUrl
{
    NetCord.ImageUrl Original { get; }
}


public interface IDiscordRestAuditLogEntry
{
    NetCord.Rest.RestAuditLogEntry Original { get; }
    IDiscordRestAuditLogEntryData Data { get; }
    IDiscordUser User { get; }
    ulong Id { get; }
    ulong? TargetId { get; }
    IReadOnlyDictionary<string, IDiscordAuditLogChange> Changes { get; }
    ulong? UserId { get; }
    NetCord.AuditLogEvent ActionType { get; }
    IDiscordAuditLogEntryInfo Options { get; }
    string Reason { get; }
    ulong GuildId { get; }
    System.DateTimeOffset CreatedAt { get; }
    bool TryGetChange<TObject, TValue>(Expression<Func<TObject, TValue>> expression, IDiscordAuditLogChange`1& change);
    IDiscordAuditLogChange<TValue> GetChange<TObject, TValue>(Expression<Func<TObject, TValue>> expression);
    bool TryGetChange<TObject, TValue>(Expression<Func<TObject, TValue>> expression, JsonTypeInfo<TValue> jsonTypeInfo, IDiscordAuditLogChange`1& change);
    IDiscordAuditLogChange<TValue> GetChange<TObject, TValue>(Expression<Func<TObject, TValue>> expression, JsonTypeInfo<TValue> jsonTypeInfo);
}


public interface IDiscordGuildAuditLogPaginationProperties
{
    NetCord.Rest.GuildAuditLogPaginationProperties Original { get; }
    ulong? UserId { get; }
    NetCord.AuditLogEvent? ActionType { get; }
    ulong? From { get; }
    NetCord.Rest.PaginationDirection? Direction { get; }
    int? BatchSize { get; }
    IDiscordGuildAuditLogPaginationProperties WithUserId(ulong? userId);
    IDiscordGuildAuditLogPaginationProperties WithActionType(NetCord.AuditLogEvent? actionType);
    IDiscordGuildAuditLogPaginationProperties WithFrom(ulong? from);
    IDiscordGuildAuditLogPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction);
    IDiscordGuildAuditLogPaginationProperties WithBatchSize(int? batchSize);
}


public interface IDiscordAutoModerationRule
{
    NetCord.AutoModerationRule Original { get; }
    ulong Id { get; }
    ulong GuildId { get; }
    string Name { get; }
    ulong CreatorId { get; }
    NetCord.AutoModerationRuleEventType EventType { get; }
    NetCord.AutoModerationRuleTriggerType TriggerType { get; }
    IDiscordAutoModerationRuleTriggerMetadata TriggerMetadata { get; }
    IReadOnlyList<IDiscordAutoModerationAction> Actions { get; }
    bool Enabled { get; }
    IReadOnlyList<ulong> ExemptRoles { get; }
    IReadOnlyList<ulong> ExemptChannels { get; }
    System.DateTimeOffset CreatedAt { get; }
    Task<IDiscordAutoModerationRule> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordAutoModerationRule> ModifyAsync(Action<IDiscordAutoModerationRuleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordAutoModerationRuleProperties
{
    NetCord.AutoModerationRuleProperties Original { get; }
    string Name { get; }
    NetCord.AutoModerationRuleEventType EventType { get; }
    NetCord.AutoModerationRuleTriggerType TriggerType { get; }
    IDiscordAutoModerationRuleTriggerMetadataProperties TriggerMetadata { get; }
    IEnumerable<IDiscordAutoModerationActionProperties> Actions { get; }
    bool Enabled { get; }
    IEnumerable<ulong> ExemptRoles { get; }
    IEnumerable<ulong> ExemptChannels { get; }
    IDiscordAutoModerationRuleProperties WithName(string name);
    IDiscordAutoModerationRuleProperties WithEventType(NetCord.AutoModerationRuleEventType eventType);
    IDiscordAutoModerationRuleProperties WithTriggerType(NetCord.AutoModerationRuleTriggerType triggerType);
    IDiscordAutoModerationRuleProperties WithTriggerMetadata(IDiscordAutoModerationRuleTriggerMetadataProperties triggerMetadata);
    IDiscordAutoModerationRuleProperties WithActions(IEnumerable<IDiscordAutoModerationActionProperties> actions);
    IDiscordAutoModerationRuleProperties AddActions(IEnumerable<IDiscordAutoModerationActionProperties> actions);
    IDiscordAutoModerationRuleProperties AddActions(IDiscordAutoModerationActionProperties[] actions);
    IDiscordAutoModerationRuleProperties WithEnabled(bool enabled = true);
    IDiscordAutoModerationRuleProperties WithExemptRoles(IEnumerable<ulong> exemptRoles);
    IDiscordAutoModerationRuleProperties AddExemptRoles(IEnumerable<ulong> exemptRoles);
    IDiscordAutoModerationRuleProperties AddExemptRoles(ulong[] exemptRoles);
    IDiscordAutoModerationRuleProperties WithExemptChannels(IEnumerable<ulong> exemptChannels);
    IDiscordAutoModerationRuleProperties AddExemptChannels(IEnumerable<ulong> exemptChannels);
    IDiscordAutoModerationRuleProperties AddExemptChannels(ulong[] exemptChannels);
}


public interface IDiscordAutoModerationRuleOptions
{
    NetCord.AutoModerationRuleOptions Original { get; }
    string Name { get; }
    NetCord.AutoModerationRuleEventType? EventType { get; }
    IDiscordAutoModerationRuleTriggerMetadataProperties TriggerMetadata { get; }
    IEnumerable<IDiscordAutoModerationActionProperties> Actions { get; }
    bool? Enabled { get; }
    IEnumerable<ulong> ExemptRoles { get; }
    IEnumerable<ulong> ExemptChannels { get; }
    IDiscordAutoModerationRuleOptions WithName(string name);
    IDiscordAutoModerationRuleOptions WithEventType(NetCord.AutoModerationRuleEventType? eventType);
    IDiscordAutoModerationRuleOptions WithTriggerMetadata(IDiscordAutoModerationRuleTriggerMetadataProperties triggerMetadata);
    IDiscordAutoModerationRuleOptions WithActions(IEnumerable<IDiscordAutoModerationActionProperties> actions);
    IDiscordAutoModerationRuleOptions AddActions(IEnumerable<IDiscordAutoModerationActionProperties> actions);
    IDiscordAutoModerationRuleOptions AddActions(IDiscordAutoModerationActionProperties[] actions);
    IDiscordAutoModerationRuleOptions WithEnabled(bool? enabled = true);
    IDiscordAutoModerationRuleOptions WithExemptRoles(IEnumerable<ulong> exemptRoles);
    IDiscordAutoModerationRuleOptions AddExemptRoles(IEnumerable<ulong> exemptRoles);
    IDiscordAutoModerationRuleOptions AddExemptRoles(ulong[] exemptRoles);
    IDiscordAutoModerationRuleOptions WithExemptChannels(IEnumerable<ulong> exemptChannels);
    IDiscordAutoModerationRuleOptions AddExemptChannels(IEnumerable<ulong> exemptChannels);
    IDiscordAutoModerationRuleOptions AddExemptChannels(ulong[] exemptChannels);
}


public interface IDiscordGuildEmojiProperties
{
    NetCord.Rest.GuildEmojiProperties Original { get; }
    string Name { get; }
    NetCord.Rest.ImageProperties Image { get; }
    IEnumerable<ulong> AllowedRoles { get; }
    IDiscordGuildEmojiProperties WithName(string name);
    IDiscordGuildEmojiProperties WithImage(NetCord.Rest.ImageProperties image);
    IDiscordGuildEmojiProperties WithAllowedRoles(IEnumerable<ulong> allowedRoles);
    IDiscordGuildEmojiProperties AddAllowedRoles(IEnumerable<ulong> allowedRoles);
    IDiscordGuildEmojiProperties AddAllowedRoles(ulong[] allowedRoles);
}


public interface IDiscordGuildEmojiOptions
{
    NetCord.Rest.GuildEmojiOptions Original { get; }
    string Name { get; }
    IEnumerable<ulong> AllowedRoles { get; }
    IDiscordGuildEmojiOptions WithName(string name);
    IDiscordGuildEmojiOptions WithAllowedRoles(IEnumerable<ulong> allowedRoles);
    IDiscordGuildEmojiOptions AddAllowedRoles(IEnumerable<ulong> allowedRoles);
    IDiscordGuildEmojiOptions AddAllowedRoles(ulong[] allowedRoles);
}


public interface IDiscordRestGuild
{
    NetCord.Rest.RestGuild Original { get; }
    ulong Id { get; }
    string Name { get; }
    bool HasIcon { get; }
    string IconHash { get; }
    bool HasSplash { get; }
    string SplashHash { get; }
    bool HasDiscoverySplash { get; }
    string DiscoverySplashHash { get; }
    bool IsOwner { get; }
    ulong OwnerId { get; }
    NetCord.Permissions? Permissions { get; }
    ulong? AfkChannelId { get; }
    int AfkTimeout { get; }
    bool? WidgetEnabled { get; }
    ulong? WidgetChannelId { get; }
    NetCord.VerificationLevel VerificationLevel { get; }
    NetCord.DefaultMessageNotificationLevel DefaultMessageNotificationLevel { get; }
    NetCord.ContentFilter ContentFilter { get; }
    ImmutableDictionary<ulong, IDiscordRole> Roles { get; }
    ImmutableDictionary<ulong, IDiscordGuildEmoji> Emojis { get; }
    IReadOnlyList<string> Features { get; }
    NetCord.MfaLevel MfaLevel { get; }
    ulong? ApplicationId { get; }
    ulong? SystemChannelId { get; }
    NetCord.Rest.SystemChannelFlags SystemChannelFlags { get; }
    ulong? RulesChannelId { get; }
    int? MaxPresences { get; }
    int? MaxUsers { get; }
    string VanityUrlCode { get; }
    string Description { get; }
    bool HasBanner { get; }
    string BannerHash { get; }
    int PremiumTier { get; }
    int? PremiumSubscriptionCount { get; }
    string PreferredLocale { get; }
    ulong? PublicUpdatesChannelId { get; }
    int? MaxVideoChannelUsers { get; }
    int? MaxStageVideoChannelUsers { get; }
    int? ApproximateUserCount { get; }
    int? ApproximatePresenceCount { get; }
    IDiscordGuildWelcomeScreen WelcomeScreen { get; }
    NetCord.NsfwLevel NsfwLevel { get; }
    ImmutableDictionary<ulong, IDiscordGuildSticker> Stickers { get; }
    bool PremiumProgressBarEnabled { get; }
    ulong? SafetyAlertsChannelId { get; }
    IDiscordRole EveryoneRole { get; }
    System.DateTimeOffset CreatedAt { get; }
    int Compare(IDiscordPartialGuildUser x, IDiscordPartialGuildUser y);
    IDiscordImageUrl GetIconUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl GetSplashUrl(NetCord.ImageFormat format);
    IDiscordImageUrl GetDiscoverySplashUrl(NetCord.ImageFormat format);
    IDiscordImageUrl GetBannerUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl GetWidgetUrl(NetCord.GuildWidgetStyle? style = default, string? hostname = null, NetCord.ApiVersion? version = default);
    IAsyncEnumerable<IDiscordRestAuditLogEntry> GetAuditLogAsync(IDiscordGuildAuditLogPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IReadOnlyList<IDiscordAutoModerationRule>> GetAutoModerationRulesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordAutoModerationRule> GetAutoModerationRuleAsync(ulong autoModerationRuleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordAutoModerationRule> CreateAutoModerationRuleAsync(IDiscordAutoModerationRuleProperties autoModerationRuleProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordAutoModerationRule> ModifyAutoModerationRuleAsync(ulong autoModerationRuleId, Action<IDiscordAutoModerationRuleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAutoModerationRuleAsync(ulong autoModerationRuleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGuildEmoji>> GetEmojisAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildEmoji> GetEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildEmoji> CreateEmojiAsync(IDiscordGuildEmojiProperties guildEmojiProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildEmoji> ModifyEmojiAsync(ulong emojiId, Action<IDiscordGuildEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestGuild> GetAsync(bool withCounts = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildPreview> GetPreviewAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestGuild> ModifyAsync(Action<IDiscordGuildOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGuildChannel>> GetChannelsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildChannel> CreateChannelAsync(IDiscordGuildChannelProperties channelProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task ModifyChannelPositionsAsync(IEnumerable<IDiscordGuildChannelPositionProperties> positions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGuildThread>> GetActiveThreadsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildUser> GetUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordGuildUser> GetUsersAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IReadOnlyList<IDiscordGuildUser>> FindUserAsync(string name, int limit, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildUser> AddUserAsync(ulong userId, IDiscordGuildUserProperties userProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildUser> ModifyUserAsync(ulong userId, Action<IDiscordGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildUser> ModifyCurrentUserAsync(Action<IDiscordCurrentGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task AddUserRoleAsync(ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task RemoveUserRoleAsync(ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task KickUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordGuildBan> GetBansAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordGuildBan> GetBanAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task BanUserAsync(ulong userId, int deleteMessageSeconds = 0, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildBulkBan> BanUsersAsync(IEnumerable<ulong> userIds, int deleteMessageSeconds = 0, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task UnbanUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordRole>> GetRolesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRole> GetRoleAsync(ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRole> CreateRoleAsync(IDiscordRoleProperties guildRoleProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordRole>> ModifyRolePositionsAsync(IEnumerable<IDiscordRolePositionProperties> positions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRole> ModifyRoleAsync(ulong roleId, Action<IDiscordRoleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteRoleAsync(ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<NetCord.MfaLevel> ModifyMfaLevelAsync(NetCord.MfaLevel mfaLevel, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<int> GetPruneCountAsync(int days, IEnumerable<ulong>? roles = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<int?> PruneAsync(IDiscordGuildPruneProperties pruneProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IEnumerable<IDiscordVoiceRegion>> GetVoiceRegionsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IEnumerable<IDiscordRestInvite>> GetInvitesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordIntegration>> GetIntegrationsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteIntegrationAsync(ulong integrationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildWidgetSettings> GetWidgetSettingsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildWidgetSettings> ModifyWidgetSettingsAsync(Action<IDiscordGuildWidgetSettingsOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildWidget> GetWidgetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildVanityInvite> GetVanityInviteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildWelcomeScreen> GetWelcomeScreenAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildWelcomeScreen> ModifyWelcomeScreenAsync(Action<IDiscordGuildWelcomeScreenOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildOnboarding> GetOnboardingAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildOnboarding> ModifyOnboardingAsync(Action<IDiscordGuildOnboardingOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGuildScheduledEvent>> GetScheduledEventsAsync(bool withUserCount = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildScheduledEvent> CreateScheduledEventAsync(IDiscordGuildScheduledEventProperties guildScheduledEventProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildScheduledEvent> GetScheduledEventAsync(ulong scheduledEventId, bool withUserCount = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildScheduledEvent> ModifyScheduledEventAsync(ulong scheduledEventId, Action<IDiscordGuildScheduledEventOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteScheduledEventAsync(ulong scheduledEventId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordGuildScheduledEventUser> GetScheduledEventUsersAsync(ulong scheduledEventId, IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IEnumerable<IDiscordGuildTemplate>> GetTemplatesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildTemplate> CreateTemplateAsync(IDiscordGuildTemplateProperties guildTemplateProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildTemplate> SyncTemplateAsync(string templateCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildTemplate> ModifyTemplateAsync(string templateCode, Action<IDiscordGuildTemplateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildTemplate> DeleteTemplateAsync(string templateCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGuildApplicationCommand>> GetApplicationCommandsAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildApplicationCommand> CreateApplicationCommandAsync(ulong applicationId, IDiscordApplicationCommandProperties applicationCommandProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildApplicationCommand> GetApplicationCommandAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildApplicationCommand> ModifyApplicationCommandAsync(ulong applicationId, ulong commandId, Action<IDiscordApplicationCommandOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteApplicationCommandAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGuildApplicationCommand>> BulkOverwriteApplicationCommandsAsync(ulong applicationId, IEnumerable<IDiscordApplicationCommandProperties> commands, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordApplicationCommandGuildPermissions>> GetApplicationCommandsPermissionsAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationCommandGuildPermissions> GetApplicationCommandPermissionsAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationCommandGuildPermissions> OverwriteApplicationCommandPermissionsAsync(ulong applicationId, ulong commandId, IEnumerable<IDiscordApplicationCommandGuildPermissionProperties> newPermissions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGuildSticker>> GetStickersAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildSticker> GetStickerAsync(ulong stickerId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildSticker> CreateStickerAsync(IDiscordGuildStickerProperties sticker, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildSticker> ModifyStickerAsync(ulong stickerId, Action<IDiscordGuildStickerOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteStickerAsync(ulong stickerId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordGuildUserInfo> SearchUsersAsync(IDiscordGuildUsersSearchPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordGuildUser> GetCurrentUserGuildUserAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task LeaveAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordVoiceState> GetCurrentUserVoiceStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordVoiceState> GetUserVoiceStateAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task ModifyCurrentUserVoiceStateAsync(Action<IDiscordCurrentUserVoiceStateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task ModifyUserVoiceStateAsync(ulong channelId, ulong userId, Action<IDiscordVoiceStateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordWebhook>> GetWebhooksAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordGuildPreview
{
    NetCord.Rest.GuildPreview Original { get; }
    ulong Id { get; }
    string Name { get; }
    string IconHash { get; }
    string SplashHash { get; }
    string DiscoverySplashHash { get; }
    ImmutableDictionary<ulong, IDiscordGuildEmoji> Emojis { get; }
    IReadOnlyList<string> Features { get; }
    int ApproximateUserCount { get; }
    int ApproximatePresenceCount { get; }
    string Description { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordGuildOptions
{
    NetCord.Rest.GuildOptions Original { get; }
    string Name { get; }
    NetCord.VerificationLevel? VerificationLevel { get; }
    NetCord.DefaultMessageNotificationLevel? DefaultMessageNotificationLevel { get; }
    NetCord.ContentFilter? ContentFilter { get; }
    ulong? AfkChannelId { get; }
    int? AfkTimeout { get; }
    NetCord.Rest.ImageProperties? Icon { get; }
    ulong? OwnerId { get; }
    NetCord.Rest.ImageProperties? Splash { get; }
    NetCord.Rest.ImageProperties? DiscoverySplash { get; }
    NetCord.Rest.ImageProperties? Banner { get; }
    ulong? SystemChannelId { get; }
    NetCord.Rest.SystemChannelFlags? SystemChannelFlags { get; }
    ulong? RulesChannelId { get; }
    ulong? PublicUpdatesChannelId { get; }
    string PreferredLocale { get; }
    IEnumerable<string> Features { get; }
    string Description { get; }
    bool? PremiumProgressBarEnabled { get; }
    ulong? SafetyAlertsChannelId { get; }
    IDiscordGuildOptions WithName(string name);
    IDiscordGuildOptions WithVerificationLevel(NetCord.VerificationLevel? verificationLevel);
    IDiscordGuildOptions WithDefaultMessageNotificationLevel(NetCord.DefaultMessageNotificationLevel? defaultMessageNotificationLevel);
    IDiscordGuildOptions WithContentFilter(NetCord.ContentFilter? contentFilter);
    IDiscordGuildOptions WithAfkChannelId(ulong? afkChannelId);
    IDiscordGuildOptions WithAfkTimeout(int? afkTimeout);
    IDiscordGuildOptions WithIcon(NetCord.Rest.ImageProperties? icon);
    IDiscordGuildOptions WithOwnerId(ulong? ownerId);
    IDiscordGuildOptions WithSplash(NetCord.Rest.ImageProperties? splash);
    IDiscordGuildOptions WithDiscoverySplash(NetCord.Rest.ImageProperties? discoverySplash);
    IDiscordGuildOptions WithBanner(NetCord.Rest.ImageProperties? banner);
    IDiscordGuildOptions WithSystemChannelId(ulong? systemChannelId);
    IDiscordGuildOptions WithSystemChannelFlags(NetCord.Rest.SystemChannelFlags? systemChannelFlags);
    IDiscordGuildOptions WithRulesChannelId(ulong? rulesChannelId);
    IDiscordGuildOptions WithPublicUpdatesChannelId(ulong? publicUpdatesChannelId);
    IDiscordGuildOptions WithPreferredLocale(string preferredLocale);
    IDiscordGuildOptions WithFeatures(IEnumerable<string> features);
    IDiscordGuildOptions AddFeatures(IEnumerable<string> features);
    IDiscordGuildOptions AddFeatures(string[] features);
    IDiscordGuildOptions WithDescription(string description);
    IDiscordGuildOptions WithPremiumProgressBarEnabled(bool? premiumProgressBarEnabled = true);
    IDiscordGuildOptions WithSafetyAlertsChannelId(ulong? safetyAlertsChannelId);
}


public interface IDiscordGuildChannelProperties
{
    NetCord.Rest.GuildChannelProperties Original { get; }
    string Name { get; }
    NetCord.ChannelType Type { get; }
    string Topic { get; }
    int? Bitrate { get; }
    int? UserLimit { get; }
    int? Slowmode { get; }
    int? Position { get; }
    IEnumerable<IDiscordPermissionOverwriteProperties> PermissionOverwrites { get; }
    ulong? ParentId { get; }
    bool? Nsfw { get; }
    string RtcRegion { get; }
    NetCord.VideoQualityMode? VideoQualityMode { get; }
    NetCord.ThreadArchiveDuration? DefaultAutoArchiveDuration { get; }
    NetCord.Rest.ForumGuildChannelDefaultReactionProperties? DefaultReactionEmoji { get; }
    IEnumerable<IDiscordForumTagProperties> AvailableTags { get; }
    NetCord.SortOrderType? DefaultSortOrder { get; }
    NetCord.ForumLayoutType? DefaultForumLayout { get; }
    int? DefaultThreadSlowmode { get; }
    IDiscordGuildChannelProperties WithName(string name);
    IDiscordGuildChannelProperties WithType(NetCord.ChannelType type);
    IDiscordGuildChannelProperties WithTopic(string topic);
    IDiscordGuildChannelProperties WithBitrate(int? bitrate);
    IDiscordGuildChannelProperties WithUserLimit(int? userLimit);
    IDiscordGuildChannelProperties WithSlowmode(int? slowmode);
    IDiscordGuildChannelProperties WithPosition(int? position);
    IDiscordGuildChannelProperties WithPermissionOverwrites(IEnumerable<IDiscordPermissionOverwriteProperties> permissionOverwrites);
    IDiscordGuildChannelProperties AddPermissionOverwrites(IEnumerable<IDiscordPermissionOverwriteProperties> permissionOverwrites);
    IDiscordGuildChannelProperties AddPermissionOverwrites(IDiscordPermissionOverwriteProperties[] permissionOverwrites);
    IDiscordGuildChannelProperties WithParentId(ulong? parentId);
    IDiscordGuildChannelProperties WithNsfw(bool? nsfw = true);
    IDiscordGuildChannelProperties WithRtcRegion(string rtcRegion);
    IDiscordGuildChannelProperties WithVideoQualityMode(NetCord.VideoQualityMode? videoQualityMode);
    IDiscordGuildChannelProperties WithDefaultAutoArchiveDuration(NetCord.ThreadArchiveDuration? defaultAutoArchiveDuration);
    IDiscordGuildChannelProperties WithDefaultReactionEmoji(NetCord.Rest.ForumGuildChannelDefaultReactionProperties? defaultReactionEmoji);
    IDiscordGuildChannelProperties WithAvailableTags(IEnumerable<IDiscordForumTagProperties> availableTags);
    IDiscordGuildChannelProperties AddAvailableTags(IEnumerable<IDiscordForumTagProperties> availableTags);
    IDiscordGuildChannelProperties AddAvailableTags(IDiscordForumTagProperties[] availableTags);
    IDiscordGuildChannelProperties WithDefaultSortOrder(NetCord.SortOrderType? defaultSortOrder);
    IDiscordGuildChannelProperties WithDefaultForumLayout(NetCord.ForumLayoutType? defaultForumLayout);
    IDiscordGuildChannelProperties WithDefaultThreadSlowmode(int? defaultThreadSlowmode);
}


public interface IDiscordGuildChannelPositionProperties
{
    NetCord.Rest.GuildChannelPositionProperties Original { get; }
    ulong Id { get; }
    int? Position { get; }
    bool? LockPermissions { get; }
    ulong? ParentId { get; }
    IDiscordGuildChannelPositionProperties WithId(ulong id);
    IDiscordGuildChannelPositionProperties WithPosition(int? position);
    IDiscordGuildChannelPositionProperties WithLockPermissions(bool? lockPermissions = true);
    IDiscordGuildChannelPositionProperties WithParentId(ulong? parentId);
}


public interface IDiscordPaginationProperties<T>
{
    NetCord.Rest.PaginationProperties<T> Original { get; }
    T? From { get; }
    NetCord.Rest.PaginationDirection? Direction { get; }
    int? BatchSize { get; }
    IDiscordPaginationProperties<T> WithFrom(T? from);
    IDiscordPaginationProperties<T> WithDirection(NetCord.Rest.PaginationDirection? direction);
    IDiscordPaginationProperties<T> WithBatchSize(int? batchSize);
}


public interface IDiscordGuildUserProperties
{
    NetCord.Rest.GuildUserProperties Original { get; }
    string AccessToken { get; }
    string Nickname { get; }
    IEnumerable<ulong> RolesIds { get; }
    bool? Muted { get; }
    bool? Deafened { get; }
    IDiscordGuildUserProperties WithAccessToken(string accessToken);
    IDiscordGuildUserProperties WithNickname(string nickname);
    IDiscordGuildUserProperties WithRolesIds(IEnumerable<ulong> rolesIds);
    IDiscordGuildUserProperties AddRolesIds(IEnumerable<ulong> rolesIds);
    IDiscordGuildUserProperties AddRolesIds(ulong[] rolesIds);
    IDiscordGuildUserProperties WithMuted(bool? muted = true);
    IDiscordGuildUserProperties WithDeafened(bool? deafened = true);
}


public interface IDiscordGuildUserOptions
{
    NetCord.Rest.GuildUserOptions Original { get; }
    IEnumerable<ulong> RoleIds { get; }
    bool? Muted { get; }
    bool? Deafened { get; }
    ulong? ChannelId { get; }
    System.DateTimeOffset? TimeOutUntil { get; }
    NetCord.GuildUserFlags? GuildFlags { get; }
    string Nickname { get; }
    IDiscordGuildUserOptions WithRoleIds(IEnumerable<ulong> roleIds);
    IDiscordGuildUserOptions AddRoleIds(IEnumerable<ulong> roleIds);
    IDiscordGuildUserOptions AddRoleIds(ulong[] roleIds);
    IDiscordGuildUserOptions WithMuted(bool? muted = true);
    IDiscordGuildUserOptions WithDeafened(bool? deafened = true);
    IDiscordGuildUserOptions WithChannelId(ulong? channelId);
    IDiscordGuildUserOptions WithTimeOutUntil(System.DateTimeOffset? timeOutUntil);
    IDiscordGuildUserOptions WithGuildFlags(NetCord.GuildUserFlags? guildFlags);
    IDiscordGuildUserOptions WithNickname(string nickname);
}


public interface IDiscordCurrentGuildUserOptions
{
    NetCord.Rest.CurrentGuildUserOptions Original { get; }
    string Nickname { get; }
    IDiscordCurrentGuildUserOptions WithNickname(string nickname);
}


public interface IDiscordGuildBan
{
    NetCord.Rest.GuildBan Original { get; }
    string Reason { get; }
    IDiscordUser User { get; }
    ulong GuildId { get; }
    Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordGuildBulkBan
{
    NetCord.Rest.GuildBulkBan Original { get; }
    IReadOnlyList<ulong> BannedUsers { get; }
    IReadOnlyList<ulong> FailedUsers { get; }
}


public interface IDiscordRoleProperties
{
    NetCord.Rest.RoleProperties Original { get; }
    string Name { get; }
    NetCord.Permissions? Permissions { get; }
    NetCord.Color? Color { get; }
    bool? Hoist { get; }
    NetCord.Rest.ImageProperties? Icon { get; }
    string UnicodeIcon { get; }
    bool? Mentionable { get; }
    IDiscordRoleProperties WithName(string name);
    IDiscordRoleProperties WithPermissions(NetCord.Permissions? permissions);
    IDiscordRoleProperties WithColor(NetCord.Color? color);
    IDiscordRoleProperties WithHoist(bool? hoist = true);
    IDiscordRoleProperties WithIcon(NetCord.Rest.ImageProperties? icon);
    IDiscordRoleProperties WithUnicodeIcon(string unicodeIcon);
    IDiscordRoleProperties WithMentionable(bool? mentionable = true);
}


public interface IDiscordRolePositionProperties
{
    NetCord.Rest.RolePositionProperties Original { get; }
    ulong Id { get; }
    int? Position { get; }
    IDiscordRolePositionProperties WithId(ulong id);
    IDiscordRolePositionProperties WithPosition(int? position);
}


public interface IDiscordRoleOptions
{
    NetCord.Rest.RoleOptions Original { get; }
    string Name { get; }
    NetCord.Permissions? Permissions { get; }
    NetCord.Color? Color { get; }
    bool? Hoist { get; }
    NetCord.Rest.ImageProperties? Icon { get; }
    string UnicodeIcon { get; }
    bool? Mentionable { get; }
    IDiscordRoleOptions WithName(string name);
    IDiscordRoleOptions WithPermissions(NetCord.Permissions? permissions);
    IDiscordRoleOptions WithColor(NetCord.Color? color);
    IDiscordRoleOptions WithHoist(bool? hoist = true);
    IDiscordRoleOptions WithIcon(NetCord.Rest.ImageProperties? icon);
    IDiscordRoleOptions WithUnicodeIcon(string unicodeIcon);
    IDiscordRoleOptions WithMentionable(bool? mentionable = true);
}


public interface IDiscordGuildPruneProperties
{
    NetCord.Rest.GuildPruneProperties Original { get; }
    int Days { get; }
    bool ComputePruneCount { get; }
    IEnumerable<ulong> Roles { get; }
    IDiscordGuildPruneProperties WithDays(int days);
    IDiscordGuildPruneProperties WithComputePruneCount(bool computePruneCount = true);
    IDiscordGuildPruneProperties WithRoles(IEnumerable<ulong> roles);
    IDiscordGuildPruneProperties AddRoles(IEnumerable<ulong> roles);
    IDiscordGuildPruneProperties AddRoles(ulong[] roles);
}


public interface IDiscordVoiceRegion
{
    NetCord.Rest.VoiceRegion Original { get; }
    string Id { get; }
    string Name { get; }
    bool Optimal { get; }
    bool Deprecated { get; }
    bool Custom { get; }
}


public interface IDiscordRestInvite
{
    NetCord.Rest.RestInvite Original { get; }
    NetCord.InviteType Type { get; }
    string Code { get; }
    IDiscordRestGuild Guild { get; }
    IDiscordChannel Channel { get; }
    IDiscordUser Inviter { get; }
    NetCord.InviteTargetType? TargetType { get; }
    IDiscordUser TargetUser { get; }
    IDiscordApplication TargetApplication { get; }
    int? ApproximatePresenceCount { get; }
    int? ApproximateUserCount { get; }
    System.DateTimeOffset? ExpiresAt { get; }
    IDiscordStageInstance StageInstance { get; }
    IDiscordGuildScheduledEvent GuildScheduledEvent { get; }
    int? Uses { get; }
    int? MaxUses { get; }
    int? MaxAge { get; }
    bool? Temporary { get; }
    System.DateTimeOffset? CreatedAt { get; }
    Task<IDiscordRestInvite> GetGuildAsync(bool withCounts = false, bool withExpiration = false, ulong? guildScheduledEventId = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestInvite> DeleteGuildAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordIntegration
{
    NetCord.Integration Original { get; }
    ulong Id { get; }
    string Name { get; }
    NetCord.IntegrationType Type { get; }
    bool Enabled { get; }
    bool? Syncing { get; }
    ulong? RoleId { get; }
    bool? EnableEmoticons { get; }
    NetCord.IntegrationExpireBehavior? ExpireBehavior { get; }
    int? ExpireGracePeriod { get; }
    IDiscordUser User { get; }
    IDiscordAccount Account { get; }
    System.DateTimeOffset? SyncedAt { get; }
    int? SubscriberCount { get; }
    bool? Revoked { get; }
    IDiscordIntegrationApplication Application { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordGuildWidgetSettings
{
    NetCord.Rest.GuildWidgetSettings Original { get; }
    bool Enabled { get; }
    ulong? ChannelId { get; }
}


public interface IDiscordGuildWidgetSettingsOptions
{
    NetCord.Rest.GuildWidgetSettingsOptions Original { get; }
    bool Enabled { get; }
    ulong? ChannelId { get; }
    IDiscordGuildWidgetSettingsOptions WithEnabled(bool enabled = true);
    IDiscordGuildWidgetSettingsOptions WithChannelId(ulong? channelId);
}


public interface IDiscordGuildWidget
{
    NetCord.Rest.GuildWidget Original { get; }
    ulong Id { get; }
    string Name { get; }
    string InstantInvite { get; }
    ImmutableDictionary<ulong, IDiscordGuildWidgetChannel> Channels { get; }
    ImmutableDictionary<ulong, IDiscordUser> Users { get; }
    int PresenceCount { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordGuildVanityInvite
{
    NetCord.Rest.GuildVanityInvite Original { get; }
    string Code { get; }
    int Uses { get; }
}


public interface IDiscordGuildWelcomeScreenOptions
{
    NetCord.Rest.GuildWelcomeScreenOptions Original { get; }
    bool? Enabled { get; }
    IEnumerable<IDiscordGuildWelcomeScreenChannelProperties> WelcomeChannels { get; }
    string Description { get; }
    IDiscordGuildWelcomeScreenOptions WithEnabled(bool? enabled = true);
    IDiscordGuildWelcomeScreenOptions WithWelcomeChannels(IEnumerable<IDiscordGuildWelcomeScreenChannelProperties> welcomeChannels);
    IDiscordGuildWelcomeScreenOptions AddWelcomeChannels(IEnumerable<IDiscordGuildWelcomeScreenChannelProperties> welcomeChannels);
    IDiscordGuildWelcomeScreenOptions AddWelcomeChannels(IDiscordGuildWelcomeScreenChannelProperties[] welcomeChannels);
    IDiscordGuildWelcomeScreenOptions WithDescription(string description);
}


public interface IDiscordGuildOnboarding
{
    NetCord.Rest.GuildOnboarding Original { get; }
    ulong GuildId { get; }
    IReadOnlyList<IDiscordGuildOnboardingPrompt> Prompts { get; }
    IReadOnlyList<ulong> DefaultChannelIds { get; }
    bool Enabled { get; }
    NetCord.Rest.GuildOnboardingMode Mode { get; }
}


public interface IDiscordGuildOnboardingOptions
{
    NetCord.Rest.GuildOnboardingOptions Original { get; }
    IEnumerable<IDiscordGuildOnboardingPromptProperties> Prompts { get; }
    IEnumerable<ulong> DefaultChannelIds { get; }
    bool? Enabled { get; }
    NetCord.Rest.GuildOnboardingMode? Mode { get; }
    IDiscordGuildOnboardingOptions WithPrompts(IEnumerable<IDiscordGuildOnboardingPromptProperties> prompts);
    IDiscordGuildOnboardingOptions AddPrompts(IEnumerable<IDiscordGuildOnboardingPromptProperties> prompts);
    IDiscordGuildOnboardingOptions AddPrompts(IDiscordGuildOnboardingPromptProperties[] prompts);
    IDiscordGuildOnboardingOptions WithDefaultChannelIds(IEnumerable<ulong> defaultChannelIds);
    IDiscordGuildOnboardingOptions AddDefaultChannelIds(IEnumerable<ulong> defaultChannelIds);
    IDiscordGuildOnboardingOptions AddDefaultChannelIds(ulong[] defaultChannelIds);
    IDiscordGuildOnboardingOptions WithEnabled(bool? enabled = true);
    IDiscordGuildOnboardingOptions WithMode(NetCord.Rest.GuildOnboardingMode? mode);
}


public interface IDiscordGuildScheduledEventProperties
{
    NetCord.Rest.GuildScheduledEventProperties Original { get; }
    ulong? ChannelId { get; }
    IDiscordGuildScheduledEventMetadataProperties Metadata { get; }
    string Name { get; }
    NetCord.GuildScheduledEventPrivacyLevel PrivacyLevel { get; }
    System.DateTimeOffset ScheduledStartTime { get; }
    System.DateTimeOffset? ScheduledEndTime { get; }
    string Description { get; }
    NetCord.GuildScheduledEventEntityType EntityType { get; }
    NetCord.Rest.ImageProperties? Image { get; }
    IDiscordGuildScheduledEventProperties WithChannelId(ulong? channelId);
    IDiscordGuildScheduledEventProperties WithMetadata(IDiscordGuildScheduledEventMetadataProperties metadata);
    IDiscordGuildScheduledEventProperties WithName(string name);
    IDiscordGuildScheduledEventProperties WithPrivacyLevel(NetCord.GuildScheduledEventPrivacyLevel privacyLevel);
    IDiscordGuildScheduledEventProperties WithScheduledStartTime(System.DateTimeOffset scheduledStartTime);
    IDiscordGuildScheduledEventProperties WithScheduledEndTime(System.DateTimeOffset? scheduledEndTime);
    IDiscordGuildScheduledEventProperties WithDescription(string description);
    IDiscordGuildScheduledEventProperties WithEntityType(NetCord.GuildScheduledEventEntityType entityType);
    IDiscordGuildScheduledEventProperties WithImage(NetCord.Rest.ImageProperties? image);
}


public interface IDiscordGuildScheduledEventOptions
{
    NetCord.Rest.GuildScheduledEventOptions Original { get; }
    ulong? ChannelId { get; }
    IDiscordGuildScheduledEventMetadataProperties Metadata { get; }
    string Name { get; }
    NetCord.GuildScheduledEventPrivacyLevel? PrivacyLevel { get; }
    System.DateTimeOffset? ScheduledStartTime { get; }
    System.DateTimeOffset? ScheduledEndTime { get; }
    string Description { get; }
    NetCord.GuildScheduledEventEntityType? EntityType { get; }
    NetCord.GuildScheduledEventStatus? Status { get; }
    NetCord.Rest.ImageProperties? Image { get; }
    IDiscordGuildScheduledEventOptions WithChannelId(ulong? channelId);
    IDiscordGuildScheduledEventOptions WithMetadata(IDiscordGuildScheduledEventMetadataProperties metadata);
    IDiscordGuildScheduledEventOptions WithName(string name);
    IDiscordGuildScheduledEventOptions WithPrivacyLevel(NetCord.GuildScheduledEventPrivacyLevel? privacyLevel);
    IDiscordGuildScheduledEventOptions WithScheduledStartTime(System.DateTimeOffset? scheduledStartTime);
    IDiscordGuildScheduledEventOptions WithScheduledEndTime(System.DateTimeOffset? scheduledEndTime);
    IDiscordGuildScheduledEventOptions WithDescription(string description);
    IDiscordGuildScheduledEventOptions WithEntityType(NetCord.GuildScheduledEventEntityType? entityType);
    IDiscordGuildScheduledEventOptions WithStatus(NetCord.GuildScheduledEventStatus? status);
    IDiscordGuildScheduledEventOptions WithImage(NetCord.Rest.ImageProperties? image);
}


public interface IDiscordGuildScheduledEventUser
{
    NetCord.Rest.GuildScheduledEventUser Original { get; }
    ulong ScheduledEventId { get; }
    IDiscordUser User { get; }
}


public interface IDiscordOptionalGuildUsersPaginationProperties
{
    NetCord.Rest.OptionalGuildUsersPaginationProperties Original { get; }
    bool WithGuildUsers { get; }
    ulong? From { get; }
    NetCord.Rest.PaginationDirection? Direction { get; }
    int? BatchSize { get; }
    IDiscordOptionalGuildUsersPaginationProperties WithWithGuildUsers(bool withGuildUsers = true);
    IDiscordOptionalGuildUsersPaginationProperties WithFrom(ulong? from);
    IDiscordOptionalGuildUsersPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction);
    IDiscordOptionalGuildUsersPaginationProperties WithBatchSize(int? batchSize);
}


public interface IDiscordGuildTemplate
{
    NetCord.GuildTemplate Original { get; }
    string Code { get; }
    string Name { get; }
    string Description { get; }
    int UsageCount { get; }
    ulong CreatorId { get; }
    IDiscordUser Creator { get; }
    System.DateTimeOffset CreatedAt { get; }
    System.DateTimeOffset UpdatedAt { get; }
    ulong SourceGuildId { get; }
    IDiscordGuildTemplatePreview Preview { get; }
    bool? IsDirty { get; }
    Task<IDiscordGuildTemplate> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestGuild> CreateGuildAsync(IDiscordGuildFromGuildTemplateProperties guildProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildTemplate> SyncAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildTemplate> ModifyAsync(Action<IDiscordGuildTemplateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildTemplate> DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordGuildTemplateProperties
{
    NetCord.Rest.GuildTemplateProperties Original { get; }
    string Name { get; }
    string Description { get; }
    IDiscordGuildTemplateProperties WithName(string name);
    IDiscordGuildTemplateProperties WithDescription(string description);
}


public interface IDiscordGuildTemplateOptions
{
    NetCord.Rest.GuildTemplateOptions Original { get; }
    string Name { get; }
    string Description { get; }
    IDiscordGuildTemplateOptions WithName(string name);
    IDiscordGuildTemplateOptions WithDescription(string description);
}


public interface IDiscordGuildApplicationCommand
{
    NetCord.Rest.GuildApplicationCommand Original { get; }
    ulong GuildId { get; }
    ulong Id { get; }
    NetCord.ApplicationCommandType Type { get; }
    ulong ApplicationId { get; }
    string Name { get; }
    IReadOnlyDictionary<string, string> NameLocalizations { get; }
    string Description { get; }
    IReadOnlyDictionary<string, string> DescriptionLocalizations { get; }
    NetCord.Permissions? DefaultGuildUserPermissions { get; }
    bool DMPermission { get; }
    IReadOnlyList<IDiscordApplicationCommandOption> Options { get; }
    bool DefaultPermission { get; }
    bool Nsfw { get; }
    IReadOnlyList<NetCord.ApplicationIntegrationType> IntegrationTypes { get; }
    IReadOnlyList<NetCord.InteractionContextType> Contexts { get; }
    ulong Version { get; }
    System.DateTimeOffset CreatedAt { get; }
    Task<IDiscordApplicationCommand> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationCommand> ModifyAsync(Action<IDiscordApplicationCommandOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationCommandGuildPermissions> GetPermissionsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationCommandGuildPermissions> OverwritePermissionsAsync(IEnumerable<IDiscordApplicationCommandGuildPermissionProperties> newPermissions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationCommandGuildPermissions> GetGuildPermissionsAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationCommandGuildPermissions> OverwriteGuildPermissionsAsync(ulong guildId, IEnumerable<IDiscordApplicationCommandGuildPermissionProperties> newPermissions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordApplicationCommandProperties
{
    NetCord.Rest.ApplicationCommandProperties Original { get; }
    NetCord.ApplicationCommandType Type { get; }
    string Name { get; }
    IReadOnlyDictionary<string, string> NameLocalizations { get; }
    NetCord.Permissions? DefaultGuildUserPermissions { get; }
    bool? DMPermission { get; }
    bool? DefaultPermission { get; }
    IEnumerable<NetCord.ApplicationIntegrationType> IntegrationTypes { get; }
    IEnumerable<NetCord.InteractionContextType> Contexts { get; }
    bool Nsfw { get; }
    IDiscordApplicationCommandProperties WithName(string name);
    IDiscordApplicationCommandProperties WithNameLocalizations(IReadOnlyDictionary<string, string> nameLocalizations);
    IDiscordApplicationCommandProperties WithDefaultGuildUserPermissions(NetCord.Permissions? defaultGuildUserPermissions);
    IDiscordApplicationCommandProperties WithDMPermission(bool? dMPermission = true);
    IDiscordApplicationCommandProperties WithDefaultPermission(bool? defaultPermission = true);
    IDiscordApplicationCommandProperties WithIntegrationTypes(IEnumerable<NetCord.ApplicationIntegrationType> integrationTypes);
    IDiscordApplicationCommandProperties AddIntegrationTypes(IEnumerable<NetCord.ApplicationIntegrationType> integrationTypes);
    IDiscordApplicationCommandProperties AddIntegrationTypes(NetCord.ApplicationIntegrationType[] integrationTypes);
    IDiscordApplicationCommandProperties WithContexts(IEnumerable<NetCord.InteractionContextType> contexts);
    IDiscordApplicationCommandProperties AddContexts(IEnumerable<NetCord.InteractionContextType> contexts);
    IDiscordApplicationCommandProperties AddContexts(NetCord.InteractionContextType[] contexts);
    IDiscordApplicationCommandProperties WithNsfw(bool nsfw = true);
}


public interface IDiscordApplicationCommandOptions
{
    NetCord.Rest.ApplicationCommandOptions Original { get; }
    string Name { get; }
    IReadOnlyDictionary<string, string> NameLocalizations { get; }
    string Description { get; }
    IReadOnlyDictionary<string, string> DescriptionLocalizations { get; }
    IEnumerable<IDiscordApplicationCommandOptionProperties> Options { get; }
    NetCord.Permissions? DefaultGuildUserPermissions { get; }
    bool? DMPermission { get; }
    bool? DefaultPermission { get; }
    IEnumerable<NetCord.ApplicationIntegrationType> IntegrationTypes { get; }
    IEnumerable<NetCord.InteractionContextType> Contexts { get; }
    bool? Nsfw { get; }
    IDiscordApplicationCommandOptions WithName(string name);
    IDiscordApplicationCommandOptions WithNameLocalizations(IReadOnlyDictionary<string, string> nameLocalizations);
    IDiscordApplicationCommandOptions WithDescription(string description);
    IDiscordApplicationCommandOptions WithDescriptionLocalizations(IReadOnlyDictionary<string, string> descriptionLocalizations);
    IDiscordApplicationCommandOptions WithOptions(IEnumerable<IDiscordApplicationCommandOptionProperties> options);
    IDiscordApplicationCommandOptions AddOptions(IEnumerable<IDiscordApplicationCommandOptionProperties> options);
    IDiscordApplicationCommandOptions AddOptions(IDiscordApplicationCommandOptionProperties[] options);
    IDiscordApplicationCommandOptions WithDefaultGuildUserPermissions(NetCord.Permissions? defaultGuildUserPermissions);
    IDiscordApplicationCommandOptions WithDMPermission(bool? dMPermission = true);
    IDiscordApplicationCommandOptions WithDefaultPermission(bool? defaultPermission = true);
    IDiscordApplicationCommandOptions WithIntegrationTypes(IEnumerable<NetCord.ApplicationIntegrationType> integrationTypes);
    IDiscordApplicationCommandOptions AddIntegrationTypes(IEnumerable<NetCord.ApplicationIntegrationType> integrationTypes);
    IDiscordApplicationCommandOptions AddIntegrationTypes(NetCord.ApplicationIntegrationType[] integrationTypes);
    IDiscordApplicationCommandOptions WithContexts(IEnumerable<NetCord.InteractionContextType> contexts);
    IDiscordApplicationCommandOptions AddContexts(IEnumerable<NetCord.InteractionContextType> contexts);
    IDiscordApplicationCommandOptions AddContexts(NetCord.InteractionContextType[] contexts);
    IDiscordApplicationCommandOptions WithNsfw(bool? nsfw = true);
}


public interface IDiscordApplicationCommandGuildPermissions
{
    NetCord.Rest.ApplicationCommandGuildPermissions Original { get; }
    ulong CommandId { get; }
    ulong ApplicationId { get; }
    ulong GuildId { get; }
    IReadOnlyDictionary<ulong, IDiscordApplicationCommandPermission> Permissions { get; }
}


public interface IDiscordApplicationCommandGuildPermissionProperties
{
    NetCord.Rest.ApplicationCommandGuildPermissionProperties Original { get; }
    ulong Id { get; }
    NetCord.ApplicationCommandGuildPermissionType Type { get; }
    bool Permission { get; }
    IDiscordApplicationCommandGuildPermissionProperties WithId(ulong id);
    IDiscordApplicationCommandGuildPermissionProperties WithType(NetCord.ApplicationCommandGuildPermissionType type);
    IDiscordApplicationCommandGuildPermissionProperties WithPermission(bool permission = true);
}


public interface IDiscordGuildStickerProperties
{
    NetCord.Rest.GuildStickerProperties Original { get; }
    IDiscordAttachmentProperties Attachment { get; }
    NetCord.StickerFormat Format { get; }
    IEnumerable<string> Tags { get; }
    HttpContent Serialize();
    IDiscordGuildStickerProperties WithAttachment(IDiscordAttachmentProperties attachment);
    IDiscordGuildStickerProperties WithFormat(NetCord.StickerFormat format);
    IDiscordGuildStickerProperties WithTags(IEnumerable<string> tags);
    IDiscordGuildStickerProperties AddTags(IEnumerable<string> tags);
    IDiscordGuildStickerProperties AddTags(string[] tags);
}


public interface IDiscordGuildStickerOptions
{
    NetCord.Rest.GuildStickerOptions Original { get; }
    string Name { get; }
    string Description { get; }
    string Tags { get; }
    IDiscordGuildStickerOptions WithName(string name);
    IDiscordGuildStickerOptions WithDescription(string description);
    IDiscordGuildStickerOptions WithTags(string tags);
}


public interface IDiscordGuildUserInfo
{
    NetCord.Rest.GuildUserInfo Original { get; }
    IDiscordGuildUser User { get; }
    string SourceInviteCode { get; }
    NetCord.Rest.GuildUserJoinSourceType JoinSourceType { get; }
    ulong? InviterId { get; }
}


public interface IDiscordGuildUsersSearchPaginationProperties
{
    NetCord.Rest.GuildUsersSearchPaginationProperties Original { get; }
    IEnumerable<IDiscordGuildUsersSearchQuery> OrQuery { get; }
    IEnumerable<IDiscordGuildUsersSearchQuery> AndQuery { get; }
    NetCord.Rest.GuildUsersSearchTimestamp? From { get; }
    NetCord.Rest.PaginationDirection? Direction { get; }
    int? BatchSize { get; }
    IDiscordGuildUsersSearchPaginationProperties WithOrQuery(IEnumerable<IDiscordGuildUsersSearchQuery> orQuery);
    IDiscordGuildUsersSearchPaginationProperties AddOrQuery(IEnumerable<IDiscordGuildUsersSearchQuery> orQuery);
    IDiscordGuildUsersSearchPaginationProperties AddOrQuery(IDiscordGuildUsersSearchQuery[] orQuery);
    IDiscordGuildUsersSearchPaginationProperties WithAndQuery(IEnumerable<IDiscordGuildUsersSearchQuery> andQuery);
    IDiscordGuildUsersSearchPaginationProperties AddAndQuery(IEnumerable<IDiscordGuildUsersSearchQuery> andQuery);
    IDiscordGuildUsersSearchPaginationProperties AddAndQuery(IDiscordGuildUsersSearchQuery[] andQuery);
    IDiscordGuildUsersSearchPaginationProperties WithFrom(NetCord.Rest.GuildUsersSearchTimestamp? from);
    IDiscordGuildUsersSearchPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction);
    IDiscordGuildUsersSearchPaginationProperties WithBatchSize(int? batchSize);
}


public interface IDiscordCurrentUserVoiceStateOptions
{
    NetCord.Rest.CurrentUserVoiceStateOptions Original { get; }
    ulong? ChannelId { get; }
    bool? Suppress { get; }
    System.DateTimeOffset? RequestToSpeakTimestamp { get; }
    IDiscordCurrentUserVoiceStateOptions WithChannelId(ulong? channelId);
    IDiscordCurrentUserVoiceStateOptions WithSuppress(bool? suppress = true);
    IDiscordCurrentUserVoiceStateOptions WithRequestToSpeakTimestamp(System.DateTimeOffset? requestToSpeakTimestamp);
}


public interface IDiscordVoiceStateOptions
{
    NetCord.Rest.VoiceStateOptions Original { get; }
    ulong ChannelId { get; }
    bool? Suppress { get; }
    IDiscordVoiceStateOptions WithSuppress(bool? suppress = true);
}


public interface IDiscordWebhook
{
    NetCord.Rest.Webhook Original { get; }
    ulong Id { get; }
    NetCord.Rest.WebhookType Type { get; }
    ulong? GuildId { get; }
    ulong? ChannelId { get; }
    IDiscordUser Creator { get; }
    string Name { get; }
    string AvatarHash { get; }
    ulong? ApplicationId { get; }
    IDiscordRestGuild Guild { get; }
    IDiscordChannel Channel { get; }
    string Url { get; }
    System.DateTimeOffset CreatedAt { get; }
    Task<IDiscordWebhook> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordWebhook> ModifyAsync(Action<IDiscordWebhookOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordMessageProperties
{
    NetCord.Rest.MessageProperties Original { get; }
    string Content { get; }
    IDiscordNonceProperties Nonce { get; }
    bool Tts { get; }
    IEnumerable<IDiscordAttachmentProperties> Attachments { get; }
    IEnumerable<IDiscordEmbedProperties> Embeds { get; }
    IDiscordAllowedMentionsProperties AllowedMentions { get; }
    IDiscordMessageReferenceProperties MessageReference { get; }
    IEnumerable<IDiscordComponentProperties> Components { get; }
    IEnumerable<ulong> StickerIds { get; }
    NetCord.MessageFlags? Flags { get; }
    IDiscordMessagePollProperties Poll { get; }
    HttpContent Serialize();
    IDiscordMessageProperties WithContent(string content);
    IDiscordMessageProperties WithNonce(IDiscordNonceProperties nonce);
    IDiscordMessageProperties WithTts(bool tts = true);
    IDiscordMessageProperties WithAttachments(IEnumerable<IDiscordAttachmentProperties> attachments);
    IDiscordMessageProperties AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments);
    IDiscordMessageProperties AddAttachments(IDiscordAttachmentProperties[] attachments);
    IDiscordMessageProperties WithEmbeds(IEnumerable<IDiscordEmbedProperties> embeds);
    IDiscordMessageProperties AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds);
    IDiscordMessageProperties AddEmbeds(IDiscordEmbedProperties[] embeds);
    IDiscordMessageProperties WithAllowedMentions(IDiscordAllowedMentionsProperties allowedMentions);
    IDiscordMessageProperties WithMessageReference(IDiscordMessageReferenceProperties messageReference);
    IDiscordMessageProperties WithComponents(IEnumerable<IDiscordComponentProperties> components);
    IDiscordMessageProperties AddComponents(IEnumerable<IDiscordComponentProperties> components);
    IDiscordMessageProperties AddComponents(IDiscordComponentProperties[] components);
    IDiscordMessageProperties WithStickerIds(IEnumerable<ulong> stickerIds);
    IDiscordMessageProperties AddStickerIds(IEnumerable<ulong> stickerIds);
    IDiscordMessageProperties AddStickerIds(ulong[] stickerIds);
    IDiscordMessageProperties WithFlags(NetCord.MessageFlags? flags);
    IDiscordMessageProperties WithPoll(IDiscordMessagePollProperties poll);
}


public interface IDiscordReactionEmojiProperties
{
    NetCord.Rest.ReactionEmojiProperties Original { get; }
    string Name { get; }
    ulong? Id { get; }
    IDiscordReactionEmojiProperties WithName(string name);
    IDiscordReactionEmojiProperties WithId(ulong? id);
}


public interface IDiscordMessageReactionsPaginationProperties
{
    NetCord.Rest.MessageReactionsPaginationProperties Original { get; }
    NetCord.ReactionType? Type { get; }
    ulong? From { get; }
    NetCord.Rest.PaginationDirection? Direction { get; }
    int? BatchSize { get; }
    IDiscordMessageReactionsPaginationProperties WithType(NetCord.ReactionType? type);
    IDiscordMessageReactionsPaginationProperties WithFrom(ulong? from);
    IDiscordMessageReactionsPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction);
    IDiscordMessageReactionsPaginationProperties WithBatchSize(int? batchSize);
}


public interface IDiscordGoogleCloudPlatformStorageBucket
{
    NetCord.Rest.GoogleCloudPlatformStorageBucket Original { get; }
    long? Id { get; }
    string UploadUrl { get; }
    string UploadFileName { get; }
}


public interface IDiscordGoogleCloudPlatformStorageBucketProperties
{
    NetCord.Rest.GoogleCloudPlatformStorageBucketProperties Original { get; }
    string FileName { get; }
    long FileSize { get; }
    long? Id { get; }
    IDiscordGoogleCloudPlatformStorageBucketProperties WithFileName(string fileName);
    IDiscordGoogleCloudPlatformStorageBucketProperties WithFileSize(long fileSize);
    IDiscordGoogleCloudPlatformStorageBucketProperties WithId(long? id);
}


public interface IDiscordAvatarDecorationData
{
    NetCord.AvatarDecorationData Original { get; }
    string Hash { get; }
    ulong SkuId { get; }
}


public interface IDiscordDMChannel
{
    NetCord.DMChannel Original { get; }
    IReadOnlyDictionary<ulong, IDiscordUser> Users { get; }
    ulong? LastMessageId { get; }
    System.DateTimeOffset? LastPin { get; }
    ulong Id { get; }
    NetCord.ChannelFlags Flags { get; }
    System.DateTimeOffset CreatedAt { get; }
    Task<IDiscordDMChannel> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordDMChannel> DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordRestMessage> GetMessagesAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IReadOnlyList<IDiscordRestMessage>> GetMessagesAroundAsync(ulong messageId, int? limit = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> SendMessageAsync(IDiscordMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task AddMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordUser> GetMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordMessageReactionsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessagesAsync(IEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessagesAsync(IAsyncEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task TriggerTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDisposable> EnterTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordRestMessage>> GetPinnedMessagesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task PinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task UnpinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordUser> GetMessagePollAnswerVotersAsync(ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordRestMessage> EndMessagePollAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordGuildChannelMention
{
    NetCord.GuildChannelMention Original { get; }
    ulong Id { get; }
    ulong GuildId { get; }
    NetCord.ChannelType Type { get; }
    string Name { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordAttachment
{
    NetCord.Attachment Original { get; }
    ulong Id { get; }
    string FileName { get; }
    string Title { get; }
    string Description { get; }
    string ContentType { get; }
    int Size { get; }
    string Url { get; }
    string ProxyUrl { get; }
    bool Ephemeral { get; }
    NetCord.AttachmentFlags Flags { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordAttachmentExpirationInfo GetExpirationInfo();
}


public interface IDiscordEmbed
{
    NetCord.Embed Original { get; }
    string Title { get; }
    NetCord.EmbedType? Type { get; }
    string Description { get; }
    string Url { get; }
    System.DateTimeOffset? Timestamp { get; }
    NetCord.Color? Color { get; }
    IDiscordEmbedFooter Footer { get; }
    IDiscordEmbedImage Image { get; }
    IDiscordEmbedThumbnail Thumbnail { get; }
    IDiscordEmbedVideo Video { get; }
    IDiscordEmbedProvider Provider { get; }
    IDiscordEmbedAuthor Author { get; }
    IReadOnlyList<IDiscordEmbedField> Fields { get; }
}


public interface IDiscordMessageReaction
{
    NetCord.MessageReaction Original { get; }
    int Count { get; }
    IDiscordMessageReactionCountDetails CountDetails { get; }
    bool Me { get; }
    bool MeBurst { get; }
    IDiscordMessageReactionEmoji Emoji { get; }
    IReadOnlyList<NetCord.Color> BurstColors { get; }
}


public interface IDiscordMessageActivity
{
    NetCord.MessageActivity Original { get; }
    NetCord.MessageActivityType Type { get; }
    string PartyId { get; }
}


public interface IDiscordApplication
{
    NetCord.Application Original { get; }
    ulong Id { get; }
    string Name { get; }
    string IconHash { get; }
    string Description { get; }
    IReadOnlyList<string> RpcOrigins { get; }
    bool? BotPublic { get; }
    bool? BotRequireCodeGrant { get; }
    IDiscordUser Bot { get; }
    string TermsOfServiceUrl { get; }
    string PrivacyPolicyUrl { get; }
    IDiscordUser Owner { get; }
    string VerifyKey { get; }
    IDiscordTeam Team { get; }
    ulong? GuildId { get; }
    IDiscordRestGuild Guild { get; }
    ulong? PrimarySkuId { get; }
    string Slug { get; }
    string CoverImageHash { get; }
    NetCord.ApplicationFlags? Flags { get; }
    int? ApproximateGuildCount { get; }
    int? ApproximateUserInstallCount { get; }
    IReadOnlyList<string> RedirectUris { get; }
    string InteractionsEndpointUrl { get; }
    string RoleConnectionsVerificationUrl { get; }
    IReadOnlyList<string> Tags { get; }
    IDiscordApplicationInstallParams InstallParams { get; }
    IReadOnlyDictionary<NetCord.ApplicationIntegrationType, IDiscordApplicationIntegrationTypeConfiguration> IntegrationTypesConfiguration { get; }
    string CustomInstallUrl { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordImageUrl GetIconUrl(NetCord.ImageFormat format);
    IDiscordImageUrl GetCoverUrl(NetCord.ImageFormat format);
    IDiscordImageUrl GetAssetUrl(ulong assetId, NetCord.ImageFormat format);
    IDiscordImageUrl GetAchievementIconUrl(ulong achievementId, string iconHash, NetCord.ImageFormat format);
    IDiscordImageUrl GetStorePageAssetUrl(ulong assetId, NetCord.ImageFormat format);
    Task<IReadOnlyList<IDiscordApplicationEmoji>> GetEmojisAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationEmoji> GetEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationEmoji> CreateEmojiAsync(IDiscordApplicationEmojiProperties applicationEmojiProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationEmoji> ModifyEmojiAsync(ulong emojiId, Action<IDiscordApplicationEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplication> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordMessageReference
{
    NetCord.MessageReference Original { get; }
    ulong MessageId { get; }
    ulong ChannelId { get; }
    ulong? GuildId { get; }
    bool? FailIfNotExists { get; }
}


public interface IDiscordMessageSnapshot
{
    NetCord.MessageSnapshot Original { get; }
    IDiscordMessageSnapshotMessage Message { get; }
}


public interface IDiscordMessageInteractionMetadata
{
    NetCord.MessageInteractionMetadata Original { get; }
    ulong Id { get; }
    NetCord.InteractionType Type { get; }
    IDiscordUser User { get; }
    IReadOnlyDictionary<NetCord.ApplicationIntegrationType, ulong> AuthorizingIntegrationOwners { get; }
    ulong? OriginalResponseMessageId { get; }
    ulong? InteractedMessageId { get; }
    IDiscordMessageInteractionMetadata TriggeringInteractionMetadata { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordMessageInteraction
{
    NetCord.MessageInteraction Original { get; }
    ulong Id { get; }
    NetCord.InteractionType Type { get; }
    string Name { get; }
    IDiscordUser User { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordComponent
{
    NetCord.IComponent Original { get; }
    int Id { get; }
}


public interface IDiscordMessageSticker
{
    NetCord.MessageSticker Original { get; }
    ulong Id { get; }
    string Name { get; }
    NetCord.StickerFormat Format { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordRoleSubscriptionData
{
    NetCord.RoleSubscriptionData Original { get; }
    ulong RoleSubscriptionListingId { get; }
    string TierName { get; }
    int TotalMonthsSubscribed { get; }
    bool IsRenewal { get; }
}


public interface IDiscordInteractionResolvedData
{
    NetCord.InteractionResolvedData Original { get; }
    IReadOnlyDictionary<ulong, IDiscordUser> Users { get; }
    IReadOnlyDictionary<ulong, IDiscordRole> Roles { get; }
    IReadOnlyDictionary<ulong, IDiscordChannel> Channels { get; }
    IReadOnlyDictionary<ulong, IDiscordAttachment> Attachments { get; }
}


public interface IDiscordMessagePoll
{
    NetCord.MessagePoll Original { get; }
    IDiscordMessagePollMedia Question { get; }
    IReadOnlyList<IDiscordMessagePollAnswer> Answers { get; }
    System.DateTimeOffset? ExpiresAt { get; }
    bool AllowMultiselect { get; }
    NetCord.MessagePollLayoutType LayoutType { get; }
    IDiscordMessagePollResults Results { get; }
}


public interface IDiscordMessageCall
{
    NetCord.Rest.MessageCall Original { get; }
    IReadOnlyList<ulong> Participants { get; }
    System.DateTimeOffset? EndedAt { get; }
}


public interface IDiscordReplyMessageProperties
{
    NetCord.Rest.ReplyMessageProperties Original { get; }
    string Content { get; }
    IDiscordNonceProperties Nonce { get; }
    bool Tts { get; }
    IEnumerable<IDiscordAttachmentProperties> Attachments { get; }
    IEnumerable<IDiscordEmbedProperties> Embeds { get; }
    IDiscordAllowedMentionsProperties AllowedMentions { get; }
    bool? FailIfNotExists { get; }
    IEnumerable<IDiscordComponentProperties> Components { get; }
    IEnumerable<ulong> StickerIds { get; }
    NetCord.MessageFlags? Flags { get; }
    IDiscordMessagePollProperties Poll { get; }
    IDiscordMessageProperties ToMessageProperties(ulong messageReferenceId);
    IDiscordReplyMessageProperties WithContent(string content);
    IDiscordReplyMessageProperties WithNonce(IDiscordNonceProperties nonce);
    IDiscordReplyMessageProperties WithTts(bool tts = true);
    IDiscordReplyMessageProperties WithAttachments(IEnumerable<IDiscordAttachmentProperties> attachments);
    IDiscordReplyMessageProperties AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments);
    IDiscordReplyMessageProperties AddAttachments(IDiscordAttachmentProperties[] attachments);
    IDiscordReplyMessageProperties WithEmbeds(IEnumerable<IDiscordEmbedProperties> embeds);
    IDiscordReplyMessageProperties AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds);
    IDiscordReplyMessageProperties AddEmbeds(IDiscordEmbedProperties[] embeds);
    IDiscordReplyMessageProperties WithAllowedMentions(IDiscordAllowedMentionsProperties allowedMentions);
    IDiscordReplyMessageProperties WithFailIfNotExists(bool? failIfNotExists = true);
    IDiscordReplyMessageProperties WithComponents(IEnumerable<IDiscordComponentProperties> components);
    IDiscordReplyMessageProperties AddComponents(IEnumerable<IDiscordComponentProperties> components);
    IDiscordReplyMessageProperties AddComponents(IDiscordComponentProperties[] components);
    IDiscordReplyMessageProperties WithStickerIds(IEnumerable<ulong> stickerIds);
    IDiscordReplyMessageProperties AddStickerIds(IEnumerable<ulong> stickerIds);
    IDiscordReplyMessageProperties AddStickerIds(ulong[] stickerIds);
    IDiscordReplyMessageProperties WithFlags(NetCord.MessageFlags? flags);
    IDiscordReplyMessageProperties WithPoll(IDiscordMessagePollProperties poll);
}


public interface IDiscordGuildThreadFromMessageProperties
{
    NetCord.Rest.GuildThreadFromMessageProperties Original { get; }
    string Name { get; }
    NetCord.ThreadArchiveDuration? AutoArchiveDuration { get; }
    int? Slowmode { get; }
    IDiscordGuildThreadFromMessageProperties WithName(string name);
    IDiscordGuildThreadFromMessageProperties WithAutoArchiveDuration(NetCord.ThreadArchiveDuration? autoArchiveDuration);
    IDiscordGuildThreadFromMessageProperties WithSlowmode(int? slowmode);
}


public interface IDiscordEmbedProperties
{
    NetCord.Rest.EmbedProperties Original { get; }
    string Title { get; }
    string Description { get; }
    string Url { get; }
    System.DateTimeOffset? Timestamp { get; }
    NetCord.Color Color { get; }
    IDiscordEmbedFooterProperties Footer { get; }
    IDiscordEmbedImageProperties Image { get; }
    IDiscordEmbedThumbnailProperties Thumbnail { get; }
    IDiscordEmbedAuthorProperties Author { get; }
    IEnumerable<IDiscordEmbedFieldProperties> Fields { get; }
    IDiscordEmbedProperties WithTitle(string title);
    IDiscordEmbedProperties WithDescription(string description);
    IDiscordEmbedProperties WithUrl(string url);
    IDiscordEmbedProperties WithTimestamp(System.DateTimeOffset? timestamp);
    IDiscordEmbedProperties WithColor(NetCord.Color color);
    IDiscordEmbedProperties WithFooter(IDiscordEmbedFooterProperties footer);
    IDiscordEmbedProperties WithImage(IDiscordEmbedImageProperties image);
    IDiscordEmbedProperties WithThumbnail(IDiscordEmbedThumbnailProperties thumbnail);
    IDiscordEmbedProperties WithAuthor(IDiscordEmbedAuthorProperties author);
    IDiscordEmbedProperties WithFields(IEnumerable<IDiscordEmbedFieldProperties> fields);
    IDiscordEmbedProperties AddFields(IEnumerable<IDiscordEmbedFieldProperties> fields);
    IDiscordEmbedProperties AddFields(IDiscordEmbedFieldProperties[] fields);
}


public interface IDiscordAllowedMentionsProperties
{
    NetCord.Rest.AllowedMentionsProperties Original { get; }
    bool Everyone { get; }
    IEnumerable<ulong> AllowedRoles { get; }
    IEnumerable<ulong> AllowedUsers { get; }
    bool ReplyMention { get; }
    static IDiscordAllowedMentionsProperties All { get; }
    static IDiscordAllowedMentionsProperties None { get; }
    IDiscordAllowedMentionsProperties WithEveryone(bool everyone = true);
    IDiscordAllowedMentionsProperties WithAllowedRoles(IEnumerable<ulong> allowedRoles);
    IDiscordAllowedMentionsProperties AddAllowedRoles(IEnumerable<ulong> allowedRoles);
    IDiscordAllowedMentionsProperties AddAllowedRoles(ulong[] allowedRoles);
    IDiscordAllowedMentionsProperties WithAllowedUsers(IEnumerable<ulong> allowedUsers);
    IDiscordAllowedMentionsProperties AddAllowedUsers(IEnumerable<ulong> allowedUsers);
    IDiscordAllowedMentionsProperties AddAllowedUsers(ulong[] allowedUsers);
    IDiscordAllowedMentionsProperties WithReplyMention(bool replyMention = true);
}


public interface IDiscordComponentProperties
{
    NetCord.Rest.IComponentProperties Original { get; }
    int? Id { get; }
    NetCord.ComponentType ComponentType { get; }
    IDiscordComponentProperties WithId(int? id);
}


public interface IDiscordAttachmentProperties
{
    NetCord.Rest.AttachmentProperties Original { get; }
    string FileName { get; }
    string Title { get; }
    string Description { get; }
    bool SupportsHttpSerialization { get; }
    HttpContent Serialize();
    IDiscordAttachmentProperties WithFileName(string fileName);
    IDiscordAttachmentProperties WithTitle(string title);
    IDiscordAttachmentProperties WithDescription(string description);
}


public interface IDiscordMessagePollProperties
{
    NetCord.MessagePollProperties Original { get; }
    IDiscordMessagePollMediaProperties Question { get; }
    IEnumerable<IDiscordMessagePollAnswerProperties> Answers { get; }
    int? DurationInHours { get; }
    bool AllowMultiselect { get; }
    NetCord.MessagePollLayoutType? LayoutType { get; }
    IDiscordMessagePollProperties WithQuestion(IDiscordMessagePollMediaProperties question);
    IDiscordMessagePollProperties WithAnswers(IEnumerable<IDiscordMessagePollAnswerProperties> answers);
    IDiscordMessagePollProperties AddAnswers(IEnumerable<IDiscordMessagePollAnswerProperties> answers);
    IDiscordMessagePollProperties AddAnswers(IDiscordMessagePollAnswerProperties[] answers);
    IDiscordMessagePollProperties WithDurationInHours(int? durationInHours);
    IDiscordMessagePollProperties WithAllowMultiselect(bool allowMultiselect = true);
    IDiscordMessagePollProperties WithLayoutType(NetCord.MessagePollLayoutType? layoutType);
}


public interface IDiscordPermissionOverwrite
{
    NetCord.PermissionOverwrite Original { get; }
    ulong Id { get; }
    NetCord.PermissionOverwriteType Type { get; }
    NetCord.Permissions Allowed { get; }
    NetCord.Permissions Denied { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordGuildChannelOptions
{
    NetCord.Rest.GuildChannelOptions Original { get; }
    string Name { get; }
    NetCord.ChannelType? ChannelType { get; }
    int? Position { get; }
    string Topic { get; }
    bool? Nsfw { get; }
    int? Slowmode { get; }
    int? Bitrate { get; }
    int? UserLimit { get; }
    IEnumerable<IDiscordPermissionOverwriteProperties> PermissionOverwrites { get; }
    ulong? ParentId { get; }
    string RtcRegion { get; }
    NetCord.VideoQualityMode? VideoQualityMode { get; }
    NetCord.ThreadArchiveDuration? DefaultAutoArchiveDuration { get; }
    IEnumerable<IDiscordForumTagProperties> AvailableTags { get; }
    NetCord.Rest.ForumGuildChannelDefaultReactionProperties? DefaultReactionEmoji { get; }
    int? DefaultThreadSlowmode { get; }
    NetCord.ChannelFlags? Flags { get; }
    NetCord.SortOrderType? DefaultSortOrder { get; }
    NetCord.ForumLayoutType? DefaultForumLayout { get; }
    bool? Archived { get; }
    NetCord.ThreadArchiveDuration? AutoArchiveDuration { get; }
    bool? Locked { get; }
    bool? Invitable { get; }
    IEnumerable<ulong> AppliedTags { get; }
    IDiscordGuildChannelOptions WithName(string name);
    IDiscordGuildChannelOptions WithChannelType(NetCord.ChannelType? channelType);
    IDiscordGuildChannelOptions WithPosition(int? position);
    IDiscordGuildChannelOptions WithTopic(string topic);
    IDiscordGuildChannelOptions WithNsfw(bool? nsfw = true);
    IDiscordGuildChannelOptions WithSlowmode(int? slowmode);
    IDiscordGuildChannelOptions WithBitrate(int? bitrate);
    IDiscordGuildChannelOptions WithUserLimit(int? userLimit);
    IDiscordGuildChannelOptions WithPermissionOverwrites(IEnumerable<IDiscordPermissionOverwriteProperties> permissionOverwrites);
    IDiscordGuildChannelOptions AddPermissionOverwrites(IEnumerable<IDiscordPermissionOverwriteProperties> permissionOverwrites);
    IDiscordGuildChannelOptions AddPermissionOverwrites(IDiscordPermissionOverwriteProperties[] permissionOverwrites);
    IDiscordGuildChannelOptions WithParentId(ulong? parentId);
    IDiscordGuildChannelOptions WithRtcRegion(string rtcRegion);
    IDiscordGuildChannelOptions WithVideoQualityMode(NetCord.VideoQualityMode? videoQualityMode);
    IDiscordGuildChannelOptions WithDefaultAutoArchiveDuration(NetCord.ThreadArchiveDuration? defaultAutoArchiveDuration);
    IDiscordGuildChannelOptions WithAvailableTags(IEnumerable<IDiscordForumTagProperties> availableTags);
    IDiscordGuildChannelOptions AddAvailableTags(IEnumerable<IDiscordForumTagProperties> availableTags);
    IDiscordGuildChannelOptions AddAvailableTags(IDiscordForumTagProperties[] availableTags);
    IDiscordGuildChannelOptions WithDefaultReactionEmoji(NetCord.Rest.ForumGuildChannelDefaultReactionProperties? defaultReactionEmoji);
    IDiscordGuildChannelOptions WithDefaultThreadSlowmode(int? defaultThreadSlowmode);
    IDiscordGuildChannelOptions WithFlags(NetCord.ChannelFlags? flags);
    IDiscordGuildChannelOptions WithDefaultSortOrder(NetCord.SortOrderType? defaultSortOrder);
    IDiscordGuildChannelOptions WithDefaultForumLayout(NetCord.ForumLayoutType? defaultForumLayout);
    IDiscordGuildChannelOptions WithArchived(bool? archived = true);
    IDiscordGuildChannelOptions WithAutoArchiveDuration(NetCord.ThreadArchiveDuration? autoArchiveDuration);
    IDiscordGuildChannelOptions WithLocked(bool? locked = true);
    IDiscordGuildChannelOptions WithInvitable(bool? invitable = true);
    IDiscordGuildChannelOptions WithAppliedTags(IEnumerable<ulong> appliedTags);
    IDiscordGuildChannelOptions AddAppliedTags(IEnumerable<ulong> appliedTags);
    IDiscordGuildChannelOptions AddAppliedTags(ulong[] appliedTags);
}


public interface IDiscordPermissionOverwriteProperties
{
    NetCord.Rest.PermissionOverwriteProperties Original { get; }
    ulong Id { get; }
    NetCord.PermissionOverwriteType Type { get; }
    NetCord.Permissions? Allowed { get; }
    NetCord.Permissions? Denied { get; }
    IDiscordPermissionOverwriteProperties WithId(ulong id);
    IDiscordPermissionOverwriteProperties WithType(NetCord.PermissionOverwriteType type);
    IDiscordPermissionOverwriteProperties WithAllowed(NetCord.Permissions? allowed);
    IDiscordPermissionOverwriteProperties WithDenied(NetCord.Permissions? denied);
}


public interface IDiscordInviteProperties
{
    NetCord.Rest.InviteProperties Original { get; }
    int? MaxAge { get; }
    int? MaxUses { get; }
    bool? Temporary { get; }
    bool? Unique { get; }
    NetCord.InviteTargetType? TargetType { get; }
    ulong? TargetUserId { get; }
    ulong? TargetApplicationId { get; }
    IDiscordInviteProperties WithMaxAge(int? maxAge);
    IDiscordInviteProperties WithMaxUses(int? maxUses);
    IDiscordInviteProperties WithTemporary(bool? temporary = true);
    IDiscordInviteProperties WithUnique(bool? unique = true);
    IDiscordInviteProperties WithTargetType(NetCord.InviteTargetType? targetType);
    IDiscordInviteProperties WithTargetUserId(ulong? targetUserId);
    IDiscordInviteProperties WithTargetApplicationId(ulong? targetApplicationId);
}


public interface IDiscordGuildThreadMetadata
{
    NetCord.GuildThreadMetadata Original { get; }
    bool Archived { get; }
    NetCord.ThreadArchiveDuration AutoArchiveDuration { get; }
    System.DateTimeOffset ArchiveTimestamp { get; }
    bool Locked { get; }
    bool? Invitable { get; }
}


public interface IDiscordThreadCurrentUser
{
    NetCord.ThreadCurrentUser Original { get; }
    System.DateTimeOffset JoinTimestamp { get; }
    int Flags { get; }
}


public interface IDiscordThreadUser
{
    NetCord.ThreadUser Original { get; }
    ulong Id { get; }
    ulong ThreadId { get; }
    System.DateTimeOffset JoinTimestamp { get; }
    int Flags { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordGuildThreadProperties
{
    NetCord.Rest.GuildThreadProperties Original { get; }
    NetCord.ChannelType? ChannelType { get; }
    bool? Invitable { get; }
    string Name { get; }
    NetCord.ThreadArchiveDuration? AutoArchiveDuration { get; }
    int? Slowmode { get; }
    IDiscordGuildThreadProperties WithChannelType(NetCord.ChannelType? channelType);
    IDiscordGuildThreadProperties WithInvitable(bool? invitable = true);
    IDiscordGuildThreadProperties WithName(string name);
    IDiscordGuildThreadProperties WithAutoArchiveDuration(NetCord.ThreadArchiveDuration? autoArchiveDuration);
    IDiscordGuildThreadProperties WithSlowmode(int? slowmode);
}


public interface IDiscordIncomingWebhook
{
    NetCord.Rest.IncomingWebhook Original { get; }
    string Token { get; }
    ulong Id { get; }
    NetCord.Rest.WebhookType Type { get; }
    ulong? GuildId { get; }
    ulong? ChannelId { get; }
    IDiscordUser Creator { get; }
    string Name { get; }
    string AvatarHash { get; }
    ulong? ApplicationId { get; }
    IDiscordRestGuild Guild { get; }
    IDiscordChannel Channel { get; }
    string Url { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordWebhookClient ToClient(IDiscordWebhookClientConfiguration? configuration = null);
    Task<IDiscordIncomingWebhook> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordIncomingWebhook> GetWithTokenAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordIncomingWebhook> ModifyAsync(Action<IDiscordWebhookOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordIncomingWebhook> ModifyWithTokenAsync(Action<IDiscordWebhookOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteWithTokenAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> ExecuteAsync(IDiscordWebhookMessageProperties message, bool wait = false, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessageAsync(ulong messageId, ulong? threadId = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordWebhookProperties
{
    NetCord.Rest.WebhookProperties Original { get; }
    string Name { get; }
    NetCord.Rest.ImageProperties? Avatar { get; }
    IDiscordWebhookProperties WithName(string name);
    IDiscordWebhookProperties WithAvatar(NetCord.Rest.ImageProperties? avatar);
}


public interface IDiscordUserActivity
{
    NetCord.Gateway.UserActivity Original { get; }
    string Name { get; }
    NetCord.Gateway.UserActivityType Type { get; }
    string Url { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordUserActivityTimestamps Timestamps { get; }
    ulong? ApplicationId { get; }
    string Details { get; }
    string State { get; }
    IDiscordEmoji Emoji { get; }
    IDiscordParty Party { get; }
    IDiscordUserActivityAssets Assets { get; }
    IDiscordUserActivitySecrets Secrets { get; }
    bool? Instance { get; }
    NetCord.Gateway.UserActivityFlags? Flags { get; }
    IReadOnlyList<IDiscordUserActivityButton> Buttons { get; }
    ulong GuildId { get; }
}


public interface IDiscordStageInstanceOptions
{
    NetCord.Rest.StageInstanceOptions Original { get; }
    string Topic { get; }
    NetCord.StageInstancePrivacyLevel? PrivacyLevel { get; }
    IDiscordStageInstanceOptions WithTopic(string topic);
    IDiscordStageInstanceOptions WithPrivacyLevel(NetCord.StageInstancePrivacyLevel? privacyLevel);
}


public interface IDiscordGuildScheduledEventRecurrenceRule
{
    NetCord.GuildScheduledEventRecurrenceRule Original { get; }
    System.DateTimeOffset? StartAt { get; }
    System.DateTimeOffset? EndAt { get; }
    NetCord.GuildScheduledEventRecurrenceRuleFrequency Frequency { get; }
    int Interval { get; }
    IReadOnlyList<NetCord.GuildScheduledEventRecurrenceRuleWeekday> ByWeekday { get; }
    IReadOnlyList<IDiscordGuildScheduledEventRecurrenceRuleNWeekday> ByNWeekday { get; }
    IReadOnlyList<NetCord.GuildScheduledEventRecurrenceRuleMonth> ByMonth { get; }
    IReadOnlyList<int> ByMonthDay { get; }
    IReadOnlyList<int> ByYearDay { get; }
    int? Count { get; }
}


public interface IDiscordRoleTags
{
    NetCord.RoleTags Original { get; }
    ulong? BotId { get; }
    ulong? IntegrationId { get; }
    bool IsPremiumSubscriber { get; }
    ulong? SubscriptionListingId { get; }
    bool IsAvailableForPurchase { get; }
    bool GuildConnections { get; }
}


public interface IDiscordGuildWelcomeScreenChannel
{
    NetCord.GuildWelcomeScreenChannel Original { get; }
    ulong Id { get; }
    string Description { get; }
    ulong? EmojiId { get; }
    string EmojiName { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordRestAuditLogEntryData
{
    NetCord.Rest.RestAuditLogEntryData Original { get; }
    IReadOnlyDictionary<ulong, IDiscordApplicationCommand> ApplicationCommands { get; }
    IReadOnlyDictionary<ulong, IDiscordAutoModerationRule> AutoModerationRules { get; }
    IReadOnlyDictionary<ulong, IDiscordGuildScheduledEvent> GuildScheduledEvents { get; }
    IReadOnlyDictionary<ulong, IDiscordIntegration> Integrations { get; }
    IReadOnlyDictionary<ulong, IDiscordGuildThread> Threads { get; }
    IReadOnlyDictionary<ulong, IDiscordUser> Users { get; }
    IReadOnlyDictionary<ulong, IDiscordWebhook> Webhooks { get; }
}


public interface IDiscordAuditLogChange
{
    NetCord.AuditLogChange Original { get; }
    string Key { get; }
    bool HasNewValue { get; }
    bool HasOldValue { get; }
    IDiscordAuditLogChange<TValue> WithValues<TValue>(JsonTypeInfo<TValue> jsonTypeInfo);
    IDiscordAuditLogChange<TValue> WithValues<TValue>();
}


public interface IDiscordAuditLogEntryInfo
{
    NetCord.AuditLogEntryInfo Original { get; }
    ulong? ApplicationId { get; }
    string AutoModerationRuleName { get; }
    NetCord.AutoModerationRuleTriggerType? AutoModerationRuleTriggerType { get; }
    ulong? ChannelId { get; }
    int? Count { get; }
    int? DeleteGuildUserDays { get; }
    ulong? Id { get; }
    int? GuildUsersRemoved { get; }
    ulong? MessageId { get; }
    string RoleName { get; }
    NetCord.PermissionOverwriteType? Type { get; }
    NetCord.IntegrationType? IntegrationType { get; }
}


public interface IDiscordAuditLogChange<TValue>
{
    NetCord.AuditLogChange<TValue> Original { get; }
    TValue NewValue { get; }
    TValue OldValue { get; }
    string Key { get; }
    bool HasNewValue { get; }
    bool HasOldValue { get; }
    IDiscordAuditLogChange<TValue> WithValues<TValue>(JsonTypeInfo<TValue> jsonTypeInfo);
    IDiscordAuditLogChange<TValue> WithValues<TValue>();
}


public interface IDiscordAutoModerationRuleTriggerMetadata
{
    NetCord.AutoModerationRuleTriggerMetadata Original { get; }
    IReadOnlyList<string> KeywordFilter { get; }
    IReadOnlyList<string> RegexPatterns { get; }
    IReadOnlyList<NetCord.AutoModerationRuleKeywordPresetType> Presets { get; }
    IReadOnlyList<string> AllowList { get; }
    int? MentionTotalLimit { get; }
    bool MentionRaidProtectionEnabled { get; }
}


public interface IDiscordAutoModerationAction
{
    NetCord.AutoModerationAction Original { get; }
    NetCord.AutoModerationActionType Type { get; }
    IDiscordAutoModerationActionMetadata Metadata { get; }
}


public interface IDiscordAutoModerationRuleTriggerMetadataProperties
{
    NetCord.AutoModerationRuleTriggerMetadataProperties Original { get; }
    IEnumerable<string> KeywordFilter { get; }
    IEnumerable<string> RegexPatterns { get; }
    IEnumerable<NetCord.AutoModerationRuleKeywordPresetType> Presets { get; }
    IEnumerable<string> AllowList { get; }
    int? MentionTotalLimit { get; }
    bool MentionRaidProtectionEnabled { get; }
    IDiscordAutoModerationRuleTriggerMetadataProperties WithKeywordFilter(IEnumerable<string> keywordFilter);
    IDiscordAutoModerationRuleTriggerMetadataProperties AddKeywordFilter(IEnumerable<string> keywordFilter);
    IDiscordAutoModerationRuleTriggerMetadataProperties AddKeywordFilter(string[] keywordFilter);
    IDiscordAutoModerationRuleTriggerMetadataProperties WithRegexPatterns(IEnumerable<string> regexPatterns);
    IDiscordAutoModerationRuleTriggerMetadataProperties AddRegexPatterns(IEnumerable<string> regexPatterns);
    IDiscordAutoModerationRuleTriggerMetadataProperties AddRegexPatterns(string[] regexPatterns);
    IDiscordAutoModerationRuleTriggerMetadataProperties WithPresets(IEnumerable<NetCord.AutoModerationRuleKeywordPresetType> presets);
    IDiscordAutoModerationRuleTriggerMetadataProperties AddPresets(IEnumerable<NetCord.AutoModerationRuleKeywordPresetType> presets);
    IDiscordAutoModerationRuleTriggerMetadataProperties AddPresets(NetCord.AutoModerationRuleKeywordPresetType[] presets);
    IDiscordAutoModerationRuleTriggerMetadataProperties WithAllowList(IEnumerable<string> allowList);
    IDiscordAutoModerationRuleTriggerMetadataProperties AddAllowList(IEnumerable<string> allowList);
    IDiscordAutoModerationRuleTriggerMetadataProperties AddAllowList(string[] allowList);
    IDiscordAutoModerationRuleTriggerMetadataProperties WithMentionTotalLimit(int? mentionTotalLimit);
    IDiscordAutoModerationRuleTriggerMetadataProperties WithMentionRaidProtectionEnabled(bool mentionRaidProtectionEnabled = true);
}


public interface IDiscordAutoModerationActionProperties
{
    NetCord.AutoModerationActionProperties Original { get; }
    NetCord.AutoModerationActionType Type { get; }
    IDiscordAutoModerationActionMetadataProperties Metadata { get; }
    IDiscordAutoModerationActionProperties WithType(NetCord.AutoModerationActionType type);
    IDiscordAutoModerationActionProperties WithMetadata(IDiscordAutoModerationActionMetadataProperties metadata);
}


public interface IDiscordForumTagProperties
{
    NetCord.Rest.ForumTagProperties Original { get; }
    ulong? Id { get; }
    string Name { get; }
    bool? Moderated { get; }
    ulong? EmojiId { get; }
    string EmojiName { get; }
    IDiscordForumTagProperties WithId(ulong? id);
    IDiscordForumTagProperties WithName(string name);
    IDiscordForumTagProperties WithModerated(bool? moderated = true);
    IDiscordForumTagProperties WithEmojiId(ulong? emojiId);
    IDiscordForumTagProperties WithEmojiName(string emojiName);
}


public interface IDiscordChannel
{
    NetCord.Channel Original { get; }
    ulong Id { get; }
    NetCord.ChannelFlags Flags { get; }
    System.DateTimeOffset CreatedAt { get; }
    Task<IDiscordChannel> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordChannel> DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordAccount
{
    NetCord.Account Original { get; }
    ulong Id { get; }
    string Name { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordIntegrationApplication
{
    NetCord.IntegrationApplication Original { get; }
    ulong Id { get; }
    string Name { get; }
    string IconHash { get; }
    string Description { get; }
    string Summary { get; }
    IDiscordUser Bot { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordGuildWidgetChannel
{
    NetCord.Rest.GuildWidgetChannel Original { get; }
    ulong Id { get; }
    string Name { get; }
    int Position { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordGuildWelcomeScreenChannelProperties
{
    NetCord.Rest.GuildWelcomeScreenChannelProperties Original { get; }
    ulong ChannelId { get; }
    string Description { get; }
    IDiscordEmojiProperties Emoji { get; }
    IDiscordGuildWelcomeScreenChannelProperties WithChannelId(ulong channelId);
    IDiscordGuildWelcomeScreenChannelProperties WithDescription(string description);
    IDiscordGuildWelcomeScreenChannelProperties WithEmoji(IDiscordEmojiProperties emoji);
}


public interface IDiscordGuildOnboardingPrompt
{
    NetCord.Rest.GuildOnboardingPrompt Original { get; }
    ulong Id { get; }
    NetCord.Rest.GuildOnboardingPromptType Type { get; }
    IReadOnlyList<IDiscordGuildOnboardingPromptOption> Options { get; }
    string Title { get; }
    bool SingleSelect { get; }
    bool Required { get; }
    bool InOnboarding { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordGuildOnboardingPromptProperties
{
    NetCord.Rest.GuildOnboardingPromptProperties Original { get; }
    ulong? Id { get; }
    NetCord.Rest.GuildOnboardingPromptType Type { get; }
    IEnumerable<IDiscordGuildOnboardingPromptOptionProperties> Options { get; }
    string Title { get; }
    bool SingleSelect { get; }
    bool Required { get; }
    bool InOnboarding { get; }
    IDiscordGuildOnboardingPromptProperties WithId(ulong? id);
    IDiscordGuildOnboardingPromptProperties WithType(NetCord.Rest.GuildOnboardingPromptType type);
    IDiscordGuildOnboardingPromptProperties WithOptions(IEnumerable<IDiscordGuildOnboardingPromptOptionProperties> options);
    IDiscordGuildOnboardingPromptProperties AddOptions(IEnumerable<IDiscordGuildOnboardingPromptOptionProperties> options);
    IDiscordGuildOnboardingPromptProperties AddOptions(IDiscordGuildOnboardingPromptOptionProperties[] options);
    IDiscordGuildOnboardingPromptProperties WithTitle(string title);
    IDiscordGuildOnboardingPromptProperties WithSingleSelect(bool singleSelect = true);
    IDiscordGuildOnboardingPromptProperties WithRequired(bool required = true);
    IDiscordGuildOnboardingPromptProperties WithInOnboarding(bool inOnboarding = true);
}


public interface IDiscordGuildScheduledEventMetadataProperties
{
    NetCord.Rest.GuildScheduledEventMetadataProperties Original { get; }
    string Location { get; }
    IDiscordGuildScheduledEventMetadataProperties WithLocation(string location);
}


public interface IDiscordGuildTemplatePreview
{
    NetCord.Rest.GuildTemplatePreview Original { get; }
    string Name { get; }
    string IconHash { get; }
    string Description { get; }
    NetCord.VerificationLevel VerificationLevel { get; }
    NetCord.DefaultMessageNotificationLevel DefaultMessageNotificationLevel { get; }
    NetCord.ContentFilter ContentFilter { get; }
    string PreferredLocale { get; }
    ulong? AfkChannelId { get; }
    int AfkTimeout { get; }
    ulong? SystemChannelId { get; }
    NetCord.Rest.SystemChannelFlags SystemChannelFlags { get; }
    IReadOnlyDictionary<ulong, IDiscordRole> Roles { get; }
    IReadOnlyDictionary<ulong, IDiscordGuildChannel> Channels { get; }
}


public interface IDiscordGuildFromGuildTemplateProperties
{
    NetCord.Rest.GuildFromGuildTemplateProperties Original { get; }
    string Name { get; }
    NetCord.Rest.ImageProperties? Icon { get; }
    IDiscordGuildFromGuildTemplateProperties WithName(string name);
    IDiscordGuildFromGuildTemplateProperties WithIcon(NetCord.Rest.ImageProperties? icon);
}


public interface IDiscordApplicationCommandOption
{
    NetCord.Rest.ApplicationCommandOption Original { get; }
    NetCord.ApplicationCommandOptionType Type { get; }
    string Name { get; }
    IReadOnlyDictionary<string, string> NameLocalizations { get; }
    string Description { get; }
    IReadOnlyDictionary<string, string> DescriptionLocalizations { get; }
    bool Required { get; }
    IReadOnlyList<IDiscordApplicationCommandOptionChoice> Choices { get; }
    IReadOnlyList<IDiscordApplicationCommandOption> Options { get; }
    IReadOnlyList<NetCord.ChannelType> ChannelTypes { get; }
    double? MinValue { get; }
    double? MaxValue { get; }
    int? MinLength { get; }
    int? MaxLength { get; }
    bool Autocomplete { get; }
}


public interface IDiscordApplicationCommand
{
    NetCord.Rest.ApplicationCommand Original { get; }
    ulong Id { get; }
    NetCord.ApplicationCommandType Type { get; }
    ulong ApplicationId { get; }
    string Name { get; }
    IReadOnlyDictionary<string, string> NameLocalizations { get; }
    string Description { get; }
    IReadOnlyDictionary<string, string> DescriptionLocalizations { get; }
    NetCord.Permissions? DefaultGuildUserPermissions { get; }
    bool DMPermission { get; }
    IReadOnlyList<IDiscordApplicationCommandOption> Options { get; }
    bool DefaultPermission { get; }
    bool Nsfw { get; }
    IReadOnlyList<NetCord.ApplicationIntegrationType> IntegrationTypes { get; }
    IReadOnlyList<NetCord.InteractionContextType> Contexts { get; }
    ulong Version { get; }
    System.DateTimeOffset CreatedAt { get; }
    Task<IDiscordApplicationCommand> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationCommand> ModifyAsync(Action<IDiscordApplicationCommandOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationCommandGuildPermissions> GetGuildPermissionsAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationCommandGuildPermissions> OverwriteGuildPermissionsAsync(ulong guildId, IEnumerable<IDiscordApplicationCommandGuildPermissionProperties> newPermissions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordApplicationCommandOptionProperties
{
    NetCord.Rest.ApplicationCommandOptionProperties Original { get; }
    NetCord.ApplicationCommandOptionType Type { get; }
    string Name { get; }
    IReadOnlyDictionary<string, string> NameLocalizations { get; }
    string Description { get; }
    IReadOnlyDictionary<string, string> DescriptionLocalizations { get; }
    bool? Required { get; }
    IEnumerable<IDiscordApplicationCommandOptionChoiceProperties> Choices { get; }
    IEnumerable<IDiscordApplicationCommandOptionProperties> Options { get; }
    IEnumerable<NetCord.ChannelType> ChannelTypes { get; }
    double? MinValue { get; }
    double? MaxValue { get; }
    int? MinLength { get; }
    int? MaxLength { get; }
    bool? Autocomplete { get; }
    IDiscordApplicationCommandOptionProperties WithType(NetCord.ApplicationCommandOptionType type);
    IDiscordApplicationCommandOptionProperties WithName(string name);
    IDiscordApplicationCommandOptionProperties WithNameLocalizations(IReadOnlyDictionary<string, string> nameLocalizations);
    IDiscordApplicationCommandOptionProperties WithDescription(string description);
    IDiscordApplicationCommandOptionProperties WithDescriptionLocalizations(IReadOnlyDictionary<string, string> descriptionLocalizations);
    IDiscordApplicationCommandOptionProperties WithRequired(bool? required = true);
    IDiscordApplicationCommandOptionProperties WithChoices(IEnumerable<IDiscordApplicationCommandOptionChoiceProperties> choices);
    IDiscordApplicationCommandOptionProperties AddChoices(IEnumerable<IDiscordApplicationCommandOptionChoiceProperties> choices);
    IDiscordApplicationCommandOptionProperties AddChoices(IDiscordApplicationCommandOptionChoiceProperties[] choices);
    IDiscordApplicationCommandOptionProperties WithOptions(IEnumerable<IDiscordApplicationCommandOptionProperties> options);
    IDiscordApplicationCommandOptionProperties AddOptions(IEnumerable<IDiscordApplicationCommandOptionProperties> options);
    IDiscordApplicationCommandOptionProperties AddOptions(IDiscordApplicationCommandOptionProperties[] options);
    IDiscordApplicationCommandOptionProperties WithChannelTypes(IEnumerable<NetCord.ChannelType> channelTypes);
    IDiscordApplicationCommandOptionProperties AddChannelTypes(IEnumerable<NetCord.ChannelType> channelTypes);
    IDiscordApplicationCommandOptionProperties AddChannelTypes(NetCord.ChannelType[] channelTypes);
    IDiscordApplicationCommandOptionProperties WithMinValue(double? minValue);
    IDiscordApplicationCommandOptionProperties WithMaxValue(double? maxValue);
    IDiscordApplicationCommandOptionProperties WithMinLength(int? minLength);
    IDiscordApplicationCommandOptionProperties WithMaxLength(int? maxLength);
    IDiscordApplicationCommandOptionProperties WithAutocomplete(bool? autocomplete = true);
}


public interface IDiscordApplicationCommandPermission
{
    NetCord.ApplicationCommandPermission Original { get; }
    ulong Id { get; }
    NetCord.ApplicationCommandGuildPermissionType Type { get; }
    bool Permission { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordGuildUsersSearchQuery
{
    NetCord.Rest.IGuildUsersSearchQuery Original { get; }
    System.Void Serialize(Utf8JsonWriter writer);
}


public interface IDiscordWebhookOptions
{
    NetCord.Rest.WebhookOptions Original { get; }
    string Name { get; }
    NetCord.Rest.ImageProperties? Avatar { get; }
    ulong? ChannelId { get; }
    IDiscordWebhookOptions WithName(string name);
    IDiscordWebhookOptions WithAvatar(NetCord.Rest.ImageProperties? avatar);
    IDiscordWebhookOptions WithChannelId(ulong? channelId);
}


public interface IDiscordNonceProperties
{
    NetCord.Rest.NonceProperties Original { get; }
    bool Unique { get; }
    IDiscordNonceProperties WithUnique(bool unique = true);
}


public interface IDiscordMessageReferenceProperties
{
    NetCord.Rest.MessageReferenceProperties Original { get; }
    NetCord.MessageReferenceType Type { get; }
    ulong? ChannelId { get; }
    ulong MessageId { get; }
    bool FailIfNotExists { get; }
    IDiscordMessageReferenceProperties WithType(NetCord.MessageReferenceType type);
    IDiscordMessageReferenceProperties WithChannelId(ulong? channelId);
    IDiscordMessageReferenceProperties WithMessageId(ulong messageId);
    IDiscordMessageReferenceProperties WithFailIfNotExists(bool failIfNotExists = true);
}


public interface IDiscordAttachmentExpirationInfo
{
    NetCord.AttachmentExpirationInfo Original { get; }
    System.DateTimeOffset ExpiresAt { get; }
    System.DateTimeOffset IssuedAt { get; }
    string Signature { get; }
}


public interface IDiscordEmbedFooter
{
    NetCord.EmbedFooter Original { get; }
    string Text { get; }
    string IconUrl { get; }
    string ProxyIconUrl { get; }
}


public interface IDiscordEmbedImage
{
    NetCord.EmbedImage Original { get; }
    string Url { get; }
    string ProxyUrl { get; }
    int? Height { get; }
    int? Width { get; }
}


public interface IDiscordEmbedThumbnail
{
    NetCord.EmbedThumbnail Original { get; }
    string Url { get; }
    string ProxyUrl { get; }
    int? Height { get; }
    int? Width { get; }
}


public interface IDiscordEmbedVideo
{
    NetCord.EmbedVideo Original { get; }
    string Url { get; }
    string ProxyUrl { get; }
    int? Height { get; }
    int? Width { get; }
}


public interface IDiscordEmbedProvider
{
    NetCord.EmbedProvider Original { get; }
    string Name { get; }
    string Url { get; }
}


public interface IDiscordEmbedAuthor
{
    NetCord.EmbedAuthor Original { get; }
    string Name { get; }
    string Url { get; }
    string IconUrl { get; }
    string ProxyIconUrl { get; }
}


public interface IDiscordEmbedField
{
    NetCord.EmbedField Original { get; }
    string Name { get; }
    string Value { get; }
    bool Inline { get; }
}


public interface IDiscordMessageReactionCountDetails
{
    NetCord.MessageReactionCountDetails Original { get; }
    int Burst { get; }
    int Normal { get; }
}


public interface IDiscordMessageReactionEmoji
{
    NetCord.MessageReactionEmoji Original { get; }
    ulong? Id { get; }
    string Name { get; }
    bool Animated { get; }
}


public interface IDiscordTeam
{
    NetCord.Team Original { get; }
    ulong Id { get; }
    string IconHash { get; }
    IReadOnlyList<IDiscordTeamUser> Users { get; }
    string Name { get; }
    ulong OwnerId { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordImageUrl GetIconUrl(NetCord.ImageFormat format);
}


public interface IDiscordApplicationInstallParams
{
    NetCord.ApplicationInstallParams Original { get; }
    IReadOnlyList<string> Scopes { get; }
    NetCord.Permissions Permissions { get; }
}


public interface IDiscordApplicationIntegrationTypeConfiguration
{
    NetCord.ApplicationIntegrationTypeConfiguration Original { get; }
    IDiscordApplicationInstallParams OAuth2InstallParams { get; }
}


public interface IDiscordApplicationEmoji
{
    NetCord.ApplicationEmoji Original { get; }
    ulong ApplicationId { get; }
    ulong Id { get; }
    IDiscordUser Creator { get; }
    bool? RequireColons { get; }
    bool? Managed { get; }
    bool? Available { get; }
    string Name { get; }
    bool Animated { get; }
    Task<IDiscordApplicationEmoji> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationEmoji> ModifyAsync(Action<IDiscordApplicationEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IDiscordImageUrl GetImageUrl(NetCord.ImageFormat format);
}


public interface IDiscordApplicationEmojiProperties
{
    NetCord.Rest.ApplicationEmojiProperties Original { get; }
    string Name { get; }
    NetCord.Rest.ImageProperties Image { get; }
    IDiscordApplicationEmojiProperties WithName(string name);
    IDiscordApplicationEmojiProperties WithImage(NetCord.Rest.ImageProperties image);
}


public interface IDiscordApplicationEmojiOptions
{
    NetCord.Rest.ApplicationEmojiOptions Original { get; }
    string Name { get; }
    IDiscordApplicationEmojiOptions WithName(string name);
}


public interface IDiscordMessageSnapshotMessage
{
    NetCord.MessageSnapshotMessage Original { get; }
    NetCord.MessageType Type { get; }
    string Content { get; }
    IReadOnlyList<IDiscordEmbed> Embeds { get; }
    IReadOnlyList<IDiscordAttachment> Attachments { get; }
    System.DateTimeOffset? EditedAt { get; }
    NetCord.MessageFlags Flags { get; }
    IReadOnlyList<IDiscordUser> MentionedUsers { get; }
    IReadOnlyList<ulong> MentionedRoleIds { get; }
}


public interface IDiscordMessagePollMedia
{
    NetCord.MessagePollMedia Original { get; }
    string Text { get; }
    IDiscordEmojiReference Emoji { get; }
}


public interface IDiscordMessagePollAnswer
{
    NetCord.MessagePollAnswer Original { get; }
    int AnswerId { get; }
    IDiscordMessagePollMedia PollMedia { get; }
}


public interface IDiscordMessagePollResults
{
    NetCord.MessagePollResults Original { get; }
    bool IsFinalized { get; }
    IReadOnlyList<IDiscordMessagePollAnswerCount> Answers { get; }
}


public interface IDiscordEmbedFooterProperties
{
    NetCord.Rest.EmbedFooterProperties Original { get; }
    string Text { get; }
    string IconUrl { get; }
    IDiscordEmbedFooterProperties WithText(string text);
    IDiscordEmbedFooterProperties WithIconUrl(string iconUrl);
}


public interface IDiscordEmbedImageProperties
{
    NetCord.Rest.EmbedImageProperties Original { get; }
    string Url { get; }
    IDiscordEmbedImageProperties WithUrl(string url);
}


public interface IDiscordEmbedThumbnailProperties
{
    NetCord.Rest.EmbedThumbnailProperties Original { get; }
    string Url { get; }
    IDiscordEmbedThumbnailProperties WithUrl(string url);
}


public interface IDiscordEmbedAuthorProperties
{
    NetCord.Rest.EmbedAuthorProperties Original { get; }
    string Name { get; }
    string Url { get; }
    string IconUrl { get; }
    IDiscordEmbedAuthorProperties WithName(string name);
    IDiscordEmbedAuthorProperties WithUrl(string url);
    IDiscordEmbedAuthorProperties WithIconUrl(string iconUrl);
}


public interface IDiscordEmbedFieldProperties
{
    NetCord.Rest.EmbedFieldProperties Original { get; }
    string Name { get; }
    string Value { get; }
    bool Inline { get; }
    IDiscordEmbedFieldProperties WithName(string name);
    IDiscordEmbedFieldProperties WithValue(string value);
    IDiscordEmbedFieldProperties WithInline(bool inline = true);
}


public interface IDiscordMessagePollMediaProperties
{
    NetCord.MessagePollMediaProperties Original { get; }
    string Text { get; }
    IDiscordEmojiProperties Emoji { get; }
    IDiscordMessagePollMediaProperties WithText(string text);
    IDiscordMessagePollMediaProperties WithEmoji(IDiscordEmojiProperties emoji);
}


public interface IDiscordMessagePollAnswerProperties
{
    NetCord.MessagePollAnswerProperties Original { get; }
    IDiscordMessagePollMediaProperties PollMedia { get; }
    IDiscordMessagePollAnswerProperties WithPollMedia(IDiscordMessagePollMediaProperties pollMedia);
}


public interface IDiscordWebhookClient
{
    NetCord.Rest.WebhookClient Original { get; }
    ulong Id { get; }
    string Token { get; }
    System.Void Dispose();
    Task<IDiscordWebhook> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordWebhook> ModifyAsync(Action<IDiscordWebhookOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> ExecuteAsync(IDiscordWebhookMessageProperties message, bool wait = false, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessageAsync(ulong messageId, ulong? threadId = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordWebhookClientConfiguration
{
    NetCord.Rest.WebhookClientConfiguration Original { get; }
    IDiscordRestClient Client { get; }
}


public interface IDiscordWebhookMessageProperties
{
    NetCord.Rest.WebhookMessageProperties Original { get; }
    string Content { get; }
    string Username { get; }
    string AvatarUrl { get; }
    bool Tts { get; }
    IEnumerable<IDiscordEmbedProperties> Embeds { get; }
    IDiscordAllowedMentionsProperties AllowedMentions { get; }
    IEnumerable<IDiscordComponentProperties> Components { get; }
    IEnumerable<IDiscordAttachmentProperties> Attachments { get; }
    NetCord.MessageFlags? Flags { get; }
    string ThreadName { get; }
    IEnumerable<ulong> AppliedTags { get; }
    IDiscordMessagePollProperties Poll { get; }
    HttpContent Serialize();
    IDiscordWebhookMessageProperties WithContent(string content);
    IDiscordWebhookMessageProperties WithUsername(string username);
    IDiscordWebhookMessageProperties WithAvatarUrl(string avatarUrl);
    IDiscordWebhookMessageProperties WithTts(bool tts = true);
    IDiscordWebhookMessageProperties WithEmbeds(IEnumerable<IDiscordEmbedProperties> embeds);
    IDiscordWebhookMessageProperties AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds);
    IDiscordWebhookMessageProperties AddEmbeds(IDiscordEmbedProperties[] embeds);
    IDiscordWebhookMessageProperties WithAllowedMentions(IDiscordAllowedMentionsProperties allowedMentions);
    IDiscordWebhookMessageProperties WithComponents(IEnumerable<IDiscordComponentProperties> components);
    IDiscordWebhookMessageProperties AddComponents(IEnumerable<IDiscordComponentProperties> components);
    IDiscordWebhookMessageProperties AddComponents(IDiscordComponentProperties[] components);
    IDiscordWebhookMessageProperties WithAttachments(IEnumerable<IDiscordAttachmentProperties> attachments);
    IDiscordWebhookMessageProperties AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments);
    IDiscordWebhookMessageProperties AddAttachments(IDiscordAttachmentProperties[] attachments);
    IDiscordWebhookMessageProperties WithFlags(NetCord.MessageFlags? flags);
    IDiscordWebhookMessageProperties WithThreadName(string threadName);
    IDiscordWebhookMessageProperties WithAppliedTags(IEnumerable<ulong> appliedTags);
    IDiscordWebhookMessageProperties AddAppliedTags(IEnumerable<ulong> appliedTags);
    IDiscordWebhookMessageProperties AddAppliedTags(ulong[] appliedTags);
    IDiscordWebhookMessageProperties WithPoll(IDiscordMessagePollProperties poll);
}


public interface IDiscordUserActivityTimestamps
{
    NetCord.Gateway.UserActivityTimestamps Original { get; }
    System.DateTimeOffset? StartTime { get; }
    System.DateTimeOffset? EndTime { get; }
}


public interface IDiscordEmoji
{
    NetCord.Emoji Original { get; }
    string Name { get; }
    bool Animated { get; }
}


public interface IDiscordParty
{
    NetCord.Gateway.Party Original { get; }
    string Id { get; }
    int? CurrentSize { get; }
    int? MaxSize { get; }
}


public interface IDiscordUserActivityAssets
{
    NetCord.Gateway.UserActivityAssets Original { get; }
    string LargeImageId { get; }
    string LargeText { get; }
    string SmallImageId { get; }
    string SmallText { get; }
}


public interface IDiscordUserActivitySecrets
{
    NetCord.Gateway.UserActivitySecrets Original { get; }
    string Join { get; }
    string Spectate { get; }
    string Match { get; }
}


public interface IDiscordUserActivityButton
{
    NetCord.Gateway.UserActivityButton Original { get; }
    string Label { get; }
}


public interface IDiscordGuildScheduledEventRecurrenceRuleNWeekday
{
    NetCord.GuildScheduledEventRecurrenceRuleNWeekday Original { get; }
    int N { get; }
    NetCord.GuildScheduledEventRecurrenceRuleWeekday Day { get; }
}


public interface IDiscordAutoModerationActionMetadata
{
    NetCord.AutoModerationActionMetadata Original { get; }
    ulong? ChannelId { get; }
    int? DurationSeconds { get; }
    string CustomMessage { get; }
}


public interface IDiscordAutoModerationActionMetadataProperties
{
    NetCord.AutoModerationActionMetadataProperties Original { get; }
    ulong? ChannelId { get; }
    int? DurationSeconds { get; }
    string CustomMessage { get; }
    IDiscordAutoModerationActionMetadataProperties WithChannelId(ulong? channelId);
    IDiscordAutoModerationActionMetadataProperties WithDurationSeconds(int? durationSeconds);
    IDiscordAutoModerationActionMetadataProperties WithCustomMessage(string customMessage);
}


public interface IDiscordEmojiProperties
{
    NetCord.EmojiProperties Original { get; }
    ulong? Id { get; }
    string Unicode { get; }
    IDiscordEmojiProperties WithId(ulong? id);
    IDiscordEmojiProperties WithUnicode(string unicode);
}


public interface IDiscordGuildOnboardingPromptOption
{
    NetCord.Rest.GuildOnboardingPromptOption Original { get; }
    ulong Id { get; }
    IReadOnlyList<ulong> ChannelIds { get; }
    IReadOnlyList<ulong> RoleIds { get; }
    IDiscordEmoji Emoji { get; }
    string Title { get; }
    string Description { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordGuildOnboardingPromptOptionProperties
{
    NetCord.Rest.GuildOnboardingPromptOptionProperties Original { get; }
    ulong? Id { get; }
    IEnumerable<ulong> ChannelIds { get; }
    IEnumerable<ulong> RoleIds { get; }
    ulong? EmojiId { get; }
    string EmojiName { get; }
    bool? EmojiAnimated { get; }
    string Title { get; }
    string Description { get; }
    IDiscordGuildOnboardingPromptOptionProperties WithId(ulong? id);
    IDiscordGuildOnboardingPromptOptionProperties WithChannelIds(IEnumerable<ulong> channelIds);
    IDiscordGuildOnboardingPromptOptionProperties AddChannelIds(IEnumerable<ulong> channelIds);
    IDiscordGuildOnboardingPromptOptionProperties AddChannelIds(ulong[] channelIds);
    IDiscordGuildOnboardingPromptOptionProperties WithRoleIds(IEnumerable<ulong> roleIds);
    IDiscordGuildOnboardingPromptOptionProperties AddRoleIds(IEnumerable<ulong> roleIds);
    IDiscordGuildOnboardingPromptOptionProperties AddRoleIds(ulong[] roleIds);
    IDiscordGuildOnboardingPromptOptionProperties WithEmojiId(ulong? emojiId);
    IDiscordGuildOnboardingPromptOptionProperties WithEmojiName(string emojiName);
    IDiscordGuildOnboardingPromptOptionProperties WithEmojiAnimated(bool? emojiAnimated = true);
    IDiscordGuildOnboardingPromptOptionProperties WithTitle(string title);
    IDiscordGuildOnboardingPromptOptionProperties WithDescription(string description);
}


public interface IDiscordApplicationCommandOptionChoice
{
    NetCord.Rest.ApplicationCommandOptionChoice Original { get; }
    string Name { get; }
    IReadOnlyDictionary<string, string> NameLocalizations { get; }
    string ValueString { get; }
    double? ValueNumeric { get; }
    NetCord.Rest.ApplicationCommandOptionChoiceValueType ValueType { get; }
}


public interface IDiscordApplicationCommandOptionChoiceProperties
{
    NetCord.Rest.ApplicationCommandOptionChoiceProperties Original { get; }
    string Name { get; }
    IReadOnlyDictionary<string, string> NameLocalizations { get; }
    string StringValue { get; }
    double? NumericValue { get; }
    NetCord.Rest.ApplicationCommandOptionChoiceValueType ValueType { get; }
    IDiscordApplicationCommandOptionChoiceProperties WithName(string name);
    IDiscordApplicationCommandOptionChoiceProperties WithNameLocalizations(IReadOnlyDictionary<string, string> nameLocalizations);
    IDiscordApplicationCommandOptionChoiceProperties WithStringValue(string stringValue);
    IDiscordApplicationCommandOptionChoiceProperties WithNumericValue(double? numericValue);
    IDiscordApplicationCommandOptionChoiceProperties WithValueType(NetCord.Rest.ApplicationCommandOptionChoiceValueType valueType);
}


public interface IDiscordTeamUser
{
    NetCord.TeamUser Original { get; }
    NetCord.MembershipState MembershipState { get; }
    ulong TeamId { get; }
    NetCord.TeamRole Role { get; }
    ulong Id { get; }
    string Username { get; }
    ushort Discriminator { get; }
    string GlobalName { get; }
    string AvatarHash { get; }
    bool IsBot { get; }
    bool? IsSystemUser { get; }
    bool? MfaEnabled { get; }
    string BannerHash { get; }
    NetCord.Color? AccentColor { get; }
    string Locale { get; }
    bool? Verified { get; }
    string Email { get; }
    NetCord.UserFlags? Flags { get; }
    NetCord.PremiumType? PremiumType { get; }
    NetCord.UserFlags? PublicFlags { get; }
    IDiscordAvatarDecorationData AvatarDecorationData { get; }
    bool HasAvatar { get; }
    bool HasBanner { get; }
    bool HasAvatarDecoration { get; }
    IDiscordImageUrl DefaultAvatarUrl { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordImageUrl GetAvatarUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl GetBannerUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl GetAvatarDecorationUrl();
    Task<IDiscordUser> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordDMChannel> GetDMChannelAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordEmojiReference
{
    NetCord.EmojiReference Original { get; }
    ulong? Id { get; }
    string Name { get; }
    bool Animated { get; }
}


public interface IDiscordMessagePollAnswerCount
{
    NetCord.MessagePollAnswerCount Original { get; }
    int AnswerId { get; }
    int Count { get; }
    bool MeVoted { get; }
}


public interface IDiscordRestClient
{
    NetCord.Rest.RestClient Original { get; }
    IDiscordToken Token { get; }
    Task<IDiscordCurrentApplication> GetCurrentApplicationAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordCurrentApplication> ModifyCurrentApplicationAsync(Action<IDiscordCurrentApplicationOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordApplicationRoleConnectionMetadata>> GetApplicationRoleConnectionMetadataRecordsAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordApplicationRoleConnectionMetadata>> UpdateApplicationRoleConnectionMetadataRecordsAsync(ulong applicationId, IEnumerable<IDiscordApplicationRoleConnectionMetadataProperties> applicationRoleConnectionMetadataProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordRestAuditLogEntry> GetGuildAuditLogAsync(ulong guildId, IDiscordGuildAuditLogPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IReadOnlyList<IDiscordAutoModerationRule>> GetAutoModerationRulesAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordAutoModerationRule> GetAutoModerationRuleAsync(ulong guildId, ulong autoModerationRuleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordAutoModerationRule> CreateAutoModerationRuleAsync(ulong guildId, IDiscordAutoModerationRuleProperties autoModerationRuleProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordAutoModerationRule> ModifyAutoModerationRuleAsync(ulong guildId, ulong autoModerationRuleId, Action<IDiscordAutoModerationRuleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAutoModerationRuleAsync(ulong guildId, ulong autoModerationRuleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordChannel> GetChannelAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordChannel> ModifyGroupDMChannelAsync(ulong channelId, Action<IDiscordGroupDMChannelOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordChannel> ModifyGuildChannelAsync(ulong channelId, Action<IDiscordGuildChannelOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordChannel> DeleteChannelAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordRestMessage> GetMessagesAsync(ulong channelId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IReadOnlyList<IDiscordRestMessage>> GetMessagesAroundAsync(ulong channelId, ulong messageId, int? limit = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> GetMessageAsync(ulong channelId, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> SendMessageAsync(ulong channelId, IDiscordMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> CrosspostMessageAsync(ulong channelId, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task AddMessageReactionAsync(ulong channelId, ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessageReactionAsync(ulong channelId, ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessageReactionAsync(ulong channelId, ulong messageId, IDiscordReactionEmojiProperties emoji, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordUser> GetMessageReactionsAsync(ulong channelId, ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordMessageReactionsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task DeleteAllMessageReactionsAsync(ulong channelId, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAllMessageReactionsAsync(ulong channelId, ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> ModifyMessageAsync(ulong channelId, ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessageAsync(ulong channelId, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessagesAsync(ulong channelId, IEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessagesAsync(ulong channelId, IAsyncEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task ModifyGuildChannelPermissionsAsync(ulong channelId, IDiscordPermissionOverwriteProperties permissionOverwrite, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IEnumerable<IDiscordRestInvite>> GetGuildChannelInvitesAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestInvite> CreateGuildChannelInviteAsync(ulong channelId, IDiscordInviteProperties? inviteProperties = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteGuildChannelPermissionAsync(ulong channelId, ulong overwriteId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordFollowedChannel> FollowAnnouncementGuildChannelAsync(ulong channelId, ulong webhookChannelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task TriggerTypingStateAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDisposable> EnterTypingStateAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordRestMessage>> GetPinnedMessagesAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task PinMessageAsync(ulong channelId, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task UnpinMessageAsync(ulong channelId, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task GroupDMChannelAddUserAsync(ulong channelId, ulong userId, IDiscordGroupDMChannelUserAddProperties groupDMChannelUserAddProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task GroupDMChannelDeleteUserAsync(ulong channelId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildThread> CreateGuildThreadAsync(ulong channelId, ulong messageId, IDiscordGuildThreadFromMessageProperties threadFromMessageProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildThread> CreateGuildThreadAsync(ulong channelId, IDiscordGuildThreadProperties threadProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordForumGuildThread> CreateForumGuildThreadAsync(ulong channelId, IDiscordForumGuildThreadProperties threadProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task JoinGuildThreadAsync(ulong threadId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task AddGuildThreadUserAsync(ulong threadId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task LeaveGuildThreadAsync(ulong threadId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteGuildThreadUserAsync(ulong threadId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordThreadUser> GetGuildThreadUserAsync(ulong threadId, ulong userId, bool withGuildUser = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordThreadUser> GetGuildThreadUsersAsync(ulong threadId, IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    IAsyncEnumerable<IDiscordGuildThread> GetPublicArchivedGuildThreadsAsync(ulong channelId, IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    IAsyncEnumerable<IDiscordGuildThread> GetPrivateArchivedGuildThreadsAsync(ulong channelId, IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    IAsyncEnumerable<IDiscordGuildThread> GetJoinedPrivateArchivedGuildThreadsAsync(ulong channelId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<Stream> SendRequestAsync(HttpMethod method, FormattableString route, string? query = null, NetCord.Rest.TopLevelResourceInfo? resourceInfo = default, IDiscordRestRequestProperties? properties = null, bool global = true, System.Threading.CancellationToken cancellationToken = default);
    Task<Stream> SendRequestAsync(HttpMethod method, HttpContent content, FormattableString route, string? query = null, NetCord.Rest.TopLevelResourceInfo? resourceInfo = default, IDiscordRestRequestProperties? properties = null, bool global = true, System.Threading.CancellationToken cancellationToken = default);
    System.Void Dispose();
    Task<IReadOnlyList<IDiscordGuildEmoji>> GetGuildEmojisAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildEmoji> GetGuildEmojiAsync(ulong guildId, ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildEmoji> CreateGuildEmojiAsync(ulong guildId, IDiscordGuildEmojiProperties guildEmojiProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildEmoji> ModifyGuildEmojiAsync(ulong guildId, ulong emojiId, Action<IDiscordGuildEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteGuildEmojiAsync(ulong guildId, ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordApplicationEmoji>> GetApplicationEmojisAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationEmoji> GetApplicationEmojiAsync(ulong applicationId, ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationEmoji> CreateApplicationEmojiAsync(ulong applicationId, IDiscordApplicationEmojiProperties applicationEmojiProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationEmoji> ModifyApplicationEmojiAsync(ulong applicationId, ulong emojiId, Action<IDiscordApplicationEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteApplicationEmojiAsync(ulong applicationId, ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<string> GetGatewayAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGatewayBot> GetGatewayBotAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestGuild> CreateGuildAsync(IDiscordGuildProperties guildProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestGuild> GetGuildAsync(ulong guildId, bool withCounts = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildPreview> GetGuildPreviewAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestGuild> ModifyGuildAsync(ulong guildId, Action<IDiscordGuildOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteGuildAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGuildChannel>> GetGuildChannelsAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildChannel> CreateGuildChannelAsync(ulong guildId, IDiscordGuildChannelProperties channelProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task ModifyGuildChannelPositionsAsync(ulong guildId, IEnumerable<IDiscordGuildChannelPositionProperties> positions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGuildThread>> GetActiveGuildThreadsAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildUser> GetGuildUserAsync(ulong guildId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordGuildUser> GetGuildUsersAsync(ulong guildId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IReadOnlyList<IDiscordGuildUser>> FindGuildUserAsync(ulong guildId, string name, int limit, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildUser> AddGuildUserAsync(ulong guildId, ulong userId, IDiscordGuildUserProperties userProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildUser> ModifyGuildUserAsync(ulong guildId, ulong userId, Action<IDiscordGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildUser> ModifyCurrentGuildUserAsync(ulong guildId, Action<IDiscordCurrentGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task AddGuildUserRoleAsync(ulong guildId, ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task RemoveGuildUserRoleAsync(ulong guildId, ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task KickGuildUserAsync(ulong guildId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordGuildBan> GetGuildBansAsync(ulong guildId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordGuildBan> GetGuildBanAsync(ulong guildId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task BanGuildUserAsync(ulong guildId, ulong userId, int deleteMessageSeconds = 0, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildBulkBan> BanGuildUsersAsync(ulong guildId, IEnumerable<ulong> userIds, int deleteMessageSeconds = 0, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task UnbanGuildUserAsync(ulong guildId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordRole>> GetGuildRolesAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRole> GetGuildRoleAsync(ulong guildId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRole> CreateGuildRoleAsync(ulong guildId, IDiscordRoleProperties guildRoleProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordRole>> ModifyGuildRolePositionsAsync(ulong guildId, IEnumerable<IDiscordRolePositionProperties> positions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRole> ModifyGuildRoleAsync(ulong guildId, ulong roleId, Action<IDiscordRoleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteGuildRoleAsync(ulong guildId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<NetCord.MfaLevel> ModifyGuildMfaLevelAsync(ulong guildId, NetCord.MfaLevel mfaLevel, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<int> GetGuildPruneCountAsync(ulong guildId, int days, IEnumerable<ulong>? roles = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<int?> GuildPruneAsync(ulong guildId, IDiscordGuildPruneProperties pruneProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IEnumerable<IDiscordVoiceRegion>> GetGuildVoiceRegionsAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IEnumerable<IDiscordRestInvite>> GetGuildInvitesAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordIntegration>> GetGuildIntegrationsAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteGuildIntegrationAsync(ulong guildId, ulong integrationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildWidgetSettings> GetGuildWidgetSettingsAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildWidgetSettings> ModifyGuildWidgetSettingsAsync(ulong guildId, Action<IDiscordGuildWidgetSettingsOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildWidget> GetGuildWidgetAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildVanityInvite> GetGuildVanityInviteAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildWelcomeScreen> GetGuildWelcomeScreenAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildWelcomeScreen> ModifyGuildWelcomeScreenAsync(ulong guildId, Action<IDiscordGuildWelcomeScreenOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildOnboarding> GetGuildOnboardingAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildOnboarding> ModifyGuildOnboardingAsync(ulong guildId, Action<IDiscordGuildOnboardingOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGuildScheduledEvent>> GetGuildScheduledEventsAsync(ulong guildId, bool withUserCount = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildScheduledEvent> CreateGuildScheduledEventAsync(ulong guildId, IDiscordGuildScheduledEventProperties guildScheduledEventProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildScheduledEvent> GetGuildScheduledEventAsync(ulong guildId, ulong scheduledEventId, bool withUserCount = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildScheduledEvent> ModifyGuildScheduledEventAsync(ulong guildId, ulong scheduledEventId, Action<IDiscordGuildScheduledEventOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteGuildScheduledEventAsync(ulong guildId, ulong scheduledEventId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordGuildScheduledEventUser> GetGuildScheduledEventUsersAsync(ulong guildId, ulong scheduledEventId, IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordGuildTemplate> GetGuildTemplateAsync(string templateCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestGuild> CreateGuildFromGuildTemplateAsync(string templateCode, IDiscordGuildFromGuildTemplateProperties guildProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IEnumerable<IDiscordGuildTemplate>> GetGuildTemplatesAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildTemplate> CreateGuildTemplateAsync(ulong guildId, IDiscordGuildTemplateProperties guildTemplateProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildTemplate> SyncGuildTemplateAsync(ulong guildId, string templateCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildTemplate> ModifyGuildTemplateAsync(ulong guildId, string templateCode, Action<IDiscordGuildTemplateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildTemplate> DeleteGuildTemplateAsync(ulong guildId, string templateCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordApplicationCommand>> GetGlobalApplicationCommandsAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationCommand> CreateGlobalApplicationCommandAsync(ulong applicationId, IDiscordApplicationCommandProperties applicationCommandProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationCommand> GetGlobalApplicationCommandAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationCommand> ModifyGlobalApplicationCommandAsync(ulong applicationId, ulong commandId, Action<IDiscordApplicationCommandOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteGlobalApplicationCommandAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordApplicationCommand>> BulkOverwriteGlobalApplicationCommandsAsync(ulong applicationId, IEnumerable<IDiscordApplicationCommandProperties> commands, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGuildApplicationCommand>> GetGuildApplicationCommandsAsync(ulong applicationId, ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildApplicationCommand> CreateGuildApplicationCommandAsync(ulong applicationId, ulong guildId, IDiscordApplicationCommandProperties applicationCommandProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildApplicationCommand> GetGuildApplicationCommandAsync(ulong applicationId, ulong guildId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildApplicationCommand> ModifyGuildApplicationCommandAsync(ulong applicationId, ulong guildId, ulong commandId, Action<IDiscordApplicationCommandOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteGuildApplicationCommandAsync(ulong applicationId, ulong guildId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGuildApplicationCommand>> BulkOverwriteGuildApplicationCommandsAsync(ulong applicationId, ulong guildId, IEnumerable<IDiscordApplicationCommandProperties> commands, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordApplicationCommandGuildPermissions>> GetApplicationCommandsGuildPermissionsAsync(ulong applicationId, ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationCommandGuildPermissions> GetApplicationCommandGuildPermissionsAsync(ulong applicationId, ulong guildId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationCommandGuildPermissions> OverwriteApplicationCommandGuildPermissionsAsync(ulong applicationId, ulong guildId, ulong commandId, IEnumerable<IDiscordApplicationCommandGuildPermissionProperties> newPermissions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task SendInteractionResponseAsync(ulong interactionId, string interactionToken, IDiscordInteractionCallback callback, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> GetInteractionResponseAsync(ulong applicationId, string interactionToken, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> ModifyInteractionResponseAsync(ulong applicationId, string interactionToken, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteInteractionResponseAsync(ulong applicationId, string interactionToken, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> SendInteractionFollowupMessageAsync(ulong applicationId, string interactionToken, IDiscordInteractionMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> GetInteractionFollowupMessageAsync(ulong applicationId, string interactionToken, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> ModifyInteractionFollowupMessageAsync(ulong applicationId, string interactionToken, ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteInteractionFollowupMessageAsync(ulong applicationId, string interactionToken, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestInvite> GetGuildInviteAsync(string inviteCode, bool withCounts = false, bool withExpiration = false, ulong? guildScheduledEventId = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestInvite> DeleteGuildInviteAsync(string inviteCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordEntitlement> GetEntitlementsAsync(ulong applicationId, IDiscordEntitlementsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordEntitlement> GetEntitlementAsync(ulong applicationId, ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task ConsumeEntitlementAsync(ulong applicationId, ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordEntitlement> CreateTestEntitlementAsync(ulong applicationId, IDiscordTestEntitlementProperties testEntitlementProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteTestEntitlementAsync(ulong applicationId, ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordSku>> GetSkusAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordCurrentApplication> GetCurrentBotApplicationInformationAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordAuthorizationInformation> GetCurrentAuthorizationInformationAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordUser> GetMessagePollAnswerVotersAsync(ulong channelId, ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordRestMessage> EndMessagePollAsync(ulong channelId, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordStageInstance> CreateStageInstanceAsync(IDiscordStageInstanceProperties stageInstanceProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordStageInstance> GetStageInstanceAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordStageInstance> ModifyStageInstanceAsync(ulong channelId, Action<IDiscordStageInstanceOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteStageInstanceAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordStandardSticker> GetStickerAsync(ulong stickerId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordStickerPack>> GetStickerPacksAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordStickerPack> GetStickerPackAsync(ulong stickerPackId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGuildSticker>> GetGuildStickersAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildSticker> GetGuildStickerAsync(ulong guildId, ulong stickerId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildSticker> CreateGuildStickerAsync(ulong guildId, IDiscordGuildStickerProperties sticker, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildSticker> ModifyGuildStickerAsync(ulong guildId, ulong stickerId, Action<IDiscordGuildStickerOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteGuildStickerAsync(ulong guildId, ulong stickerId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordSubscription> GetSkuSubscriptionsAsync(ulong skuId, IDiscordSubscriptionPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordSubscription> GetSkuSubscriptionAsync(ulong skuId, ulong subscriptionId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplication> GetApplicationAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(ulong channelId, IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordGuildUserInfo> SearchGuildUsersAsync(ulong guildId, IDiscordGuildUsersSearchPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordCurrentUser> GetCurrentUserAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordUser> GetUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordCurrentUser> ModifyCurrentUserAsync(Action<IDiscordCurrentUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordRestGuild> GetCurrentUserGuildsAsync(IDiscordGuildsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordGuildUser> GetCurrentUserGuildUserAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task LeaveGuildAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordDMChannel> GetDMChannelAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGroupDMChannel> CreateGroupDMChannelAsync(IDiscordGroupDMChannelProperties groupDMChannelProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordConnection>> GetCurrentUserConnectionsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationRoleConnection> GetCurrentUserApplicationRoleConnectionAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationRoleConnection> UpdateCurrentUserApplicationRoleConnectionAsync(ulong applicationId, IDiscordApplicationRoleConnectionProperties applicationRoleConnectionProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IEnumerable<IDiscordVoiceRegion>> GetVoiceRegionsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordVoiceState> GetCurrentGuildUserVoiceStateAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordVoiceState> GetGuildUserVoiceStateAsync(ulong guildId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task ModifyCurrentGuildUserVoiceStateAsync(ulong guildId, Action<IDiscordCurrentUserVoiceStateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task ModifyGuildUserVoiceStateAsync(ulong guildId, ulong channelId, ulong userId, Action<IDiscordVoiceStateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordIncomingWebhook> CreateWebhookAsync(ulong channelId, IDiscordWebhookProperties webhookProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordWebhook>> GetChannelWebhooksAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordWebhook>> GetGuildWebhooksAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordWebhook> GetWebhookAsync(ulong webhookId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordWebhook> GetWebhookWithTokenAsync(ulong webhookId, string webhookToken, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordWebhook> ModifyWebhookAsync(ulong webhookId, Action<IDiscordWebhookOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordWebhook> ModifyWebhookWithTokenAsync(ulong webhookId, string webhookToken, Action<IDiscordWebhookOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteWebhookAsync(ulong webhookId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteWebhookWithTokenAsync(ulong webhookId, string webhookToken, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> ExecuteWebhookAsync(ulong webhookId, string webhookToken, IDiscordWebhookMessageProperties message, bool wait = false, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> GetWebhookMessageAsync(ulong webhookId, string webhookToken, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> ModifyWebhookMessageAsync(ulong webhookId, string webhookToken, ulong messageId, Action<IDiscordMessageOptions> action, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteWebhookMessageAsync(ulong webhookId, string webhookToken, ulong messageId, ulong? threadId = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordToken
{
    NetCord.IToken Original { get; }
    string RawToken { get; }
    string HttpHeaderValue { get; }
}


public interface IDiscordCurrentApplication
{
    NetCord.CurrentApplication Original { get; }
    ulong Id { get; }
    string Name { get; }
    string IconHash { get; }
    string Description { get; }
    IReadOnlyList<string> RpcOrigins { get; }
    bool? BotPublic { get; }
    bool? BotRequireCodeGrant { get; }
    IDiscordUser Bot { get; }
    string TermsOfServiceUrl { get; }
    string PrivacyPolicyUrl { get; }
    IDiscordUser Owner { get; }
    string VerifyKey { get; }
    IDiscordTeam Team { get; }
    ulong? GuildId { get; }
    IDiscordRestGuild Guild { get; }
    ulong? PrimarySkuId { get; }
    string Slug { get; }
    string CoverImageHash { get; }
    NetCord.ApplicationFlags? Flags { get; }
    int? ApproximateGuildCount { get; }
    int? ApproximateUserInstallCount { get; }
    IReadOnlyList<string> RedirectUris { get; }
    string InteractionsEndpointUrl { get; }
    string RoleConnectionsVerificationUrl { get; }
    IReadOnlyList<string> Tags { get; }
    IDiscordApplicationInstallParams InstallParams { get; }
    IReadOnlyDictionary<NetCord.ApplicationIntegrationType, IDiscordApplicationIntegrationTypeConfiguration> IntegrationTypesConfiguration { get; }
    string CustomInstallUrl { get; }
    System.DateTimeOffset CreatedAt { get; }
    Task<IDiscordApplication> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordCurrentApplication> ModifyAsync(Action<IDiscordCurrentApplicationOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordApplicationRoleConnectionMetadata>> GetRoleConnectionMetadataRecordsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordApplicationRoleConnectionMetadata>> UpdateRoleConnectionMetadataRecordsAsync(IEnumerable<IDiscordApplicationRoleConnectionMetadataProperties> applicationRoleConnectionMetadataProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordEntitlement> GetEntitlementsAsync(IDiscordEntitlementsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordEntitlement> GetEntitlementAsync(ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task ConsumeEntitlementAsync(ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordEntitlement> CreateTestEntitlementAsync(IDiscordTestEntitlementProperties testEntitlementProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteTestEntitlementAsync(ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordSku>> GetSkusAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IDiscordImageUrl GetIconUrl(NetCord.ImageFormat format);
    IDiscordImageUrl GetCoverUrl(NetCord.ImageFormat format);
    IDiscordImageUrl GetAssetUrl(ulong assetId, NetCord.ImageFormat format);
    IDiscordImageUrl GetAchievementIconUrl(ulong achievementId, string iconHash, NetCord.ImageFormat format);
    IDiscordImageUrl GetStorePageAssetUrl(ulong assetId, NetCord.ImageFormat format);
    Task<IReadOnlyList<IDiscordApplicationEmoji>> GetEmojisAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationEmoji> GetEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationEmoji> CreateEmojiAsync(IDiscordApplicationEmojiProperties applicationEmojiProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationEmoji> ModifyEmojiAsync(ulong emojiId, Action<IDiscordApplicationEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordCurrentApplicationOptions
{
    NetCord.Rest.CurrentApplicationOptions Original { get; }
    string CustomInstallUrl { get; }
    string Description { get; }
    string RoleConnectionsVerificationUrl { get; }
    IDiscordApplicationInstallParamsProperties InstallParams { get; }
    IReadOnlyDictionary<NetCord.ApplicationIntegrationType, IDiscordApplicationIntegrationTypeConfigurationProperties> IntegrationTypesConfiguration { get; }
    NetCord.ApplicationFlags? Flags { get; }
    NetCord.Rest.ImageProperties? Icon { get; }
    NetCord.Rest.ImageProperties? CoverImage { get; }
    string InteractionsEndpointUrl { get; }
    IEnumerable<string> Tags { get; }
    IDiscordCurrentApplicationOptions WithCustomInstallUrl(string customInstallUrl);
    IDiscordCurrentApplicationOptions WithDescription(string description);
    IDiscordCurrentApplicationOptions WithRoleConnectionsVerificationUrl(string roleConnectionsVerificationUrl);
    IDiscordCurrentApplicationOptions WithInstallParams(IDiscordApplicationInstallParamsProperties installParams);
    IDiscordCurrentApplicationOptions WithIntegrationTypesConfiguration(IReadOnlyDictionary<NetCord.ApplicationIntegrationType, IDiscordApplicationIntegrationTypeConfigurationProperties> integrationTypesConfiguration);
    IDiscordCurrentApplicationOptions WithFlags(NetCord.ApplicationFlags? flags);
    IDiscordCurrentApplicationOptions WithIcon(NetCord.Rest.ImageProperties? icon);
    IDiscordCurrentApplicationOptions WithCoverImage(NetCord.Rest.ImageProperties? coverImage);
    IDiscordCurrentApplicationOptions WithInteractionsEndpointUrl(string interactionsEndpointUrl);
    IDiscordCurrentApplicationOptions WithTags(IEnumerable<string> tags);
    IDiscordCurrentApplicationOptions AddTags(IEnumerable<string> tags);
    IDiscordCurrentApplicationOptions AddTags(string[] tags);
}


public interface IDiscordApplicationRoleConnectionMetadata
{
    NetCord.Rest.ApplicationRoleConnectionMetadata Original { get; }
    NetCord.Rest.ApplicationRoleConnectionMetadataType Type { get; }
    string Key { get; }
    string Name { get; }
    IReadOnlyDictionary<string, string> NameLocalizations { get; }
    string Description { get; }
    IReadOnlyDictionary<string, string> DescriptionLocalizations { get; }
}


public interface IDiscordApplicationRoleConnectionMetadataProperties
{
    NetCord.Rest.ApplicationRoleConnectionMetadataProperties Original { get; }
    NetCord.Rest.ApplicationRoleConnectionMetadataType Type { get; }
    string Key { get; }
    string Name { get; }
    IReadOnlyDictionary<string, string> NameLocalizations { get; }
    string Description { get; }
    IReadOnlyDictionary<string, string> DescriptionLocalizations { get; }
    IDiscordApplicationRoleConnectionMetadataProperties WithType(NetCord.Rest.ApplicationRoleConnectionMetadataType type);
    IDiscordApplicationRoleConnectionMetadataProperties WithKey(string key);
    IDiscordApplicationRoleConnectionMetadataProperties WithName(string name);
    IDiscordApplicationRoleConnectionMetadataProperties WithNameLocalizations(IReadOnlyDictionary<string, string> nameLocalizations);
    IDiscordApplicationRoleConnectionMetadataProperties WithDescription(string description);
    IDiscordApplicationRoleConnectionMetadataProperties WithDescriptionLocalizations(IReadOnlyDictionary<string, string> descriptionLocalizations);
}


public interface IDiscordGroupDMChannelOptions
{
    NetCord.Rest.GroupDMChannelOptions Original { get; }
    string Name { get; }
    NetCord.Rest.ImageProperties? Icon { get; }
    IDiscordGroupDMChannelOptions WithName(string name);
    IDiscordGroupDMChannelOptions WithIcon(NetCord.Rest.ImageProperties? icon);
}


public interface IDiscordFollowedChannel
{
    NetCord.Rest.FollowedChannel Original { get; }
    ulong Id { get; }
    ulong WebhookId { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordGroupDMChannelUserAddProperties
{
    NetCord.Rest.GroupDMChannelUserAddProperties Original { get; }
    string AccessToken { get; }
    string Nickname { get; }
    IDiscordGroupDMChannelUserAddProperties WithAccessToken(string accessToken);
    IDiscordGroupDMChannelUserAddProperties WithNickname(string nickname);
}


public interface IDiscordForumGuildThread
{
    NetCord.ForumGuildThread Original { get; }
    IDiscordRestMessage Message { get; }
    IReadOnlyList<ulong> AppliedTags { get; }
    ulong OwnerId { get; }
    int MessageCount { get; }
    int UserCount { get; }
    IDiscordGuildThreadMetadata Metadata { get; }
    IDiscordThreadCurrentUser CurrentUser { get; }
    int TotalMessageSent { get; }
    ulong GuildId { get; }
    int? Position { get; }
    IReadOnlyDictionary<ulong, IDiscordPermissionOverwrite> PermissionOverwrites { get; }
    string Name { get; }
    string Topic { get; }
    bool Nsfw { get; }
    int Slowmode { get; }
    ulong? ParentId { get; }
    NetCord.ThreadArchiveDuration? DefaultAutoArchiveDuration { get; }
    int DefaultThreadSlowmode { get; }
    ulong? LastMessageId { get; }
    System.DateTimeOffset? LastPin { get; }
    ulong Id { get; }
    NetCord.ChannelFlags Flags { get; }
    System.DateTimeOffset CreatedAt { get; }
    Task<IDiscordForumGuildThread> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordForumGuildThread> ModifyAsync(Action<IDiscordGuildChannelOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordForumGuildThread> DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task JoinAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task AddUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task LeaveAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordThreadUser> GetUserAsync(ulong userId, bool withGuildUser = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordThreadUser> GetUsersAsync(IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task ModifyPermissionsAsync(IDiscordPermissionOverwriteProperties permissionOverwrite, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IEnumerable<IDiscordRestInvite>> GetInvitesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestInvite> CreateInviteAsync(IDiscordInviteProperties? inviteProperties = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeletePermissionAsync(ulong overwriteId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildThread> CreateGuildThreadAsync(ulong messageId, IDiscordGuildThreadFromMessageProperties threadFromMessageProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildThread> CreateGuildThreadAsync(IDiscordGuildThreadProperties threadProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordGuildThread> GetPublicArchivedGuildThreadsAsync(IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    IAsyncEnumerable<IDiscordGuildThread> GetPrivateArchivedGuildThreadsAsync(IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    IAsyncEnumerable<IDiscordGuildThread> GetJoinedPrivateArchivedGuildThreadsAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordIncomingWebhook> CreateWebhookAsync(IDiscordWebhookProperties webhookProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordWebhook>> GetChannelWebhooksAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordRestMessage> GetMessagesAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IReadOnlyList<IDiscordRestMessage>> GetMessagesAroundAsync(ulong messageId, int? limit = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> SendMessageAsync(IDiscordMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task AddMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordUser> GetMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordMessageReactionsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessagesAsync(IEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessagesAsync(IAsyncEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task TriggerTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDisposable> EnterTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordRestMessage>> GetPinnedMessagesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task PinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task UnpinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordUser> GetMessagePollAnswerVotersAsync(ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordRestMessage> EndMessagePollAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordForumGuildThreadProperties
{
    NetCord.Rest.ForumGuildThreadProperties Original { get; }
    IDiscordForumGuildThreadMessageProperties Message { get; }
    IEnumerable<ulong> AppliedTags { get; }
    string Name { get; }
    NetCord.ThreadArchiveDuration? AutoArchiveDuration { get; }
    int? Slowmode { get; }
    HttpContent Serialize();
    IDiscordForumGuildThreadProperties WithMessage(IDiscordForumGuildThreadMessageProperties message);
    IDiscordForumGuildThreadProperties WithAppliedTags(IEnumerable<ulong> appliedTags);
    IDiscordForumGuildThreadProperties AddAppliedTags(IEnumerable<ulong> appliedTags);
    IDiscordForumGuildThreadProperties AddAppliedTags(ulong[] appliedTags);
    IDiscordForumGuildThreadProperties WithName(string name);
    IDiscordForumGuildThreadProperties WithAutoArchiveDuration(NetCord.ThreadArchiveDuration? autoArchiveDuration);
    IDiscordForumGuildThreadProperties WithSlowmode(int? slowmode);
}


public interface IDiscordGatewayBot
{
    NetCord.Rest.GatewayBot Original { get; }
    string Url { get; }
    int ShardCount { get; }
    IDiscordGatewaySessionStartLimit SessionStartLimit { get; }
}


public interface IDiscordGuildProperties
{
    NetCord.Rest.GuildProperties Original { get; }
    string Name { get; }
    NetCord.Rest.ImageProperties? Icon { get; }
    NetCord.VerificationLevel? VerificationLevel { get; }
    NetCord.DefaultMessageNotificationLevel? DefaultMessageNotificationLevel { get; }
    NetCord.ContentFilter? ContentFilter { get; }
    IEnumerable<IDiscordRoleProperties> Roles { get; }
    IEnumerable<IDiscordGuildChannelProperties> Channels { get; }
    ulong? AfkChannelId { get; }
    int? AfkTimeout { get; }
    ulong? SystemChannelId { get; }
    NetCord.Rest.SystemChannelFlags? SystemChannelFlags { get; }
    IDiscordGuildProperties WithName(string name);
    IDiscordGuildProperties WithIcon(NetCord.Rest.ImageProperties? icon);
    IDiscordGuildProperties WithVerificationLevel(NetCord.VerificationLevel? verificationLevel);
    IDiscordGuildProperties WithDefaultMessageNotificationLevel(NetCord.DefaultMessageNotificationLevel? defaultMessageNotificationLevel);
    IDiscordGuildProperties WithContentFilter(NetCord.ContentFilter? contentFilter);
    IDiscordGuildProperties WithRoles(IEnumerable<IDiscordRoleProperties> roles);
    IDiscordGuildProperties AddRoles(IEnumerable<IDiscordRoleProperties> roles);
    IDiscordGuildProperties AddRoles(IDiscordRoleProperties[] roles);
    IDiscordGuildProperties WithChannels(IEnumerable<IDiscordGuildChannelProperties> channels);
    IDiscordGuildProperties AddChannels(IEnumerable<IDiscordGuildChannelProperties> channels);
    IDiscordGuildProperties AddChannels(IDiscordGuildChannelProperties[] channels);
    IDiscordGuildProperties WithAfkChannelId(ulong? afkChannelId);
    IDiscordGuildProperties WithAfkTimeout(int? afkTimeout);
    IDiscordGuildProperties WithSystemChannelId(ulong? systemChannelId);
    IDiscordGuildProperties WithSystemChannelFlags(NetCord.Rest.SystemChannelFlags? systemChannelFlags);
}


public interface IDiscordEntitlementsPaginationProperties
{
    NetCord.Rest.EntitlementsPaginationProperties Original { get; }
    ulong? UserId { get; }
    IEnumerable<ulong> SkuIds { get; }
    ulong? GuildId { get; }
    bool? ExcludeEnded { get; }
    ulong? From { get; }
    NetCord.Rest.PaginationDirection? Direction { get; }
    int? BatchSize { get; }
    IDiscordEntitlementsPaginationProperties WithUserId(ulong? userId);
    IDiscordEntitlementsPaginationProperties WithSkuIds(IEnumerable<ulong> skuIds);
    IDiscordEntitlementsPaginationProperties AddSkuIds(IEnumerable<ulong> skuIds);
    IDiscordEntitlementsPaginationProperties AddSkuIds(ulong[] skuIds);
    IDiscordEntitlementsPaginationProperties WithGuildId(ulong? guildId);
    IDiscordEntitlementsPaginationProperties WithExcludeEnded(bool? excludeEnded = true);
    IDiscordEntitlementsPaginationProperties WithFrom(ulong? from);
    IDiscordEntitlementsPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction);
    IDiscordEntitlementsPaginationProperties WithBatchSize(int? batchSize);
}


public interface IDiscordTestEntitlementProperties
{
    NetCord.Rest.TestEntitlementProperties Original { get; }
    ulong SkuId { get; }
    ulong OwnerId { get; }
    NetCord.Rest.TestEntitlementOwnerType OwnerType { get; }
    IDiscordTestEntitlementProperties WithSkuId(ulong skuId);
    IDiscordTestEntitlementProperties WithOwnerId(ulong ownerId);
    IDiscordTestEntitlementProperties WithOwnerType(NetCord.Rest.TestEntitlementOwnerType ownerType);
}


public interface IDiscordSku
{
    NetCord.Rest.Sku Original { get; }
    ulong Id { get; }
    NetCord.Rest.SkuType Type { get; }
    ulong ApplicationId { get; }
    string Name { get; }
    string Slug { get; }
    NetCord.Rest.SkuFlags Flags { get; }
    System.DateTimeOffset CreatedAt { get; }
    IAsyncEnumerable<IDiscordSubscription> GetSubscriptionsAsync(IDiscordSubscriptionPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordSubscription> GetSubscriptionAsync(ulong subscriptionId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordAuthorizationInformation
{
    NetCord.Rest.AuthorizationInformation Original { get; }
    IDiscordApplication Application { get; }
    IReadOnlyList<string> Scopes { get; }
    System.DateTimeOffset ExpiresAt { get; }
    IDiscordUser User { get; }
}


public interface IDiscordStageInstanceProperties
{
    NetCord.Rest.StageInstanceProperties Original { get; }
    ulong ChannelId { get; }
    string Topic { get; }
    NetCord.StageInstancePrivacyLevel? PrivacyLevel { get; }
    bool? SendStartNotification { get; }
    ulong? GuildScheduledEventId { get; }
    IDiscordStageInstanceProperties WithChannelId(ulong channelId);
    IDiscordStageInstanceProperties WithTopic(string topic);
    IDiscordStageInstanceProperties WithPrivacyLevel(NetCord.StageInstancePrivacyLevel? privacyLevel);
    IDiscordStageInstanceProperties WithSendStartNotification(bool? sendStartNotification = true);
    IDiscordStageInstanceProperties WithGuildScheduledEventId(ulong? guildScheduledEventId);
}


public interface IDiscordStandardSticker
{
    NetCord.StandardSticker Original { get; }
    ulong PackId { get; }
    int? SortValue { get; }
    ulong Id { get; }
    string Name { get; }
    string Description { get; }
    IReadOnlyList<string> Tags { get; }
    NetCord.StickerFormat Format { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordImageUrl GetImageUrl(NetCord.ImageFormat format);
}


public interface IDiscordStickerPack
{
    NetCord.Rest.StickerPack Original { get; }
    IReadOnlyList<IDiscordSticker> Stickers { get; }
    string Name { get; }
    ulong SkuId { get; }
    ulong? CoverStickerId { get; }
    string Description { get; }
    ulong? BannerAssetId { get; }
}


public interface IDiscordSubscription
{
    NetCord.Subscription Original { get; }
    ulong Id { get; }
    ulong UserId { get; }
    IReadOnlyList<ulong> SkuIds { get; }
    IReadOnlyList<ulong> EntitlementIds { get; }
    IReadOnlyList<ulong> RenewalSkuIds { get; }
    System.DateTimeOffset CurrentPeriodStart { get; }
    System.DateTimeOffset CurrentPeriodEnd { get; }
    NetCord.SubscriptionStatus Status { get; }
    System.DateTimeOffset? CanceledAt { get; }
    string Country { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordSubscriptionPaginationProperties
{
    NetCord.Rest.SubscriptionPaginationProperties Original { get; }
    ulong? UserId { get; }
    ulong? From { get; }
    NetCord.Rest.PaginationDirection? Direction { get; }
    int? BatchSize { get; }
    IDiscordSubscriptionPaginationProperties WithUserId(ulong? userId);
    IDiscordSubscriptionPaginationProperties WithFrom(ulong? from);
    IDiscordSubscriptionPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction);
    IDiscordSubscriptionPaginationProperties WithBatchSize(int? batchSize);
}


public interface IDiscordCurrentUser
{
    NetCord.CurrentUser Original { get; }
    ulong Id { get; }
    string Username { get; }
    ushort Discriminator { get; }
    string GlobalName { get; }
    string AvatarHash { get; }
    bool IsBot { get; }
    bool? IsSystemUser { get; }
    bool? MfaEnabled { get; }
    string BannerHash { get; }
    NetCord.Color? AccentColor { get; }
    string Locale { get; }
    bool? Verified { get; }
    string Email { get; }
    NetCord.UserFlags? Flags { get; }
    NetCord.PremiumType? PremiumType { get; }
    NetCord.UserFlags? PublicFlags { get; }
    IDiscordAvatarDecorationData AvatarDecorationData { get; }
    bool HasAvatar { get; }
    bool HasBanner { get; }
    bool HasAvatarDecoration { get; }
    IDiscordImageUrl DefaultAvatarUrl { get; }
    System.DateTimeOffset CreatedAt { get; }
    Task<IDiscordCurrentUser> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordCurrentUser> ModifyAsync(Action<IDiscordCurrentUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordRestGuild> GetGuildsAsync(IDiscordGuildsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordGuildUser> GetGuildUserAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task LeaveGuildAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordConnection>> GetConnectionsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationRoleConnection> GetApplicationRoleConnectionAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationRoleConnection> UpdateApplicationRoleConnectionAsync(ulong applicationId, IDiscordApplicationRoleConnectionProperties applicationRoleConnectionProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IDiscordImageUrl GetAvatarUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl GetBannerUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl GetAvatarDecorationUrl();
    Task<IDiscordDMChannel> GetDMChannelAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordCurrentUserOptions
{
    NetCord.Rest.CurrentUserOptions Original { get; }
    string Username { get; }
    NetCord.Rest.ImageProperties? Avatar { get; }
    NetCord.Rest.ImageProperties? Banner { get; }
    IDiscordCurrentUserOptions WithUsername(string username);
    IDiscordCurrentUserOptions WithAvatar(NetCord.Rest.ImageProperties? avatar);
    IDiscordCurrentUserOptions WithBanner(NetCord.Rest.ImageProperties? banner);
}


public interface IDiscordGuildsPaginationProperties
{
    NetCord.Rest.GuildsPaginationProperties Original { get; }
    bool WithCounts { get; }
    ulong? From { get; }
    NetCord.Rest.PaginationDirection? Direction { get; }
    int? BatchSize { get; }
    IDiscordGuildsPaginationProperties WithWithCounts(bool withCounts = true);
    IDiscordGuildsPaginationProperties WithFrom(ulong? from);
    IDiscordGuildsPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction);
    IDiscordGuildsPaginationProperties WithBatchSize(int? batchSize);
}


public interface IDiscordGroupDMChannel
{
    NetCord.GroupDMChannel Original { get; }
    string Name { get; }
    string IconHash { get; }
    ulong OwnerId { get; }
    ulong? ApplicationId { get; }
    bool Managed { get; }
    IReadOnlyDictionary<ulong, IDiscordUser> Users { get; }
    ulong? LastMessageId { get; }
    System.DateTimeOffset? LastPin { get; }
    ulong Id { get; }
    NetCord.ChannelFlags Flags { get; }
    System.DateTimeOffset CreatedAt { get; }
    Task<IDiscordGroupDMChannel> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGroupDMChannel> ModifyAsync(Action<IDiscordGroupDMChannelOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGroupDMChannel> DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task AddUserAsync(ulong userId, IDiscordGroupDMChannelUserAddProperties groupDMChannelUserAddProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordRestMessage> GetMessagesAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IReadOnlyList<IDiscordRestMessage>> GetMessagesAroundAsync(ulong messageId, int? limit = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> SendMessageAsync(IDiscordMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task AddMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordUser> GetMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordMessageReactionsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessagesAsync(IEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessagesAsync(IAsyncEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task TriggerTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDisposable> EnterTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordRestMessage>> GetPinnedMessagesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task PinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task UnpinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordUser> GetMessagePollAnswerVotersAsync(ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordRestMessage> EndMessagePollAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordGroupDMChannelProperties
{
    NetCord.Rest.GroupDMChannelProperties Original { get; }
    IEnumerable<string> AccessTokens { get; }
    IReadOnlyDictionary<ulong, string> Nicknames { get; }
    IDiscordGroupDMChannelProperties WithAccessTokens(IEnumerable<string> accessTokens);
    IDiscordGroupDMChannelProperties AddAccessTokens(IEnumerable<string> accessTokens);
    IDiscordGroupDMChannelProperties AddAccessTokens(string[] accessTokens);
    IDiscordGroupDMChannelProperties WithNicknames(IReadOnlyDictionary<ulong, string> nicknames);
}


public interface IDiscordConnection
{
    NetCord.Rest.Connection Original { get; }
    string Id { get; }
    string Name { get; }
    NetCord.Rest.ConnectionType Type { get; }
    bool? Revoked { get; }
    IReadOnlyList<IDiscordIntegration> Integrations { get; }
    bool Verified { get; }
    bool FriendSync { get; }
    bool ShowActivity { get; }
    bool TwoWayLink { get; }
    NetCord.Rest.ConnectionVisibility Visibility { get; }
}


public interface IDiscordApplicationRoleConnection
{
    NetCord.Rest.ApplicationRoleConnection Original { get; }
    string PlatformName { get; }
    string PlatformUsername { get; }
    IReadOnlyDictionary<string, string> Metadata { get; }
}


public interface IDiscordApplicationRoleConnectionProperties
{
    NetCord.Rest.ApplicationRoleConnectionProperties Original { get; }
    string PlatformName { get; }
    string PlatformUsername { get; }
    IReadOnlyDictionary<string, string> Metadata { get; }
    IDiscordApplicationRoleConnectionProperties WithPlatformName(string platformName);
    IDiscordApplicationRoleConnectionProperties WithPlatformUsername(string platformUsername);
    IDiscordApplicationRoleConnectionProperties WithMetadata(IReadOnlyDictionary<string, string> metadata);
}


public interface IDiscordApplicationInstallParamsProperties
{
    NetCord.Rest.ApplicationInstallParamsProperties Original { get; }
    IEnumerable<string> Scopes { get; }
    NetCord.Permissions? Permissions { get; }
    IDiscordApplicationInstallParamsProperties WithScopes(IEnumerable<string> scopes);
    IDiscordApplicationInstallParamsProperties AddScopes(IEnumerable<string> scopes);
    IDiscordApplicationInstallParamsProperties AddScopes(string[] scopes);
    IDiscordApplicationInstallParamsProperties WithPermissions(NetCord.Permissions? permissions);
}


public interface IDiscordApplicationIntegrationTypeConfigurationProperties
{
    NetCord.Rest.ApplicationIntegrationTypeConfigurationProperties Original { get; }
    IDiscordApplicationInstallParamsProperties OAuth2InstallParams { get; }
    IDiscordApplicationIntegrationTypeConfigurationProperties WithOAuth2InstallParams(IDiscordApplicationInstallParamsProperties oAuth2InstallParams);
}


public interface IDiscordForumGuildThreadMessageProperties
{
    NetCord.Rest.ForumGuildThreadMessageProperties Original { get; }
    string Content { get; }
    IEnumerable<IDiscordEmbedProperties> Embeds { get; }
    IDiscordAllowedMentionsProperties AllowedMentions { get; }
    IEnumerable<IDiscordComponentProperties> Components { get; }
    IEnumerable<ulong> StickerIds { get; }
    IEnumerable<IDiscordAttachmentProperties> Attachments { get; }
    NetCord.MessageFlags? Flags { get; }
    IDiscordForumGuildThreadMessageProperties WithContent(string content);
    IDiscordForumGuildThreadMessageProperties WithEmbeds(IEnumerable<IDiscordEmbedProperties> embeds);
    IDiscordForumGuildThreadMessageProperties AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds);
    IDiscordForumGuildThreadMessageProperties AddEmbeds(IDiscordEmbedProperties[] embeds);
    IDiscordForumGuildThreadMessageProperties WithAllowedMentions(IDiscordAllowedMentionsProperties allowedMentions);
    IDiscordForumGuildThreadMessageProperties WithComponents(IEnumerable<IDiscordComponentProperties> components);
    IDiscordForumGuildThreadMessageProperties AddComponents(IEnumerable<IDiscordComponentProperties> components);
    IDiscordForumGuildThreadMessageProperties AddComponents(IDiscordComponentProperties[] components);
    IDiscordForumGuildThreadMessageProperties WithStickerIds(IEnumerable<ulong> stickerIds);
    IDiscordForumGuildThreadMessageProperties AddStickerIds(IEnumerable<ulong> stickerIds);
    IDiscordForumGuildThreadMessageProperties AddStickerIds(ulong[] stickerIds);
    IDiscordForumGuildThreadMessageProperties WithAttachments(IEnumerable<IDiscordAttachmentProperties> attachments);
    IDiscordForumGuildThreadMessageProperties AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments);
    IDiscordForumGuildThreadMessageProperties AddAttachments(IDiscordAttachmentProperties[] attachments);
    IDiscordForumGuildThreadMessageProperties WithFlags(NetCord.MessageFlags? flags);
}


public interface IDiscordGatewaySessionStartLimit
{
    NetCord.Rest.GatewaySessionStartLimit Original { get; }
    int Total { get; }
    int Remaining { get; }
    System.TimeSpan ResetAfter { get; }
    int MaxConcurrency { get; }
}


public interface IDiscordSticker
{
    NetCord.Sticker Original { get; }
    ulong Id { get; }
    string Name { get; }
    string Description { get; }
    IReadOnlyList<string> Tags { get; }
    NetCord.StickerFormat Format { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordImageUrl GetImageUrl(NetCord.ImageFormat format);
}


public class DiscordInteractionContext : IDiscordInteractionContext
{
    private readonly NetCord.Services.IInteractionContext _original;
    public DiscordInteractionContext(NetCord.Services.IInteractionContext original)
    {
        _original = original;
    }
    public NetCord.Services.IInteractionContext Original => _original;
    public IDiscordInteraction Interaction => new DiscordInteraction(_original.Interaction);
}


public class DiscordInteraction : IDiscordInteraction
{
    private readonly NetCord.Interaction _original;
    public DiscordInteraction(NetCord.Interaction original)
    {
        _original = original;
    }
    public NetCord.Interaction Original => _original;
    public ulong Id => _original.Id;
    public ulong ApplicationId => _original.ApplicationId;
    public ulong? GuildId => _original.GuildId;
    public IDiscordInteractionGuildReference GuildReference => new DiscordInteractionGuildReference(_original.GuildReference);
    public IDiscordGuild Guild => new DiscordGuild(_original.Guild);
    public IDiscordTextChannel Channel => new DiscordTextChannel(_original.Channel);
    public IDiscordUser User => new DiscordUser(_original.User);
    public string Token => _original.Token;
    public NetCord.Permissions AppPermissions => _original.AppPermissions;
    public string UserLocale => _original.UserLocale;
    public string GuildLocale => _original.GuildLocale;
    public IReadOnlyList<IDiscordEntitlement> Entitlements => _original.Entitlements.Select(x => new DiscordEntitlement(x)).ToList();
    public IReadOnlyDictionary<NetCord.ApplicationIntegrationType, ulong> AuthorizingIntegrationOwners => _original.AuthorizingIntegrationOwners;
    public NetCord.InteractionContextType Context => _original.Context;
    public IDiscordInteractionData Data => new DiscordInteractionData(_original.Data);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public Task SendResponseAsync(IDiscordInteractionCallback callback, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.SendResponseAsync(callback.Original, properties.Original, cancellationToken);
    public async Task<IDiscordRestMessage> GetResponseAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.GetResponseAsync(properties.Original, cancellationToken));
    public async Task<IDiscordRestMessage> ModifyResponseAsync(Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.ModifyResponseAsync(action, properties.Original, cancellationToken));
    public Task DeleteResponseAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteResponseAsync(properties.Original, cancellationToken);
    public async Task<IDiscordRestMessage> SendFollowupMessageAsync(IDiscordInteractionMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.SendFollowupMessageAsync(message.Original, properties.Original, cancellationToken));
    public async Task<IDiscordRestMessage> GetFollowupMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.GetFollowupMessageAsync(messageId, properties.Original, cancellationToken));
    public async Task<IDiscordRestMessage> ModifyFollowupMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.ModifyFollowupMessageAsync(messageId, action, properties.Original, cancellationToken));
    public Task DeleteFollowupMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteFollowupMessageAsync(messageId, properties.Original, cancellationToken);
}


public class DiscordInteractionGuildReference : IDiscordInteractionGuildReference
{
    private readonly NetCord.InteractionGuildReference _original;
    public DiscordInteractionGuildReference(NetCord.InteractionGuildReference original)
    {
        _original = original;
    }
    public NetCord.InteractionGuildReference Original => _original;
    public ulong Id => _original.Id;
    public IReadOnlyList<string> Features => _original.Features;
    public string Locale => _original.Locale;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordGuild : IDiscordGuild
{
    private readonly NetCord.Gateway.Guild _original;
    public DiscordGuild(NetCord.Gateway.Guild original)
    {
        _original = original;
    }
    public NetCord.Gateway.Guild Original => _original;
    public System.DateTimeOffset JoinedAt => _original.JoinedAt;
    public bool IsLarge => _original.IsLarge;
    public bool IsUnavailable => _original.IsUnavailable;
    public int UserCount => _original.UserCount;
    public ImmutableDictionary<ulong, IDiscordVoiceState> VoiceStates => _original.VoiceStates.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordVoiceState)new DiscordVoiceState(kv.Value));
    public ImmutableDictionary<ulong, IDiscordGuildUser> Users => _original.Users.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordGuildUser)new DiscordGuildUser(kv.Value));
    public ImmutableDictionary<ulong, IDiscordGuildChannel> Channels => _original.Channels.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordGuildChannel)new DiscordGuildChannel(kv.Value));
    public ImmutableDictionary<ulong, IDiscordGuildThread> ActiveThreads => _original.ActiveThreads.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordGuildThread)new DiscordGuildThread(kv.Value));
    public ImmutableDictionary<ulong, IDiscordPresence> Presences => _original.Presences.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordPresence)new DiscordPresence(kv.Value));
    public ImmutableDictionary<ulong, IDiscordStageInstance> StageInstances => _original.StageInstances.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordStageInstance)new DiscordStageInstance(kv.Value));
    public ImmutableDictionary<ulong, IDiscordGuildScheduledEvent> ScheduledEvents => _original.ScheduledEvents.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordGuildScheduledEvent)new DiscordGuildScheduledEvent(kv.Value));
    public bool IsOwner => _original.IsOwner;
    public ulong Id => _original.Id;
    public string Name => _original.Name;
    public bool HasIcon => _original.HasIcon;
    public string IconHash => _original.IconHash;
    public bool HasSplash => _original.HasSplash;
    public string SplashHash => _original.SplashHash;
    public bool HasDiscoverySplash => _original.HasDiscoverySplash;
    public string DiscoverySplashHash => _original.DiscoverySplashHash;
    public ulong OwnerId => _original.OwnerId;
    public NetCord.Permissions? Permissions => _original.Permissions;
    public ulong? AfkChannelId => _original.AfkChannelId;
    public int AfkTimeout => _original.AfkTimeout;
    public bool? WidgetEnabled => _original.WidgetEnabled;
    public ulong? WidgetChannelId => _original.WidgetChannelId;
    public NetCord.VerificationLevel VerificationLevel => _original.VerificationLevel;
    public NetCord.DefaultMessageNotificationLevel DefaultMessageNotificationLevel => _original.DefaultMessageNotificationLevel;
    public NetCord.ContentFilter ContentFilter => _original.ContentFilter;
    public ImmutableDictionary<ulong, IDiscordRole> Roles => _original.Roles.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordRole)new DiscordRole(kv.Value));
    public ImmutableDictionary<ulong, IDiscordGuildEmoji> Emojis => _original.Emojis.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordGuildEmoji)new DiscordGuildEmoji(kv.Value));
    public IReadOnlyList<string> Features => _original.Features;
    public NetCord.MfaLevel MfaLevel => _original.MfaLevel;
    public ulong? ApplicationId => _original.ApplicationId;
    public ulong? SystemChannelId => _original.SystemChannelId;
    public NetCord.Rest.SystemChannelFlags SystemChannelFlags => _original.SystemChannelFlags;
    public ulong? RulesChannelId => _original.RulesChannelId;
    public int? MaxPresences => _original.MaxPresences;
    public int? MaxUsers => _original.MaxUsers;
    public string VanityUrlCode => _original.VanityUrlCode;
    public string Description => _original.Description;
    public bool HasBanner => _original.HasBanner;
    public string BannerHash => _original.BannerHash;
    public int PremiumTier => _original.PremiumTier;
    public int? PremiumSubscriptionCount => _original.PremiumSubscriptionCount;
    public string PreferredLocale => _original.PreferredLocale;
    public ulong? PublicUpdatesChannelId => _original.PublicUpdatesChannelId;
    public int? MaxVideoChannelUsers => _original.MaxVideoChannelUsers;
    public int? MaxStageVideoChannelUsers => _original.MaxStageVideoChannelUsers;
    public int? ApproximateUserCount => _original.ApproximateUserCount;
    public int? ApproximatePresenceCount => _original.ApproximatePresenceCount;
    public IDiscordGuildWelcomeScreen WelcomeScreen => new DiscordGuildWelcomeScreen(_original.WelcomeScreen);
    public NetCord.NsfwLevel NsfwLevel => _original.NsfwLevel;
    public ImmutableDictionary<ulong, IDiscordGuildSticker> Stickers => _original.Stickers.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordGuildSticker)new DiscordGuildSticker(kv.Value));
    public bool PremiumProgressBarEnabled => _original.PremiumProgressBarEnabled;
    public ulong? SafetyAlertsChannelId => _original.SafetyAlertsChannelId;
    public IDiscordRole EveryoneRole => new DiscordRole(_original.EveryoneRole);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordGuild With(Action<IDiscordGuild> action) => new DiscordGuild(_original.With(action));
    public int Compare(IDiscordPartialGuildUser x, IDiscordPartialGuildUser y) => _original.Compare(x.Original, y.Original);
    public IDiscordImageUrl GetIconUrl(NetCord.ImageFormat? format = default) => new DiscordImageUrl(_original.GetIconUrl(format));
    public IDiscordImageUrl GetSplashUrl(NetCord.ImageFormat format) => new DiscordImageUrl(_original.GetSplashUrl(format));
    public IDiscordImageUrl GetDiscoverySplashUrl(NetCord.ImageFormat format) => new DiscordImageUrl(_original.GetDiscoverySplashUrl(format));
    public IDiscordImageUrl GetBannerUrl(NetCord.ImageFormat? format = default) => new DiscordImageUrl(_original.GetBannerUrl(format));
    public IDiscordImageUrl GetWidgetUrl(NetCord.GuildWidgetStyle? style = default, string? hostname = null, NetCord.ApiVersion? version = default) => new DiscordImageUrl(_original.GetWidgetUrl(style, hostname, version));
    public async IAsyncEnumerable<IDiscordRestAuditLogEntry> GetAuditLogAsync(IDiscordGuildAuditLogPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetAuditLogAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordRestAuditLogEntry(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordAutoModerationRule>> GetAutoModerationRulesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetAutoModerationRulesAsync(properties.Original, cancellationToken)).Select(x => new DiscordAutoModerationRule(x)).ToList();
    public async Task<IDiscordAutoModerationRule> GetAutoModerationRuleAsync(ulong autoModerationRuleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordAutoModerationRule(await _original.GetAutoModerationRuleAsync(autoModerationRuleId, properties.Original, cancellationToken));
    public async Task<IDiscordAutoModerationRule> CreateAutoModerationRuleAsync(IDiscordAutoModerationRuleProperties autoModerationRuleProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordAutoModerationRule(await _original.CreateAutoModerationRuleAsync(autoModerationRuleProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordAutoModerationRule> ModifyAutoModerationRuleAsync(ulong autoModerationRuleId, Action<IDiscordAutoModerationRuleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordAutoModerationRule(await _original.ModifyAutoModerationRuleAsync(autoModerationRuleId, action, properties.Original, cancellationToken));
    public Task DeleteAutoModerationRuleAsync(ulong autoModerationRuleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAutoModerationRuleAsync(autoModerationRuleId, properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordGuildEmoji>> GetEmojisAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetEmojisAsync(properties.Original, cancellationToken)).Select(x => new DiscordGuildEmoji(x)).ToList();
    public async Task<IDiscordGuildEmoji> GetEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildEmoji(await _original.GetEmojiAsync(emojiId, properties.Original, cancellationToken));
    public async Task<IDiscordGuildEmoji> CreateEmojiAsync(IDiscordGuildEmojiProperties guildEmojiProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildEmoji(await _original.CreateEmojiAsync(guildEmojiProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordGuildEmoji> ModifyEmojiAsync(ulong emojiId, Action<IDiscordGuildEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildEmoji(await _original.ModifyEmojiAsync(emojiId, action, properties.Original, cancellationToken));
    public Task DeleteEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteEmojiAsync(emojiId, properties.Original, cancellationToken);
    public async Task<IDiscordRestGuild> GetAsync(bool withCounts = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestGuild(await _original.GetAsync(withCounts, properties.Original, cancellationToken));
    public async Task<IDiscordGuildPreview> GetPreviewAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildPreview(await _original.GetPreviewAsync(properties.Original, cancellationToken));
    public async Task<IDiscordRestGuild> ModifyAsync(Action<IDiscordGuildOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestGuild(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAsync(properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordGuildChannel>> GetChannelsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetChannelsAsync(properties.Original, cancellationToken)).Select(x => new DiscordGuildChannel(x)).ToList();
    public async Task<IDiscordGuildChannel> CreateChannelAsync(IDiscordGuildChannelProperties channelProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildChannel(await _original.CreateChannelAsync(channelProperties.Original, properties.Original, cancellationToken));
    public Task ModifyChannelPositionsAsync(IEnumerable<IDiscordGuildChannelPositionProperties> positions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.ModifyChannelPositionsAsync(positions?.Select(x => x.Original), properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordGuildThread>> GetActiveThreadsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetActiveThreadsAsync(properties.Original, cancellationToken)).Select(x => new DiscordGuildThread(x)).ToList();
    public async Task<IDiscordGuildUser> GetUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildUser(await _original.GetUserAsync(userId, properties.Original, cancellationToken));
    public async IAsyncEnumerable<IDiscordGuildUser> GetUsersAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetUsersAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordGuildUser(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordGuildUser>> FindUserAsync(string name, int limit, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.FindUserAsync(name, limit, properties.Original, cancellationToken)).Select(x => new DiscordGuildUser(x)).ToList();
    public async Task<IDiscordGuildUser> AddUserAsync(ulong userId, IDiscordGuildUserProperties userProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildUser(await _original.AddUserAsync(userId, userProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordGuildUser> ModifyUserAsync(ulong userId, Action<IDiscordGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildUser(await _original.ModifyUserAsync(userId, action, properties.Original, cancellationToken));
    public async Task<IDiscordGuildUser> ModifyCurrentUserAsync(Action<IDiscordCurrentGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildUser(await _original.ModifyCurrentUserAsync(action, properties.Original, cancellationToken));
    public Task AddUserRoleAsync(ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.AddUserRoleAsync(userId, roleId, properties.Original, cancellationToken);
    public Task RemoveUserRoleAsync(ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.RemoveUserRoleAsync(userId, roleId, properties.Original, cancellationToken);
    public Task KickUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.KickUserAsync(userId, properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordGuildBan> GetBansAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetBansAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordGuildBan(original);
        }
    }
    public async Task<IDiscordGuildBan> GetBanAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildBan(await _original.GetBanAsync(userId, properties.Original, cancellationToken));
    public Task BanUserAsync(ulong userId, int deleteMessageSeconds = 0, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.BanUserAsync(userId, deleteMessageSeconds, properties.Original, cancellationToken);
    public async Task<IDiscordGuildBulkBan> BanUsersAsync(IEnumerable<ulong> userIds, int deleteMessageSeconds = 0, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildBulkBan(await _original.BanUsersAsync(userIds, deleteMessageSeconds, properties.Original, cancellationToken));
    public Task UnbanUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.UnbanUserAsync(userId, properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordRole>> GetRolesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetRolesAsync(properties.Original, cancellationToken)).Select(x => new DiscordRole(x)).ToList();
    public async Task<IDiscordRole> GetRoleAsync(ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRole(await _original.GetRoleAsync(roleId, properties.Original, cancellationToken));
    public async Task<IDiscordRole> CreateRoleAsync(IDiscordRoleProperties guildRoleProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRole(await _original.CreateRoleAsync(guildRoleProperties.Original, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordRole>> ModifyRolePositionsAsync(IEnumerable<IDiscordRolePositionProperties> positions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.ModifyRolePositionsAsync(positions?.Select(x => x.Original), properties.Original, cancellationToken)).Select(x => new DiscordRole(x)).ToList();
    public async Task<IDiscordRole> ModifyRoleAsync(ulong roleId, Action<IDiscordRoleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRole(await _original.ModifyRoleAsync(roleId, action, properties.Original, cancellationToken));
    public Task DeleteRoleAsync(ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteRoleAsync(roleId, properties.Original, cancellationToken);
    public Task<NetCord.MfaLevel> ModifyMfaLevelAsync(NetCord.MfaLevel mfaLevel, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.ModifyMfaLevelAsync(mfaLevel, properties.Original, cancellationToken);
    public Task<int> GetPruneCountAsync(int days, IEnumerable<ulong>? roles = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.GetPruneCountAsync(days, roles, properties.Original, cancellationToken);
    public Task<int?> PruneAsync(IDiscordGuildPruneProperties pruneProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.PruneAsync(pruneProperties.Original, properties.Original, cancellationToken);
    public async Task<IEnumerable<IDiscordVoiceRegion>> GetVoiceRegionsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetVoiceRegionsAsync(properties.Original, cancellationToken)).Select(x => new DiscordVoiceRegion(x));
    public async Task<IEnumerable<IDiscordRestInvite>> GetInvitesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetInvitesAsync(properties.Original, cancellationToken)).Select(x => new DiscordRestInvite(x));
    public async Task<IReadOnlyList<IDiscordIntegration>> GetIntegrationsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetIntegrationsAsync(properties.Original, cancellationToken)).Select(x => new DiscordIntegration(x)).ToList();
    public Task DeleteIntegrationAsync(ulong integrationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteIntegrationAsync(integrationId, properties.Original, cancellationToken);
    public async Task<IDiscordGuildWidgetSettings> GetWidgetSettingsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildWidgetSettings(await _original.GetWidgetSettingsAsync(properties.Original, cancellationToken));
    public async Task<IDiscordGuildWidgetSettings> ModifyWidgetSettingsAsync(Action<IDiscordGuildWidgetSettingsOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildWidgetSettings(await _original.ModifyWidgetSettingsAsync(action, properties.Original, cancellationToken));
    public async Task<IDiscordGuildWidget> GetWidgetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildWidget(await _original.GetWidgetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordGuildVanityInvite> GetVanityInviteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildVanityInvite(await _original.GetVanityInviteAsync(properties.Original, cancellationToken));
    public async Task<IDiscordGuildWelcomeScreen> GetWelcomeScreenAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildWelcomeScreen(await _original.GetWelcomeScreenAsync(properties.Original, cancellationToken));
    public async Task<IDiscordGuildWelcomeScreen> ModifyWelcomeScreenAsync(Action<IDiscordGuildWelcomeScreenOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildWelcomeScreen(await _original.ModifyWelcomeScreenAsync(action, properties.Original, cancellationToken));
    public async Task<IDiscordGuildOnboarding> GetOnboardingAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildOnboarding(await _original.GetOnboardingAsync(properties.Original, cancellationToken));
    public async Task<IDiscordGuildOnboarding> ModifyOnboardingAsync(Action<IDiscordGuildOnboardingOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildOnboarding(await _original.ModifyOnboardingAsync(action, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordGuildScheduledEvent>> GetScheduledEventsAsync(bool withUserCount = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetScheduledEventsAsync(withUserCount, properties.Original, cancellationToken)).Select(x => new DiscordGuildScheduledEvent(x)).ToList();
    public async Task<IDiscordGuildScheduledEvent> CreateScheduledEventAsync(IDiscordGuildScheduledEventProperties guildScheduledEventProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildScheduledEvent(await _original.CreateScheduledEventAsync(guildScheduledEventProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordGuildScheduledEvent> GetScheduledEventAsync(ulong scheduledEventId, bool withUserCount = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildScheduledEvent(await _original.GetScheduledEventAsync(scheduledEventId, withUserCount, properties.Original, cancellationToken));
    public async Task<IDiscordGuildScheduledEvent> ModifyScheduledEventAsync(ulong scheduledEventId, Action<IDiscordGuildScheduledEventOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildScheduledEvent(await _original.ModifyScheduledEventAsync(scheduledEventId, action, properties.Original, cancellationToken));
    public Task DeleteScheduledEventAsync(ulong scheduledEventId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteScheduledEventAsync(scheduledEventId, properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordGuildScheduledEventUser> GetScheduledEventUsersAsync(ulong scheduledEventId, IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetScheduledEventUsersAsync(scheduledEventId, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordGuildScheduledEventUser(original);
        }
    }
    public async Task<IEnumerable<IDiscordGuildTemplate>> GetTemplatesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetTemplatesAsync(properties.Original, cancellationToken)).Select(x => new DiscordGuildTemplate(x));
    public async Task<IDiscordGuildTemplate> CreateTemplateAsync(IDiscordGuildTemplateProperties guildTemplateProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildTemplate(await _original.CreateTemplateAsync(guildTemplateProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordGuildTemplate> SyncTemplateAsync(string templateCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildTemplate(await _original.SyncTemplateAsync(templateCode, properties.Original, cancellationToken));
    public async Task<IDiscordGuildTemplate> ModifyTemplateAsync(string templateCode, Action<IDiscordGuildTemplateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildTemplate(await _original.ModifyTemplateAsync(templateCode, action, properties.Original, cancellationToken));
    public async Task<IDiscordGuildTemplate> DeleteTemplateAsync(string templateCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildTemplate(await _original.DeleteTemplateAsync(templateCode, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordGuildApplicationCommand>> GetApplicationCommandsAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetApplicationCommandsAsync(applicationId, properties.Original, cancellationToken)).Select(x => new DiscordGuildApplicationCommand(x)).ToList();
    public async Task<IDiscordGuildApplicationCommand> CreateApplicationCommandAsync(ulong applicationId, IDiscordApplicationCommandProperties applicationCommandProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildApplicationCommand(await _original.CreateApplicationCommandAsync(applicationId, applicationCommandProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordGuildApplicationCommand> GetApplicationCommandAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildApplicationCommand(await _original.GetApplicationCommandAsync(applicationId, commandId, properties.Original, cancellationToken));
    public async Task<IDiscordGuildApplicationCommand> ModifyApplicationCommandAsync(ulong applicationId, ulong commandId, Action<IDiscordApplicationCommandOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildApplicationCommand(await _original.ModifyApplicationCommandAsync(applicationId, commandId, action, properties.Original, cancellationToken));
    public Task DeleteApplicationCommandAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteApplicationCommandAsync(applicationId, commandId, properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordGuildApplicationCommand>> BulkOverwriteApplicationCommandsAsync(ulong applicationId, IEnumerable<IDiscordApplicationCommandProperties> commands, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.BulkOverwriteApplicationCommandsAsync(applicationId, commands?.Select(x => x.Original), properties.Original, cancellationToken)).Select(x => new DiscordGuildApplicationCommand(x)).ToList();
    public async Task<IReadOnlyList<IDiscordApplicationCommandGuildPermissions>> GetApplicationCommandsPermissionsAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetApplicationCommandsPermissionsAsync(applicationId, properties.Original, cancellationToken)).Select(x => new DiscordApplicationCommandGuildPermissions(x)).ToList();
    public async Task<IDiscordApplicationCommandGuildPermissions> GetApplicationCommandPermissionsAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationCommandGuildPermissions(await _original.GetApplicationCommandPermissionsAsync(applicationId, commandId, properties.Original, cancellationToken));
    public async Task<IDiscordApplicationCommandGuildPermissions> OverwriteApplicationCommandPermissionsAsync(ulong applicationId, ulong commandId, IEnumerable<IDiscordApplicationCommandGuildPermissionProperties> newPermissions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationCommandGuildPermissions(await _original.OverwriteApplicationCommandPermissionsAsync(applicationId, commandId, newPermissions?.Select(x => x.Original), properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordGuildSticker>> GetStickersAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetStickersAsync(properties.Original, cancellationToken)).Select(x => new DiscordGuildSticker(x)).ToList();
    public async Task<IDiscordGuildSticker> GetStickerAsync(ulong stickerId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildSticker(await _original.GetStickerAsync(stickerId, properties.Original, cancellationToken));
    public async Task<IDiscordGuildSticker> CreateStickerAsync(IDiscordGuildStickerProperties sticker, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildSticker(await _original.CreateStickerAsync(sticker.Original, properties.Original, cancellationToken));
    public async Task<IDiscordGuildSticker> ModifyStickerAsync(ulong stickerId, Action<IDiscordGuildStickerOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildSticker(await _original.ModifyStickerAsync(stickerId, action, properties.Original, cancellationToken));
    public Task DeleteStickerAsync(ulong stickerId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteStickerAsync(stickerId, properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordGuildUserInfo> SearchUsersAsync(IDiscordGuildUsersSearchPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.SearchUsersAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordGuildUserInfo(original);
        }
    }
    public async Task<IDiscordGuildUser> GetCurrentUserGuildUserAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildUser(await _original.GetCurrentUserGuildUserAsync(properties.Original, cancellationToken));
    public Task LeaveAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.LeaveAsync(properties.Original, cancellationToken);
    public async Task<IDiscordVoiceState> GetCurrentUserVoiceStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordVoiceState(await _original.GetCurrentUserVoiceStateAsync(properties.Original, cancellationToken));
    public async Task<IDiscordVoiceState> GetUserVoiceStateAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordVoiceState(await _original.GetUserVoiceStateAsync(userId, properties.Original, cancellationToken));
    public Task ModifyCurrentUserVoiceStateAsync(Action<IDiscordCurrentUserVoiceStateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.ModifyCurrentUserVoiceStateAsync(action, properties.Original, cancellationToken);
    public Task ModifyUserVoiceStateAsync(ulong channelId, ulong userId, Action<IDiscordVoiceStateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.ModifyUserVoiceStateAsync(channelId, userId, action, properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordWebhook>> GetWebhooksAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetWebhooksAsync(properties.Original, cancellationToken)).Select(x => new DiscordWebhook(x)).ToList();
}


public class DiscordTextChannel : IDiscordTextChannel
{
    private readonly NetCord.TextChannel _original;
    public DiscordTextChannel(NetCord.TextChannel original)
    {
        _original = original;
    }
    public NetCord.TextChannel Original => _original;
    public ulong? LastMessageId => _original.LastMessageId;
    public System.DateTimeOffset? LastPin => _original.LastPin;
    public ulong Id => _original.Id;
    public NetCord.ChannelFlags Flags => _original.Flags;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordTextChannel> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordTextChannel(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordTextChannel> DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordTextChannel(await _original.DeleteAsync(properties.Original, cancellationToken));
    public async IAsyncEnumerable<IDiscordRestMessage> GetMessagesAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetMessagesAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordRestMessage(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetMessagesAroundAsync(ulong messageId, int? limit = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetMessagesAroundAsync(messageId, limit, properties.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    public async Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.GetMessageAsync(messageId, properties.Original, cancellationToken));
    public async Task<IDiscordRestMessage> SendMessageAsync(IDiscordMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.SendMessageAsync(message.Original, properties.Original, cancellationToken));
    public Task AddMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.AddMessageReactionAsync(messageId, emoji.Original, properties.Original, cancellationToken);
    public Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessageReactionAsync(messageId, emoji.Original, properties.Original, cancellationToken);
    public Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessageReactionAsync(messageId, emoji.Original, userId, properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordUser> GetMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordMessageReactionsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetMessageReactionsAsync(messageId, emoji.Original, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAllMessageReactionsAsync(messageId, properties.Original, cancellationToken);
    public Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAllMessageReactionsAsync(messageId, emoji.Original, properties.Original, cancellationToken);
    public async Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.ModifyMessageAsync(messageId, action, properties.Original, cancellationToken));
    public Task DeleteMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessageAsync(messageId, properties.Original, cancellationToken);
    public Task DeleteMessagesAsync(IEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessagesAsync(messageIds, properties.Original, cancellationToken);
    public Task DeleteMessagesAsync(IAsyncEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessagesAsync(messageIds, properties.Original, cancellationToken);
    public Task TriggerTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.TriggerTypingStateAsync(properties.Original, cancellationToken);
    public Task<IDisposable> EnterTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.EnterTypingStateAsync(properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetPinnedMessagesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetPinnedMessagesAsync(properties.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    public Task PinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.PinMessageAsync(messageId, properties.Original, cancellationToken);
    public Task UnpinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.UnpinMessageAsync(messageId, properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordUser> GetMessagePollAnswerVotersAsync(ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetMessagePollAnswerVotersAsync(messageId, answerId, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public async Task<IDiscordRestMessage> EndMessagePollAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.EndMessagePollAsync(messageId, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.CreateGoogleCloudPlatformStorageBucketsAsync(buckets?.Select(x => x.Original), properties.Original, cancellationToken)).Select(x => new DiscordGoogleCloudPlatformStorageBucket(x)).ToList();
}


public class DiscordUser : IDiscordUser
{
    private readonly NetCord.User _original;
    public DiscordUser(NetCord.User original)
    {
        _original = original;
    }
    public NetCord.User Original => _original;
    public ulong Id => _original.Id;
    public string Username => _original.Username;
    public ushort Discriminator => _original.Discriminator;
    public string GlobalName => _original.GlobalName;
    public string AvatarHash => _original.AvatarHash;
    public bool IsBot => _original.IsBot;
    public bool? IsSystemUser => _original.IsSystemUser;
    public bool? MfaEnabled => _original.MfaEnabled;
    public string BannerHash => _original.BannerHash;
    public NetCord.Color? AccentColor => _original.AccentColor;
    public string Locale => _original.Locale;
    public bool? Verified => _original.Verified;
    public string Email => _original.Email;
    public NetCord.UserFlags? Flags => _original.Flags;
    public NetCord.PremiumType? PremiumType => _original.PremiumType;
    public NetCord.UserFlags? PublicFlags => _original.PublicFlags;
    public IDiscordAvatarDecorationData AvatarDecorationData => new DiscordAvatarDecorationData(_original.AvatarDecorationData);
    public bool HasAvatar => _original.HasAvatar;
    public bool HasBanner => _original.HasBanner;
    public bool HasAvatarDecoration => _original.HasAvatarDecoration;
    public IDiscordImageUrl DefaultAvatarUrl => new DiscordImageUrl(_original.DefaultAvatarUrl);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordImageUrl GetAvatarUrl(NetCord.ImageFormat? format = default) => new DiscordImageUrl(_original.GetAvatarUrl(format));
    public IDiscordImageUrl GetBannerUrl(NetCord.ImageFormat? format = default) => new DiscordImageUrl(_original.GetBannerUrl(format));
    public IDiscordImageUrl GetAvatarDecorationUrl() => new DiscordImageUrl(_original.GetAvatarDecorationUrl());
    public async Task<IDiscordUser> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordUser(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordDMChannel> GetDMChannelAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordDMChannel(await _original.GetDMChannelAsync(properties.Original, cancellationToken));
}


public class DiscordEntitlement : IDiscordEntitlement
{
    private readonly NetCord.Entitlement _original;
    public DiscordEntitlement(NetCord.Entitlement original)
    {
        _original = original;
    }
    public NetCord.Entitlement Original => _original;
    public ulong Id => _original.Id;
    public ulong SkuId => _original.SkuId;
    public ulong ApplicationId => _original.ApplicationId;
    public ulong? UserId => _original.UserId;
    public NetCord.EntitlementType Type => _original.Type;
    public bool Deleted => _original.Deleted;
    public System.DateTimeOffset? StartsAt => _original.StartsAt;
    public System.DateTimeOffset? EndsAt => _original.EndsAt;
    public ulong? GuildId => _original.GuildId;
    public bool? Consumed => _original.Consumed;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordEntitlement> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordEntitlement(await _original.GetAsync(properties.Original, cancellationToken));
    public Task ConsumeAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.ConsumeAsync(properties.Original, cancellationToken);
}


public class DiscordInteractionData : IDiscordInteractionData
{
    private readonly NetCord.InteractionData _original;
    public DiscordInteractionData(NetCord.InteractionData original)
    {
        _original = original;
    }
    public NetCord.InteractionData Original => _original;
}


public class DiscordInteractionCallback : IDiscordInteractionCallback
{
    private readonly NetCord.Rest.InteractionCallback _original;
    public DiscordInteractionCallback(NetCord.Rest.InteractionCallback original)
    {
        _original = original;
    }
    public NetCord.Rest.InteractionCallback Original => _original;
    public NetCord.Rest.InteractionCallbackType Type => _original.Type;
    public static IDiscordInteractionCallback Pong => new DiscordInteractionCallback(NetCord.Rest.InteractionCallback.Pong);
    public static IDiscordInteractionCallback DeferredModifyMessage => new DiscordInteractionCallback(NetCord.Rest.InteractionCallback.DeferredModifyMessage);
    public HttpContent Serialize() => _original.Serialize();
}


public class DiscordRestRequestProperties : IDiscordRestRequestProperties
{
    private readonly NetCord.Rest.RestRequestProperties _original;
    public DiscordRestRequestProperties(NetCord.Rest.RestRequestProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.RestRequestProperties Original => _original;
    public NetCord.Rest.RestRateLimitHandling? RateLimitHandling => _original.RateLimitHandling;
    public string AuditLogReason => _original.AuditLogReason;
    public string ErrorLocalization => _original.ErrorLocalization;
    public IDiscordRestRequestProperties WithRateLimitHandling(NetCord.Rest.RestRateLimitHandling? rateLimitHandling) => new DiscordRestRequestProperties(_original.WithRateLimitHandling(rateLimitHandling));
    public IDiscordRestRequestProperties WithAuditLogReason(string auditLogReason) => new DiscordRestRequestProperties(_original.WithAuditLogReason(auditLogReason));
    public IDiscordRestRequestProperties WithErrorLocalization(string errorLocalization) => new DiscordRestRequestProperties(_original.WithErrorLocalization(errorLocalization));
}


public class DiscordRestMessage : IDiscordRestMessage
{
    private readonly NetCord.Rest.RestMessage _original;
    public DiscordRestMessage(NetCord.Rest.RestMessage original)
    {
        _original = original;
    }
    public NetCord.Rest.RestMessage Original => _original;
    public ulong Id => _original.Id;
    public ulong ChannelId => _original.ChannelId;
    public IDiscordUser Author => new DiscordUser(_original.Author);
    public string Content => _original.Content;
    public System.DateTimeOffset? EditedAt => _original.EditedAt;
    public bool IsTts => _original.IsTts;
    public bool MentionEveryone => _original.MentionEveryone;
    public IReadOnlyList<IDiscordUser> MentionedUsers => _original.MentionedUsers.Select(x => new DiscordUser(x)).ToList();
    public IReadOnlyList<ulong> MentionedRoleIds => _original.MentionedRoleIds;
    public IReadOnlyList<IDiscordGuildChannelMention> MentionedChannels => _original.MentionedChannels.Select(x => new DiscordGuildChannelMention(x)).ToList();
    public IReadOnlyList<IDiscordAttachment> Attachments => _original.Attachments.Select(x => new DiscordAttachment(x)).ToList();
    public IReadOnlyList<IDiscordEmbed> Embeds => _original.Embeds.Select(x => new DiscordEmbed(x)).ToList();
    public IReadOnlyList<IDiscordMessageReaction> Reactions => _original.Reactions.Select(x => new DiscordMessageReaction(x)).ToList();
    public string Nonce => _original.Nonce;
    public bool IsPinned => _original.IsPinned;
    public ulong? WebhookId => _original.WebhookId;
    public NetCord.MessageType Type => _original.Type;
    public IDiscordMessageActivity Activity => new DiscordMessageActivity(_original.Activity);
    public IDiscordApplication Application => new DiscordApplication(_original.Application);
    public ulong? ApplicationId => _original.ApplicationId;
    public NetCord.MessageFlags Flags => _original.Flags;
    public IDiscordMessageReference MessageReference => new DiscordMessageReference(_original.MessageReference);
    public IReadOnlyList<IDiscordMessageSnapshot> MessageSnapshots => _original.MessageSnapshots.Select(x => new DiscordMessageSnapshot(x)).ToList();
    public IDiscordRestMessage ReferencedMessage => new DiscordRestMessage(_original.ReferencedMessage);
    public IDiscordMessageInteractionMetadata InteractionMetadata => new DiscordMessageInteractionMetadata(_original.InteractionMetadata);
    public IDiscordMessageInteraction Interaction => new DiscordMessageInteraction(_original.Interaction);
    public IDiscordGuildThread StartedThread => new DiscordGuildThread(_original.StartedThread);
    public IReadOnlyList<IDiscordComponent> Components => _original.Components.Select(x => new DiscordComponent(x)).ToList();
    public IReadOnlyList<IDiscordMessageSticker> Stickers => _original.Stickers.Select(x => new DiscordMessageSticker(x)).ToList();
    public int? Position => _original.Position;
    public IDiscordRoleSubscriptionData RoleSubscriptionData => new DiscordRoleSubscriptionData(_original.RoleSubscriptionData);
    public IDiscordInteractionResolvedData ResolvedData => new DiscordInteractionResolvedData(_original.ResolvedData);
    public IDiscordMessagePoll Poll => new DiscordMessagePoll(_original.Poll);
    public IDiscordMessageCall Call => new DiscordMessageCall(_original.Call);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordRestMessage> ReplyAsync(IDiscordReplyMessageProperties replyMessage, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.ReplyAsync(replyMessage.Original, properties.Original, cancellationToken));
    public async Task<IDiscordRestMessage> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordRestMessage> SendAsync(IDiscordMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.SendAsync(message.Original, properties.Original, cancellationToken));
    public async Task<IDiscordRestMessage> CrosspostAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.CrosspostAsync(properties.Original, cancellationToken));
    public Task AddReactionAsync(IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.AddReactionAsync(emoji.Original, properties.Original, cancellationToken);
    public Task DeleteReactionAsync(IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteReactionAsync(emoji.Original, properties.Original, cancellationToken);
    public Task DeleteReactionAsync(IDiscordReactionEmojiProperties emoji, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteReactionAsync(emoji.Original, userId, properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordUser> GetReactionsAsync(IDiscordReactionEmojiProperties emoji, IDiscordMessageReactionsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetReactionsAsync(emoji.Original, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public Task DeleteAllReactionsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAllReactionsAsync(properties.Original, cancellationToken);
    public Task DeleteAllReactionsAsync(IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAllReactionsAsync(emoji.Original, properties.Original, cancellationToken);
    public async Task<IDiscordRestMessage> ModifyAsync(Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAsync(properties.Original, cancellationToken);
    public Task PinAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.PinAsync(properties.Original, cancellationToken);
    public Task UnpinAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.UnpinAsync(properties.Original, cancellationToken);
    public async Task<IDiscordGuildThread> CreateGuildThreadAsync(IDiscordGuildThreadFromMessageProperties threadFromMessageProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildThread(await _original.CreateGuildThreadAsync(threadFromMessageProperties.Original, properties.Original, cancellationToken));
    public async IAsyncEnumerable<IDiscordUser> GetPollAnswerVotersAsync(int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetPollAnswerVotersAsync(answerId, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public async Task<IDiscordRestMessage> EndPollAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.EndPollAsync(properties.Original, cancellationToken));
}


public class DiscordMessageOptions : IDiscordMessageOptions
{
    private readonly NetCord.Rest.MessageOptions _original;
    public DiscordMessageOptions(NetCord.Rest.MessageOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.MessageOptions Original => _original;
    public string Content => _original.Content;
    public IEnumerable<IDiscordEmbedProperties> Embeds => _original.Embeds.Select(x => new DiscordEmbedProperties(x));
    public NetCord.MessageFlags? Flags => _original.Flags;
    public IDiscordAllowedMentionsProperties AllowedMentions => new DiscordAllowedMentionsProperties(_original.AllowedMentions);
    public IEnumerable<IDiscordComponentProperties> Components => _original.Components.Select(x => new DiscordComponentProperties(x));
    public IEnumerable<IDiscordAttachmentProperties> Attachments => _original.Attachments.Select(x => new DiscordAttachmentProperties(x));
    public HttpContent Serialize() => _original.Serialize();
    public IDiscordMessageOptions WithContent(string content) => new DiscordMessageOptions(_original.WithContent(content));
    public IDiscordMessageOptions WithEmbeds(IEnumerable<IDiscordEmbedProperties> embeds) => new DiscordMessageOptions(_original.WithEmbeds(embeds?.Select(x => x.Original)));
    public IDiscordMessageOptions AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds) => new DiscordMessageOptions(_original.AddEmbeds(embeds?.Select(x => x.Original)));
    public IDiscordMessageOptions AddEmbeds(IDiscordEmbedProperties[] embeds) => new DiscordMessageOptions(_original.AddEmbeds(embeds.Original));
    public IDiscordMessageOptions WithFlags(NetCord.MessageFlags? flags) => new DiscordMessageOptions(_original.WithFlags(flags));
    public IDiscordMessageOptions WithAllowedMentions(IDiscordAllowedMentionsProperties allowedMentions) => new DiscordMessageOptions(_original.WithAllowedMentions(allowedMentions.Original));
    public IDiscordMessageOptions WithComponents(IEnumerable<IDiscordComponentProperties> components) => new DiscordMessageOptions(_original.WithComponents(components?.Select(x => x.Original)));
    public IDiscordMessageOptions AddComponents(IEnumerable<IDiscordComponentProperties> components) => new DiscordMessageOptions(_original.AddComponents(components?.Select(x => x.Original)));
    public IDiscordMessageOptions AddComponents(IDiscordComponentProperties[] components) => new DiscordMessageOptions(_original.AddComponents(components.Original));
    public IDiscordMessageOptions WithAttachments(IEnumerable<IDiscordAttachmentProperties> attachments) => new DiscordMessageOptions(_original.WithAttachments(attachments?.Select(x => x.Original)));
    public IDiscordMessageOptions AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments) => new DiscordMessageOptions(_original.AddAttachments(attachments?.Select(x => x.Original)));
    public IDiscordMessageOptions AddAttachments(IDiscordAttachmentProperties[] attachments) => new DiscordMessageOptions(_original.AddAttachments(attachments.Original));
}


public class DiscordInteractionMessageProperties : IDiscordInteractionMessageProperties
{
    private readonly NetCord.Rest.InteractionMessageProperties _original;
    public DiscordInteractionMessageProperties(NetCord.Rest.InteractionMessageProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.InteractionMessageProperties Original => _original;
    public bool Tts => _original.Tts;
    public string Content => _original.Content;
    public IEnumerable<IDiscordEmbedProperties> Embeds => _original.Embeds.Select(x => new DiscordEmbedProperties(x));
    public IDiscordAllowedMentionsProperties AllowedMentions => new DiscordAllowedMentionsProperties(_original.AllowedMentions);
    public NetCord.MessageFlags? Flags => _original.Flags;
    public IEnumerable<IDiscordComponentProperties> Components => _original.Components.Select(x => new DiscordComponentProperties(x));
    public IEnumerable<IDiscordAttachmentProperties> Attachments => _original.Attachments.Select(x => new DiscordAttachmentProperties(x));
    public IDiscordMessagePollProperties Poll => new DiscordMessagePollProperties(_original.Poll);
    public HttpContent Serialize() => _original.Serialize();
    public IDiscordInteractionMessageProperties WithTts(bool tts = true) => new DiscordInteractionMessageProperties(_original.WithTts(tts));
    public IDiscordInteractionMessageProperties WithContent(string content) => new DiscordInteractionMessageProperties(_original.WithContent(content));
    public IDiscordInteractionMessageProperties WithEmbeds(IEnumerable<IDiscordEmbedProperties> embeds) => new DiscordInteractionMessageProperties(_original.WithEmbeds(embeds?.Select(x => x.Original)));
    public IDiscordInteractionMessageProperties AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds) => new DiscordInteractionMessageProperties(_original.AddEmbeds(embeds?.Select(x => x.Original)));
    public IDiscordInteractionMessageProperties AddEmbeds(IDiscordEmbedProperties[] embeds) => new DiscordInteractionMessageProperties(_original.AddEmbeds(embeds.Original));
    public IDiscordInteractionMessageProperties WithAllowedMentions(IDiscordAllowedMentionsProperties allowedMentions) => new DiscordInteractionMessageProperties(_original.WithAllowedMentions(allowedMentions.Original));
    public IDiscordInteractionMessageProperties WithFlags(NetCord.MessageFlags? flags) => new DiscordInteractionMessageProperties(_original.WithFlags(flags));
    public IDiscordInteractionMessageProperties WithComponents(IEnumerable<IDiscordComponentProperties> components) => new DiscordInteractionMessageProperties(_original.WithComponents(components?.Select(x => x.Original)));
    public IDiscordInteractionMessageProperties AddComponents(IEnumerable<IDiscordComponentProperties> components) => new DiscordInteractionMessageProperties(_original.AddComponents(components?.Select(x => x.Original)));
    public IDiscordInteractionMessageProperties AddComponents(IDiscordComponentProperties[] components) => new DiscordInteractionMessageProperties(_original.AddComponents(components.Original));
    public IDiscordInteractionMessageProperties WithAttachments(IEnumerable<IDiscordAttachmentProperties> attachments) => new DiscordInteractionMessageProperties(_original.WithAttachments(attachments?.Select(x => x.Original)));
    public IDiscordInteractionMessageProperties AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments) => new DiscordInteractionMessageProperties(_original.AddAttachments(attachments?.Select(x => x.Original)));
    public IDiscordInteractionMessageProperties AddAttachments(IDiscordAttachmentProperties[] attachments) => new DiscordInteractionMessageProperties(_original.AddAttachments(attachments.Original));
    public IDiscordInteractionMessageProperties WithPoll(IDiscordMessagePollProperties poll) => new DiscordInteractionMessageProperties(_original.WithPoll(poll.Original));
}


public class DiscordVoiceState : IDiscordVoiceState
{
    private readonly NetCord.Gateway.VoiceState _original;
    public DiscordVoiceState(NetCord.Gateway.VoiceState original)
    {
        _original = original;
    }
    public NetCord.Gateway.VoiceState Original => _original;
    public ulong GuildId => _original.GuildId;
    public ulong? ChannelId => _original.ChannelId;
    public ulong UserId => _original.UserId;
    public IDiscordGuildUser User => new DiscordGuildUser(_original.User);
    public string SessionId => _original.SessionId;
    public bool IsDeafened => _original.IsDeafened;
    public bool IsMuted => _original.IsMuted;
    public bool IsSelfDeafened => _original.IsSelfDeafened;
    public bool IsSelfMuted => _original.IsSelfMuted;
    public bool? SelfStreamExists => _original.SelfStreamExists;
    public bool SelfVideoExists => _original.SelfVideoExists;
    public bool Suppressed => _original.Suppressed;
    public System.DateTimeOffset? RequestToSpeakTimestamp => _original.RequestToSpeakTimestamp;
}


public class DiscordGuildUser : IDiscordGuildUser
{
    private readonly NetCord.GuildUser _original;
    public DiscordGuildUser(NetCord.GuildUser original)
    {
        _original = original;
    }
    public NetCord.GuildUser Original => _original;
    public ulong GuildId => _original.GuildId;
    public string Nickname => _original.Nickname;
    public string GuildAvatarHash => _original.GuildAvatarHash;
    public string GuildBannerHash => _original.GuildBannerHash;
    public IReadOnlyList<ulong> RoleIds => _original.RoleIds;
    public ulong? HoistedRoleId => _original.HoistedRoleId;
    public System.DateTimeOffset JoinedAt => _original.JoinedAt;
    public System.DateTimeOffset? GuildBoostStart => _original.GuildBoostStart;
    public bool Deafened => _original.Deafened;
    public bool Muted => _original.Muted;
    public NetCord.GuildUserFlags GuildFlags => _original.GuildFlags;
    public bool? IsPending => _original.IsPending;
    public System.DateTimeOffset? TimeOutUntil => _original.TimeOutUntil;
    public IDiscordAvatarDecorationData GuildAvatarDecorationData => new DiscordAvatarDecorationData(_original.GuildAvatarDecorationData);
    public bool HasGuildAvatar => _original.HasGuildAvatar;
    public bool HasGuildBanner => _original.HasGuildBanner;
    public bool HasGuildAvatarDecoration => _original.HasGuildAvatarDecoration;
    public ulong Id => _original.Id;
    public string Username => _original.Username;
    public ushort Discriminator => _original.Discriminator;
    public string GlobalName => _original.GlobalName;
    public string AvatarHash => _original.AvatarHash;
    public bool IsBot => _original.IsBot;
    public bool? IsSystemUser => _original.IsSystemUser;
    public bool? MfaEnabled => _original.MfaEnabled;
    public string BannerHash => _original.BannerHash;
    public NetCord.Color? AccentColor => _original.AccentColor;
    public string Locale => _original.Locale;
    public bool? Verified => _original.Verified;
    public string Email => _original.Email;
    public NetCord.UserFlags? Flags => _original.Flags;
    public NetCord.PremiumType? PremiumType => _original.PremiumType;
    public NetCord.UserFlags? PublicFlags => _original.PublicFlags;
    public IDiscordAvatarDecorationData AvatarDecorationData => new DiscordAvatarDecorationData(_original.AvatarDecorationData);
    public bool HasAvatar => _original.HasAvatar;
    public bool HasBanner => _original.HasBanner;
    public bool HasAvatarDecoration => _original.HasAvatarDecoration;
    public IDiscordImageUrl DefaultAvatarUrl => new DiscordImageUrl(_original.DefaultAvatarUrl);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordImageUrl GetGuildAvatarUrl(NetCord.ImageFormat? format = default) => new DiscordImageUrl(_original.GetGuildAvatarUrl(format));
    public IDiscordImageUrl GetGuildBannerUrl(NetCord.ImageFormat? format = default) => new DiscordImageUrl(_original.GetGuildBannerUrl(format));
    public async Task<IDiscordGuildUser> TimeOutAsync(System.DateTimeOffset until, IDiscordRestRequestProperties? properties = null) => new DiscordGuildUser(await _original.TimeOutAsync(until, properties.Original));
    public async Task<IDiscordGuildUserInfo> GetInfoAsync(IDiscordRestRequestProperties? properties = null) => new DiscordGuildUserInfo(await _original.GetInfoAsync(properties.Original));
    public async Task<IDiscordGuildUser> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildUser(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordGuildUser> ModifyAsync(Action<IDiscordGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildUser(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public Task AddRoleAsync(ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.AddRoleAsync(roleId, properties.Original, cancellationToken);
    public Task RemoveRoleAsync(ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.RemoveRoleAsync(roleId, properties.Original, cancellationToken);
    public Task KickAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.KickAsync(properties.Original, cancellationToken);
    public Task BanAsync(int deleteMessageSeconds = 0, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.BanAsync(deleteMessageSeconds, properties.Original, cancellationToken);
    public Task UnbanAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.UnbanAsync(properties.Original, cancellationToken);
    public async Task<IDiscordVoiceState> GetVoiceStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordVoiceState(await _original.GetVoiceStateAsync(properties.Original, cancellationToken));
    public Task ModifyVoiceStateAsync(ulong channelId, Action<IDiscordVoiceStateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.ModifyVoiceStateAsync(channelId, action, properties.Original, cancellationToken);
    public IDiscordImageUrl GetGuildAvatarDecorationUrl() => new DiscordImageUrl(_original.GetGuildAvatarDecorationUrl());
    public IDiscordImageUrl GetAvatarUrl(NetCord.ImageFormat? format = default) => new DiscordImageUrl(_original.GetAvatarUrl(format));
    public IDiscordImageUrl GetBannerUrl(NetCord.ImageFormat? format = default) => new DiscordImageUrl(_original.GetBannerUrl(format));
    public IDiscordImageUrl GetAvatarDecorationUrl() => new DiscordImageUrl(_original.GetAvatarDecorationUrl());
    public async Task<IDiscordDMChannel> GetDMChannelAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordDMChannel(await _original.GetDMChannelAsync(properties.Original, cancellationToken));
}


public class DiscordGuildChannel : IDiscordGuildChannel
{
    private readonly NetCord.IGuildChannel _original;
    public DiscordGuildChannel(NetCord.IGuildChannel original)
    {
        _original = original;
    }
    public NetCord.IGuildChannel Original => _original;
    public ulong GuildId => _original.GuildId;
    public int? Position => _original.Position;
    public IReadOnlyDictionary<ulong, IDiscordPermissionOverwrite> PermissionOverwrites => _original.PermissionOverwrites.ToDictionary(kv => kv.Key, kv => (IDiscordPermissionOverwrite)new DiscordPermissionOverwrite(kv.Value));
    public async Task<IDiscordGuildChannel> ModifyAsync(Action<IDiscordGuildChannelOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildChannel(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public Task ModifyPermissionsAsync(IDiscordPermissionOverwriteProperties permissionOverwrite, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.ModifyPermissionsAsync(permissionOverwrite.Original, properties.Original, cancellationToken);
    public async Task<IEnumerable<IDiscordRestInvite>> GetInvitesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetInvitesAsync(properties.Original, cancellationToken)).Select(x => new DiscordRestInvite(x));
    public async Task<IDiscordRestInvite> CreateInviteAsync(IDiscordInviteProperties? inviteProperties = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestInvite(await _original.CreateInviteAsync(inviteProperties.Original, properties.Original, cancellationToken));
    public Task DeletePermissionAsync(ulong overwriteId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeletePermissionAsync(overwriteId, properties.Original, cancellationToken);
}


public class DiscordGuildThread : IDiscordGuildThread
{
    private readonly NetCord.GuildThread _original;
    public DiscordGuildThread(NetCord.GuildThread original)
    {
        _original = original;
    }
    public NetCord.GuildThread Original => _original;
    public ulong OwnerId => _original.OwnerId;
    public int MessageCount => _original.MessageCount;
    public int UserCount => _original.UserCount;
    public IDiscordGuildThreadMetadata Metadata => new DiscordGuildThreadMetadata(_original.Metadata);
    public IDiscordThreadCurrentUser CurrentUser => new DiscordThreadCurrentUser(_original.CurrentUser);
    public int TotalMessageSent => _original.TotalMessageSent;
    public ulong GuildId => _original.GuildId;
    public int? Position => _original.Position;
    public IReadOnlyDictionary<ulong, IDiscordPermissionOverwrite> PermissionOverwrites => _original.PermissionOverwrites.ToDictionary(kv => kv.Key, kv => (IDiscordPermissionOverwrite)new DiscordPermissionOverwrite(kv.Value));
    public string Name => _original.Name;
    public string Topic => _original.Topic;
    public bool Nsfw => _original.Nsfw;
    public int Slowmode => _original.Slowmode;
    public ulong? ParentId => _original.ParentId;
    public NetCord.ThreadArchiveDuration? DefaultAutoArchiveDuration => _original.DefaultAutoArchiveDuration;
    public int DefaultThreadSlowmode => _original.DefaultThreadSlowmode;
    public ulong? LastMessageId => _original.LastMessageId;
    public System.DateTimeOffset? LastPin => _original.LastPin;
    public ulong Id => _original.Id;
    public NetCord.ChannelFlags Flags => _original.Flags;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordGuildThread> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildThread(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordGuildThread> ModifyAsync(Action<IDiscordGuildChannelOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildThread(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public async Task<IDiscordGuildThread> DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildThread(await _original.DeleteAsync(properties.Original, cancellationToken));
    public Task JoinAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.JoinAsync(properties.Original, cancellationToken);
    public Task AddUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.AddUserAsync(userId, properties.Original, cancellationToken);
    public Task LeaveAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.LeaveAsync(properties.Original, cancellationToken);
    public Task DeleteUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteUserAsync(userId, properties.Original, cancellationToken);
    public async Task<IDiscordThreadUser> GetUserAsync(ulong userId, bool withGuildUser = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordThreadUser(await _original.GetUserAsync(userId, withGuildUser, properties.Original, cancellationToken));
    public async IAsyncEnumerable<IDiscordThreadUser> GetUsersAsync(IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetUsersAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordThreadUser(original);
        }
    }
    public Task ModifyPermissionsAsync(IDiscordPermissionOverwriteProperties permissionOverwrite, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.ModifyPermissionsAsync(permissionOverwrite.Original, properties.Original, cancellationToken);
    public async Task<IEnumerable<IDiscordRestInvite>> GetInvitesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetInvitesAsync(properties.Original, cancellationToken)).Select(x => new DiscordRestInvite(x));
    public async Task<IDiscordRestInvite> CreateInviteAsync(IDiscordInviteProperties? inviteProperties = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestInvite(await _original.CreateInviteAsync(inviteProperties.Original, properties.Original, cancellationToken));
    public Task DeletePermissionAsync(ulong overwriteId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeletePermissionAsync(overwriteId, properties.Original, cancellationToken);
    public async Task<IDiscordGuildThread> CreateGuildThreadAsync(ulong messageId, IDiscordGuildThreadFromMessageProperties threadFromMessageProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildThread(await _original.CreateGuildThreadAsync(messageId, threadFromMessageProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordGuildThread> CreateGuildThreadAsync(IDiscordGuildThreadProperties threadProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildThread(await _original.CreateGuildThreadAsync(threadProperties.Original, properties.Original, cancellationToken));
    public async IAsyncEnumerable<IDiscordGuildThread> GetPublicArchivedGuildThreadsAsync(IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetPublicArchivedGuildThreadsAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordGuildThread(original);
        }
    }
    public async IAsyncEnumerable<IDiscordGuildThread> GetPrivateArchivedGuildThreadsAsync(IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetPrivateArchivedGuildThreadsAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordGuildThread(original);
        }
    }
    public async IAsyncEnumerable<IDiscordGuildThread> GetJoinedPrivateArchivedGuildThreadsAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetJoinedPrivateArchivedGuildThreadsAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordGuildThread(original);
        }
    }
    public async Task<IDiscordIncomingWebhook> CreateWebhookAsync(IDiscordWebhookProperties webhookProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordIncomingWebhook(await _original.CreateWebhookAsync(webhookProperties.Original, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordWebhook>> GetChannelWebhooksAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetChannelWebhooksAsync(properties.Original, cancellationToken)).Select(x => new DiscordWebhook(x)).ToList();
    public async IAsyncEnumerable<IDiscordRestMessage> GetMessagesAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetMessagesAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordRestMessage(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetMessagesAroundAsync(ulong messageId, int? limit = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetMessagesAroundAsync(messageId, limit, properties.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    public async Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.GetMessageAsync(messageId, properties.Original, cancellationToken));
    public async Task<IDiscordRestMessage> SendMessageAsync(IDiscordMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.SendMessageAsync(message.Original, properties.Original, cancellationToken));
    public Task AddMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.AddMessageReactionAsync(messageId, emoji.Original, properties.Original, cancellationToken);
    public Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessageReactionAsync(messageId, emoji.Original, properties.Original, cancellationToken);
    public Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessageReactionAsync(messageId, emoji.Original, userId, properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordUser> GetMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordMessageReactionsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetMessageReactionsAsync(messageId, emoji.Original, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAllMessageReactionsAsync(messageId, properties.Original, cancellationToken);
    public Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAllMessageReactionsAsync(messageId, emoji.Original, properties.Original, cancellationToken);
    public async Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.ModifyMessageAsync(messageId, action, properties.Original, cancellationToken));
    public Task DeleteMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessageAsync(messageId, properties.Original, cancellationToken);
    public Task DeleteMessagesAsync(IEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessagesAsync(messageIds, properties.Original, cancellationToken);
    public Task DeleteMessagesAsync(IAsyncEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessagesAsync(messageIds, properties.Original, cancellationToken);
    public Task TriggerTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.TriggerTypingStateAsync(properties.Original, cancellationToken);
    public Task<IDisposable> EnterTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.EnterTypingStateAsync(properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetPinnedMessagesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetPinnedMessagesAsync(properties.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    public Task PinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.PinMessageAsync(messageId, properties.Original, cancellationToken);
    public Task UnpinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.UnpinMessageAsync(messageId, properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordUser> GetMessagePollAnswerVotersAsync(ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetMessagePollAnswerVotersAsync(messageId, answerId, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public async Task<IDiscordRestMessage> EndMessagePollAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.EndMessagePollAsync(messageId, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.CreateGoogleCloudPlatformStorageBucketsAsync(buckets?.Select(x => x.Original), properties.Original, cancellationToken)).Select(x => new DiscordGoogleCloudPlatformStorageBucket(x)).ToList();
}


public class DiscordPresence : IDiscordPresence
{
    private readonly NetCord.Gateway.Presence _original;
    public DiscordPresence(NetCord.Gateway.Presence original)
    {
        _original = original;
    }
    public NetCord.Gateway.Presence Original => _original;
    public IDiscordUser User => new DiscordUser(_original.User);
    public ulong GuildId => _original.GuildId;
    public NetCord.UserStatusType Status => _original.Status;
    public IReadOnlyList<IDiscordUserActivity> Activities => _original.Activities.Select(x => new DiscordUserActivity(x)).ToList();
    public IReadOnlyDictionary<NetCord.Gateway.Platform, NetCord.UserStatusType> Platform => _original.Platform;
}


public class DiscordStageInstance : IDiscordStageInstance
{
    private readonly NetCord.StageInstance _original;
    public DiscordStageInstance(NetCord.StageInstance original)
    {
        _original = original;
    }
    public NetCord.StageInstance Original => _original;
    public ulong Id => _original.Id;
    public ulong GuildId => _original.GuildId;
    public ulong ChannelId => _original.ChannelId;
    public string Topic => _original.Topic;
    public NetCord.StageInstancePrivacyLevel PrivacyLevel => _original.PrivacyLevel;
    public bool DiscoverableDisabled => _original.DiscoverableDisabled;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordStageInstance> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordStageInstance(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordStageInstance> ModifyAsync(Action<IDiscordStageInstanceOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordStageInstance(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAsync(properties.Original, cancellationToken);
}


public class DiscordGuildScheduledEvent : IDiscordGuildScheduledEvent
{
    private readonly NetCord.GuildScheduledEvent _original;
    public DiscordGuildScheduledEvent(NetCord.GuildScheduledEvent original)
    {
        _original = original;
    }
    public NetCord.GuildScheduledEvent Original => _original;
    public ulong Id => _original.Id;
    public ulong GuildId => _original.GuildId;
    public ulong? ChannelId => _original.ChannelId;
    public ulong? CreatorId => _original.CreatorId;
    public string Name => _original.Name;
    public string Description => _original.Description;
    public System.DateTimeOffset ScheduledStartTime => _original.ScheduledStartTime;
    public System.DateTimeOffset? ScheduledEndTime => _original.ScheduledEndTime;
    public NetCord.GuildScheduledEventPrivacyLevel PrivacyLevel => _original.PrivacyLevel;
    public NetCord.GuildScheduledEventStatus Status => _original.Status;
    public NetCord.GuildScheduledEventEntityType EntityType => _original.EntityType;
    public ulong? EntityId => _original.EntityId;
    public string Location => _original.Location;
    public IDiscordUser Creator => new DiscordUser(_original.Creator);
    public int? UserCount => _original.UserCount;
    public string CoverImageHash => _original.CoverImageHash;
    public IDiscordGuildScheduledEventRecurrenceRule RecurrenceRule => new DiscordGuildScheduledEventRecurrenceRule(_original.RecurrenceRule);
    public bool HasCoverImage => _original.HasCoverImage;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordImageUrl GetCoverImageUrl(NetCord.ImageFormat format) => new DiscordImageUrl(_original.GetCoverImageUrl(format));
    public async Task<IDiscordGuildScheduledEvent> GetAsync(bool withUserCount = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildScheduledEvent(await _original.GetAsync(withUserCount, properties.Original, cancellationToken));
    public async Task<IDiscordGuildScheduledEvent> ModifyAsync(Action<IDiscordGuildScheduledEventOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildScheduledEvent(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAsync(properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordGuildScheduledEventUser> GetUsersAsync(IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetUsersAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordGuildScheduledEventUser(original);
        }
    }
}


public class DiscordRole : IDiscordRole
{
    private readonly NetCord.Role _original;
    public DiscordRole(NetCord.Role original)
    {
        _original = original;
    }
    public NetCord.Role Original => _original;
    public ulong Id => _original.Id;
    public string Name => _original.Name;
    public NetCord.Color Color => _original.Color;
    public bool Hoist => _original.Hoist;
    public string IconHash => _original.IconHash;
    public string UnicodeEmoji => _original.UnicodeEmoji;
    public int Position => _original.Position;
    public NetCord.Permissions Permissions => _original.Permissions;
    public bool Managed => _original.Managed;
    public bool Mentionable => _original.Mentionable;
    public IDiscordRoleTags Tags => new DiscordRoleTags(_original.Tags);
    public NetCord.RoleFlags Flags => _original.Flags;
    public ulong GuildId => _original.GuildId;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordImageUrl GetIconUrl(NetCord.ImageFormat format) => new DiscordImageUrl(_original.GetIconUrl(format));
    public int CompareTo(IDiscordRole other) => _original.CompareTo(other.Original);
    public async Task<IDiscordRole> ModifyAsync(Action<IDiscordRoleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRole(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAsync(properties.Original, cancellationToken);
}


public class DiscordGuildEmoji : IDiscordGuildEmoji
{
    private readonly NetCord.GuildEmoji _original;
    public DiscordGuildEmoji(NetCord.GuildEmoji original)
    {
        _original = original;
    }
    public NetCord.GuildEmoji Original => _original;
    public IReadOnlyList<ulong> AllowedRoles => _original.AllowedRoles;
    public ulong GuildId => _original.GuildId;
    public ulong Id => _original.Id;
    public IDiscordUser Creator => new DiscordUser(_original.Creator);
    public bool? RequireColons => _original.RequireColons;
    public bool? Managed => _original.Managed;
    public bool? Available => _original.Available;
    public string Name => _original.Name;
    public bool Animated => _original.Animated;
    public async Task<IDiscordGuildEmoji> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildEmoji(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordGuildEmoji> ModifyAsync(Action<IDiscordGuildEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildEmoji(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAsync(properties.Original, cancellationToken);
    public IDiscordImageUrl GetImageUrl(NetCord.ImageFormat format) => new DiscordImageUrl(_original.GetImageUrl(format));
}


public class DiscordGuildWelcomeScreen : IDiscordGuildWelcomeScreen
{
    private readonly NetCord.GuildWelcomeScreen _original;
    public DiscordGuildWelcomeScreen(NetCord.GuildWelcomeScreen original)
    {
        _original = original;
    }
    public NetCord.GuildWelcomeScreen Original => _original;
    public string Description => _original.Description;
    public ImmutableDictionary<ulong, IDiscordGuildWelcomeScreenChannel> WelcomeChannels => _original.WelcomeChannels.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordGuildWelcomeScreenChannel)new DiscordGuildWelcomeScreenChannel(kv.Value));
}


public class DiscordGuildSticker : IDiscordGuildSticker
{
    private readonly NetCord.GuildSticker _original;
    public DiscordGuildSticker(NetCord.GuildSticker original)
    {
        _original = original;
    }
    public NetCord.GuildSticker Original => _original;
    public bool? Available => _original.Available;
    public ulong GuildId => _original.GuildId;
    public IDiscordUser Creator => new DiscordUser(_original.Creator);
    public ulong Id => _original.Id;
    public string Name => _original.Name;
    public string Description => _original.Description;
    public IReadOnlyList<string> Tags => _original.Tags;
    public NetCord.StickerFormat Format => _original.Format;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordGuildSticker> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildSticker(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordGuildSticker> ModifyAsync(Action<IDiscordGuildStickerOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildSticker(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAsync(properties.Original, cancellationToken);
    public IDiscordImageUrl GetImageUrl(NetCord.ImageFormat format) => new DiscordImageUrl(_original.GetImageUrl(format));
}


public class DiscordPartialGuildUser : IDiscordPartialGuildUser
{
    private readonly NetCord.PartialGuildUser _original;
    public DiscordPartialGuildUser(NetCord.PartialGuildUser original)
    {
        _original = original;
    }
    public NetCord.PartialGuildUser Original => _original;
    public string Nickname => _original.Nickname;
    public string GuildAvatarHash => _original.GuildAvatarHash;
    public string GuildBannerHash => _original.GuildBannerHash;
    public IReadOnlyList<ulong> RoleIds => _original.RoleIds;
    public ulong? HoistedRoleId => _original.HoistedRoleId;
    public System.DateTimeOffset JoinedAt => _original.JoinedAt;
    public System.DateTimeOffset? GuildBoostStart => _original.GuildBoostStart;
    public bool Deafened => _original.Deafened;
    public bool Muted => _original.Muted;
    public NetCord.GuildUserFlags GuildFlags => _original.GuildFlags;
    public bool? IsPending => _original.IsPending;
    public System.DateTimeOffset? TimeOutUntil => _original.TimeOutUntil;
    public IDiscordAvatarDecorationData GuildAvatarDecorationData => new DiscordAvatarDecorationData(_original.GuildAvatarDecorationData);
    public bool HasGuildAvatar => _original.HasGuildAvatar;
    public bool HasGuildBanner => _original.HasGuildBanner;
    public bool HasGuildAvatarDecoration => _original.HasGuildAvatarDecoration;
    public ulong Id => _original.Id;
    public string Username => _original.Username;
    public ushort Discriminator => _original.Discriminator;
    public string GlobalName => _original.GlobalName;
    public string AvatarHash => _original.AvatarHash;
    public bool IsBot => _original.IsBot;
    public bool? IsSystemUser => _original.IsSystemUser;
    public bool? MfaEnabled => _original.MfaEnabled;
    public string BannerHash => _original.BannerHash;
    public NetCord.Color? AccentColor => _original.AccentColor;
    public string Locale => _original.Locale;
    public bool? Verified => _original.Verified;
    public string Email => _original.Email;
    public NetCord.UserFlags? Flags => _original.Flags;
    public NetCord.PremiumType? PremiumType => _original.PremiumType;
    public NetCord.UserFlags? PublicFlags => _original.PublicFlags;
    public IDiscordAvatarDecorationData AvatarDecorationData => new DiscordAvatarDecorationData(_original.AvatarDecorationData);
    public bool HasAvatar => _original.HasAvatar;
    public bool HasBanner => _original.HasBanner;
    public bool HasAvatarDecoration => _original.HasAvatarDecoration;
    public IDiscordImageUrl DefaultAvatarUrl => new DiscordImageUrl(_original.DefaultAvatarUrl);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordImageUrl GetGuildAvatarDecorationUrl() => new DiscordImageUrl(_original.GetGuildAvatarDecorationUrl());
    public IDiscordImageUrl GetAvatarUrl(NetCord.ImageFormat? format = default) => new DiscordImageUrl(_original.GetAvatarUrl(format));
    public IDiscordImageUrl GetBannerUrl(NetCord.ImageFormat? format = default) => new DiscordImageUrl(_original.GetBannerUrl(format));
    public IDiscordImageUrl GetAvatarDecorationUrl() => new DiscordImageUrl(_original.GetAvatarDecorationUrl());
    public async Task<IDiscordUser> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordUser(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordDMChannel> GetDMChannelAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordDMChannel(await _original.GetDMChannelAsync(properties.Original, cancellationToken));
}


public class DiscordImageUrl : IDiscordImageUrl
{
    private readonly NetCord.ImageUrl _original;
    public DiscordImageUrl(NetCord.ImageUrl original)
    {
        _original = original;
    }
    public NetCord.ImageUrl Original => _original;
}


public class DiscordRestAuditLogEntry : IDiscordRestAuditLogEntry
{
    private readonly NetCord.Rest.RestAuditLogEntry _original;
    public DiscordRestAuditLogEntry(NetCord.Rest.RestAuditLogEntry original)
    {
        _original = original;
    }
    public NetCord.Rest.RestAuditLogEntry Original => _original;
    public IDiscordRestAuditLogEntryData Data => new DiscordRestAuditLogEntryData(_original.Data);
    public IDiscordUser User => new DiscordUser(_original.User);
    public ulong Id => _original.Id;
    public ulong? TargetId => _original.TargetId;
    public IReadOnlyDictionary<string, IDiscordAuditLogChange> Changes => _original.Changes.ToDictionary(kv => kv.Key, kv => (IDiscordAuditLogChange)new DiscordAuditLogChange(kv.Value));
    public ulong? UserId => _original.UserId;
    public NetCord.AuditLogEvent ActionType => _original.ActionType;
    public IDiscordAuditLogEntryInfo Options => new DiscordAuditLogEntryInfo(_original.Options);
    public string Reason => _original.Reason;
    public ulong GuildId => _original.GuildId;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public bool TryGetChange<TObject, TValue>(Expression<Func<TObject, TValue>> expression, IDiscordAuditLogChange`1& change) => _original.TryGetChange(expression, change.Original);
    public IDiscordAuditLogChange<TValue> GetChange<TObject, TValue>(Expression<Func<TObject, TValue>> expression) => new DiscordAuditLogChange<TValue>(_original.GetChange(expression));
    public bool TryGetChange<TObject, TValue>(Expression<Func<TObject, TValue>> expression, JsonTypeInfo<TValue> jsonTypeInfo, IDiscordAuditLogChange`1& change) => _original.TryGetChange(expression, jsonTypeInfo, change.Original);
    public IDiscordAuditLogChange<TValue> GetChange<TObject, TValue>(Expression<Func<TObject, TValue>> expression, JsonTypeInfo<TValue> jsonTypeInfo) => new DiscordAuditLogChange<TValue>(_original.GetChange(expression, jsonTypeInfo));
}


public class DiscordGuildAuditLogPaginationProperties : IDiscordGuildAuditLogPaginationProperties
{
    private readonly NetCord.Rest.GuildAuditLogPaginationProperties _original;
    public DiscordGuildAuditLogPaginationProperties(NetCord.Rest.GuildAuditLogPaginationProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildAuditLogPaginationProperties Original => _original;
    public ulong? UserId => _original.UserId;
    public NetCord.AuditLogEvent? ActionType => _original.ActionType;
    public ulong? From => _original.From;
    public NetCord.Rest.PaginationDirection? Direction => _original.Direction;
    public int? BatchSize => _original.BatchSize;
    public IDiscordGuildAuditLogPaginationProperties WithUserId(ulong? userId) => new DiscordGuildAuditLogPaginationProperties(_original.WithUserId(userId));
    public IDiscordGuildAuditLogPaginationProperties WithActionType(NetCord.AuditLogEvent? actionType) => new DiscordGuildAuditLogPaginationProperties(_original.WithActionType(actionType));
    public IDiscordGuildAuditLogPaginationProperties WithFrom(ulong? from) => new DiscordGuildAuditLogPaginationProperties(_original.WithFrom(from));
    public IDiscordGuildAuditLogPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction) => new DiscordGuildAuditLogPaginationProperties(_original.WithDirection(direction));
    public IDiscordGuildAuditLogPaginationProperties WithBatchSize(int? batchSize) => new DiscordGuildAuditLogPaginationProperties(_original.WithBatchSize(batchSize));
}


public class DiscordAutoModerationRule : IDiscordAutoModerationRule
{
    private readonly NetCord.AutoModerationRule _original;
    public DiscordAutoModerationRule(NetCord.AutoModerationRule original)
    {
        _original = original;
    }
    public NetCord.AutoModerationRule Original => _original;
    public ulong Id => _original.Id;
    public ulong GuildId => _original.GuildId;
    public string Name => _original.Name;
    public ulong CreatorId => _original.CreatorId;
    public NetCord.AutoModerationRuleEventType EventType => _original.EventType;
    public NetCord.AutoModerationRuleTriggerType TriggerType => _original.TriggerType;
    public IDiscordAutoModerationRuleTriggerMetadata TriggerMetadata => new DiscordAutoModerationRuleTriggerMetadata(_original.TriggerMetadata);
    public IReadOnlyList<IDiscordAutoModerationAction> Actions => _original.Actions.Select(x => new DiscordAutoModerationAction(x)).ToList();
    public bool Enabled => _original.Enabled;
    public IReadOnlyList<ulong> ExemptRoles => _original.ExemptRoles;
    public IReadOnlyList<ulong> ExemptChannels => _original.ExemptChannels;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordAutoModerationRule> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordAutoModerationRule(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordAutoModerationRule> ModifyAsync(Action<IDiscordAutoModerationRuleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordAutoModerationRule(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAsync(properties.Original, cancellationToken);
}


public class DiscordAutoModerationRuleProperties : IDiscordAutoModerationRuleProperties
{
    private readonly NetCord.AutoModerationRuleProperties _original;
    public DiscordAutoModerationRuleProperties(NetCord.AutoModerationRuleProperties original)
    {
        _original = original;
    }
    public NetCord.AutoModerationRuleProperties Original => _original;
    public string Name => _original.Name;
    public NetCord.AutoModerationRuleEventType EventType => _original.EventType;
    public NetCord.AutoModerationRuleTriggerType TriggerType => _original.TriggerType;
    public IDiscordAutoModerationRuleTriggerMetadataProperties TriggerMetadata => new DiscordAutoModerationRuleTriggerMetadataProperties(_original.TriggerMetadata);
    public IEnumerable<IDiscordAutoModerationActionProperties> Actions => _original.Actions.Select(x => new DiscordAutoModerationActionProperties(x));
    public bool Enabled => _original.Enabled;
    public IEnumerable<ulong> ExemptRoles => _original.ExemptRoles;
    public IEnumerable<ulong> ExemptChannels => _original.ExemptChannels;
    public IDiscordAutoModerationRuleProperties WithName(string name) => new DiscordAutoModerationRuleProperties(_original.WithName(name));
    public IDiscordAutoModerationRuleProperties WithEventType(NetCord.AutoModerationRuleEventType eventType) => new DiscordAutoModerationRuleProperties(_original.WithEventType(eventType));
    public IDiscordAutoModerationRuleProperties WithTriggerType(NetCord.AutoModerationRuleTriggerType triggerType) => new DiscordAutoModerationRuleProperties(_original.WithTriggerType(triggerType));
    public IDiscordAutoModerationRuleProperties WithTriggerMetadata(IDiscordAutoModerationRuleTriggerMetadataProperties triggerMetadata) => new DiscordAutoModerationRuleProperties(_original.WithTriggerMetadata(triggerMetadata.Original));
    public IDiscordAutoModerationRuleProperties WithActions(IEnumerable<IDiscordAutoModerationActionProperties> actions) => new DiscordAutoModerationRuleProperties(_original.WithActions(actions?.Select(x => x.Original)));
    public IDiscordAutoModerationRuleProperties AddActions(IEnumerable<IDiscordAutoModerationActionProperties> actions) => new DiscordAutoModerationRuleProperties(_original.AddActions(actions?.Select(x => x.Original)));
    public IDiscordAutoModerationRuleProperties AddActions(IDiscordAutoModerationActionProperties[] actions) => new DiscordAutoModerationRuleProperties(_original.AddActions(actions.Original));
    public IDiscordAutoModerationRuleProperties WithEnabled(bool enabled = true) => new DiscordAutoModerationRuleProperties(_original.WithEnabled(enabled));
    public IDiscordAutoModerationRuleProperties WithExemptRoles(IEnumerable<ulong> exemptRoles) => new DiscordAutoModerationRuleProperties(_original.WithExemptRoles(exemptRoles));
    public IDiscordAutoModerationRuleProperties AddExemptRoles(IEnumerable<ulong> exemptRoles) => new DiscordAutoModerationRuleProperties(_original.AddExemptRoles(exemptRoles));
    public IDiscordAutoModerationRuleProperties AddExemptRoles(ulong[] exemptRoles) => new DiscordAutoModerationRuleProperties(_original.AddExemptRoles(exemptRoles));
    public IDiscordAutoModerationRuleProperties WithExemptChannels(IEnumerable<ulong> exemptChannels) => new DiscordAutoModerationRuleProperties(_original.WithExemptChannels(exemptChannels));
    public IDiscordAutoModerationRuleProperties AddExemptChannels(IEnumerable<ulong> exemptChannels) => new DiscordAutoModerationRuleProperties(_original.AddExemptChannels(exemptChannels));
    public IDiscordAutoModerationRuleProperties AddExemptChannels(ulong[] exemptChannels) => new DiscordAutoModerationRuleProperties(_original.AddExemptChannels(exemptChannels));
}


public class DiscordAutoModerationRuleOptions : IDiscordAutoModerationRuleOptions
{
    private readonly NetCord.AutoModerationRuleOptions _original;
    public DiscordAutoModerationRuleOptions(NetCord.AutoModerationRuleOptions original)
    {
        _original = original;
    }
    public NetCord.AutoModerationRuleOptions Original => _original;
    public string Name => _original.Name;
    public NetCord.AutoModerationRuleEventType? EventType => _original.EventType;
    public IDiscordAutoModerationRuleTriggerMetadataProperties TriggerMetadata => new DiscordAutoModerationRuleTriggerMetadataProperties(_original.TriggerMetadata);
    public IEnumerable<IDiscordAutoModerationActionProperties> Actions => _original.Actions.Select(x => new DiscordAutoModerationActionProperties(x));
    public bool? Enabled => _original.Enabled;
    public IEnumerable<ulong> ExemptRoles => _original.ExemptRoles;
    public IEnumerable<ulong> ExemptChannels => _original.ExemptChannels;
    public IDiscordAutoModerationRuleOptions WithName(string name) => new DiscordAutoModerationRuleOptions(_original.WithName(name));
    public IDiscordAutoModerationRuleOptions WithEventType(NetCord.AutoModerationRuleEventType? eventType) => new DiscordAutoModerationRuleOptions(_original.WithEventType(eventType));
    public IDiscordAutoModerationRuleOptions WithTriggerMetadata(IDiscordAutoModerationRuleTriggerMetadataProperties triggerMetadata) => new DiscordAutoModerationRuleOptions(_original.WithTriggerMetadata(triggerMetadata.Original));
    public IDiscordAutoModerationRuleOptions WithActions(IEnumerable<IDiscordAutoModerationActionProperties> actions) => new DiscordAutoModerationRuleOptions(_original.WithActions(actions?.Select(x => x.Original)));
    public IDiscordAutoModerationRuleOptions AddActions(IEnumerable<IDiscordAutoModerationActionProperties> actions) => new DiscordAutoModerationRuleOptions(_original.AddActions(actions?.Select(x => x.Original)));
    public IDiscordAutoModerationRuleOptions AddActions(IDiscordAutoModerationActionProperties[] actions) => new DiscordAutoModerationRuleOptions(_original.AddActions(actions.Original));
    public IDiscordAutoModerationRuleOptions WithEnabled(bool? enabled = true) => new DiscordAutoModerationRuleOptions(_original.WithEnabled(enabled));
    public IDiscordAutoModerationRuleOptions WithExemptRoles(IEnumerable<ulong> exemptRoles) => new DiscordAutoModerationRuleOptions(_original.WithExemptRoles(exemptRoles));
    public IDiscordAutoModerationRuleOptions AddExemptRoles(IEnumerable<ulong> exemptRoles) => new DiscordAutoModerationRuleOptions(_original.AddExemptRoles(exemptRoles));
    public IDiscordAutoModerationRuleOptions AddExemptRoles(ulong[] exemptRoles) => new DiscordAutoModerationRuleOptions(_original.AddExemptRoles(exemptRoles));
    public IDiscordAutoModerationRuleOptions WithExemptChannels(IEnumerable<ulong> exemptChannels) => new DiscordAutoModerationRuleOptions(_original.WithExemptChannels(exemptChannels));
    public IDiscordAutoModerationRuleOptions AddExemptChannels(IEnumerable<ulong> exemptChannels) => new DiscordAutoModerationRuleOptions(_original.AddExemptChannels(exemptChannels));
    public IDiscordAutoModerationRuleOptions AddExemptChannels(ulong[] exemptChannels) => new DiscordAutoModerationRuleOptions(_original.AddExemptChannels(exemptChannels));
}


public class DiscordGuildEmojiProperties : IDiscordGuildEmojiProperties
{
    private readonly NetCord.Rest.GuildEmojiProperties _original;
    public DiscordGuildEmojiProperties(NetCord.Rest.GuildEmojiProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildEmojiProperties Original => _original;
    public string Name => _original.Name;
    public NetCord.Rest.ImageProperties Image => _original.Image;
    public IEnumerable<ulong> AllowedRoles => _original.AllowedRoles;
    public IDiscordGuildEmojiProperties WithName(string name) => new DiscordGuildEmojiProperties(_original.WithName(name));
    public IDiscordGuildEmojiProperties WithImage(NetCord.Rest.ImageProperties image) => new DiscordGuildEmojiProperties(_original.WithImage(image));
    public IDiscordGuildEmojiProperties WithAllowedRoles(IEnumerable<ulong> allowedRoles) => new DiscordGuildEmojiProperties(_original.WithAllowedRoles(allowedRoles));
    public IDiscordGuildEmojiProperties AddAllowedRoles(IEnumerable<ulong> allowedRoles) => new DiscordGuildEmojiProperties(_original.AddAllowedRoles(allowedRoles));
    public IDiscordGuildEmojiProperties AddAllowedRoles(ulong[] allowedRoles) => new DiscordGuildEmojiProperties(_original.AddAllowedRoles(allowedRoles));
}


public class DiscordGuildEmojiOptions : IDiscordGuildEmojiOptions
{
    private readonly NetCord.Rest.GuildEmojiOptions _original;
    public DiscordGuildEmojiOptions(NetCord.Rest.GuildEmojiOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildEmojiOptions Original => _original;
    public string Name => _original.Name;
    public IEnumerable<ulong> AllowedRoles => _original.AllowedRoles;
    public IDiscordGuildEmojiOptions WithName(string name) => new DiscordGuildEmojiOptions(_original.WithName(name));
    public IDiscordGuildEmojiOptions WithAllowedRoles(IEnumerable<ulong> allowedRoles) => new DiscordGuildEmojiOptions(_original.WithAllowedRoles(allowedRoles));
    public IDiscordGuildEmojiOptions AddAllowedRoles(IEnumerable<ulong> allowedRoles) => new DiscordGuildEmojiOptions(_original.AddAllowedRoles(allowedRoles));
    public IDiscordGuildEmojiOptions AddAllowedRoles(ulong[] allowedRoles) => new DiscordGuildEmojiOptions(_original.AddAllowedRoles(allowedRoles));
}


public class DiscordRestGuild : IDiscordRestGuild
{
    private readonly NetCord.Rest.RestGuild _original;
    public DiscordRestGuild(NetCord.Rest.RestGuild original)
    {
        _original = original;
    }
    public NetCord.Rest.RestGuild Original => _original;
    public ulong Id => _original.Id;
    public string Name => _original.Name;
    public bool HasIcon => _original.HasIcon;
    public string IconHash => _original.IconHash;
    public bool HasSplash => _original.HasSplash;
    public string SplashHash => _original.SplashHash;
    public bool HasDiscoverySplash => _original.HasDiscoverySplash;
    public string DiscoverySplashHash => _original.DiscoverySplashHash;
    public bool IsOwner => _original.IsOwner;
    public ulong OwnerId => _original.OwnerId;
    public NetCord.Permissions? Permissions => _original.Permissions;
    public ulong? AfkChannelId => _original.AfkChannelId;
    public int AfkTimeout => _original.AfkTimeout;
    public bool? WidgetEnabled => _original.WidgetEnabled;
    public ulong? WidgetChannelId => _original.WidgetChannelId;
    public NetCord.VerificationLevel VerificationLevel => _original.VerificationLevel;
    public NetCord.DefaultMessageNotificationLevel DefaultMessageNotificationLevel => _original.DefaultMessageNotificationLevel;
    public NetCord.ContentFilter ContentFilter => _original.ContentFilter;
    public ImmutableDictionary<ulong, IDiscordRole> Roles => _original.Roles.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordRole)new DiscordRole(kv.Value));
    public ImmutableDictionary<ulong, IDiscordGuildEmoji> Emojis => _original.Emojis.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordGuildEmoji)new DiscordGuildEmoji(kv.Value));
    public IReadOnlyList<string> Features => _original.Features;
    public NetCord.MfaLevel MfaLevel => _original.MfaLevel;
    public ulong? ApplicationId => _original.ApplicationId;
    public ulong? SystemChannelId => _original.SystemChannelId;
    public NetCord.Rest.SystemChannelFlags SystemChannelFlags => _original.SystemChannelFlags;
    public ulong? RulesChannelId => _original.RulesChannelId;
    public int? MaxPresences => _original.MaxPresences;
    public int? MaxUsers => _original.MaxUsers;
    public string VanityUrlCode => _original.VanityUrlCode;
    public string Description => _original.Description;
    public bool HasBanner => _original.HasBanner;
    public string BannerHash => _original.BannerHash;
    public int PremiumTier => _original.PremiumTier;
    public int? PremiumSubscriptionCount => _original.PremiumSubscriptionCount;
    public string PreferredLocale => _original.PreferredLocale;
    public ulong? PublicUpdatesChannelId => _original.PublicUpdatesChannelId;
    public int? MaxVideoChannelUsers => _original.MaxVideoChannelUsers;
    public int? MaxStageVideoChannelUsers => _original.MaxStageVideoChannelUsers;
    public int? ApproximateUserCount => _original.ApproximateUserCount;
    public int? ApproximatePresenceCount => _original.ApproximatePresenceCount;
    public IDiscordGuildWelcomeScreen WelcomeScreen => new DiscordGuildWelcomeScreen(_original.WelcomeScreen);
    public NetCord.NsfwLevel NsfwLevel => _original.NsfwLevel;
    public ImmutableDictionary<ulong, IDiscordGuildSticker> Stickers => _original.Stickers.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordGuildSticker)new DiscordGuildSticker(kv.Value));
    public bool PremiumProgressBarEnabled => _original.PremiumProgressBarEnabled;
    public ulong? SafetyAlertsChannelId => _original.SafetyAlertsChannelId;
    public IDiscordRole EveryoneRole => new DiscordRole(_original.EveryoneRole);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public int Compare(IDiscordPartialGuildUser x, IDiscordPartialGuildUser y) => _original.Compare(x.Original, y.Original);
    public IDiscordImageUrl GetIconUrl(NetCord.ImageFormat? format = default) => new DiscordImageUrl(_original.GetIconUrl(format));
    public IDiscordImageUrl GetSplashUrl(NetCord.ImageFormat format) => new DiscordImageUrl(_original.GetSplashUrl(format));
    public IDiscordImageUrl GetDiscoverySplashUrl(NetCord.ImageFormat format) => new DiscordImageUrl(_original.GetDiscoverySplashUrl(format));
    public IDiscordImageUrl GetBannerUrl(NetCord.ImageFormat? format = default) => new DiscordImageUrl(_original.GetBannerUrl(format));
    public IDiscordImageUrl GetWidgetUrl(NetCord.GuildWidgetStyle? style = default, string? hostname = null, NetCord.ApiVersion? version = default) => new DiscordImageUrl(_original.GetWidgetUrl(style, hostname, version));
    public async IAsyncEnumerable<IDiscordRestAuditLogEntry> GetAuditLogAsync(IDiscordGuildAuditLogPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetAuditLogAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordRestAuditLogEntry(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordAutoModerationRule>> GetAutoModerationRulesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetAutoModerationRulesAsync(properties.Original, cancellationToken)).Select(x => new DiscordAutoModerationRule(x)).ToList();
    public async Task<IDiscordAutoModerationRule> GetAutoModerationRuleAsync(ulong autoModerationRuleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordAutoModerationRule(await _original.GetAutoModerationRuleAsync(autoModerationRuleId, properties.Original, cancellationToken));
    public async Task<IDiscordAutoModerationRule> CreateAutoModerationRuleAsync(IDiscordAutoModerationRuleProperties autoModerationRuleProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordAutoModerationRule(await _original.CreateAutoModerationRuleAsync(autoModerationRuleProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordAutoModerationRule> ModifyAutoModerationRuleAsync(ulong autoModerationRuleId, Action<IDiscordAutoModerationRuleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordAutoModerationRule(await _original.ModifyAutoModerationRuleAsync(autoModerationRuleId, action, properties.Original, cancellationToken));
    public Task DeleteAutoModerationRuleAsync(ulong autoModerationRuleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAutoModerationRuleAsync(autoModerationRuleId, properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordGuildEmoji>> GetEmojisAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetEmojisAsync(properties.Original, cancellationToken)).Select(x => new DiscordGuildEmoji(x)).ToList();
    public async Task<IDiscordGuildEmoji> GetEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildEmoji(await _original.GetEmojiAsync(emojiId, properties.Original, cancellationToken));
    public async Task<IDiscordGuildEmoji> CreateEmojiAsync(IDiscordGuildEmojiProperties guildEmojiProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildEmoji(await _original.CreateEmojiAsync(guildEmojiProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordGuildEmoji> ModifyEmojiAsync(ulong emojiId, Action<IDiscordGuildEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildEmoji(await _original.ModifyEmojiAsync(emojiId, action, properties.Original, cancellationToken));
    public Task DeleteEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteEmojiAsync(emojiId, properties.Original, cancellationToken);
    public async Task<IDiscordRestGuild> GetAsync(bool withCounts = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestGuild(await _original.GetAsync(withCounts, properties.Original, cancellationToken));
    public async Task<IDiscordGuildPreview> GetPreviewAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildPreview(await _original.GetPreviewAsync(properties.Original, cancellationToken));
    public async Task<IDiscordRestGuild> ModifyAsync(Action<IDiscordGuildOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestGuild(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAsync(properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordGuildChannel>> GetChannelsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetChannelsAsync(properties.Original, cancellationToken)).Select(x => new DiscordGuildChannel(x)).ToList();
    public async Task<IDiscordGuildChannel> CreateChannelAsync(IDiscordGuildChannelProperties channelProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildChannel(await _original.CreateChannelAsync(channelProperties.Original, properties.Original, cancellationToken));
    public Task ModifyChannelPositionsAsync(IEnumerable<IDiscordGuildChannelPositionProperties> positions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.ModifyChannelPositionsAsync(positions?.Select(x => x.Original), properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordGuildThread>> GetActiveThreadsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetActiveThreadsAsync(properties.Original, cancellationToken)).Select(x => new DiscordGuildThread(x)).ToList();
    public async Task<IDiscordGuildUser> GetUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildUser(await _original.GetUserAsync(userId, properties.Original, cancellationToken));
    public async IAsyncEnumerable<IDiscordGuildUser> GetUsersAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetUsersAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordGuildUser(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordGuildUser>> FindUserAsync(string name, int limit, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.FindUserAsync(name, limit, properties.Original, cancellationToken)).Select(x => new DiscordGuildUser(x)).ToList();
    public async Task<IDiscordGuildUser> AddUserAsync(ulong userId, IDiscordGuildUserProperties userProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildUser(await _original.AddUserAsync(userId, userProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordGuildUser> ModifyUserAsync(ulong userId, Action<IDiscordGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildUser(await _original.ModifyUserAsync(userId, action, properties.Original, cancellationToken));
    public async Task<IDiscordGuildUser> ModifyCurrentUserAsync(Action<IDiscordCurrentGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildUser(await _original.ModifyCurrentUserAsync(action, properties.Original, cancellationToken));
    public Task AddUserRoleAsync(ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.AddUserRoleAsync(userId, roleId, properties.Original, cancellationToken);
    public Task RemoveUserRoleAsync(ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.RemoveUserRoleAsync(userId, roleId, properties.Original, cancellationToken);
    public Task KickUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.KickUserAsync(userId, properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordGuildBan> GetBansAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetBansAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordGuildBan(original);
        }
    }
    public async Task<IDiscordGuildBan> GetBanAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildBan(await _original.GetBanAsync(userId, properties.Original, cancellationToken));
    public Task BanUserAsync(ulong userId, int deleteMessageSeconds = 0, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.BanUserAsync(userId, deleteMessageSeconds, properties.Original, cancellationToken);
    public async Task<IDiscordGuildBulkBan> BanUsersAsync(IEnumerable<ulong> userIds, int deleteMessageSeconds = 0, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildBulkBan(await _original.BanUsersAsync(userIds, deleteMessageSeconds, properties.Original, cancellationToken));
    public Task UnbanUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.UnbanUserAsync(userId, properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordRole>> GetRolesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetRolesAsync(properties.Original, cancellationToken)).Select(x => new DiscordRole(x)).ToList();
    public async Task<IDiscordRole> GetRoleAsync(ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRole(await _original.GetRoleAsync(roleId, properties.Original, cancellationToken));
    public async Task<IDiscordRole> CreateRoleAsync(IDiscordRoleProperties guildRoleProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRole(await _original.CreateRoleAsync(guildRoleProperties.Original, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordRole>> ModifyRolePositionsAsync(IEnumerable<IDiscordRolePositionProperties> positions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.ModifyRolePositionsAsync(positions?.Select(x => x.Original), properties.Original, cancellationToken)).Select(x => new DiscordRole(x)).ToList();
    public async Task<IDiscordRole> ModifyRoleAsync(ulong roleId, Action<IDiscordRoleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRole(await _original.ModifyRoleAsync(roleId, action, properties.Original, cancellationToken));
    public Task DeleteRoleAsync(ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteRoleAsync(roleId, properties.Original, cancellationToken);
    public Task<NetCord.MfaLevel> ModifyMfaLevelAsync(NetCord.MfaLevel mfaLevel, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.ModifyMfaLevelAsync(mfaLevel, properties.Original, cancellationToken);
    public Task<int> GetPruneCountAsync(int days, IEnumerable<ulong>? roles = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.GetPruneCountAsync(days, roles, properties.Original, cancellationToken);
    public Task<int?> PruneAsync(IDiscordGuildPruneProperties pruneProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.PruneAsync(pruneProperties.Original, properties.Original, cancellationToken);
    public async Task<IEnumerable<IDiscordVoiceRegion>> GetVoiceRegionsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetVoiceRegionsAsync(properties.Original, cancellationToken)).Select(x => new DiscordVoiceRegion(x));
    public async Task<IEnumerable<IDiscordRestInvite>> GetInvitesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetInvitesAsync(properties.Original, cancellationToken)).Select(x => new DiscordRestInvite(x));
    public async Task<IReadOnlyList<IDiscordIntegration>> GetIntegrationsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetIntegrationsAsync(properties.Original, cancellationToken)).Select(x => new DiscordIntegration(x)).ToList();
    public Task DeleteIntegrationAsync(ulong integrationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteIntegrationAsync(integrationId, properties.Original, cancellationToken);
    public async Task<IDiscordGuildWidgetSettings> GetWidgetSettingsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildWidgetSettings(await _original.GetWidgetSettingsAsync(properties.Original, cancellationToken));
    public async Task<IDiscordGuildWidgetSettings> ModifyWidgetSettingsAsync(Action<IDiscordGuildWidgetSettingsOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildWidgetSettings(await _original.ModifyWidgetSettingsAsync(action, properties.Original, cancellationToken));
    public async Task<IDiscordGuildWidget> GetWidgetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildWidget(await _original.GetWidgetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordGuildVanityInvite> GetVanityInviteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildVanityInvite(await _original.GetVanityInviteAsync(properties.Original, cancellationToken));
    public async Task<IDiscordGuildWelcomeScreen> GetWelcomeScreenAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildWelcomeScreen(await _original.GetWelcomeScreenAsync(properties.Original, cancellationToken));
    public async Task<IDiscordGuildWelcomeScreen> ModifyWelcomeScreenAsync(Action<IDiscordGuildWelcomeScreenOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildWelcomeScreen(await _original.ModifyWelcomeScreenAsync(action, properties.Original, cancellationToken));
    public async Task<IDiscordGuildOnboarding> GetOnboardingAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildOnboarding(await _original.GetOnboardingAsync(properties.Original, cancellationToken));
    public async Task<IDiscordGuildOnboarding> ModifyOnboardingAsync(Action<IDiscordGuildOnboardingOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildOnboarding(await _original.ModifyOnboardingAsync(action, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordGuildScheduledEvent>> GetScheduledEventsAsync(bool withUserCount = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetScheduledEventsAsync(withUserCount, properties.Original, cancellationToken)).Select(x => new DiscordGuildScheduledEvent(x)).ToList();
    public async Task<IDiscordGuildScheduledEvent> CreateScheduledEventAsync(IDiscordGuildScheduledEventProperties guildScheduledEventProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildScheduledEvent(await _original.CreateScheduledEventAsync(guildScheduledEventProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordGuildScheduledEvent> GetScheduledEventAsync(ulong scheduledEventId, bool withUserCount = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildScheduledEvent(await _original.GetScheduledEventAsync(scheduledEventId, withUserCount, properties.Original, cancellationToken));
    public async Task<IDiscordGuildScheduledEvent> ModifyScheduledEventAsync(ulong scheduledEventId, Action<IDiscordGuildScheduledEventOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildScheduledEvent(await _original.ModifyScheduledEventAsync(scheduledEventId, action, properties.Original, cancellationToken));
    public Task DeleteScheduledEventAsync(ulong scheduledEventId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteScheduledEventAsync(scheduledEventId, properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordGuildScheduledEventUser> GetScheduledEventUsersAsync(ulong scheduledEventId, IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetScheduledEventUsersAsync(scheduledEventId, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordGuildScheduledEventUser(original);
        }
    }
    public async Task<IEnumerable<IDiscordGuildTemplate>> GetTemplatesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetTemplatesAsync(properties.Original, cancellationToken)).Select(x => new DiscordGuildTemplate(x));
    public async Task<IDiscordGuildTemplate> CreateTemplateAsync(IDiscordGuildTemplateProperties guildTemplateProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildTemplate(await _original.CreateTemplateAsync(guildTemplateProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordGuildTemplate> SyncTemplateAsync(string templateCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildTemplate(await _original.SyncTemplateAsync(templateCode, properties.Original, cancellationToken));
    public async Task<IDiscordGuildTemplate> ModifyTemplateAsync(string templateCode, Action<IDiscordGuildTemplateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildTemplate(await _original.ModifyTemplateAsync(templateCode, action, properties.Original, cancellationToken));
    public async Task<IDiscordGuildTemplate> DeleteTemplateAsync(string templateCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildTemplate(await _original.DeleteTemplateAsync(templateCode, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordGuildApplicationCommand>> GetApplicationCommandsAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetApplicationCommandsAsync(applicationId, properties.Original, cancellationToken)).Select(x => new DiscordGuildApplicationCommand(x)).ToList();
    public async Task<IDiscordGuildApplicationCommand> CreateApplicationCommandAsync(ulong applicationId, IDiscordApplicationCommandProperties applicationCommandProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildApplicationCommand(await _original.CreateApplicationCommandAsync(applicationId, applicationCommandProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordGuildApplicationCommand> GetApplicationCommandAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildApplicationCommand(await _original.GetApplicationCommandAsync(applicationId, commandId, properties.Original, cancellationToken));
    public async Task<IDiscordGuildApplicationCommand> ModifyApplicationCommandAsync(ulong applicationId, ulong commandId, Action<IDiscordApplicationCommandOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildApplicationCommand(await _original.ModifyApplicationCommandAsync(applicationId, commandId, action, properties.Original, cancellationToken));
    public Task DeleteApplicationCommandAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteApplicationCommandAsync(applicationId, commandId, properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordGuildApplicationCommand>> BulkOverwriteApplicationCommandsAsync(ulong applicationId, IEnumerable<IDiscordApplicationCommandProperties> commands, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.BulkOverwriteApplicationCommandsAsync(applicationId, commands?.Select(x => x.Original), properties.Original, cancellationToken)).Select(x => new DiscordGuildApplicationCommand(x)).ToList();
    public async Task<IReadOnlyList<IDiscordApplicationCommandGuildPermissions>> GetApplicationCommandsPermissionsAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetApplicationCommandsPermissionsAsync(applicationId, properties.Original, cancellationToken)).Select(x => new DiscordApplicationCommandGuildPermissions(x)).ToList();
    public async Task<IDiscordApplicationCommandGuildPermissions> GetApplicationCommandPermissionsAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationCommandGuildPermissions(await _original.GetApplicationCommandPermissionsAsync(applicationId, commandId, properties.Original, cancellationToken));
    public async Task<IDiscordApplicationCommandGuildPermissions> OverwriteApplicationCommandPermissionsAsync(ulong applicationId, ulong commandId, IEnumerable<IDiscordApplicationCommandGuildPermissionProperties> newPermissions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationCommandGuildPermissions(await _original.OverwriteApplicationCommandPermissionsAsync(applicationId, commandId, newPermissions?.Select(x => x.Original), properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordGuildSticker>> GetStickersAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetStickersAsync(properties.Original, cancellationToken)).Select(x => new DiscordGuildSticker(x)).ToList();
    public async Task<IDiscordGuildSticker> GetStickerAsync(ulong stickerId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildSticker(await _original.GetStickerAsync(stickerId, properties.Original, cancellationToken));
    public async Task<IDiscordGuildSticker> CreateStickerAsync(IDiscordGuildStickerProperties sticker, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildSticker(await _original.CreateStickerAsync(sticker.Original, properties.Original, cancellationToken));
    public async Task<IDiscordGuildSticker> ModifyStickerAsync(ulong stickerId, Action<IDiscordGuildStickerOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildSticker(await _original.ModifyStickerAsync(stickerId, action, properties.Original, cancellationToken));
    public Task DeleteStickerAsync(ulong stickerId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteStickerAsync(stickerId, properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordGuildUserInfo> SearchUsersAsync(IDiscordGuildUsersSearchPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.SearchUsersAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordGuildUserInfo(original);
        }
    }
    public async Task<IDiscordGuildUser> GetCurrentUserGuildUserAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildUser(await _original.GetCurrentUserGuildUserAsync(properties.Original, cancellationToken));
    public Task LeaveAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.LeaveAsync(properties.Original, cancellationToken);
    public async Task<IDiscordVoiceState> GetCurrentUserVoiceStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordVoiceState(await _original.GetCurrentUserVoiceStateAsync(properties.Original, cancellationToken));
    public async Task<IDiscordVoiceState> GetUserVoiceStateAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordVoiceState(await _original.GetUserVoiceStateAsync(userId, properties.Original, cancellationToken));
    public Task ModifyCurrentUserVoiceStateAsync(Action<IDiscordCurrentUserVoiceStateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.ModifyCurrentUserVoiceStateAsync(action, properties.Original, cancellationToken);
    public Task ModifyUserVoiceStateAsync(ulong channelId, ulong userId, Action<IDiscordVoiceStateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.ModifyUserVoiceStateAsync(channelId, userId, action, properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordWebhook>> GetWebhooksAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetWebhooksAsync(properties.Original, cancellationToken)).Select(x => new DiscordWebhook(x)).ToList();
}


public class DiscordGuildPreview : IDiscordGuildPreview
{
    private readonly NetCord.Rest.GuildPreview _original;
    public DiscordGuildPreview(NetCord.Rest.GuildPreview original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildPreview Original => _original;
    public ulong Id => _original.Id;
    public string Name => _original.Name;
    public string IconHash => _original.IconHash;
    public string SplashHash => _original.SplashHash;
    public string DiscoverySplashHash => _original.DiscoverySplashHash;
    public ImmutableDictionary<ulong, IDiscordGuildEmoji> Emojis => _original.Emojis.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordGuildEmoji)new DiscordGuildEmoji(kv.Value));
    public IReadOnlyList<string> Features => _original.Features;
    public int ApproximateUserCount => _original.ApproximateUserCount;
    public int ApproximatePresenceCount => _original.ApproximatePresenceCount;
    public string Description => _original.Description;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordGuildOptions : IDiscordGuildOptions
{
    private readonly NetCord.Rest.GuildOptions _original;
    public DiscordGuildOptions(NetCord.Rest.GuildOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildOptions Original => _original;
    public string Name => _original.Name;
    public NetCord.VerificationLevel? VerificationLevel => _original.VerificationLevel;
    public NetCord.DefaultMessageNotificationLevel? DefaultMessageNotificationLevel => _original.DefaultMessageNotificationLevel;
    public NetCord.ContentFilter? ContentFilter => _original.ContentFilter;
    public ulong? AfkChannelId => _original.AfkChannelId;
    public int? AfkTimeout => _original.AfkTimeout;
    public NetCord.Rest.ImageProperties? Icon => _original.Icon;
    public ulong? OwnerId => _original.OwnerId;
    public NetCord.Rest.ImageProperties? Splash => _original.Splash;
    public NetCord.Rest.ImageProperties? DiscoverySplash => _original.DiscoverySplash;
    public NetCord.Rest.ImageProperties? Banner => _original.Banner;
    public ulong? SystemChannelId => _original.SystemChannelId;
    public NetCord.Rest.SystemChannelFlags? SystemChannelFlags => _original.SystemChannelFlags;
    public ulong? RulesChannelId => _original.RulesChannelId;
    public ulong? PublicUpdatesChannelId => _original.PublicUpdatesChannelId;
    public string PreferredLocale => _original.PreferredLocale;
    public IEnumerable<string> Features => _original.Features;
    public string Description => _original.Description;
    public bool? PremiumProgressBarEnabled => _original.PremiumProgressBarEnabled;
    public ulong? SafetyAlertsChannelId => _original.SafetyAlertsChannelId;
    public IDiscordGuildOptions WithName(string name) => new DiscordGuildOptions(_original.WithName(name));
    public IDiscordGuildOptions WithVerificationLevel(NetCord.VerificationLevel? verificationLevel) => new DiscordGuildOptions(_original.WithVerificationLevel(verificationLevel));
    public IDiscordGuildOptions WithDefaultMessageNotificationLevel(NetCord.DefaultMessageNotificationLevel? defaultMessageNotificationLevel) => new DiscordGuildOptions(_original.WithDefaultMessageNotificationLevel(defaultMessageNotificationLevel));
    public IDiscordGuildOptions WithContentFilter(NetCord.ContentFilter? contentFilter) => new DiscordGuildOptions(_original.WithContentFilter(contentFilter));
    public IDiscordGuildOptions WithAfkChannelId(ulong? afkChannelId) => new DiscordGuildOptions(_original.WithAfkChannelId(afkChannelId));
    public IDiscordGuildOptions WithAfkTimeout(int? afkTimeout) => new DiscordGuildOptions(_original.WithAfkTimeout(afkTimeout));
    public IDiscordGuildOptions WithIcon(NetCord.Rest.ImageProperties? icon) => new DiscordGuildOptions(_original.WithIcon(icon));
    public IDiscordGuildOptions WithOwnerId(ulong? ownerId) => new DiscordGuildOptions(_original.WithOwnerId(ownerId));
    public IDiscordGuildOptions WithSplash(NetCord.Rest.ImageProperties? splash) => new DiscordGuildOptions(_original.WithSplash(splash));
    public IDiscordGuildOptions WithDiscoverySplash(NetCord.Rest.ImageProperties? discoverySplash) => new DiscordGuildOptions(_original.WithDiscoverySplash(discoverySplash));
    public IDiscordGuildOptions WithBanner(NetCord.Rest.ImageProperties? banner) => new DiscordGuildOptions(_original.WithBanner(banner));
    public IDiscordGuildOptions WithSystemChannelId(ulong? systemChannelId) => new DiscordGuildOptions(_original.WithSystemChannelId(systemChannelId));
    public IDiscordGuildOptions WithSystemChannelFlags(NetCord.Rest.SystemChannelFlags? systemChannelFlags) => new DiscordGuildOptions(_original.WithSystemChannelFlags(systemChannelFlags));
    public IDiscordGuildOptions WithRulesChannelId(ulong? rulesChannelId) => new DiscordGuildOptions(_original.WithRulesChannelId(rulesChannelId));
    public IDiscordGuildOptions WithPublicUpdatesChannelId(ulong? publicUpdatesChannelId) => new DiscordGuildOptions(_original.WithPublicUpdatesChannelId(publicUpdatesChannelId));
    public IDiscordGuildOptions WithPreferredLocale(string preferredLocale) => new DiscordGuildOptions(_original.WithPreferredLocale(preferredLocale));
    public IDiscordGuildOptions WithFeatures(IEnumerable<string> features) => new DiscordGuildOptions(_original.WithFeatures(features));
    public IDiscordGuildOptions AddFeatures(IEnumerable<string> features) => new DiscordGuildOptions(_original.AddFeatures(features));
    public IDiscordGuildOptions AddFeatures(string[] features) => new DiscordGuildOptions(_original.AddFeatures(features));
    public IDiscordGuildOptions WithDescription(string description) => new DiscordGuildOptions(_original.WithDescription(description));
    public IDiscordGuildOptions WithPremiumProgressBarEnabled(bool? premiumProgressBarEnabled = true) => new DiscordGuildOptions(_original.WithPremiumProgressBarEnabled(premiumProgressBarEnabled));
    public IDiscordGuildOptions WithSafetyAlertsChannelId(ulong? safetyAlertsChannelId) => new DiscordGuildOptions(_original.WithSafetyAlertsChannelId(safetyAlertsChannelId));
}


public class DiscordGuildChannelProperties : IDiscordGuildChannelProperties
{
    private readonly NetCord.Rest.GuildChannelProperties _original;
    public DiscordGuildChannelProperties(NetCord.Rest.GuildChannelProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildChannelProperties Original => _original;
    public string Name => _original.Name;
    public NetCord.ChannelType Type => _original.Type;
    public string Topic => _original.Topic;
    public int? Bitrate => _original.Bitrate;
    public int? UserLimit => _original.UserLimit;
    public int? Slowmode => _original.Slowmode;
    public int? Position => _original.Position;
    public IEnumerable<IDiscordPermissionOverwriteProperties> PermissionOverwrites => _original.PermissionOverwrites.Select(x => new DiscordPermissionOverwriteProperties(x));
    public ulong? ParentId => _original.ParentId;
    public bool? Nsfw => _original.Nsfw;
    public string RtcRegion => _original.RtcRegion;
    public NetCord.VideoQualityMode? VideoQualityMode => _original.VideoQualityMode;
    public NetCord.ThreadArchiveDuration? DefaultAutoArchiveDuration => _original.DefaultAutoArchiveDuration;
    public NetCord.Rest.ForumGuildChannelDefaultReactionProperties? DefaultReactionEmoji => _original.DefaultReactionEmoji;
    public IEnumerable<IDiscordForumTagProperties> AvailableTags => _original.AvailableTags.Select(x => new DiscordForumTagProperties(x));
    public NetCord.SortOrderType? DefaultSortOrder => _original.DefaultSortOrder;
    public NetCord.ForumLayoutType? DefaultForumLayout => _original.DefaultForumLayout;
    public int? DefaultThreadSlowmode => _original.DefaultThreadSlowmode;
    public IDiscordGuildChannelProperties WithName(string name) => new DiscordGuildChannelProperties(_original.WithName(name));
    public IDiscordGuildChannelProperties WithType(NetCord.ChannelType type) => new DiscordGuildChannelProperties(_original.WithType(type));
    public IDiscordGuildChannelProperties WithTopic(string topic) => new DiscordGuildChannelProperties(_original.WithTopic(topic));
    public IDiscordGuildChannelProperties WithBitrate(int? bitrate) => new DiscordGuildChannelProperties(_original.WithBitrate(bitrate));
    public IDiscordGuildChannelProperties WithUserLimit(int? userLimit) => new DiscordGuildChannelProperties(_original.WithUserLimit(userLimit));
    public IDiscordGuildChannelProperties WithSlowmode(int? slowmode) => new DiscordGuildChannelProperties(_original.WithSlowmode(slowmode));
    public IDiscordGuildChannelProperties WithPosition(int? position) => new DiscordGuildChannelProperties(_original.WithPosition(position));
    public IDiscordGuildChannelProperties WithPermissionOverwrites(IEnumerable<IDiscordPermissionOverwriteProperties> permissionOverwrites) => new DiscordGuildChannelProperties(_original.WithPermissionOverwrites(permissionOverwrites?.Select(x => x.Original)));
    public IDiscordGuildChannelProperties AddPermissionOverwrites(IEnumerable<IDiscordPermissionOverwriteProperties> permissionOverwrites) => new DiscordGuildChannelProperties(_original.AddPermissionOverwrites(permissionOverwrites?.Select(x => x.Original)));
    public IDiscordGuildChannelProperties AddPermissionOverwrites(IDiscordPermissionOverwriteProperties[] permissionOverwrites) => new DiscordGuildChannelProperties(_original.AddPermissionOverwrites(permissionOverwrites.Original));
    public IDiscordGuildChannelProperties WithParentId(ulong? parentId) => new DiscordGuildChannelProperties(_original.WithParentId(parentId));
    public IDiscordGuildChannelProperties WithNsfw(bool? nsfw = true) => new DiscordGuildChannelProperties(_original.WithNsfw(nsfw));
    public IDiscordGuildChannelProperties WithRtcRegion(string rtcRegion) => new DiscordGuildChannelProperties(_original.WithRtcRegion(rtcRegion));
    public IDiscordGuildChannelProperties WithVideoQualityMode(NetCord.VideoQualityMode? videoQualityMode) => new DiscordGuildChannelProperties(_original.WithVideoQualityMode(videoQualityMode));
    public IDiscordGuildChannelProperties WithDefaultAutoArchiveDuration(NetCord.ThreadArchiveDuration? defaultAutoArchiveDuration) => new DiscordGuildChannelProperties(_original.WithDefaultAutoArchiveDuration(defaultAutoArchiveDuration));
    public IDiscordGuildChannelProperties WithDefaultReactionEmoji(NetCord.Rest.ForumGuildChannelDefaultReactionProperties? defaultReactionEmoji) => new DiscordGuildChannelProperties(_original.WithDefaultReactionEmoji(defaultReactionEmoji));
    public IDiscordGuildChannelProperties WithAvailableTags(IEnumerable<IDiscordForumTagProperties> availableTags) => new DiscordGuildChannelProperties(_original.WithAvailableTags(availableTags?.Select(x => x.Original)));
    public IDiscordGuildChannelProperties AddAvailableTags(IEnumerable<IDiscordForumTagProperties> availableTags) => new DiscordGuildChannelProperties(_original.AddAvailableTags(availableTags?.Select(x => x.Original)));
    public IDiscordGuildChannelProperties AddAvailableTags(IDiscordForumTagProperties[] availableTags) => new DiscordGuildChannelProperties(_original.AddAvailableTags(availableTags.Original));
    public IDiscordGuildChannelProperties WithDefaultSortOrder(NetCord.SortOrderType? defaultSortOrder) => new DiscordGuildChannelProperties(_original.WithDefaultSortOrder(defaultSortOrder));
    public IDiscordGuildChannelProperties WithDefaultForumLayout(NetCord.ForumLayoutType? defaultForumLayout) => new DiscordGuildChannelProperties(_original.WithDefaultForumLayout(defaultForumLayout));
    public IDiscordGuildChannelProperties WithDefaultThreadSlowmode(int? defaultThreadSlowmode) => new DiscordGuildChannelProperties(_original.WithDefaultThreadSlowmode(defaultThreadSlowmode));
}


public class DiscordGuildChannelPositionProperties : IDiscordGuildChannelPositionProperties
{
    private readonly NetCord.Rest.GuildChannelPositionProperties _original;
    public DiscordGuildChannelPositionProperties(NetCord.Rest.GuildChannelPositionProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildChannelPositionProperties Original => _original;
    public ulong Id => _original.Id;
    public int? Position => _original.Position;
    public bool? LockPermissions => _original.LockPermissions;
    public ulong? ParentId => _original.ParentId;
    public IDiscordGuildChannelPositionProperties WithId(ulong id) => new DiscordGuildChannelPositionProperties(_original.WithId(id));
    public IDiscordGuildChannelPositionProperties WithPosition(int? position) => new DiscordGuildChannelPositionProperties(_original.WithPosition(position));
    public IDiscordGuildChannelPositionProperties WithLockPermissions(bool? lockPermissions = true) => new DiscordGuildChannelPositionProperties(_original.WithLockPermissions(lockPermissions));
    public IDiscordGuildChannelPositionProperties WithParentId(ulong? parentId) => new DiscordGuildChannelPositionProperties(_original.WithParentId(parentId));
}


public class DiscordPaginationProperties<T> : IDiscordPaginationProperties<T> where T : struct
{
    private readonly NetCord.Rest.PaginationProperties<T> _original;
    public DiscordPaginationProperties(NetCord.Rest.PaginationProperties<T> original)
    {
        _original = original;
    }
    public NetCord.Rest.PaginationProperties<T> Original => _original;
    public T? From => _original.From;
    public NetCord.Rest.PaginationDirection? Direction => _original.Direction;
    public int? BatchSize => _original.BatchSize;
    public IDiscordPaginationProperties<T> WithFrom(T? from) => new DiscordPaginationProperties<T>(_original.WithFrom(from));
    public IDiscordPaginationProperties<T> WithDirection(NetCord.Rest.PaginationDirection? direction) => new DiscordPaginationProperties<T>(_original.WithDirection(direction));
    public IDiscordPaginationProperties<T> WithBatchSize(int? batchSize) => new DiscordPaginationProperties<T>(_original.WithBatchSize(batchSize));
}


public class DiscordGuildUserProperties : IDiscordGuildUserProperties
{
    private readonly NetCord.Rest.GuildUserProperties _original;
    public DiscordGuildUserProperties(NetCord.Rest.GuildUserProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildUserProperties Original => _original;
    public string AccessToken => _original.AccessToken;
    public string Nickname => _original.Nickname;
    public IEnumerable<ulong> RolesIds => _original.RolesIds;
    public bool? Muted => _original.Muted;
    public bool? Deafened => _original.Deafened;
    public IDiscordGuildUserProperties WithAccessToken(string accessToken) => new DiscordGuildUserProperties(_original.WithAccessToken(accessToken));
    public IDiscordGuildUserProperties WithNickname(string nickname) => new DiscordGuildUserProperties(_original.WithNickname(nickname));
    public IDiscordGuildUserProperties WithRolesIds(IEnumerable<ulong> rolesIds) => new DiscordGuildUserProperties(_original.WithRolesIds(rolesIds));
    public IDiscordGuildUserProperties AddRolesIds(IEnumerable<ulong> rolesIds) => new DiscordGuildUserProperties(_original.AddRolesIds(rolesIds));
    public IDiscordGuildUserProperties AddRolesIds(ulong[] rolesIds) => new DiscordGuildUserProperties(_original.AddRolesIds(rolesIds));
    public IDiscordGuildUserProperties WithMuted(bool? muted = true) => new DiscordGuildUserProperties(_original.WithMuted(muted));
    public IDiscordGuildUserProperties WithDeafened(bool? deafened = true) => new DiscordGuildUserProperties(_original.WithDeafened(deafened));
}


public class DiscordGuildUserOptions : IDiscordGuildUserOptions
{
    private readonly NetCord.Rest.GuildUserOptions _original;
    public DiscordGuildUserOptions(NetCord.Rest.GuildUserOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildUserOptions Original => _original;
    public IEnumerable<ulong> RoleIds => _original.RoleIds;
    public bool? Muted => _original.Muted;
    public bool? Deafened => _original.Deafened;
    public ulong? ChannelId => _original.ChannelId;
    public System.DateTimeOffset? TimeOutUntil => _original.TimeOutUntil;
    public NetCord.GuildUserFlags? GuildFlags => _original.GuildFlags;
    public string Nickname => _original.Nickname;
    public IDiscordGuildUserOptions WithRoleIds(IEnumerable<ulong> roleIds) => new DiscordGuildUserOptions(_original.WithRoleIds(roleIds));
    public IDiscordGuildUserOptions AddRoleIds(IEnumerable<ulong> roleIds) => new DiscordGuildUserOptions(_original.AddRoleIds(roleIds));
    public IDiscordGuildUserOptions AddRoleIds(ulong[] roleIds) => new DiscordGuildUserOptions(_original.AddRoleIds(roleIds));
    public IDiscordGuildUserOptions WithMuted(bool? muted = true) => new DiscordGuildUserOptions(_original.WithMuted(muted));
    public IDiscordGuildUserOptions WithDeafened(bool? deafened = true) => new DiscordGuildUserOptions(_original.WithDeafened(deafened));
    public IDiscordGuildUserOptions WithChannelId(ulong? channelId) => new DiscordGuildUserOptions(_original.WithChannelId(channelId));
    public IDiscordGuildUserOptions WithTimeOutUntil(System.DateTimeOffset? timeOutUntil) => new DiscordGuildUserOptions(_original.WithTimeOutUntil(timeOutUntil));
    public IDiscordGuildUserOptions WithGuildFlags(NetCord.GuildUserFlags? guildFlags) => new DiscordGuildUserOptions(_original.WithGuildFlags(guildFlags));
    public IDiscordGuildUserOptions WithNickname(string nickname) => new DiscordGuildUserOptions(_original.WithNickname(nickname));
}


public class DiscordCurrentGuildUserOptions : IDiscordCurrentGuildUserOptions
{
    private readonly NetCord.Rest.CurrentGuildUserOptions _original;
    public DiscordCurrentGuildUserOptions(NetCord.Rest.CurrentGuildUserOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.CurrentGuildUserOptions Original => _original;
    public string Nickname => _original.Nickname;
    public IDiscordCurrentGuildUserOptions WithNickname(string nickname) => new DiscordCurrentGuildUserOptions(_original.WithNickname(nickname));
}


public class DiscordGuildBan : IDiscordGuildBan
{
    private readonly NetCord.Rest.GuildBan _original;
    public DiscordGuildBan(NetCord.Rest.GuildBan original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildBan Original => _original;
    public string Reason => _original.Reason;
    public IDiscordUser User => new DiscordUser(_original.User);
    public ulong GuildId => _original.GuildId;
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAsync(properties.Original, cancellationToken);
}


public class DiscordGuildBulkBan : IDiscordGuildBulkBan
{
    private readonly NetCord.Rest.GuildBulkBan _original;
    public DiscordGuildBulkBan(NetCord.Rest.GuildBulkBan original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildBulkBan Original => _original;
    public IReadOnlyList<ulong> BannedUsers => _original.BannedUsers;
    public IReadOnlyList<ulong> FailedUsers => _original.FailedUsers;
}


public class DiscordRoleProperties : IDiscordRoleProperties
{
    private readonly NetCord.Rest.RoleProperties _original;
    public DiscordRoleProperties(NetCord.Rest.RoleProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.RoleProperties Original => _original;
    public string Name => _original.Name;
    public NetCord.Permissions? Permissions => _original.Permissions;
    public NetCord.Color? Color => _original.Color;
    public bool? Hoist => _original.Hoist;
    public NetCord.Rest.ImageProperties? Icon => _original.Icon;
    public string UnicodeIcon => _original.UnicodeIcon;
    public bool? Mentionable => _original.Mentionable;
    public IDiscordRoleProperties WithName(string name) => new DiscordRoleProperties(_original.WithName(name));
    public IDiscordRoleProperties WithPermissions(NetCord.Permissions? permissions) => new DiscordRoleProperties(_original.WithPermissions(permissions));
    public IDiscordRoleProperties WithColor(NetCord.Color? color) => new DiscordRoleProperties(_original.WithColor(color));
    public IDiscordRoleProperties WithHoist(bool? hoist = true) => new DiscordRoleProperties(_original.WithHoist(hoist));
    public IDiscordRoleProperties WithIcon(NetCord.Rest.ImageProperties? icon) => new DiscordRoleProperties(_original.WithIcon(icon));
    public IDiscordRoleProperties WithUnicodeIcon(string unicodeIcon) => new DiscordRoleProperties(_original.WithUnicodeIcon(unicodeIcon));
    public IDiscordRoleProperties WithMentionable(bool? mentionable = true) => new DiscordRoleProperties(_original.WithMentionable(mentionable));
}


public class DiscordRolePositionProperties : IDiscordRolePositionProperties
{
    private readonly NetCord.Rest.RolePositionProperties _original;
    public DiscordRolePositionProperties(NetCord.Rest.RolePositionProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.RolePositionProperties Original => _original;
    public ulong Id => _original.Id;
    public int? Position => _original.Position;
    public IDiscordRolePositionProperties WithId(ulong id) => new DiscordRolePositionProperties(_original.WithId(id));
    public IDiscordRolePositionProperties WithPosition(int? position) => new DiscordRolePositionProperties(_original.WithPosition(position));
}


public class DiscordRoleOptions : IDiscordRoleOptions
{
    private readonly NetCord.Rest.RoleOptions _original;
    public DiscordRoleOptions(NetCord.Rest.RoleOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.RoleOptions Original => _original;
    public string Name => _original.Name;
    public NetCord.Permissions? Permissions => _original.Permissions;
    public NetCord.Color? Color => _original.Color;
    public bool? Hoist => _original.Hoist;
    public NetCord.Rest.ImageProperties? Icon => _original.Icon;
    public string UnicodeIcon => _original.UnicodeIcon;
    public bool? Mentionable => _original.Mentionable;
    public IDiscordRoleOptions WithName(string name) => new DiscordRoleOptions(_original.WithName(name));
    public IDiscordRoleOptions WithPermissions(NetCord.Permissions? permissions) => new DiscordRoleOptions(_original.WithPermissions(permissions));
    public IDiscordRoleOptions WithColor(NetCord.Color? color) => new DiscordRoleOptions(_original.WithColor(color));
    public IDiscordRoleOptions WithHoist(bool? hoist = true) => new DiscordRoleOptions(_original.WithHoist(hoist));
    public IDiscordRoleOptions WithIcon(NetCord.Rest.ImageProperties? icon) => new DiscordRoleOptions(_original.WithIcon(icon));
    public IDiscordRoleOptions WithUnicodeIcon(string unicodeIcon) => new DiscordRoleOptions(_original.WithUnicodeIcon(unicodeIcon));
    public IDiscordRoleOptions WithMentionable(bool? mentionable = true) => new DiscordRoleOptions(_original.WithMentionable(mentionable));
}


public class DiscordGuildPruneProperties : IDiscordGuildPruneProperties
{
    private readonly NetCord.Rest.GuildPruneProperties _original;
    public DiscordGuildPruneProperties(NetCord.Rest.GuildPruneProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildPruneProperties Original => _original;
    public int Days => _original.Days;
    public bool ComputePruneCount => _original.ComputePruneCount;
    public IEnumerable<ulong> Roles => _original.Roles;
    public IDiscordGuildPruneProperties WithDays(int days) => new DiscordGuildPruneProperties(_original.WithDays(days));
    public IDiscordGuildPruneProperties WithComputePruneCount(bool computePruneCount = true) => new DiscordGuildPruneProperties(_original.WithComputePruneCount(computePruneCount));
    public IDiscordGuildPruneProperties WithRoles(IEnumerable<ulong> roles) => new DiscordGuildPruneProperties(_original.WithRoles(roles));
    public IDiscordGuildPruneProperties AddRoles(IEnumerable<ulong> roles) => new DiscordGuildPruneProperties(_original.AddRoles(roles));
    public IDiscordGuildPruneProperties AddRoles(ulong[] roles) => new DiscordGuildPruneProperties(_original.AddRoles(roles));
}


public class DiscordVoiceRegion : IDiscordVoiceRegion
{
    private readonly NetCord.Rest.VoiceRegion _original;
    public DiscordVoiceRegion(NetCord.Rest.VoiceRegion original)
    {
        _original = original;
    }
    public NetCord.Rest.VoiceRegion Original => _original;
    public string Id => _original.Id;
    public string Name => _original.Name;
    public bool Optimal => _original.Optimal;
    public bool Deprecated => _original.Deprecated;
    public bool Custom => _original.Custom;
}


public class DiscordRestInvite : IDiscordRestInvite
{
    private readonly NetCord.Rest.RestInvite _original;
    public DiscordRestInvite(NetCord.Rest.RestInvite original)
    {
        _original = original;
    }
    public NetCord.Rest.RestInvite Original => _original;
    public NetCord.InviteType Type => _original.Type;
    public string Code => _original.Code;
    public IDiscordRestGuild Guild => new DiscordRestGuild(_original.Guild);
    public IDiscordChannel Channel => new DiscordChannel(_original.Channel);
    public IDiscordUser Inviter => new DiscordUser(_original.Inviter);
    public NetCord.InviteTargetType? TargetType => _original.TargetType;
    public IDiscordUser TargetUser => new DiscordUser(_original.TargetUser);
    public IDiscordApplication TargetApplication => new DiscordApplication(_original.TargetApplication);
    public int? ApproximatePresenceCount => _original.ApproximatePresenceCount;
    public int? ApproximateUserCount => _original.ApproximateUserCount;
    public System.DateTimeOffset? ExpiresAt => _original.ExpiresAt;
    public IDiscordStageInstance StageInstance => new DiscordStageInstance(_original.StageInstance);
    public IDiscordGuildScheduledEvent GuildScheduledEvent => new DiscordGuildScheduledEvent(_original.GuildScheduledEvent);
    public int? Uses => _original.Uses;
    public int? MaxUses => _original.MaxUses;
    public int? MaxAge => _original.MaxAge;
    public bool? Temporary => _original.Temporary;
    public System.DateTimeOffset? CreatedAt => _original.CreatedAt;
    public async Task<IDiscordRestInvite> GetGuildAsync(bool withCounts = false, bool withExpiration = false, ulong? guildScheduledEventId = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestInvite(await _original.GetGuildAsync(withCounts, withExpiration, guildScheduledEventId, properties.Original, cancellationToken));
    public async Task<IDiscordRestInvite> DeleteGuildAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestInvite(await _original.DeleteGuildAsync(properties.Original, cancellationToken));
}


public class DiscordIntegration : IDiscordIntegration
{
    private readonly NetCord.Integration _original;
    public DiscordIntegration(NetCord.Integration original)
    {
        _original = original;
    }
    public NetCord.Integration Original => _original;
    public ulong Id => _original.Id;
    public string Name => _original.Name;
    public NetCord.IntegrationType Type => _original.Type;
    public bool Enabled => _original.Enabled;
    public bool? Syncing => _original.Syncing;
    public ulong? RoleId => _original.RoleId;
    public bool? EnableEmoticons => _original.EnableEmoticons;
    public NetCord.IntegrationExpireBehavior? ExpireBehavior => _original.ExpireBehavior;
    public int? ExpireGracePeriod => _original.ExpireGracePeriod;
    public IDiscordUser User => new DiscordUser(_original.User);
    public IDiscordAccount Account => new DiscordAccount(_original.Account);
    public System.DateTimeOffset? SyncedAt => _original.SyncedAt;
    public int? SubscriberCount => _original.SubscriberCount;
    public bool? Revoked => _original.Revoked;
    public IDiscordIntegrationApplication Application => new DiscordIntegrationApplication(_original.Application);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordGuildWidgetSettings : IDiscordGuildWidgetSettings
{
    private readonly NetCord.Rest.GuildWidgetSettings _original;
    public DiscordGuildWidgetSettings(NetCord.Rest.GuildWidgetSettings original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildWidgetSettings Original => _original;
    public bool Enabled => _original.Enabled;
    public ulong? ChannelId => _original.ChannelId;
}


public class DiscordGuildWidgetSettingsOptions : IDiscordGuildWidgetSettingsOptions
{
    private readonly NetCord.Rest.GuildWidgetSettingsOptions _original;
    public DiscordGuildWidgetSettingsOptions(NetCord.Rest.GuildWidgetSettingsOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildWidgetSettingsOptions Original => _original;
    public bool Enabled => _original.Enabled;
    public ulong? ChannelId => _original.ChannelId;
    public IDiscordGuildWidgetSettingsOptions WithEnabled(bool enabled = true) => new DiscordGuildWidgetSettingsOptions(_original.WithEnabled(enabled));
    public IDiscordGuildWidgetSettingsOptions WithChannelId(ulong? channelId) => new DiscordGuildWidgetSettingsOptions(_original.WithChannelId(channelId));
}


public class DiscordGuildWidget : IDiscordGuildWidget
{
    private readonly NetCord.Rest.GuildWidget _original;
    public DiscordGuildWidget(NetCord.Rest.GuildWidget original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildWidget Original => _original;
    public ulong Id => _original.Id;
    public string Name => _original.Name;
    public string InstantInvite => _original.InstantInvite;
    public ImmutableDictionary<ulong, IDiscordGuildWidgetChannel> Channels => _original.Channels.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordGuildWidgetChannel)new DiscordGuildWidgetChannel(kv.Value));
    public ImmutableDictionary<ulong, IDiscordUser> Users => _original.Users.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordUser)new DiscordUser(kv.Value));
    public int PresenceCount => _original.PresenceCount;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordGuildVanityInvite : IDiscordGuildVanityInvite
{
    private readonly NetCord.Rest.GuildVanityInvite _original;
    public DiscordGuildVanityInvite(NetCord.Rest.GuildVanityInvite original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildVanityInvite Original => _original;
    public string Code => _original.Code;
    public int Uses => _original.Uses;
}


public class DiscordGuildWelcomeScreenOptions : IDiscordGuildWelcomeScreenOptions
{
    private readonly NetCord.Rest.GuildWelcomeScreenOptions _original;
    public DiscordGuildWelcomeScreenOptions(NetCord.Rest.GuildWelcomeScreenOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildWelcomeScreenOptions Original => _original;
    public bool? Enabled => _original.Enabled;
    public IEnumerable<IDiscordGuildWelcomeScreenChannelProperties> WelcomeChannels => _original.WelcomeChannels.Select(x => new DiscordGuildWelcomeScreenChannelProperties(x));
    public string Description => _original.Description;
    public IDiscordGuildWelcomeScreenOptions WithEnabled(bool? enabled = true) => new DiscordGuildWelcomeScreenOptions(_original.WithEnabled(enabled));
    public IDiscordGuildWelcomeScreenOptions WithWelcomeChannels(IEnumerable<IDiscordGuildWelcomeScreenChannelProperties> welcomeChannels) => new DiscordGuildWelcomeScreenOptions(_original.WithWelcomeChannels(welcomeChannels?.Select(x => x.Original)));
    public IDiscordGuildWelcomeScreenOptions AddWelcomeChannels(IEnumerable<IDiscordGuildWelcomeScreenChannelProperties> welcomeChannels) => new DiscordGuildWelcomeScreenOptions(_original.AddWelcomeChannels(welcomeChannels?.Select(x => x.Original)));
    public IDiscordGuildWelcomeScreenOptions AddWelcomeChannels(IDiscordGuildWelcomeScreenChannelProperties[] welcomeChannels) => new DiscordGuildWelcomeScreenOptions(_original.AddWelcomeChannels(welcomeChannels.Original));
    public IDiscordGuildWelcomeScreenOptions WithDescription(string description) => new DiscordGuildWelcomeScreenOptions(_original.WithDescription(description));
}


public class DiscordGuildOnboarding : IDiscordGuildOnboarding
{
    private readonly NetCord.Rest.GuildOnboarding _original;
    public DiscordGuildOnboarding(NetCord.Rest.GuildOnboarding original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildOnboarding Original => _original;
    public ulong GuildId => _original.GuildId;
    public IReadOnlyList<IDiscordGuildOnboardingPrompt> Prompts => _original.Prompts.Select(x => new DiscordGuildOnboardingPrompt(x)).ToList();
    public IReadOnlyList<ulong> DefaultChannelIds => _original.DefaultChannelIds;
    public bool Enabled => _original.Enabled;
    public NetCord.Rest.GuildOnboardingMode Mode => _original.Mode;
}


public class DiscordGuildOnboardingOptions : IDiscordGuildOnboardingOptions
{
    private readonly NetCord.Rest.GuildOnboardingOptions _original;
    public DiscordGuildOnboardingOptions(NetCord.Rest.GuildOnboardingOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildOnboardingOptions Original => _original;
    public IEnumerable<IDiscordGuildOnboardingPromptProperties> Prompts => _original.Prompts.Select(x => new DiscordGuildOnboardingPromptProperties(x));
    public IEnumerable<ulong> DefaultChannelIds => _original.DefaultChannelIds;
    public bool? Enabled => _original.Enabled;
    public NetCord.Rest.GuildOnboardingMode? Mode => _original.Mode;
    public IDiscordGuildOnboardingOptions WithPrompts(IEnumerable<IDiscordGuildOnboardingPromptProperties> prompts) => new DiscordGuildOnboardingOptions(_original.WithPrompts(prompts?.Select(x => x.Original)));
    public IDiscordGuildOnboardingOptions AddPrompts(IEnumerable<IDiscordGuildOnboardingPromptProperties> prompts) => new DiscordGuildOnboardingOptions(_original.AddPrompts(prompts?.Select(x => x.Original)));
    public IDiscordGuildOnboardingOptions AddPrompts(IDiscordGuildOnboardingPromptProperties[] prompts) => new DiscordGuildOnboardingOptions(_original.AddPrompts(prompts.Original));
    public IDiscordGuildOnboardingOptions WithDefaultChannelIds(IEnumerable<ulong> defaultChannelIds) => new DiscordGuildOnboardingOptions(_original.WithDefaultChannelIds(defaultChannelIds));
    public IDiscordGuildOnboardingOptions AddDefaultChannelIds(IEnumerable<ulong> defaultChannelIds) => new DiscordGuildOnboardingOptions(_original.AddDefaultChannelIds(defaultChannelIds));
    public IDiscordGuildOnboardingOptions AddDefaultChannelIds(ulong[] defaultChannelIds) => new DiscordGuildOnboardingOptions(_original.AddDefaultChannelIds(defaultChannelIds));
    public IDiscordGuildOnboardingOptions WithEnabled(bool? enabled = true) => new DiscordGuildOnboardingOptions(_original.WithEnabled(enabled));
    public IDiscordGuildOnboardingOptions WithMode(NetCord.Rest.GuildOnboardingMode? mode) => new DiscordGuildOnboardingOptions(_original.WithMode(mode));
}


public class DiscordGuildScheduledEventProperties : IDiscordGuildScheduledEventProperties
{
    private readonly NetCord.Rest.GuildScheduledEventProperties _original;
    public DiscordGuildScheduledEventProperties(NetCord.Rest.GuildScheduledEventProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildScheduledEventProperties Original => _original;
    public ulong? ChannelId => _original.ChannelId;
    public IDiscordGuildScheduledEventMetadataProperties Metadata => new DiscordGuildScheduledEventMetadataProperties(_original.Metadata);
    public string Name => _original.Name;
    public NetCord.GuildScheduledEventPrivacyLevel PrivacyLevel => _original.PrivacyLevel;
    public System.DateTimeOffset ScheduledStartTime => _original.ScheduledStartTime;
    public System.DateTimeOffset? ScheduledEndTime => _original.ScheduledEndTime;
    public string Description => _original.Description;
    public NetCord.GuildScheduledEventEntityType EntityType => _original.EntityType;
    public NetCord.Rest.ImageProperties? Image => _original.Image;
    public IDiscordGuildScheduledEventProperties WithChannelId(ulong? channelId) => new DiscordGuildScheduledEventProperties(_original.WithChannelId(channelId));
    public IDiscordGuildScheduledEventProperties WithMetadata(IDiscordGuildScheduledEventMetadataProperties metadata) => new DiscordGuildScheduledEventProperties(_original.WithMetadata(metadata.Original));
    public IDiscordGuildScheduledEventProperties WithName(string name) => new DiscordGuildScheduledEventProperties(_original.WithName(name));
    public IDiscordGuildScheduledEventProperties WithPrivacyLevel(NetCord.GuildScheduledEventPrivacyLevel privacyLevel) => new DiscordGuildScheduledEventProperties(_original.WithPrivacyLevel(privacyLevel));
    public IDiscordGuildScheduledEventProperties WithScheduledStartTime(System.DateTimeOffset scheduledStartTime) => new DiscordGuildScheduledEventProperties(_original.WithScheduledStartTime(scheduledStartTime));
    public IDiscordGuildScheduledEventProperties WithScheduledEndTime(System.DateTimeOffset? scheduledEndTime) => new DiscordGuildScheduledEventProperties(_original.WithScheduledEndTime(scheduledEndTime));
    public IDiscordGuildScheduledEventProperties WithDescription(string description) => new DiscordGuildScheduledEventProperties(_original.WithDescription(description));
    public IDiscordGuildScheduledEventProperties WithEntityType(NetCord.GuildScheduledEventEntityType entityType) => new DiscordGuildScheduledEventProperties(_original.WithEntityType(entityType));
    public IDiscordGuildScheduledEventProperties WithImage(NetCord.Rest.ImageProperties? image) => new DiscordGuildScheduledEventProperties(_original.WithImage(image));
}


public class DiscordGuildScheduledEventOptions : IDiscordGuildScheduledEventOptions
{
    private readonly NetCord.Rest.GuildScheduledEventOptions _original;
    public DiscordGuildScheduledEventOptions(NetCord.Rest.GuildScheduledEventOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildScheduledEventOptions Original => _original;
    public ulong? ChannelId => _original.ChannelId;
    public IDiscordGuildScheduledEventMetadataProperties Metadata => new DiscordGuildScheduledEventMetadataProperties(_original.Metadata);
    public string Name => _original.Name;
    public NetCord.GuildScheduledEventPrivacyLevel? PrivacyLevel => _original.PrivacyLevel;
    public System.DateTimeOffset? ScheduledStartTime => _original.ScheduledStartTime;
    public System.DateTimeOffset? ScheduledEndTime => _original.ScheduledEndTime;
    public string Description => _original.Description;
    public NetCord.GuildScheduledEventEntityType? EntityType => _original.EntityType;
    public NetCord.GuildScheduledEventStatus? Status => _original.Status;
    public NetCord.Rest.ImageProperties? Image => _original.Image;
    public IDiscordGuildScheduledEventOptions WithChannelId(ulong? channelId) => new DiscordGuildScheduledEventOptions(_original.WithChannelId(channelId));
    public IDiscordGuildScheduledEventOptions WithMetadata(IDiscordGuildScheduledEventMetadataProperties metadata) => new DiscordGuildScheduledEventOptions(_original.WithMetadata(metadata.Original));
    public IDiscordGuildScheduledEventOptions WithName(string name) => new DiscordGuildScheduledEventOptions(_original.WithName(name));
    public IDiscordGuildScheduledEventOptions WithPrivacyLevel(NetCord.GuildScheduledEventPrivacyLevel? privacyLevel) => new DiscordGuildScheduledEventOptions(_original.WithPrivacyLevel(privacyLevel));
    public IDiscordGuildScheduledEventOptions WithScheduledStartTime(System.DateTimeOffset? scheduledStartTime) => new DiscordGuildScheduledEventOptions(_original.WithScheduledStartTime(scheduledStartTime));
    public IDiscordGuildScheduledEventOptions WithScheduledEndTime(System.DateTimeOffset? scheduledEndTime) => new DiscordGuildScheduledEventOptions(_original.WithScheduledEndTime(scheduledEndTime));
    public IDiscordGuildScheduledEventOptions WithDescription(string description) => new DiscordGuildScheduledEventOptions(_original.WithDescription(description));
    public IDiscordGuildScheduledEventOptions WithEntityType(NetCord.GuildScheduledEventEntityType? entityType) => new DiscordGuildScheduledEventOptions(_original.WithEntityType(entityType));
    public IDiscordGuildScheduledEventOptions WithStatus(NetCord.GuildScheduledEventStatus? status) => new DiscordGuildScheduledEventOptions(_original.WithStatus(status));
    public IDiscordGuildScheduledEventOptions WithImage(NetCord.Rest.ImageProperties? image) => new DiscordGuildScheduledEventOptions(_original.WithImage(image));
}


public class DiscordGuildScheduledEventUser : IDiscordGuildScheduledEventUser
{
    private readonly NetCord.Rest.GuildScheduledEventUser _original;
    public DiscordGuildScheduledEventUser(NetCord.Rest.GuildScheduledEventUser original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildScheduledEventUser Original => _original;
    public ulong ScheduledEventId => _original.ScheduledEventId;
    public IDiscordUser User => new DiscordUser(_original.User);
}


public class DiscordOptionalGuildUsersPaginationProperties : IDiscordOptionalGuildUsersPaginationProperties
{
    private readonly NetCord.Rest.OptionalGuildUsersPaginationProperties _original;
    public DiscordOptionalGuildUsersPaginationProperties(NetCord.Rest.OptionalGuildUsersPaginationProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.OptionalGuildUsersPaginationProperties Original => _original;
    public bool WithGuildUsers => _original.WithGuildUsers;
    public ulong? From => _original.From;
    public NetCord.Rest.PaginationDirection? Direction => _original.Direction;
    public int? BatchSize => _original.BatchSize;
    public IDiscordOptionalGuildUsersPaginationProperties WithWithGuildUsers(bool withGuildUsers = true) => new DiscordOptionalGuildUsersPaginationProperties(_original.WithWithGuildUsers(withGuildUsers));
    public IDiscordOptionalGuildUsersPaginationProperties WithFrom(ulong? from) => new DiscordOptionalGuildUsersPaginationProperties(_original.WithFrom(from));
    public IDiscordOptionalGuildUsersPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction) => new DiscordOptionalGuildUsersPaginationProperties(_original.WithDirection(direction));
    public IDiscordOptionalGuildUsersPaginationProperties WithBatchSize(int? batchSize) => new DiscordOptionalGuildUsersPaginationProperties(_original.WithBatchSize(batchSize));
}


public class DiscordGuildTemplate : IDiscordGuildTemplate
{
    private readonly NetCord.GuildTemplate _original;
    public DiscordGuildTemplate(NetCord.GuildTemplate original)
    {
        _original = original;
    }
    public NetCord.GuildTemplate Original => _original;
    public string Code => _original.Code;
    public string Name => _original.Name;
    public string Description => _original.Description;
    public int UsageCount => _original.UsageCount;
    public ulong CreatorId => _original.CreatorId;
    public IDiscordUser Creator => new DiscordUser(_original.Creator);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public System.DateTimeOffset UpdatedAt => _original.UpdatedAt;
    public ulong SourceGuildId => _original.SourceGuildId;
    public IDiscordGuildTemplatePreview Preview => new DiscordGuildTemplatePreview(_original.Preview);
    public bool? IsDirty => _original.IsDirty;
    public async Task<IDiscordGuildTemplate> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildTemplate(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordRestGuild> CreateGuildAsync(IDiscordGuildFromGuildTemplateProperties guildProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestGuild(await _original.CreateGuildAsync(guildProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordGuildTemplate> SyncAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildTemplate(await _original.SyncAsync(properties.Original, cancellationToken));
    public async Task<IDiscordGuildTemplate> ModifyAsync(Action<IDiscordGuildTemplateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildTemplate(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public async Task<IDiscordGuildTemplate> DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildTemplate(await _original.DeleteAsync(properties.Original, cancellationToken));
}


public class DiscordGuildTemplateProperties : IDiscordGuildTemplateProperties
{
    private readonly NetCord.Rest.GuildTemplateProperties _original;
    public DiscordGuildTemplateProperties(NetCord.Rest.GuildTemplateProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildTemplateProperties Original => _original;
    public string Name => _original.Name;
    public string Description => _original.Description;
    public IDiscordGuildTemplateProperties WithName(string name) => new DiscordGuildTemplateProperties(_original.WithName(name));
    public IDiscordGuildTemplateProperties WithDescription(string description) => new DiscordGuildTemplateProperties(_original.WithDescription(description));
}


public class DiscordGuildTemplateOptions : IDiscordGuildTemplateOptions
{
    private readonly NetCord.Rest.GuildTemplateOptions _original;
    public DiscordGuildTemplateOptions(NetCord.Rest.GuildTemplateOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildTemplateOptions Original => _original;
    public string Name => _original.Name;
    public string Description => _original.Description;
    public IDiscordGuildTemplateOptions WithName(string name) => new DiscordGuildTemplateOptions(_original.WithName(name));
    public IDiscordGuildTemplateOptions WithDescription(string description) => new DiscordGuildTemplateOptions(_original.WithDescription(description));
}


public class DiscordGuildApplicationCommand : IDiscordGuildApplicationCommand
{
    private readonly NetCord.Rest.GuildApplicationCommand _original;
    public DiscordGuildApplicationCommand(NetCord.Rest.GuildApplicationCommand original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildApplicationCommand Original => _original;
    public ulong GuildId => _original.GuildId;
    public ulong Id => _original.Id;
    public NetCord.ApplicationCommandType Type => _original.Type;
    public ulong ApplicationId => _original.ApplicationId;
    public string Name => _original.Name;
    public IReadOnlyDictionary<string, string> NameLocalizations => _original.NameLocalizations;
    public string Description => _original.Description;
    public IReadOnlyDictionary<string, string> DescriptionLocalizations => _original.DescriptionLocalizations;
    public NetCord.Permissions? DefaultGuildUserPermissions => _original.DefaultGuildUserPermissions;
    public bool DMPermission => _original.DMPermission;
    public IReadOnlyList<IDiscordApplicationCommandOption> Options => _original.Options.Select(x => new DiscordApplicationCommandOption(x)).ToList();
    public bool DefaultPermission => _original.DefaultPermission;
    public bool Nsfw => _original.Nsfw;
    public IReadOnlyList<NetCord.ApplicationIntegrationType> IntegrationTypes => _original.IntegrationTypes;
    public IReadOnlyList<NetCord.InteractionContextType> Contexts => _original.Contexts;
    public ulong Version => _original.Version;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordApplicationCommand> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationCommand(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordApplicationCommand> ModifyAsync(Action<IDiscordApplicationCommandOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationCommand(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAsync(properties.Original, cancellationToken);
    public async Task<IDiscordApplicationCommandGuildPermissions> GetPermissionsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationCommandGuildPermissions(await _original.GetPermissionsAsync(properties.Original, cancellationToken));
    public async Task<IDiscordApplicationCommandGuildPermissions> OverwritePermissionsAsync(IEnumerable<IDiscordApplicationCommandGuildPermissionProperties> newPermissions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationCommandGuildPermissions(await _original.OverwritePermissionsAsync(newPermissions?.Select(x => x.Original), properties.Original, cancellationToken));
    public async Task<IDiscordApplicationCommandGuildPermissions> GetGuildPermissionsAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationCommandGuildPermissions(await _original.GetGuildPermissionsAsync(guildId, properties.Original, cancellationToken));
    public async Task<IDiscordApplicationCommandGuildPermissions> OverwriteGuildPermissionsAsync(ulong guildId, IEnumerable<IDiscordApplicationCommandGuildPermissionProperties> newPermissions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationCommandGuildPermissions(await _original.OverwriteGuildPermissionsAsync(guildId, newPermissions?.Select(x => x.Original), properties.Original, cancellationToken));
}


public class DiscordApplicationCommandProperties : IDiscordApplicationCommandProperties
{
    private readonly NetCord.Rest.ApplicationCommandProperties _original;
    public DiscordApplicationCommandProperties(NetCord.Rest.ApplicationCommandProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationCommandProperties Original => _original;
    public NetCord.ApplicationCommandType Type => _original.Type;
    public string Name => _original.Name;
    public IReadOnlyDictionary<string, string> NameLocalizations => _original.NameLocalizations;
    public NetCord.Permissions? DefaultGuildUserPermissions => _original.DefaultGuildUserPermissions;
    public bool? DMPermission => _original.DMPermission;
    public bool? DefaultPermission => _original.DefaultPermission;
    public IEnumerable<NetCord.ApplicationIntegrationType> IntegrationTypes => _original.IntegrationTypes;
    public IEnumerable<NetCord.InteractionContextType> Contexts => _original.Contexts;
    public bool Nsfw => _original.Nsfw;
    public IDiscordApplicationCommandProperties WithName(string name) => new DiscordApplicationCommandProperties(_original.WithName(name));
    public IDiscordApplicationCommandProperties WithNameLocalizations(IReadOnlyDictionary<string, string> nameLocalizations) => new DiscordApplicationCommandProperties(_original.WithNameLocalizations(nameLocalizations));
    public IDiscordApplicationCommandProperties WithDefaultGuildUserPermissions(NetCord.Permissions? defaultGuildUserPermissions) => new DiscordApplicationCommandProperties(_original.WithDefaultGuildUserPermissions(defaultGuildUserPermissions));
    public IDiscordApplicationCommandProperties WithDMPermission(bool? dMPermission = true) => new DiscordApplicationCommandProperties(_original.WithDMPermission(dMPermission));
    public IDiscordApplicationCommandProperties WithDefaultPermission(bool? defaultPermission = true) => new DiscordApplicationCommandProperties(_original.WithDefaultPermission(defaultPermission));
    public IDiscordApplicationCommandProperties WithIntegrationTypes(IEnumerable<NetCord.ApplicationIntegrationType> integrationTypes) => new DiscordApplicationCommandProperties(_original.WithIntegrationTypes(integrationTypes));
    public IDiscordApplicationCommandProperties AddIntegrationTypes(IEnumerable<NetCord.ApplicationIntegrationType> integrationTypes) => new DiscordApplicationCommandProperties(_original.AddIntegrationTypes(integrationTypes));
    public IDiscordApplicationCommandProperties AddIntegrationTypes(NetCord.ApplicationIntegrationType[] integrationTypes) => new DiscordApplicationCommandProperties(_original.AddIntegrationTypes(integrationTypes));
    public IDiscordApplicationCommandProperties WithContexts(IEnumerable<NetCord.InteractionContextType> contexts) => new DiscordApplicationCommandProperties(_original.WithContexts(contexts));
    public IDiscordApplicationCommandProperties AddContexts(IEnumerable<NetCord.InteractionContextType> contexts) => new DiscordApplicationCommandProperties(_original.AddContexts(contexts));
    public IDiscordApplicationCommandProperties AddContexts(NetCord.InteractionContextType[] contexts) => new DiscordApplicationCommandProperties(_original.AddContexts(contexts));
    public IDiscordApplicationCommandProperties WithNsfw(bool nsfw = true) => new DiscordApplicationCommandProperties(_original.WithNsfw(nsfw));
}


public class DiscordApplicationCommandOptions : IDiscordApplicationCommandOptions
{
    private readonly NetCord.Rest.ApplicationCommandOptions _original;
    public DiscordApplicationCommandOptions(NetCord.Rest.ApplicationCommandOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationCommandOptions Original => _original;
    public string Name => _original.Name;
    public IReadOnlyDictionary<string, string> NameLocalizations => _original.NameLocalizations;
    public string Description => _original.Description;
    public IReadOnlyDictionary<string, string> DescriptionLocalizations => _original.DescriptionLocalizations;
    public IEnumerable<IDiscordApplicationCommandOptionProperties> Options => _original.Options.Select(x => new DiscordApplicationCommandOptionProperties(x));
    public NetCord.Permissions? DefaultGuildUserPermissions => _original.DefaultGuildUserPermissions;
    public bool? DMPermission => _original.DMPermission;
    public bool? DefaultPermission => _original.DefaultPermission;
    public IEnumerable<NetCord.ApplicationIntegrationType> IntegrationTypes => _original.IntegrationTypes;
    public IEnumerable<NetCord.InteractionContextType> Contexts => _original.Contexts;
    public bool? Nsfw => _original.Nsfw;
    public IDiscordApplicationCommandOptions WithName(string name) => new DiscordApplicationCommandOptions(_original.WithName(name));
    public IDiscordApplicationCommandOptions WithNameLocalizations(IReadOnlyDictionary<string, string> nameLocalizations) => new DiscordApplicationCommandOptions(_original.WithNameLocalizations(nameLocalizations));
    public IDiscordApplicationCommandOptions WithDescription(string description) => new DiscordApplicationCommandOptions(_original.WithDescription(description));
    public IDiscordApplicationCommandOptions WithDescriptionLocalizations(IReadOnlyDictionary<string, string> descriptionLocalizations) => new DiscordApplicationCommandOptions(_original.WithDescriptionLocalizations(descriptionLocalizations));
    public IDiscordApplicationCommandOptions WithOptions(IEnumerable<IDiscordApplicationCommandOptionProperties> options) => new DiscordApplicationCommandOptions(_original.WithOptions(options?.Select(x => x.Original)));
    public IDiscordApplicationCommandOptions AddOptions(IEnumerable<IDiscordApplicationCommandOptionProperties> options) => new DiscordApplicationCommandOptions(_original.AddOptions(options?.Select(x => x.Original)));
    public IDiscordApplicationCommandOptions AddOptions(IDiscordApplicationCommandOptionProperties[] options) => new DiscordApplicationCommandOptions(_original.AddOptions(options.Original));
    public IDiscordApplicationCommandOptions WithDefaultGuildUserPermissions(NetCord.Permissions? defaultGuildUserPermissions) => new DiscordApplicationCommandOptions(_original.WithDefaultGuildUserPermissions(defaultGuildUserPermissions));
    public IDiscordApplicationCommandOptions WithDMPermission(bool? dMPermission = true) => new DiscordApplicationCommandOptions(_original.WithDMPermission(dMPermission));
    public IDiscordApplicationCommandOptions WithDefaultPermission(bool? defaultPermission = true) => new DiscordApplicationCommandOptions(_original.WithDefaultPermission(defaultPermission));
    public IDiscordApplicationCommandOptions WithIntegrationTypes(IEnumerable<NetCord.ApplicationIntegrationType> integrationTypes) => new DiscordApplicationCommandOptions(_original.WithIntegrationTypes(integrationTypes));
    public IDiscordApplicationCommandOptions AddIntegrationTypes(IEnumerable<NetCord.ApplicationIntegrationType> integrationTypes) => new DiscordApplicationCommandOptions(_original.AddIntegrationTypes(integrationTypes));
    public IDiscordApplicationCommandOptions AddIntegrationTypes(NetCord.ApplicationIntegrationType[] integrationTypes) => new DiscordApplicationCommandOptions(_original.AddIntegrationTypes(integrationTypes));
    public IDiscordApplicationCommandOptions WithContexts(IEnumerable<NetCord.InteractionContextType> contexts) => new DiscordApplicationCommandOptions(_original.WithContexts(contexts));
    public IDiscordApplicationCommandOptions AddContexts(IEnumerable<NetCord.InteractionContextType> contexts) => new DiscordApplicationCommandOptions(_original.AddContexts(contexts));
    public IDiscordApplicationCommandOptions AddContexts(NetCord.InteractionContextType[] contexts) => new DiscordApplicationCommandOptions(_original.AddContexts(contexts));
    public IDiscordApplicationCommandOptions WithNsfw(bool? nsfw = true) => new DiscordApplicationCommandOptions(_original.WithNsfw(nsfw));
}


public class DiscordApplicationCommandGuildPermissions : IDiscordApplicationCommandGuildPermissions
{
    private readonly NetCord.Rest.ApplicationCommandGuildPermissions _original;
    public DiscordApplicationCommandGuildPermissions(NetCord.Rest.ApplicationCommandGuildPermissions original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationCommandGuildPermissions Original => _original;
    public ulong CommandId => _original.CommandId;
    public ulong ApplicationId => _original.ApplicationId;
    public ulong GuildId => _original.GuildId;
    public IReadOnlyDictionary<ulong, IDiscordApplicationCommandPermission> Permissions => _original.Permissions.ToDictionary(kv => kv.Key, kv => (IDiscordApplicationCommandPermission)new DiscordApplicationCommandPermission(kv.Value));
}


public class DiscordApplicationCommandGuildPermissionProperties : IDiscordApplicationCommandGuildPermissionProperties
{
    private readonly NetCord.Rest.ApplicationCommandGuildPermissionProperties _original;
    public DiscordApplicationCommandGuildPermissionProperties(NetCord.Rest.ApplicationCommandGuildPermissionProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationCommandGuildPermissionProperties Original => _original;
    public ulong Id => _original.Id;
    public NetCord.ApplicationCommandGuildPermissionType Type => _original.Type;
    public bool Permission => _original.Permission;
    public IDiscordApplicationCommandGuildPermissionProperties WithId(ulong id) => new DiscordApplicationCommandGuildPermissionProperties(_original.WithId(id));
    public IDiscordApplicationCommandGuildPermissionProperties WithType(NetCord.ApplicationCommandGuildPermissionType type) => new DiscordApplicationCommandGuildPermissionProperties(_original.WithType(type));
    public IDiscordApplicationCommandGuildPermissionProperties WithPermission(bool permission = true) => new DiscordApplicationCommandGuildPermissionProperties(_original.WithPermission(permission));
}


public class DiscordGuildStickerProperties : IDiscordGuildStickerProperties
{
    private readonly NetCord.Rest.GuildStickerProperties _original;
    public DiscordGuildStickerProperties(NetCord.Rest.GuildStickerProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildStickerProperties Original => _original;
    public IDiscordAttachmentProperties Attachment => new DiscordAttachmentProperties(_original.Attachment);
    public NetCord.StickerFormat Format => _original.Format;
    public IEnumerable<string> Tags => _original.Tags;
    public HttpContent Serialize() => _original.Serialize();
    public IDiscordGuildStickerProperties WithAttachment(IDiscordAttachmentProperties attachment) => new DiscordGuildStickerProperties(_original.WithAttachment(attachment.Original));
    public IDiscordGuildStickerProperties WithFormat(NetCord.StickerFormat format) => new DiscordGuildStickerProperties(_original.WithFormat(format));
    public IDiscordGuildStickerProperties WithTags(IEnumerable<string> tags) => new DiscordGuildStickerProperties(_original.WithTags(tags));
    public IDiscordGuildStickerProperties AddTags(IEnumerable<string> tags) => new DiscordGuildStickerProperties(_original.AddTags(tags));
    public IDiscordGuildStickerProperties AddTags(string[] tags) => new DiscordGuildStickerProperties(_original.AddTags(tags));
}


public class DiscordGuildStickerOptions : IDiscordGuildStickerOptions
{
    private readonly NetCord.Rest.GuildStickerOptions _original;
    public DiscordGuildStickerOptions(NetCord.Rest.GuildStickerOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildStickerOptions Original => _original;
    public string Name => _original.Name;
    public string Description => _original.Description;
    public string Tags => _original.Tags;
    public IDiscordGuildStickerOptions WithName(string name) => new DiscordGuildStickerOptions(_original.WithName(name));
    public IDiscordGuildStickerOptions WithDescription(string description) => new DiscordGuildStickerOptions(_original.WithDescription(description));
    public IDiscordGuildStickerOptions WithTags(string tags) => new DiscordGuildStickerOptions(_original.WithTags(tags));
}


public class DiscordGuildUserInfo : IDiscordGuildUserInfo
{
    private readonly NetCord.Rest.GuildUserInfo _original;
    public DiscordGuildUserInfo(NetCord.Rest.GuildUserInfo original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildUserInfo Original => _original;
    public IDiscordGuildUser User => new DiscordGuildUser(_original.User);
    public string SourceInviteCode => _original.SourceInviteCode;
    public NetCord.Rest.GuildUserJoinSourceType JoinSourceType => _original.JoinSourceType;
    public ulong? InviterId => _original.InviterId;
}


public class DiscordGuildUsersSearchPaginationProperties : IDiscordGuildUsersSearchPaginationProperties
{
    private readonly NetCord.Rest.GuildUsersSearchPaginationProperties _original;
    public DiscordGuildUsersSearchPaginationProperties(NetCord.Rest.GuildUsersSearchPaginationProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildUsersSearchPaginationProperties Original => _original;
    public IEnumerable<IDiscordGuildUsersSearchQuery> OrQuery => _original.OrQuery.Select(x => new DiscordGuildUsersSearchQuery(x));
    public IEnumerable<IDiscordGuildUsersSearchQuery> AndQuery => _original.AndQuery.Select(x => new DiscordGuildUsersSearchQuery(x));
    public NetCord.Rest.GuildUsersSearchTimestamp? From => _original.From;
    public NetCord.Rest.PaginationDirection? Direction => _original.Direction;
    public int? BatchSize => _original.BatchSize;
    public IDiscordGuildUsersSearchPaginationProperties WithOrQuery(IEnumerable<IDiscordGuildUsersSearchQuery> orQuery) => new DiscordGuildUsersSearchPaginationProperties(_original.WithOrQuery(orQuery?.Select(x => x.Original)));
    public IDiscordGuildUsersSearchPaginationProperties AddOrQuery(IEnumerable<IDiscordGuildUsersSearchQuery> orQuery) => new DiscordGuildUsersSearchPaginationProperties(_original.AddOrQuery(orQuery?.Select(x => x.Original)));
    public IDiscordGuildUsersSearchPaginationProperties AddOrQuery(IDiscordGuildUsersSearchQuery[] orQuery) => new DiscordGuildUsersSearchPaginationProperties(_original.AddOrQuery(orQuery.Original));
    public IDiscordGuildUsersSearchPaginationProperties WithAndQuery(IEnumerable<IDiscordGuildUsersSearchQuery> andQuery) => new DiscordGuildUsersSearchPaginationProperties(_original.WithAndQuery(andQuery?.Select(x => x.Original)));
    public IDiscordGuildUsersSearchPaginationProperties AddAndQuery(IEnumerable<IDiscordGuildUsersSearchQuery> andQuery) => new DiscordGuildUsersSearchPaginationProperties(_original.AddAndQuery(andQuery?.Select(x => x.Original)));
    public IDiscordGuildUsersSearchPaginationProperties AddAndQuery(IDiscordGuildUsersSearchQuery[] andQuery) => new DiscordGuildUsersSearchPaginationProperties(_original.AddAndQuery(andQuery.Original));
    public IDiscordGuildUsersSearchPaginationProperties WithFrom(NetCord.Rest.GuildUsersSearchTimestamp? from) => new DiscordGuildUsersSearchPaginationProperties(_original.WithFrom(from));
    public IDiscordGuildUsersSearchPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction) => new DiscordGuildUsersSearchPaginationProperties(_original.WithDirection(direction));
    public IDiscordGuildUsersSearchPaginationProperties WithBatchSize(int? batchSize) => new DiscordGuildUsersSearchPaginationProperties(_original.WithBatchSize(batchSize));
}


public class DiscordCurrentUserVoiceStateOptions : IDiscordCurrentUserVoiceStateOptions
{
    private readonly NetCord.Rest.CurrentUserVoiceStateOptions _original;
    public DiscordCurrentUserVoiceStateOptions(NetCord.Rest.CurrentUserVoiceStateOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.CurrentUserVoiceStateOptions Original => _original;
    public ulong? ChannelId => _original.ChannelId;
    public bool? Suppress => _original.Suppress;
    public System.DateTimeOffset? RequestToSpeakTimestamp => _original.RequestToSpeakTimestamp;
    public IDiscordCurrentUserVoiceStateOptions WithChannelId(ulong? channelId) => new DiscordCurrentUserVoiceStateOptions(_original.WithChannelId(channelId));
    public IDiscordCurrentUserVoiceStateOptions WithSuppress(bool? suppress = true) => new DiscordCurrentUserVoiceStateOptions(_original.WithSuppress(suppress));
    public IDiscordCurrentUserVoiceStateOptions WithRequestToSpeakTimestamp(System.DateTimeOffset? requestToSpeakTimestamp) => new DiscordCurrentUserVoiceStateOptions(_original.WithRequestToSpeakTimestamp(requestToSpeakTimestamp));
}


public class DiscordVoiceStateOptions : IDiscordVoiceStateOptions
{
    private readonly NetCord.Rest.VoiceStateOptions _original;
    public DiscordVoiceStateOptions(NetCord.Rest.VoiceStateOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.VoiceStateOptions Original => _original;
    public ulong ChannelId => _original.ChannelId;
    public bool? Suppress => _original.Suppress;
    public IDiscordVoiceStateOptions WithSuppress(bool? suppress = true) => new DiscordVoiceStateOptions(_original.WithSuppress(suppress));
}


public class DiscordWebhook : IDiscordWebhook
{
    private readonly NetCord.Rest.Webhook _original;
    public DiscordWebhook(NetCord.Rest.Webhook original)
    {
        _original = original;
    }
    public NetCord.Rest.Webhook Original => _original;
    public ulong Id => _original.Id;
    public NetCord.Rest.WebhookType Type => _original.Type;
    public ulong? GuildId => _original.GuildId;
    public ulong? ChannelId => _original.ChannelId;
    public IDiscordUser Creator => new DiscordUser(_original.Creator);
    public string Name => _original.Name;
    public string AvatarHash => _original.AvatarHash;
    public ulong? ApplicationId => _original.ApplicationId;
    public IDiscordRestGuild Guild => new DiscordRestGuild(_original.Guild);
    public IDiscordChannel Channel => new DiscordChannel(_original.Channel);
    public string Url => _original.Url;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordWebhook> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordWebhook(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordWebhook> ModifyAsync(Action<IDiscordWebhookOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordWebhook(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAsync(properties.Original, cancellationToken);
}


public class DiscordMessageProperties : IDiscordMessageProperties
{
    private readonly NetCord.Rest.MessageProperties _original;
    public DiscordMessageProperties(NetCord.Rest.MessageProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.MessageProperties Original => _original;
    public string Content => _original.Content;
    public IDiscordNonceProperties Nonce => new DiscordNonceProperties(_original.Nonce);
    public bool Tts => _original.Tts;
    public IEnumerable<IDiscordAttachmentProperties> Attachments => _original.Attachments.Select(x => new DiscordAttachmentProperties(x));
    public IEnumerable<IDiscordEmbedProperties> Embeds => _original.Embeds.Select(x => new DiscordEmbedProperties(x));
    public IDiscordAllowedMentionsProperties AllowedMentions => new DiscordAllowedMentionsProperties(_original.AllowedMentions);
    public IDiscordMessageReferenceProperties MessageReference => new DiscordMessageReferenceProperties(_original.MessageReference);
    public IEnumerable<IDiscordComponentProperties> Components => _original.Components.Select(x => new DiscordComponentProperties(x));
    public IEnumerable<ulong> StickerIds => _original.StickerIds;
    public NetCord.MessageFlags? Flags => _original.Flags;
    public IDiscordMessagePollProperties Poll => new DiscordMessagePollProperties(_original.Poll);
    public HttpContent Serialize() => _original.Serialize();
    public IDiscordMessageProperties WithContent(string content) => new DiscordMessageProperties(_original.WithContent(content));
    public IDiscordMessageProperties WithNonce(IDiscordNonceProperties nonce) => new DiscordMessageProperties(_original.WithNonce(nonce.Original));
    public IDiscordMessageProperties WithTts(bool tts = true) => new DiscordMessageProperties(_original.WithTts(tts));
    public IDiscordMessageProperties WithAttachments(IEnumerable<IDiscordAttachmentProperties> attachments) => new DiscordMessageProperties(_original.WithAttachments(attachments?.Select(x => x.Original)));
    public IDiscordMessageProperties AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments) => new DiscordMessageProperties(_original.AddAttachments(attachments?.Select(x => x.Original)));
    public IDiscordMessageProperties AddAttachments(IDiscordAttachmentProperties[] attachments) => new DiscordMessageProperties(_original.AddAttachments(attachments.Original));
    public IDiscordMessageProperties WithEmbeds(IEnumerable<IDiscordEmbedProperties> embeds) => new DiscordMessageProperties(_original.WithEmbeds(embeds?.Select(x => x.Original)));
    public IDiscordMessageProperties AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds) => new DiscordMessageProperties(_original.AddEmbeds(embeds?.Select(x => x.Original)));
    public IDiscordMessageProperties AddEmbeds(IDiscordEmbedProperties[] embeds) => new DiscordMessageProperties(_original.AddEmbeds(embeds.Original));
    public IDiscordMessageProperties WithAllowedMentions(IDiscordAllowedMentionsProperties allowedMentions) => new DiscordMessageProperties(_original.WithAllowedMentions(allowedMentions.Original));
    public IDiscordMessageProperties WithMessageReference(IDiscordMessageReferenceProperties messageReference) => new DiscordMessageProperties(_original.WithMessageReference(messageReference.Original));
    public IDiscordMessageProperties WithComponents(IEnumerable<IDiscordComponentProperties> components) => new DiscordMessageProperties(_original.WithComponents(components?.Select(x => x.Original)));
    public IDiscordMessageProperties AddComponents(IEnumerable<IDiscordComponentProperties> components) => new DiscordMessageProperties(_original.AddComponents(components?.Select(x => x.Original)));
    public IDiscordMessageProperties AddComponents(IDiscordComponentProperties[] components) => new DiscordMessageProperties(_original.AddComponents(components.Original));
    public IDiscordMessageProperties WithStickerIds(IEnumerable<ulong> stickerIds) => new DiscordMessageProperties(_original.WithStickerIds(stickerIds));
    public IDiscordMessageProperties AddStickerIds(IEnumerable<ulong> stickerIds) => new DiscordMessageProperties(_original.AddStickerIds(stickerIds));
    public IDiscordMessageProperties AddStickerIds(ulong[] stickerIds) => new DiscordMessageProperties(_original.AddStickerIds(stickerIds));
    public IDiscordMessageProperties WithFlags(NetCord.MessageFlags? flags) => new DiscordMessageProperties(_original.WithFlags(flags));
    public IDiscordMessageProperties WithPoll(IDiscordMessagePollProperties poll) => new DiscordMessageProperties(_original.WithPoll(poll.Original));
}


public class DiscordReactionEmojiProperties : IDiscordReactionEmojiProperties
{
    private readonly NetCord.Rest.ReactionEmojiProperties _original;
    public DiscordReactionEmojiProperties(NetCord.Rest.ReactionEmojiProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ReactionEmojiProperties Original => _original;
    public string Name => _original.Name;
    public ulong? Id => _original.Id;
    public IDiscordReactionEmojiProperties WithName(string name) => new DiscordReactionEmojiProperties(_original.WithName(name));
    public IDiscordReactionEmojiProperties WithId(ulong? id) => new DiscordReactionEmojiProperties(_original.WithId(id));
}


public class DiscordMessageReactionsPaginationProperties : IDiscordMessageReactionsPaginationProperties
{
    private readonly NetCord.Rest.MessageReactionsPaginationProperties _original;
    public DiscordMessageReactionsPaginationProperties(NetCord.Rest.MessageReactionsPaginationProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.MessageReactionsPaginationProperties Original => _original;
    public NetCord.ReactionType? Type => _original.Type;
    public ulong? From => _original.From;
    public NetCord.Rest.PaginationDirection? Direction => _original.Direction;
    public int? BatchSize => _original.BatchSize;
    public IDiscordMessageReactionsPaginationProperties WithType(NetCord.ReactionType? type) => new DiscordMessageReactionsPaginationProperties(_original.WithType(type));
    public IDiscordMessageReactionsPaginationProperties WithFrom(ulong? from) => new DiscordMessageReactionsPaginationProperties(_original.WithFrom(from));
    public IDiscordMessageReactionsPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction) => new DiscordMessageReactionsPaginationProperties(_original.WithDirection(direction));
    public IDiscordMessageReactionsPaginationProperties WithBatchSize(int? batchSize) => new DiscordMessageReactionsPaginationProperties(_original.WithBatchSize(batchSize));
}


public class DiscordGoogleCloudPlatformStorageBucket : IDiscordGoogleCloudPlatformStorageBucket
{
    private readonly NetCord.Rest.GoogleCloudPlatformStorageBucket _original;
    public DiscordGoogleCloudPlatformStorageBucket(NetCord.Rest.GoogleCloudPlatformStorageBucket original)
    {
        _original = original;
    }
    public NetCord.Rest.GoogleCloudPlatformStorageBucket Original => _original;
    public long? Id => _original.Id;
    public string UploadUrl => _original.UploadUrl;
    public string UploadFileName => _original.UploadFileName;
}


public class DiscordGoogleCloudPlatformStorageBucketProperties : IDiscordGoogleCloudPlatformStorageBucketProperties
{
    private readonly NetCord.Rest.GoogleCloudPlatformStorageBucketProperties _original;
    public DiscordGoogleCloudPlatformStorageBucketProperties(NetCord.Rest.GoogleCloudPlatformStorageBucketProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GoogleCloudPlatformStorageBucketProperties Original => _original;
    public string FileName => _original.FileName;
    public long FileSize => _original.FileSize;
    public long? Id => _original.Id;
    public IDiscordGoogleCloudPlatformStorageBucketProperties WithFileName(string fileName) => new DiscordGoogleCloudPlatformStorageBucketProperties(_original.WithFileName(fileName));
    public IDiscordGoogleCloudPlatformStorageBucketProperties WithFileSize(long fileSize) => new DiscordGoogleCloudPlatformStorageBucketProperties(_original.WithFileSize(fileSize));
    public IDiscordGoogleCloudPlatformStorageBucketProperties WithId(long? id) => new DiscordGoogleCloudPlatformStorageBucketProperties(_original.WithId(id));
}


public class DiscordAvatarDecorationData : IDiscordAvatarDecorationData
{
    private readonly NetCord.AvatarDecorationData _original;
    public DiscordAvatarDecorationData(NetCord.AvatarDecorationData original)
    {
        _original = original;
    }
    public NetCord.AvatarDecorationData Original => _original;
    public string Hash => _original.Hash;
    public ulong SkuId => _original.SkuId;
}


public class DiscordDMChannel : IDiscordDMChannel
{
    private readonly NetCord.DMChannel _original;
    public DiscordDMChannel(NetCord.DMChannel original)
    {
        _original = original;
    }
    public NetCord.DMChannel Original => _original;
    public IReadOnlyDictionary<ulong, IDiscordUser> Users => _original.Users.ToDictionary(kv => kv.Key, kv => (IDiscordUser)new DiscordUser(kv.Value));
    public ulong? LastMessageId => _original.LastMessageId;
    public System.DateTimeOffset? LastPin => _original.LastPin;
    public ulong Id => _original.Id;
    public NetCord.ChannelFlags Flags => _original.Flags;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordDMChannel> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordDMChannel(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordDMChannel> DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordDMChannel(await _original.DeleteAsync(properties.Original, cancellationToken));
    public async IAsyncEnumerable<IDiscordRestMessage> GetMessagesAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetMessagesAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordRestMessage(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetMessagesAroundAsync(ulong messageId, int? limit = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetMessagesAroundAsync(messageId, limit, properties.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    public async Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.GetMessageAsync(messageId, properties.Original, cancellationToken));
    public async Task<IDiscordRestMessage> SendMessageAsync(IDiscordMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.SendMessageAsync(message.Original, properties.Original, cancellationToken));
    public Task AddMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.AddMessageReactionAsync(messageId, emoji.Original, properties.Original, cancellationToken);
    public Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessageReactionAsync(messageId, emoji.Original, properties.Original, cancellationToken);
    public Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessageReactionAsync(messageId, emoji.Original, userId, properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordUser> GetMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordMessageReactionsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetMessageReactionsAsync(messageId, emoji.Original, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAllMessageReactionsAsync(messageId, properties.Original, cancellationToken);
    public Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAllMessageReactionsAsync(messageId, emoji.Original, properties.Original, cancellationToken);
    public async Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.ModifyMessageAsync(messageId, action, properties.Original, cancellationToken));
    public Task DeleteMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessageAsync(messageId, properties.Original, cancellationToken);
    public Task DeleteMessagesAsync(IEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessagesAsync(messageIds, properties.Original, cancellationToken);
    public Task DeleteMessagesAsync(IAsyncEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessagesAsync(messageIds, properties.Original, cancellationToken);
    public Task TriggerTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.TriggerTypingStateAsync(properties.Original, cancellationToken);
    public Task<IDisposable> EnterTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.EnterTypingStateAsync(properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetPinnedMessagesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetPinnedMessagesAsync(properties.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    public Task PinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.PinMessageAsync(messageId, properties.Original, cancellationToken);
    public Task UnpinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.UnpinMessageAsync(messageId, properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordUser> GetMessagePollAnswerVotersAsync(ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetMessagePollAnswerVotersAsync(messageId, answerId, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public async Task<IDiscordRestMessage> EndMessagePollAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.EndMessagePollAsync(messageId, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.CreateGoogleCloudPlatformStorageBucketsAsync(buckets?.Select(x => x.Original), properties.Original, cancellationToken)).Select(x => new DiscordGoogleCloudPlatformStorageBucket(x)).ToList();
}


public class DiscordGuildChannelMention : IDiscordGuildChannelMention
{
    private readonly NetCord.GuildChannelMention _original;
    public DiscordGuildChannelMention(NetCord.GuildChannelMention original)
    {
        _original = original;
    }
    public NetCord.GuildChannelMention Original => _original;
    public ulong Id => _original.Id;
    public ulong GuildId => _original.GuildId;
    public NetCord.ChannelType Type => _original.Type;
    public string Name => _original.Name;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordAttachment : IDiscordAttachment
{
    private readonly NetCord.Attachment _original;
    public DiscordAttachment(NetCord.Attachment original)
    {
        _original = original;
    }
    public NetCord.Attachment Original => _original;
    public ulong Id => _original.Id;
    public string FileName => _original.FileName;
    public string Title => _original.Title;
    public string Description => _original.Description;
    public string ContentType => _original.ContentType;
    public int Size => _original.Size;
    public string Url => _original.Url;
    public string ProxyUrl => _original.ProxyUrl;
    public bool Ephemeral => _original.Ephemeral;
    public NetCord.AttachmentFlags Flags => _original.Flags;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordAttachmentExpirationInfo GetExpirationInfo() => new DiscordAttachmentExpirationInfo(_original.GetExpirationInfo());
}


public class DiscordEmbed : IDiscordEmbed
{
    private readonly NetCord.Embed _original;
    public DiscordEmbed(NetCord.Embed original)
    {
        _original = original;
    }
    public NetCord.Embed Original => _original;
    public string Title => _original.Title;
    public NetCord.EmbedType? Type => _original.Type;
    public string Description => _original.Description;
    public string Url => _original.Url;
    public System.DateTimeOffset? Timestamp => _original.Timestamp;
    public NetCord.Color? Color => _original.Color;
    public IDiscordEmbedFooter Footer => new DiscordEmbedFooter(_original.Footer);
    public IDiscordEmbedImage Image => new DiscordEmbedImage(_original.Image);
    public IDiscordEmbedThumbnail Thumbnail => new DiscordEmbedThumbnail(_original.Thumbnail);
    public IDiscordEmbedVideo Video => new DiscordEmbedVideo(_original.Video);
    public IDiscordEmbedProvider Provider => new DiscordEmbedProvider(_original.Provider);
    public IDiscordEmbedAuthor Author => new DiscordEmbedAuthor(_original.Author);
    public IReadOnlyList<IDiscordEmbedField> Fields => _original.Fields.Select(x => new DiscordEmbedField(x)).ToList();
}


public class DiscordMessageReaction : IDiscordMessageReaction
{
    private readonly NetCord.MessageReaction _original;
    public DiscordMessageReaction(NetCord.MessageReaction original)
    {
        _original = original;
    }
    public NetCord.MessageReaction Original => _original;
    public int Count => _original.Count;
    public IDiscordMessageReactionCountDetails CountDetails => new DiscordMessageReactionCountDetails(_original.CountDetails);
    public bool Me => _original.Me;
    public bool MeBurst => _original.MeBurst;
    public IDiscordMessageReactionEmoji Emoji => new DiscordMessageReactionEmoji(_original.Emoji);
    public IReadOnlyList<NetCord.Color> BurstColors => _original.BurstColors;
}


public class DiscordMessageActivity : IDiscordMessageActivity
{
    private readonly NetCord.MessageActivity _original;
    public DiscordMessageActivity(NetCord.MessageActivity original)
    {
        _original = original;
    }
    public NetCord.MessageActivity Original => _original;
    public NetCord.MessageActivityType Type => _original.Type;
    public string PartyId => _original.PartyId;
}


public class DiscordApplication : IDiscordApplication
{
    private readonly NetCord.Application _original;
    public DiscordApplication(NetCord.Application original)
    {
        _original = original;
    }
    public NetCord.Application Original => _original;
    public ulong Id => _original.Id;
    public string Name => _original.Name;
    public string IconHash => _original.IconHash;
    public string Description => _original.Description;
    public IReadOnlyList<string> RpcOrigins => _original.RpcOrigins;
    public bool? BotPublic => _original.BotPublic;
    public bool? BotRequireCodeGrant => _original.BotRequireCodeGrant;
    public IDiscordUser Bot => new DiscordUser(_original.Bot);
    public string TermsOfServiceUrl => _original.TermsOfServiceUrl;
    public string PrivacyPolicyUrl => _original.PrivacyPolicyUrl;
    public IDiscordUser Owner => new DiscordUser(_original.Owner);
    public string VerifyKey => _original.VerifyKey;
    public IDiscordTeam Team => new DiscordTeam(_original.Team);
    public ulong? GuildId => _original.GuildId;
    public IDiscordRestGuild Guild => new DiscordRestGuild(_original.Guild);
    public ulong? PrimarySkuId => _original.PrimarySkuId;
    public string Slug => _original.Slug;
    public string CoverImageHash => _original.CoverImageHash;
    public NetCord.ApplicationFlags? Flags => _original.Flags;
    public int? ApproximateGuildCount => _original.ApproximateGuildCount;
    public int? ApproximateUserInstallCount => _original.ApproximateUserInstallCount;
    public IReadOnlyList<string> RedirectUris => _original.RedirectUris;
    public string InteractionsEndpointUrl => _original.InteractionsEndpointUrl;
    public string RoleConnectionsVerificationUrl => _original.RoleConnectionsVerificationUrl;
    public IReadOnlyList<string> Tags => _original.Tags;
    public IDiscordApplicationInstallParams InstallParams => new DiscordApplicationInstallParams(_original.InstallParams);
    public IReadOnlyDictionary<NetCord.ApplicationIntegrationType, IDiscordApplicationIntegrationTypeConfiguration> IntegrationTypesConfiguration => _original.IntegrationTypesConfiguration.ToDictionary(kv => kv.Key, kv => (IDiscordApplicationIntegrationTypeConfiguration)new DiscordApplicationIntegrationTypeConfiguration(kv.Value));
    public string CustomInstallUrl => _original.CustomInstallUrl;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordImageUrl GetIconUrl(NetCord.ImageFormat format) => new DiscordImageUrl(_original.GetIconUrl(format));
    public IDiscordImageUrl GetCoverUrl(NetCord.ImageFormat format) => new DiscordImageUrl(_original.GetCoverUrl(format));
    public IDiscordImageUrl GetAssetUrl(ulong assetId, NetCord.ImageFormat format) => new DiscordImageUrl(_original.GetAssetUrl(assetId, format));
    public IDiscordImageUrl GetAchievementIconUrl(ulong achievementId, string iconHash, NetCord.ImageFormat format) => new DiscordImageUrl(_original.GetAchievementIconUrl(achievementId, iconHash, format));
    public IDiscordImageUrl GetStorePageAssetUrl(ulong assetId, NetCord.ImageFormat format) => new DiscordImageUrl(_original.GetStorePageAssetUrl(assetId, format));
    public async Task<IReadOnlyList<IDiscordApplicationEmoji>> GetEmojisAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetEmojisAsync(properties.Original, cancellationToken)).Select(x => new DiscordApplicationEmoji(x)).ToList();
    public async Task<IDiscordApplicationEmoji> GetEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationEmoji(await _original.GetEmojiAsync(emojiId, properties.Original, cancellationToken));
    public async Task<IDiscordApplicationEmoji> CreateEmojiAsync(IDiscordApplicationEmojiProperties applicationEmojiProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationEmoji(await _original.CreateEmojiAsync(applicationEmojiProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordApplicationEmoji> ModifyEmojiAsync(ulong emojiId, Action<IDiscordApplicationEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationEmoji(await _original.ModifyEmojiAsync(emojiId, action, properties.Original, cancellationToken));
    public Task DeleteEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteEmojiAsync(emojiId, properties.Original, cancellationToken);
    public async Task<IDiscordApplication> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplication(await _original.GetAsync(properties.Original, cancellationToken));
}


public class DiscordMessageReference : IDiscordMessageReference
{
    private readonly NetCord.MessageReference _original;
    public DiscordMessageReference(NetCord.MessageReference original)
    {
        _original = original;
    }
    public NetCord.MessageReference Original => _original;
    public ulong MessageId => _original.MessageId;
    public ulong ChannelId => _original.ChannelId;
    public ulong? GuildId => _original.GuildId;
    public bool? FailIfNotExists => _original.FailIfNotExists;
}


public class DiscordMessageSnapshot : IDiscordMessageSnapshot
{
    private readonly NetCord.MessageSnapshot _original;
    public DiscordMessageSnapshot(NetCord.MessageSnapshot original)
    {
        _original = original;
    }
    public NetCord.MessageSnapshot Original => _original;
    public IDiscordMessageSnapshotMessage Message => new DiscordMessageSnapshotMessage(_original.Message);
}


public class DiscordMessageInteractionMetadata : IDiscordMessageInteractionMetadata
{
    private readonly NetCord.MessageInteractionMetadata _original;
    public DiscordMessageInteractionMetadata(NetCord.MessageInteractionMetadata original)
    {
        _original = original;
    }
    public NetCord.MessageInteractionMetadata Original => _original;
    public ulong Id => _original.Id;
    public NetCord.InteractionType Type => _original.Type;
    public IDiscordUser User => new DiscordUser(_original.User);
    public IReadOnlyDictionary<NetCord.ApplicationIntegrationType, ulong> AuthorizingIntegrationOwners => _original.AuthorizingIntegrationOwners;
    public ulong? OriginalResponseMessageId => _original.OriginalResponseMessageId;
    public ulong? InteractedMessageId => _original.InteractedMessageId;
    public IDiscordMessageInteractionMetadata TriggeringInteractionMetadata => new DiscordMessageInteractionMetadata(_original.TriggeringInteractionMetadata);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordMessageInteraction : IDiscordMessageInteraction
{
    private readonly NetCord.MessageInteraction _original;
    public DiscordMessageInteraction(NetCord.MessageInteraction original)
    {
        _original = original;
    }
    public NetCord.MessageInteraction Original => _original;
    public ulong Id => _original.Id;
    public NetCord.InteractionType Type => _original.Type;
    public string Name => _original.Name;
    public IDiscordUser User => new DiscordUser(_original.User);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordComponent : IDiscordComponent
{
    private readonly NetCord.IComponent _original;
    public DiscordComponent(NetCord.IComponent original)
    {
        _original = original;
    }
    public NetCord.IComponent Original => _original;
    public int Id => _original.Id;
}


public class DiscordMessageSticker : IDiscordMessageSticker
{
    private readonly NetCord.MessageSticker _original;
    public DiscordMessageSticker(NetCord.MessageSticker original)
    {
        _original = original;
    }
    public NetCord.MessageSticker Original => _original;
    public ulong Id => _original.Id;
    public string Name => _original.Name;
    public NetCord.StickerFormat Format => _original.Format;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordRoleSubscriptionData : IDiscordRoleSubscriptionData
{
    private readonly NetCord.RoleSubscriptionData _original;
    public DiscordRoleSubscriptionData(NetCord.RoleSubscriptionData original)
    {
        _original = original;
    }
    public NetCord.RoleSubscriptionData Original => _original;
    public ulong RoleSubscriptionListingId => _original.RoleSubscriptionListingId;
    public string TierName => _original.TierName;
    public int TotalMonthsSubscribed => _original.TotalMonthsSubscribed;
    public bool IsRenewal => _original.IsRenewal;
}


public class DiscordInteractionResolvedData : IDiscordInteractionResolvedData
{
    private readonly NetCord.InteractionResolvedData _original;
    public DiscordInteractionResolvedData(NetCord.InteractionResolvedData original)
    {
        _original = original;
    }
    public NetCord.InteractionResolvedData Original => _original;
    public IReadOnlyDictionary<ulong, IDiscordUser> Users => _original.Users.ToDictionary(kv => kv.Key, kv => (IDiscordUser)new DiscordUser(kv.Value));
    public IReadOnlyDictionary<ulong, IDiscordRole> Roles => _original.Roles.ToDictionary(kv => kv.Key, kv => (IDiscordRole)new DiscordRole(kv.Value));
    public IReadOnlyDictionary<ulong, IDiscordChannel> Channels => _original.Channels.ToDictionary(kv => kv.Key, kv => (IDiscordChannel)new DiscordChannel(kv.Value));
    public IReadOnlyDictionary<ulong, IDiscordAttachment> Attachments => _original.Attachments.ToDictionary(kv => kv.Key, kv => (IDiscordAttachment)new DiscordAttachment(kv.Value));
}


public class DiscordMessagePoll : IDiscordMessagePoll
{
    private readonly NetCord.MessagePoll _original;
    public DiscordMessagePoll(NetCord.MessagePoll original)
    {
        _original = original;
    }
    public NetCord.MessagePoll Original => _original;
    public IDiscordMessagePollMedia Question => new DiscordMessagePollMedia(_original.Question);
    public IReadOnlyList<IDiscordMessagePollAnswer> Answers => _original.Answers.Select(x => new DiscordMessagePollAnswer(x)).ToList();
    public System.DateTimeOffset? ExpiresAt => _original.ExpiresAt;
    public bool AllowMultiselect => _original.AllowMultiselect;
    public NetCord.MessagePollLayoutType LayoutType => _original.LayoutType;
    public IDiscordMessagePollResults Results => new DiscordMessagePollResults(_original.Results);
}


public class DiscordMessageCall : IDiscordMessageCall
{
    private readonly NetCord.Rest.MessageCall _original;
    public DiscordMessageCall(NetCord.Rest.MessageCall original)
    {
        _original = original;
    }
    public NetCord.Rest.MessageCall Original => _original;
    public IReadOnlyList<ulong> Participants => _original.Participants;
    public System.DateTimeOffset? EndedAt => _original.EndedAt;
}


public class DiscordReplyMessageProperties : IDiscordReplyMessageProperties
{
    private readonly NetCord.Rest.ReplyMessageProperties _original;
    public DiscordReplyMessageProperties(NetCord.Rest.ReplyMessageProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ReplyMessageProperties Original => _original;
    public string Content => _original.Content;
    public IDiscordNonceProperties Nonce => new DiscordNonceProperties(_original.Nonce);
    public bool Tts => _original.Tts;
    public IEnumerable<IDiscordAttachmentProperties> Attachments => _original.Attachments.Select(x => new DiscordAttachmentProperties(x));
    public IEnumerable<IDiscordEmbedProperties> Embeds => _original.Embeds.Select(x => new DiscordEmbedProperties(x));
    public IDiscordAllowedMentionsProperties AllowedMentions => new DiscordAllowedMentionsProperties(_original.AllowedMentions);
    public bool? FailIfNotExists => _original.FailIfNotExists;
    public IEnumerable<IDiscordComponentProperties> Components => _original.Components.Select(x => new DiscordComponentProperties(x));
    public IEnumerable<ulong> StickerIds => _original.StickerIds;
    public NetCord.MessageFlags? Flags => _original.Flags;
    public IDiscordMessagePollProperties Poll => new DiscordMessagePollProperties(_original.Poll);
    public IDiscordMessageProperties ToMessageProperties(ulong messageReferenceId) => new DiscordMessageProperties(_original.ToMessageProperties(messageReferenceId));
    public IDiscordReplyMessageProperties WithContent(string content) => new DiscordReplyMessageProperties(_original.WithContent(content));
    public IDiscordReplyMessageProperties WithNonce(IDiscordNonceProperties nonce) => new DiscordReplyMessageProperties(_original.WithNonce(nonce.Original));
    public IDiscordReplyMessageProperties WithTts(bool tts = true) => new DiscordReplyMessageProperties(_original.WithTts(tts));
    public IDiscordReplyMessageProperties WithAttachments(IEnumerable<IDiscordAttachmentProperties> attachments) => new DiscordReplyMessageProperties(_original.WithAttachments(attachments?.Select(x => x.Original)));
    public IDiscordReplyMessageProperties AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments) => new DiscordReplyMessageProperties(_original.AddAttachments(attachments?.Select(x => x.Original)));
    public IDiscordReplyMessageProperties AddAttachments(IDiscordAttachmentProperties[] attachments) => new DiscordReplyMessageProperties(_original.AddAttachments(attachments.Original));
    public IDiscordReplyMessageProperties WithEmbeds(IEnumerable<IDiscordEmbedProperties> embeds) => new DiscordReplyMessageProperties(_original.WithEmbeds(embeds?.Select(x => x.Original)));
    public IDiscordReplyMessageProperties AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds) => new DiscordReplyMessageProperties(_original.AddEmbeds(embeds?.Select(x => x.Original)));
    public IDiscordReplyMessageProperties AddEmbeds(IDiscordEmbedProperties[] embeds) => new DiscordReplyMessageProperties(_original.AddEmbeds(embeds.Original));
    public IDiscordReplyMessageProperties WithAllowedMentions(IDiscordAllowedMentionsProperties allowedMentions) => new DiscordReplyMessageProperties(_original.WithAllowedMentions(allowedMentions.Original));
    public IDiscordReplyMessageProperties WithFailIfNotExists(bool? failIfNotExists = true) => new DiscordReplyMessageProperties(_original.WithFailIfNotExists(failIfNotExists));
    public IDiscordReplyMessageProperties WithComponents(IEnumerable<IDiscordComponentProperties> components) => new DiscordReplyMessageProperties(_original.WithComponents(components?.Select(x => x.Original)));
    public IDiscordReplyMessageProperties AddComponents(IEnumerable<IDiscordComponentProperties> components) => new DiscordReplyMessageProperties(_original.AddComponents(components?.Select(x => x.Original)));
    public IDiscordReplyMessageProperties AddComponents(IDiscordComponentProperties[] components) => new DiscordReplyMessageProperties(_original.AddComponents(components.Original));
    public IDiscordReplyMessageProperties WithStickerIds(IEnumerable<ulong> stickerIds) => new DiscordReplyMessageProperties(_original.WithStickerIds(stickerIds));
    public IDiscordReplyMessageProperties AddStickerIds(IEnumerable<ulong> stickerIds) => new DiscordReplyMessageProperties(_original.AddStickerIds(stickerIds));
    public IDiscordReplyMessageProperties AddStickerIds(ulong[] stickerIds) => new DiscordReplyMessageProperties(_original.AddStickerIds(stickerIds));
    public IDiscordReplyMessageProperties WithFlags(NetCord.MessageFlags? flags) => new DiscordReplyMessageProperties(_original.WithFlags(flags));
    public IDiscordReplyMessageProperties WithPoll(IDiscordMessagePollProperties poll) => new DiscordReplyMessageProperties(_original.WithPoll(poll.Original));
}


public class DiscordGuildThreadFromMessageProperties : IDiscordGuildThreadFromMessageProperties
{
    private readonly NetCord.Rest.GuildThreadFromMessageProperties _original;
    public DiscordGuildThreadFromMessageProperties(NetCord.Rest.GuildThreadFromMessageProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildThreadFromMessageProperties Original => _original;
    public string Name => _original.Name;
    public NetCord.ThreadArchiveDuration? AutoArchiveDuration => _original.AutoArchiveDuration;
    public int? Slowmode => _original.Slowmode;
    public IDiscordGuildThreadFromMessageProperties WithName(string name) => new DiscordGuildThreadFromMessageProperties(_original.WithName(name));
    public IDiscordGuildThreadFromMessageProperties WithAutoArchiveDuration(NetCord.ThreadArchiveDuration? autoArchiveDuration) => new DiscordGuildThreadFromMessageProperties(_original.WithAutoArchiveDuration(autoArchiveDuration));
    public IDiscordGuildThreadFromMessageProperties WithSlowmode(int? slowmode) => new DiscordGuildThreadFromMessageProperties(_original.WithSlowmode(slowmode));
}


public class DiscordEmbedProperties : IDiscordEmbedProperties
{
    private readonly NetCord.Rest.EmbedProperties _original;
    public DiscordEmbedProperties(NetCord.Rest.EmbedProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.EmbedProperties Original => _original;
    public string Title => _original.Title;
    public string Description => _original.Description;
    public string Url => _original.Url;
    public System.DateTimeOffset? Timestamp => _original.Timestamp;
    public NetCord.Color Color => _original.Color;
    public IDiscordEmbedFooterProperties Footer => new DiscordEmbedFooterProperties(_original.Footer);
    public IDiscordEmbedImageProperties Image => new DiscordEmbedImageProperties(_original.Image);
    public IDiscordEmbedThumbnailProperties Thumbnail => new DiscordEmbedThumbnailProperties(_original.Thumbnail);
    public IDiscordEmbedAuthorProperties Author => new DiscordEmbedAuthorProperties(_original.Author);
    public IEnumerable<IDiscordEmbedFieldProperties> Fields => _original.Fields.Select(x => new DiscordEmbedFieldProperties(x));
    public IDiscordEmbedProperties WithTitle(string title) => new DiscordEmbedProperties(_original.WithTitle(title));
    public IDiscordEmbedProperties WithDescription(string description) => new DiscordEmbedProperties(_original.WithDescription(description));
    public IDiscordEmbedProperties WithUrl(string url) => new DiscordEmbedProperties(_original.WithUrl(url));
    public IDiscordEmbedProperties WithTimestamp(System.DateTimeOffset? timestamp) => new DiscordEmbedProperties(_original.WithTimestamp(timestamp));
    public IDiscordEmbedProperties WithColor(NetCord.Color color) => new DiscordEmbedProperties(_original.WithColor(color));
    public IDiscordEmbedProperties WithFooter(IDiscordEmbedFooterProperties footer) => new DiscordEmbedProperties(_original.WithFooter(footer.Original));
    public IDiscordEmbedProperties WithImage(IDiscordEmbedImageProperties image) => new DiscordEmbedProperties(_original.WithImage(image.Original));
    public IDiscordEmbedProperties WithThumbnail(IDiscordEmbedThumbnailProperties thumbnail) => new DiscordEmbedProperties(_original.WithThumbnail(thumbnail.Original));
    public IDiscordEmbedProperties WithAuthor(IDiscordEmbedAuthorProperties author) => new DiscordEmbedProperties(_original.WithAuthor(author.Original));
    public IDiscordEmbedProperties WithFields(IEnumerable<IDiscordEmbedFieldProperties> fields) => new DiscordEmbedProperties(_original.WithFields(fields?.Select(x => x.Original)));
    public IDiscordEmbedProperties AddFields(IEnumerable<IDiscordEmbedFieldProperties> fields) => new DiscordEmbedProperties(_original.AddFields(fields?.Select(x => x.Original)));
    public IDiscordEmbedProperties AddFields(IDiscordEmbedFieldProperties[] fields) => new DiscordEmbedProperties(_original.AddFields(fields.Original));
}


public class DiscordAllowedMentionsProperties : IDiscordAllowedMentionsProperties
{
    private readonly NetCord.Rest.AllowedMentionsProperties _original;
    public DiscordAllowedMentionsProperties(NetCord.Rest.AllowedMentionsProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.AllowedMentionsProperties Original => _original;
    public bool Everyone => _original.Everyone;
    public IEnumerable<ulong> AllowedRoles => _original.AllowedRoles;
    public IEnumerable<ulong> AllowedUsers => _original.AllowedUsers;
    public bool ReplyMention => _original.ReplyMention;
    public static IDiscordAllowedMentionsProperties All => new DiscordAllowedMentionsProperties(NetCord.Rest.AllowedMentionsProperties.All);
    public static IDiscordAllowedMentionsProperties None => new DiscordAllowedMentionsProperties(NetCord.Rest.AllowedMentionsProperties.None);
    public IDiscordAllowedMentionsProperties WithEveryone(bool everyone = true) => new DiscordAllowedMentionsProperties(_original.WithEveryone(everyone));
    public IDiscordAllowedMentionsProperties WithAllowedRoles(IEnumerable<ulong> allowedRoles) => new DiscordAllowedMentionsProperties(_original.WithAllowedRoles(allowedRoles));
    public IDiscordAllowedMentionsProperties AddAllowedRoles(IEnumerable<ulong> allowedRoles) => new DiscordAllowedMentionsProperties(_original.AddAllowedRoles(allowedRoles));
    public IDiscordAllowedMentionsProperties AddAllowedRoles(ulong[] allowedRoles) => new DiscordAllowedMentionsProperties(_original.AddAllowedRoles(allowedRoles));
    public IDiscordAllowedMentionsProperties WithAllowedUsers(IEnumerable<ulong> allowedUsers) => new DiscordAllowedMentionsProperties(_original.WithAllowedUsers(allowedUsers));
    public IDiscordAllowedMentionsProperties AddAllowedUsers(IEnumerable<ulong> allowedUsers) => new DiscordAllowedMentionsProperties(_original.AddAllowedUsers(allowedUsers));
    public IDiscordAllowedMentionsProperties AddAllowedUsers(ulong[] allowedUsers) => new DiscordAllowedMentionsProperties(_original.AddAllowedUsers(allowedUsers));
    public IDiscordAllowedMentionsProperties WithReplyMention(bool replyMention = true) => new DiscordAllowedMentionsProperties(_original.WithReplyMention(replyMention));
}


public class DiscordComponentProperties : IDiscordComponentProperties
{
    private readonly NetCord.Rest.IComponentProperties _original;
    public DiscordComponentProperties(NetCord.Rest.IComponentProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.IComponentProperties Original => _original;
    public int? Id => _original.Id;
    public NetCord.ComponentType ComponentType => _original.ComponentType;
    public IDiscordComponentProperties WithId(int? id) => new DiscordComponentProperties(_original.WithId(id));
}


public class DiscordAttachmentProperties : IDiscordAttachmentProperties
{
    private readonly NetCord.Rest.AttachmentProperties _original;
    public DiscordAttachmentProperties(NetCord.Rest.AttachmentProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.AttachmentProperties Original => _original;
    public string FileName => _original.FileName;
    public string Title => _original.Title;
    public string Description => _original.Description;
    public bool SupportsHttpSerialization => _original.SupportsHttpSerialization;
    public HttpContent Serialize() => _original.Serialize();
    public IDiscordAttachmentProperties WithFileName(string fileName) => new DiscordAttachmentProperties(_original.WithFileName(fileName));
    public IDiscordAttachmentProperties WithTitle(string title) => new DiscordAttachmentProperties(_original.WithTitle(title));
    public IDiscordAttachmentProperties WithDescription(string description) => new DiscordAttachmentProperties(_original.WithDescription(description));
}


public class DiscordMessagePollProperties : IDiscordMessagePollProperties
{
    private readonly NetCord.MessagePollProperties _original;
    public DiscordMessagePollProperties(NetCord.MessagePollProperties original)
    {
        _original = original;
    }
    public NetCord.MessagePollProperties Original => _original;
    public IDiscordMessagePollMediaProperties Question => new DiscordMessagePollMediaProperties(_original.Question);
    public IEnumerable<IDiscordMessagePollAnswerProperties> Answers => _original.Answers.Select(x => new DiscordMessagePollAnswerProperties(x));
    public int? DurationInHours => _original.DurationInHours;
    public bool AllowMultiselect => _original.AllowMultiselect;
    public NetCord.MessagePollLayoutType? LayoutType => _original.LayoutType;
    public IDiscordMessagePollProperties WithQuestion(IDiscordMessagePollMediaProperties question) => new DiscordMessagePollProperties(_original.WithQuestion(question.Original));
    public IDiscordMessagePollProperties WithAnswers(IEnumerable<IDiscordMessagePollAnswerProperties> answers) => new DiscordMessagePollProperties(_original.WithAnswers(answers?.Select(x => x.Original)));
    public IDiscordMessagePollProperties AddAnswers(IEnumerable<IDiscordMessagePollAnswerProperties> answers) => new DiscordMessagePollProperties(_original.AddAnswers(answers?.Select(x => x.Original)));
    public IDiscordMessagePollProperties AddAnswers(IDiscordMessagePollAnswerProperties[] answers) => new DiscordMessagePollProperties(_original.AddAnswers(answers.Original));
    public IDiscordMessagePollProperties WithDurationInHours(int? durationInHours) => new DiscordMessagePollProperties(_original.WithDurationInHours(durationInHours));
    public IDiscordMessagePollProperties WithAllowMultiselect(bool allowMultiselect = true) => new DiscordMessagePollProperties(_original.WithAllowMultiselect(allowMultiselect));
    public IDiscordMessagePollProperties WithLayoutType(NetCord.MessagePollLayoutType? layoutType) => new DiscordMessagePollProperties(_original.WithLayoutType(layoutType));
}


public class DiscordPermissionOverwrite : IDiscordPermissionOverwrite
{
    private readonly NetCord.PermissionOverwrite _original;
    public DiscordPermissionOverwrite(NetCord.PermissionOverwrite original)
    {
        _original = original;
    }
    public NetCord.PermissionOverwrite Original => _original;
    public ulong Id => _original.Id;
    public NetCord.PermissionOverwriteType Type => _original.Type;
    public NetCord.Permissions Allowed => _original.Allowed;
    public NetCord.Permissions Denied => _original.Denied;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordGuildChannelOptions : IDiscordGuildChannelOptions
{
    private readonly NetCord.Rest.GuildChannelOptions _original;
    public DiscordGuildChannelOptions(NetCord.Rest.GuildChannelOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildChannelOptions Original => _original;
    public string Name => _original.Name;
    public NetCord.ChannelType? ChannelType => _original.ChannelType;
    public int? Position => _original.Position;
    public string Topic => _original.Topic;
    public bool? Nsfw => _original.Nsfw;
    public int? Slowmode => _original.Slowmode;
    public int? Bitrate => _original.Bitrate;
    public int? UserLimit => _original.UserLimit;
    public IEnumerable<IDiscordPermissionOverwriteProperties> PermissionOverwrites => _original.PermissionOverwrites.Select(x => new DiscordPermissionOverwriteProperties(x));
    public ulong? ParentId => _original.ParentId;
    public string RtcRegion => _original.RtcRegion;
    public NetCord.VideoQualityMode? VideoQualityMode => _original.VideoQualityMode;
    public NetCord.ThreadArchiveDuration? DefaultAutoArchiveDuration => _original.DefaultAutoArchiveDuration;
    public IEnumerable<IDiscordForumTagProperties> AvailableTags => _original.AvailableTags.Select(x => new DiscordForumTagProperties(x));
    public NetCord.Rest.ForumGuildChannelDefaultReactionProperties? DefaultReactionEmoji => _original.DefaultReactionEmoji;
    public int? DefaultThreadSlowmode => _original.DefaultThreadSlowmode;
    public NetCord.ChannelFlags? Flags => _original.Flags;
    public NetCord.SortOrderType? DefaultSortOrder => _original.DefaultSortOrder;
    public NetCord.ForumLayoutType? DefaultForumLayout => _original.DefaultForumLayout;
    public bool? Archived => _original.Archived;
    public NetCord.ThreadArchiveDuration? AutoArchiveDuration => _original.AutoArchiveDuration;
    public bool? Locked => _original.Locked;
    public bool? Invitable => _original.Invitable;
    public IEnumerable<ulong> AppliedTags => _original.AppliedTags;
    public IDiscordGuildChannelOptions WithName(string name) => new DiscordGuildChannelOptions(_original.WithName(name));
    public IDiscordGuildChannelOptions WithChannelType(NetCord.ChannelType? channelType) => new DiscordGuildChannelOptions(_original.WithChannelType(channelType));
    public IDiscordGuildChannelOptions WithPosition(int? position) => new DiscordGuildChannelOptions(_original.WithPosition(position));
    public IDiscordGuildChannelOptions WithTopic(string topic) => new DiscordGuildChannelOptions(_original.WithTopic(topic));
    public IDiscordGuildChannelOptions WithNsfw(bool? nsfw = true) => new DiscordGuildChannelOptions(_original.WithNsfw(nsfw));
    public IDiscordGuildChannelOptions WithSlowmode(int? slowmode) => new DiscordGuildChannelOptions(_original.WithSlowmode(slowmode));
    public IDiscordGuildChannelOptions WithBitrate(int? bitrate) => new DiscordGuildChannelOptions(_original.WithBitrate(bitrate));
    public IDiscordGuildChannelOptions WithUserLimit(int? userLimit) => new DiscordGuildChannelOptions(_original.WithUserLimit(userLimit));
    public IDiscordGuildChannelOptions WithPermissionOverwrites(IEnumerable<IDiscordPermissionOverwriteProperties> permissionOverwrites) => new DiscordGuildChannelOptions(_original.WithPermissionOverwrites(permissionOverwrites?.Select(x => x.Original)));
    public IDiscordGuildChannelOptions AddPermissionOverwrites(IEnumerable<IDiscordPermissionOverwriteProperties> permissionOverwrites) => new DiscordGuildChannelOptions(_original.AddPermissionOverwrites(permissionOverwrites?.Select(x => x.Original)));
    public IDiscordGuildChannelOptions AddPermissionOverwrites(IDiscordPermissionOverwriteProperties[] permissionOverwrites) => new DiscordGuildChannelOptions(_original.AddPermissionOverwrites(permissionOverwrites.Original));
    public IDiscordGuildChannelOptions WithParentId(ulong? parentId) => new DiscordGuildChannelOptions(_original.WithParentId(parentId));
    public IDiscordGuildChannelOptions WithRtcRegion(string rtcRegion) => new DiscordGuildChannelOptions(_original.WithRtcRegion(rtcRegion));
    public IDiscordGuildChannelOptions WithVideoQualityMode(NetCord.VideoQualityMode? videoQualityMode) => new DiscordGuildChannelOptions(_original.WithVideoQualityMode(videoQualityMode));
    public IDiscordGuildChannelOptions WithDefaultAutoArchiveDuration(NetCord.ThreadArchiveDuration? defaultAutoArchiveDuration) => new DiscordGuildChannelOptions(_original.WithDefaultAutoArchiveDuration(defaultAutoArchiveDuration));
    public IDiscordGuildChannelOptions WithAvailableTags(IEnumerable<IDiscordForumTagProperties> availableTags) => new DiscordGuildChannelOptions(_original.WithAvailableTags(availableTags?.Select(x => x.Original)));
    public IDiscordGuildChannelOptions AddAvailableTags(IEnumerable<IDiscordForumTagProperties> availableTags) => new DiscordGuildChannelOptions(_original.AddAvailableTags(availableTags?.Select(x => x.Original)));
    public IDiscordGuildChannelOptions AddAvailableTags(IDiscordForumTagProperties[] availableTags) => new DiscordGuildChannelOptions(_original.AddAvailableTags(availableTags.Original));
    public IDiscordGuildChannelOptions WithDefaultReactionEmoji(NetCord.Rest.ForumGuildChannelDefaultReactionProperties? defaultReactionEmoji) => new DiscordGuildChannelOptions(_original.WithDefaultReactionEmoji(defaultReactionEmoji));
    public IDiscordGuildChannelOptions WithDefaultThreadSlowmode(int? defaultThreadSlowmode) => new DiscordGuildChannelOptions(_original.WithDefaultThreadSlowmode(defaultThreadSlowmode));
    public IDiscordGuildChannelOptions WithFlags(NetCord.ChannelFlags? flags) => new DiscordGuildChannelOptions(_original.WithFlags(flags));
    public IDiscordGuildChannelOptions WithDefaultSortOrder(NetCord.SortOrderType? defaultSortOrder) => new DiscordGuildChannelOptions(_original.WithDefaultSortOrder(defaultSortOrder));
    public IDiscordGuildChannelOptions WithDefaultForumLayout(NetCord.ForumLayoutType? defaultForumLayout) => new DiscordGuildChannelOptions(_original.WithDefaultForumLayout(defaultForumLayout));
    public IDiscordGuildChannelOptions WithArchived(bool? archived = true) => new DiscordGuildChannelOptions(_original.WithArchived(archived));
    public IDiscordGuildChannelOptions WithAutoArchiveDuration(NetCord.ThreadArchiveDuration? autoArchiveDuration) => new DiscordGuildChannelOptions(_original.WithAutoArchiveDuration(autoArchiveDuration));
    public IDiscordGuildChannelOptions WithLocked(bool? locked = true) => new DiscordGuildChannelOptions(_original.WithLocked(locked));
    public IDiscordGuildChannelOptions WithInvitable(bool? invitable = true) => new DiscordGuildChannelOptions(_original.WithInvitable(invitable));
    public IDiscordGuildChannelOptions WithAppliedTags(IEnumerable<ulong> appliedTags) => new DiscordGuildChannelOptions(_original.WithAppliedTags(appliedTags));
    public IDiscordGuildChannelOptions AddAppliedTags(IEnumerable<ulong> appliedTags) => new DiscordGuildChannelOptions(_original.AddAppliedTags(appliedTags));
    public IDiscordGuildChannelOptions AddAppliedTags(ulong[] appliedTags) => new DiscordGuildChannelOptions(_original.AddAppliedTags(appliedTags));
}


public class DiscordPermissionOverwriteProperties : IDiscordPermissionOverwriteProperties
{
    private readonly NetCord.Rest.PermissionOverwriteProperties _original;
    public DiscordPermissionOverwriteProperties(NetCord.Rest.PermissionOverwriteProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.PermissionOverwriteProperties Original => _original;
    public ulong Id => _original.Id;
    public NetCord.PermissionOverwriteType Type => _original.Type;
    public NetCord.Permissions? Allowed => _original.Allowed;
    public NetCord.Permissions? Denied => _original.Denied;
    public IDiscordPermissionOverwriteProperties WithId(ulong id) => new DiscordPermissionOverwriteProperties(_original.WithId(id));
    public IDiscordPermissionOverwriteProperties WithType(NetCord.PermissionOverwriteType type) => new DiscordPermissionOverwriteProperties(_original.WithType(type));
    public IDiscordPermissionOverwriteProperties WithAllowed(NetCord.Permissions? allowed) => new DiscordPermissionOverwriteProperties(_original.WithAllowed(allowed));
    public IDiscordPermissionOverwriteProperties WithDenied(NetCord.Permissions? denied) => new DiscordPermissionOverwriteProperties(_original.WithDenied(denied));
}


public class DiscordInviteProperties : IDiscordInviteProperties
{
    private readonly NetCord.Rest.InviteProperties _original;
    public DiscordInviteProperties(NetCord.Rest.InviteProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.InviteProperties Original => _original;
    public int? MaxAge => _original.MaxAge;
    public int? MaxUses => _original.MaxUses;
    public bool? Temporary => _original.Temporary;
    public bool? Unique => _original.Unique;
    public NetCord.InviteTargetType? TargetType => _original.TargetType;
    public ulong? TargetUserId => _original.TargetUserId;
    public ulong? TargetApplicationId => _original.TargetApplicationId;
    public IDiscordInviteProperties WithMaxAge(int? maxAge) => new DiscordInviteProperties(_original.WithMaxAge(maxAge));
    public IDiscordInviteProperties WithMaxUses(int? maxUses) => new DiscordInviteProperties(_original.WithMaxUses(maxUses));
    public IDiscordInviteProperties WithTemporary(bool? temporary = true) => new DiscordInviteProperties(_original.WithTemporary(temporary));
    public IDiscordInviteProperties WithUnique(bool? unique = true) => new DiscordInviteProperties(_original.WithUnique(unique));
    public IDiscordInviteProperties WithTargetType(NetCord.InviteTargetType? targetType) => new DiscordInviteProperties(_original.WithTargetType(targetType));
    public IDiscordInviteProperties WithTargetUserId(ulong? targetUserId) => new DiscordInviteProperties(_original.WithTargetUserId(targetUserId));
    public IDiscordInviteProperties WithTargetApplicationId(ulong? targetApplicationId) => new DiscordInviteProperties(_original.WithTargetApplicationId(targetApplicationId));
}


public class DiscordGuildThreadMetadata : IDiscordGuildThreadMetadata
{
    private readonly NetCord.GuildThreadMetadata _original;
    public DiscordGuildThreadMetadata(NetCord.GuildThreadMetadata original)
    {
        _original = original;
    }
    public NetCord.GuildThreadMetadata Original => _original;
    public bool Archived => _original.Archived;
    public NetCord.ThreadArchiveDuration AutoArchiveDuration => _original.AutoArchiveDuration;
    public System.DateTimeOffset ArchiveTimestamp => _original.ArchiveTimestamp;
    public bool Locked => _original.Locked;
    public bool? Invitable => _original.Invitable;
}


public class DiscordThreadCurrentUser : IDiscordThreadCurrentUser
{
    private readonly NetCord.ThreadCurrentUser _original;
    public DiscordThreadCurrentUser(NetCord.ThreadCurrentUser original)
    {
        _original = original;
    }
    public NetCord.ThreadCurrentUser Original => _original;
    public System.DateTimeOffset JoinTimestamp => _original.JoinTimestamp;
    public int Flags => _original.Flags;
}


public class DiscordThreadUser : IDiscordThreadUser
{
    private readonly NetCord.ThreadUser _original;
    public DiscordThreadUser(NetCord.ThreadUser original)
    {
        _original = original;
    }
    public NetCord.ThreadUser Original => _original;
    public ulong Id => _original.Id;
    public ulong ThreadId => _original.ThreadId;
    public System.DateTimeOffset JoinTimestamp => _original.JoinTimestamp;
    public int Flags => _original.Flags;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordGuildThreadProperties : IDiscordGuildThreadProperties
{
    private readonly NetCord.Rest.GuildThreadProperties _original;
    public DiscordGuildThreadProperties(NetCord.Rest.GuildThreadProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildThreadProperties Original => _original;
    public NetCord.ChannelType? ChannelType => _original.ChannelType;
    public bool? Invitable => _original.Invitable;
    public string Name => _original.Name;
    public NetCord.ThreadArchiveDuration? AutoArchiveDuration => _original.AutoArchiveDuration;
    public int? Slowmode => _original.Slowmode;
    public IDiscordGuildThreadProperties WithChannelType(NetCord.ChannelType? channelType) => new DiscordGuildThreadProperties(_original.WithChannelType(channelType));
    public IDiscordGuildThreadProperties WithInvitable(bool? invitable = true) => new DiscordGuildThreadProperties(_original.WithInvitable(invitable));
    public IDiscordGuildThreadProperties WithName(string name) => new DiscordGuildThreadProperties(_original.WithName(name));
    public IDiscordGuildThreadProperties WithAutoArchiveDuration(NetCord.ThreadArchiveDuration? autoArchiveDuration) => new DiscordGuildThreadProperties(_original.WithAutoArchiveDuration(autoArchiveDuration));
    public IDiscordGuildThreadProperties WithSlowmode(int? slowmode) => new DiscordGuildThreadProperties(_original.WithSlowmode(slowmode));
}


public class DiscordIncomingWebhook : IDiscordIncomingWebhook
{
    private readonly NetCord.Rest.IncomingWebhook _original;
    public DiscordIncomingWebhook(NetCord.Rest.IncomingWebhook original)
    {
        _original = original;
    }
    public NetCord.Rest.IncomingWebhook Original => _original;
    public string Token => _original.Token;
    public ulong Id => _original.Id;
    public NetCord.Rest.WebhookType Type => _original.Type;
    public ulong? GuildId => _original.GuildId;
    public ulong? ChannelId => _original.ChannelId;
    public IDiscordUser Creator => new DiscordUser(_original.Creator);
    public string Name => _original.Name;
    public string AvatarHash => _original.AvatarHash;
    public ulong? ApplicationId => _original.ApplicationId;
    public IDiscordRestGuild Guild => new DiscordRestGuild(_original.Guild);
    public IDiscordChannel Channel => new DiscordChannel(_original.Channel);
    public string Url => _original.Url;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordWebhookClient ToClient(IDiscordWebhookClientConfiguration? configuration = null) => new DiscordWebhookClient(_original.ToClient(configuration.Original));
    public async Task<IDiscordIncomingWebhook> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordIncomingWebhook(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordIncomingWebhook> GetWithTokenAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordIncomingWebhook(await _original.GetWithTokenAsync(properties.Original, cancellationToken));
    public async Task<IDiscordIncomingWebhook> ModifyAsync(Action<IDiscordWebhookOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordIncomingWebhook(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public async Task<IDiscordIncomingWebhook> ModifyWithTokenAsync(Action<IDiscordWebhookOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordIncomingWebhook(await _original.ModifyWithTokenAsync(action, properties.Original, cancellationToken));
    public Task DeleteWithTokenAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteWithTokenAsync(properties.Original, cancellationToken);
    public async Task<IDiscordRestMessage> ExecuteAsync(IDiscordWebhookMessageProperties message, bool wait = false, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.ExecuteAsync(message.Original, wait, threadId, withComponents, properties.Original, cancellationToken));
    public async Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.GetMessageAsync(messageId, properties.Original, cancellationToken));
    public async Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.ModifyMessageAsync(messageId, action, threadId, withComponents, properties.Original, cancellationToken));
    public Task DeleteMessageAsync(ulong messageId, ulong? threadId = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessageAsync(messageId, threadId, properties.Original, cancellationToken);
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAsync(properties.Original, cancellationToken);
}


public class DiscordWebhookProperties : IDiscordWebhookProperties
{
    private readonly NetCord.Rest.WebhookProperties _original;
    public DiscordWebhookProperties(NetCord.Rest.WebhookProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.WebhookProperties Original => _original;
    public string Name => _original.Name;
    public NetCord.Rest.ImageProperties? Avatar => _original.Avatar;
    public IDiscordWebhookProperties WithName(string name) => new DiscordWebhookProperties(_original.WithName(name));
    public IDiscordWebhookProperties WithAvatar(NetCord.Rest.ImageProperties? avatar) => new DiscordWebhookProperties(_original.WithAvatar(avatar));
}


public class DiscordUserActivity : IDiscordUserActivity
{
    private readonly NetCord.Gateway.UserActivity _original;
    public DiscordUserActivity(NetCord.Gateway.UserActivity original)
    {
        _original = original;
    }
    public NetCord.Gateway.UserActivity Original => _original;
    public string Name => _original.Name;
    public NetCord.Gateway.UserActivityType Type => _original.Type;
    public string Url => _original.Url;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordUserActivityTimestamps Timestamps => new DiscordUserActivityTimestamps(_original.Timestamps);
    public ulong? ApplicationId => _original.ApplicationId;
    public string Details => _original.Details;
    public string State => _original.State;
    public IDiscordEmoji Emoji => new DiscordEmoji(_original.Emoji);
    public IDiscordParty Party => new DiscordParty(_original.Party);
    public IDiscordUserActivityAssets Assets => new DiscordUserActivityAssets(_original.Assets);
    public IDiscordUserActivitySecrets Secrets => new DiscordUserActivitySecrets(_original.Secrets);
    public bool? Instance => _original.Instance;
    public NetCord.Gateway.UserActivityFlags? Flags => _original.Flags;
    public IReadOnlyList<IDiscordUserActivityButton> Buttons => _original.Buttons.Select(x => new DiscordUserActivityButton(x)).ToList();
    public ulong GuildId => _original.GuildId;
}


public class DiscordStageInstanceOptions : IDiscordStageInstanceOptions
{
    private readonly NetCord.Rest.StageInstanceOptions _original;
    public DiscordStageInstanceOptions(NetCord.Rest.StageInstanceOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.StageInstanceOptions Original => _original;
    public string Topic => _original.Topic;
    public NetCord.StageInstancePrivacyLevel? PrivacyLevel => _original.PrivacyLevel;
    public IDiscordStageInstanceOptions WithTopic(string topic) => new DiscordStageInstanceOptions(_original.WithTopic(topic));
    public IDiscordStageInstanceOptions WithPrivacyLevel(NetCord.StageInstancePrivacyLevel? privacyLevel) => new DiscordStageInstanceOptions(_original.WithPrivacyLevel(privacyLevel));
}


public class DiscordGuildScheduledEventRecurrenceRule : IDiscordGuildScheduledEventRecurrenceRule
{
    private readonly NetCord.GuildScheduledEventRecurrenceRule _original;
    public DiscordGuildScheduledEventRecurrenceRule(NetCord.GuildScheduledEventRecurrenceRule original)
    {
        _original = original;
    }
    public NetCord.GuildScheduledEventRecurrenceRule Original => _original;
    public System.DateTimeOffset? StartAt => _original.StartAt;
    public System.DateTimeOffset? EndAt => _original.EndAt;
    public NetCord.GuildScheduledEventRecurrenceRuleFrequency Frequency => _original.Frequency;
    public int Interval => _original.Interval;
    public IReadOnlyList<NetCord.GuildScheduledEventRecurrenceRuleWeekday> ByWeekday => _original.ByWeekday;
    public IReadOnlyList<IDiscordGuildScheduledEventRecurrenceRuleNWeekday> ByNWeekday => _original.ByNWeekday.Select(x => new DiscordGuildScheduledEventRecurrenceRuleNWeekday(x)).ToList();
    public IReadOnlyList<NetCord.GuildScheduledEventRecurrenceRuleMonth> ByMonth => _original.ByMonth;
    public IReadOnlyList<int> ByMonthDay => _original.ByMonthDay;
    public IReadOnlyList<int> ByYearDay => _original.ByYearDay;
    public int? Count => _original.Count;
}


public class DiscordRoleTags : IDiscordRoleTags
{
    private readonly NetCord.RoleTags _original;
    public DiscordRoleTags(NetCord.RoleTags original)
    {
        _original = original;
    }
    public NetCord.RoleTags Original => _original;
    public ulong? BotId => _original.BotId;
    public ulong? IntegrationId => _original.IntegrationId;
    public bool IsPremiumSubscriber => _original.IsPremiumSubscriber;
    public ulong? SubscriptionListingId => _original.SubscriptionListingId;
    public bool IsAvailableForPurchase => _original.IsAvailableForPurchase;
    public bool GuildConnections => _original.GuildConnections;
}


public class DiscordGuildWelcomeScreenChannel : IDiscordGuildWelcomeScreenChannel
{
    private readonly NetCord.GuildWelcomeScreenChannel _original;
    public DiscordGuildWelcomeScreenChannel(NetCord.GuildWelcomeScreenChannel original)
    {
        _original = original;
    }
    public NetCord.GuildWelcomeScreenChannel Original => _original;
    public ulong Id => _original.Id;
    public string Description => _original.Description;
    public ulong? EmojiId => _original.EmojiId;
    public string EmojiName => _original.EmojiName;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordRestAuditLogEntryData : IDiscordRestAuditLogEntryData
{
    private readonly NetCord.Rest.RestAuditLogEntryData _original;
    public DiscordRestAuditLogEntryData(NetCord.Rest.RestAuditLogEntryData original)
    {
        _original = original;
    }
    public NetCord.Rest.RestAuditLogEntryData Original => _original;
    public IReadOnlyDictionary<ulong, IDiscordApplicationCommand> ApplicationCommands => _original.ApplicationCommands.ToDictionary(kv => kv.Key, kv => (IDiscordApplicationCommand)new DiscordApplicationCommand(kv.Value));
    public IReadOnlyDictionary<ulong, IDiscordAutoModerationRule> AutoModerationRules => _original.AutoModerationRules.ToDictionary(kv => kv.Key, kv => (IDiscordAutoModerationRule)new DiscordAutoModerationRule(kv.Value));
    public IReadOnlyDictionary<ulong, IDiscordGuildScheduledEvent> GuildScheduledEvents => _original.GuildScheduledEvents.ToDictionary(kv => kv.Key, kv => (IDiscordGuildScheduledEvent)new DiscordGuildScheduledEvent(kv.Value));
    public IReadOnlyDictionary<ulong, IDiscordIntegration> Integrations => _original.Integrations.ToDictionary(kv => kv.Key, kv => (IDiscordIntegration)new DiscordIntegration(kv.Value));
    public IReadOnlyDictionary<ulong, IDiscordGuildThread> Threads => _original.Threads.ToDictionary(kv => kv.Key, kv => (IDiscordGuildThread)new DiscordGuildThread(kv.Value));
    public IReadOnlyDictionary<ulong, IDiscordUser> Users => _original.Users.ToDictionary(kv => kv.Key, kv => (IDiscordUser)new DiscordUser(kv.Value));
    public IReadOnlyDictionary<ulong, IDiscordWebhook> Webhooks => _original.Webhooks.ToDictionary(kv => kv.Key, kv => (IDiscordWebhook)new DiscordWebhook(kv.Value));
}


public class DiscordAuditLogChange : IDiscordAuditLogChange
{
    private readonly NetCord.AuditLogChange _original;
    public DiscordAuditLogChange(NetCord.AuditLogChange original)
    {
        _original = original;
    }
    public NetCord.AuditLogChange Original => _original;
    public string Key => _original.Key;
    public bool HasNewValue => _original.HasNewValue;
    public bool HasOldValue => _original.HasOldValue;
    public IDiscordAuditLogChange<TValue> WithValues<TValue>(JsonTypeInfo<TValue> jsonTypeInfo) => new DiscordAuditLogChange<TValue>(_original.WithValues(jsonTypeInfo));
    public IDiscordAuditLogChange<TValue> WithValues<TValue>() => new DiscordAuditLogChange<TValue>(_original.WithValues());
}


public class DiscordAuditLogEntryInfo : IDiscordAuditLogEntryInfo
{
    private readonly NetCord.AuditLogEntryInfo _original;
    public DiscordAuditLogEntryInfo(NetCord.AuditLogEntryInfo original)
    {
        _original = original;
    }
    public NetCord.AuditLogEntryInfo Original => _original;
    public ulong? ApplicationId => _original.ApplicationId;
    public string AutoModerationRuleName => _original.AutoModerationRuleName;
    public NetCord.AutoModerationRuleTriggerType? AutoModerationRuleTriggerType => _original.AutoModerationRuleTriggerType;
    public ulong? ChannelId => _original.ChannelId;
    public int? Count => _original.Count;
    public int? DeleteGuildUserDays => _original.DeleteGuildUserDays;
    public ulong? Id => _original.Id;
    public int? GuildUsersRemoved => _original.GuildUsersRemoved;
    public ulong? MessageId => _original.MessageId;
    public string RoleName => _original.RoleName;
    public NetCord.PermissionOverwriteType? Type => _original.Type;
    public NetCord.IntegrationType? IntegrationType => _original.IntegrationType;
}


public class DiscordAuditLogChange<TValue> : IDiscordAuditLogChange<TValue> where T : struct
{
    private readonly NetCord.AuditLogChange<TValue> _original;
    public DiscordAuditLogChange(NetCord.AuditLogChange<TValue> original)
    {
        _original = original;
    }
    public NetCord.AuditLogChange<TValue> Original => _original;
    public TValue NewValue => _original.NewValue;
    public TValue OldValue => _original.OldValue;
    public string Key => _original.Key;
    public bool HasNewValue => _original.HasNewValue;
    public bool HasOldValue => _original.HasOldValue;
    public IDiscordAuditLogChange<TValue> WithValues<TValue>(JsonTypeInfo<TValue> jsonTypeInfo) => new DiscordAuditLogChange<TValue>(_original.WithValues(jsonTypeInfo));
    public IDiscordAuditLogChange<TValue> WithValues<TValue>() => new DiscordAuditLogChange<TValue>(_original.WithValues());
}


public class DiscordAutoModerationRuleTriggerMetadata : IDiscordAutoModerationRuleTriggerMetadata
{
    private readonly NetCord.AutoModerationRuleTriggerMetadata _original;
    public DiscordAutoModerationRuleTriggerMetadata(NetCord.AutoModerationRuleTriggerMetadata original)
    {
        _original = original;
    }
    public NetCord.AutoModerationRuleTriggerMetadata Original => _original;
    public IReadOnlyList<string> KeywordFilter => _original.KeywordFilter;
    public IReadOnlyList<string> RegexPatterns => _original.RegexPatterns;
    public IReadOnlyList<NetCord.AutoModerationRuleKeywordPresetType> Presets => _original.Presets;
    public IReadOnlyList<string> AllowList => _original.AllowList;
    public int? MentionTotalLimit => _original.MentionTotalLimit;
    public bool MentionRaidProtectionEnabled => _original.MentionRaidProtectionEnabled;
}


public class DiscordAutoModerationAction : IDiscordAutoModerationAction
{
    private readonly NetCord.AutoModerationAction _original;
    public DiscordAutoModerationAction(NetCord.AutoModerationAction original)
    {
        _original = original;
    }
    public NetCord.AutoModerationAction Original => _original;
    public NetCord.AutoModerationActionType Type => _original.Type;
    public IDiscordAutoModerationActionMetadata Metadata => new DiscordAutoModerationActionMetadata(_original.Metadata);
}


public class DiscordAutoModerationRuleTriggerMetadataProperties : IDiscordAutoModerationRuleTriggerMetadataProperties
{
    private readonly NetCord.AutoModerationRuleTriggerMetadataProperties _original;
    public DiscordAutoModerationRuleTriggerMetadataProperties(NetCord.AutoModerationRuleTriggerMetadataProperties original)
    {
        _original = original;
    }
    public NetCord.AutoModerationRuleTriggerMetadataProperties Original => _original;
    public IEnumerable<string> KeywordFilter => _original.KeywordFilter;
    public IEnumerable<string> RegexPatterns => _original.RegexPatterns;
    public IEnumerable<NetCord.AutoModerationRuleKeywordPresetType> Presets => _original.Presets;
    public IEnumerable<string> AllowList => _original.AllowList;
    public int? MentionTotalLimit => _original.MentionTotalLimit;
    public bool MentionRaidProtectionEnabled => _original.MentionRaidProtectionEnabled;
    public IDiscordAutoModerationRuleTriggerMetadataProperties WithKeywordFilter(IEnumerable<string> keywordFilter) => new DiscordAutoModerationRuleTriggerMetadataProperties(_original.WithKeywordFilter(keywordFilter));
    public IDiscordAutoModerationRuleTriggerMetadataProperties AddKeywordFilter(IEnumerable<string> keywordFilter) => new DiscordAutoModerationRuleTriggerMetadataProperties(_original.AddKeywordFilter(keywordFilter));
    public IDiscordAutoModerationRuleTriggerMetadataProperties AddKeywordFilter(string[] keywordFilter) => new DiscordAutoModerationRuleTriggerMetadataProperties(_original.AddKeywordFilter(keywordFilter));
    public IDiscordAutoModerationRuleTriggerMetadataProperties WithRegexPatterns(IEnumerable<string> regexPatterns) => new DiscordAutoModerationRuleTriggerMetadataProperties(_original.WithRegexPatterns(regexPatterns));
    public IDiscordAutoModerationRuleTriggerMetadataProperties AddRegexPatterns(IEnumerable<string> regexPatterns) => new DiscordAutoModerationRuleTriggerMetadataProperties(_original.AddRegexPatterns(regexPatterns));
    public IDiscordAutoModerationRuleTriggerMetadataProperties AddRegexPatterns(string[] regexPatterns) => new DiscordAutoModerationRuleTriggerMetadataProperties(_original.AddRegexPatterns(regexPatterns));
    public IDiscordAutoModerationRuleTriggerMetadataProperties WithPresets(IEnumerable<NetCord.AutoModerationRuleKeywordPresetType> presets) => new DiscordAutoModerationRuleTriggerMetadataProperties(_original.WithPresets(presets));
    public IDiscordAutoModerationRuleTriggerMetadataProperties AddPresets(IEnumerable<NetCord.AutoModerationRuleKeywordPresetType> presets) => new DiscordAutoModerationRuleTriggerMetadataProperties(_original.AddPresets(presets));
    public IDiscordAutoModerationRuleTriggerMetadataProperties AddPresets(NetCord.AutoModerationRuleKeywordPresetType[] presets) => new DiscordAutoModerationRuleTriggerMetadataProperties(_original.AddPresets(presets));
    public IDiscordAutoModerationRuleTriggerMetadataProperties WithAllowList(IEnumerable<string> allowList) => new DiscordAutoModerationRuleTriggerMetadataProperties(_original.WithAllowList(allowList));
    public IDiscordAutoModerationRuleTriggerMetadataProperties AddAllowList(IEnumerable<string> allowList) => new DiscordAutoModerationRuleTriggerMetadataProperties(_original.AddAllowList(allowList));
    public IDiscordAutoModerationRuleTriggerMetadataProperties AddAllowList(string[] allowList) => new DiscordAutoModerationRuleTriggerMetadataProperties(_original.AddAllowList(allowList));
    public IDiscordAutoModerationRuleTriggerMetadataProperties WithMentionTotalLimit(int? mentionTotalLimit) => new DiscordAutoModerationRuleTriggerMetadataProperties(_original.WithMentionTotalLimit(mentionTotalLimit));
    public IDiscordAutoModerationRuleTriggerMetadataProperties WithMentionRaidProtectionEnabled(bool mentionRaidProtectionEnabled = true) => new DiscordAutoModerationRuleTriggerMetadataProperties(_original.WithMentionRaidProtectionEnabled(mentionRaidProtectionEnabled));
}


public class DiscordAutoModerationActionProperties : IDiscordAutoModerationActionProperties
{
    private readonly NetCord.AutoModerationActionProperties _original;
    public DiscordAutoModerationActionProperties(NetCord.AutoModerationActionProperties original)
    {
        _original = original;
    }
    public NetCord.AutoModerationActionProperties Original => _original;
    public NetCord.AutoModerationActionType Type => _original.Type;
    public IDiscordAutoModerationActionMetadataProperties Metadata => new DiscordAutoModerationActionMetadataProperties(_original.Metadata);
    public IDiscordAutoModerationActionProperties WithType(NetCord.AutoModerationActionType type) => new DiscordAutoModerationActionProperties(_original.WithType(type));
    public IDiscordAutoModerationActionProperties WithMetadata(IDiscordAutoModerationActionMetadataProperties metadata) => new DiscordAutoModerationActionProperties(_original.WithMetadata(metadata.Original));
}


public class DiscordForumTagProperties : IDiscordForumTagProperties
{
    private readonly NetCord.Rest.ForumTagProperties _original;
    public DiscordForumTagProperties(NetCord.Rest.ForumTagProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ForumTagProperties Original => _original;
    public ulong? Id => _original.Id;
    public string Name => _original.Name;
    public bool? Moderated => _original.Moderated;
    public ulong? EmojiId => _original.EmojiId;
    public string EmojiName => _original.EmojiName;
    public IDiscordForumTagProperties WithId(ulong? id) => new DiscordForumTagProperties(_original.WithId(id));
    public IDiscordForumTagProperties WithName(string name) => new DiscordForumTagProperties(_original.WithName(name));
    public IDiscordForumTagProperties WithModerated(bool? moderated = true) => new DiscordForumTagProperties(_original.WithModerated(moderated));
    public IDiscordForumTagProperties WithEmojiId(ulong? emojiId) => new DiscordForumTagProperties(_original.WithEmojiId(emojiId));
    public IDiscordForumTagProperties WithEmojiName(string emojiName) => new DiscordForumTagProperties(_original.WithEmojiName(emojiName));
}


public class DiscordChannel : IDiscordChannel
{
    private readonly NetCord.Channel _original;
    public DiscordChannel(NetCord.Channel original)
    {
        _original = original;
    }
    public NetCord.Channel Original => _original;
    public ulong Id => _original.Id;
    public NetCord.ChannelFlags Flags => _original.Flags;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordChannel> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordChannel(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordChannel> DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordChannel(await _original.DeleteAsync(properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.CreateGoogleCloudPlatformStorageBucketsAsync(buckets?.Select(x => x.Original), properties.Original, cancellationToken)).Select(x => new DiscordGoogleCloudPlatformStorageBucket(x)).ToList();
}


public class DiscordAccount : IDiscordAccount
{
    private readonly NetCord.Account _original;
    public DiscordAccount(NetCord.Account original)
    {
        _original = original;
    }
    public NetCord.Account Original => _original;
    public ulong Id => _original.Id;
    public string Name => _original.Name;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordIntegrationApplication : IDiscordIntegrationApplication
{
    private readonly NetCord.IntegrationApplication _original;
    public DiscordIntegrationApplication(NetCord.IntegrationApplication original)
    {
        _original = original;
    }
    public NetCord.IntegrationApplication Original => _original;
    public ulong Id => _original.Id;
    public string Name => _original.Name;
    public string IconHash => _original.IconHash;
    public string Description => _original.Description;
    public string Summary => _original.Summary;
    public IDiscordUser Bot => new DiscordUser(_original.Bot);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordGuildWidgetChannel : IDiscordGuildWidgetChannel
{
    private readonly NetCord.Rest.GuildWidgetChannel _original;
    public DiscordGuildWidgetChannel(NetCord.Rest.GuildWidgetChannel original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildWidgetChannel Original => _original;
    public ulong Id => _original.Id;
    public string Name => _original.Name;
    public int Position => _original.Position;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordGuildWelcomeScreenChannelProperties : IDiscordGuildWelcomeScreenChannelProperties
{
    private readonly NetCord.Rest.GuildWelcomeScreenChannelProperties _original;
    public DiscordGuildWelcomeScreenChannelProperties(NetCord.Rest.GuildWelcomeScreenChannelProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildWelcomeScreenChannelProperties Original => _original;
    public ulong ChannelId => _original.ChannelId;
    public string Description => _original.Description;
    public IDiscordEmojiProperties Emoji => new DiscordEmojiProperties(_original.Emoji);
    public IDiscordGuildWelcomeScreenChannelProperties WithChannelId(ulong channelId) => new DiscordGuildWelcomeScreenChannelProperties(_original.WithChannelId(channelId));
    public IDiscordGuildWelcomeScreenChannelProperties WithDescription(string description) => new DiscordGuildWelcomeScreenChannelProperties(_original.WithDescription(description));
    public IDiscordGuildWelcomeScreenChannelProperties WithEmoji(IDiscordEmojiProperties emoji) => new DiscordGuildWelcomeScreenChannelProperties(_original.WithEmoji(emoji.Original));
}


public class DiscordGuildOnboardingPrompt : IDiscordGuildOnboardingPrompt
{
    private readonly NetCord.Rest.GuildOnboardingPrompt _original;
    public DiscordGuildOnboardingPrompt(NetCord.Rest.GuildOnboardingPrompt original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildOnboardingPrompt Original => _original;
    public ulong Id => _original.Id;
    public NetCord.Rest.GuildOnboardingPromptType Type => _original.Type;
    public IReadOnlyList<IDiscordGuildOnboardingPromptOption> Options => _original.Options.Select(x => new DiscordGuildOnboardingPromptOption(x)).ToList();
    public string Title => _original.Title;
    public bool SingleSelect => _original.SingleSelect;
    public bool Required => _original.Required;
    public bool InOnboarding => _original.InOnboarding;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordGuildOnboardingPromptProperties : IDiscordGuildOnboardingPromptProperties
{
    private readonly NetCord.Rest.GuildOnboardingPromptProperties _original;
    public DiscordGuildOnboardingPromptProperties(NetCord.Rest.GuildOnboardingPromptProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildOnboardingPromptProperties Original => _original;
    public ulong? Id => _original.Id;
    public NetCord.Rest.GuildOnboardingPromptType Type => _original.Type;
    public IEnumerable<IDiscordGuildOnboardingPromptOptionProperties> Options => _original.Options.Select(x => new DiscordGuildOnboardingPromptOptionProperties(x));
    public string Title => _original.Title;
    public bool SingleSelect => _original.SingleSelect;
    public bool Required => _original.Required;
    public bool InOnboarding => _original.InOnboarding;
    public IDiscordGuildOnboardingPromptProperties WithId(ulong? id) => new DiscordGuildOnboardingPromptProperties(_original.WithId(id));
    public IDiscordGuildOnboardingPromptProperties WithType(NetCord.Rest.GuildOnboardingPromptType type) => new DiscordGuildOnboardingPromptProperties(_original.WithType(type));
    public IDiscordGuildOnboardingPromptProperties WithOptions(IEnumerable<IDiscordGuildOnboardingPromptOptionProperties> options) => new DiscordGuildOnboardingPromptProperties(_original.WithOptions(options?.Select(x => x.Original)));
    public IDiscordGuildOnboardingPromptProperties AddOptions(IEnumerable<IDiscordGuildOnboardingPromptOptionProperties> options) => new DiscordGuildOnboardingPromptProperties(_original.AddOptions(options?.Select(x => x.Original)));
    public IDiscordGuildOnboardingPromptProperties AddOptions(IDiscordGuildOnboardingPromptOptionProperties[] options) => new DiscordGuildOnboardingPromptProperties(_original.AddOptions(options.Original));
    public IDiscordGuildOnboardingPromptProperties WithTitle(string title) => new DiscordGuildOnboardingPromptProperties(_original.WithTitle(title));
    public IDiscordGuildOnboardingPromptProperties WithSingleSelect(bool singleSelect = true) => new DiscordGuildOnboardingPromptProperties(_original.WithSingleSelect(singleSelect));
    public IDiscordGuildOnboardingPromptProperties WithRequired(bool required = true) => new DiscordGuildOnboardingPromptProperties(_original.WithRequired(required));
    public IDiscordGuildOnboardingPromptProperties WithInOnboarding(bool inOnboarding = true) => new DiscordGuildOnboardingPromptProperties(_original.WithInOnboarding(inOnboarding));
}


public class DiscordGuildScheduledEventMetadataProperties : IDiscordGuildScheduledEventMetadataProperties
{
    private readonly NetCord.Rest.GuildScheduledEventMetadataProperties _original;
    public DiscordGuildScheduledEventMetadataProperties(NetCord.Rest.GuildScheduledEventMetadataProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildScheduledEventMetadataProperties Original => _original;
    public string Location => _original.Location;
    public IDiscordGuildScheduledEventMetadataProperties WithLocation(string location) => new DiscordGuildScheduledEventMetadataProperties(_original.WithLocation(location));
}


public class DiscordGuildTemplatePreview : IDiscordGuildTemplatePreview
{
    private readonly NetCord.Rest.GuildTemplatePreview _original;
    public DiscordGuildTemplatePreview(NetCord.Rest.GuildTemplatePreview original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildTemplatePreview Original => _original;
    public string Name => _original.Name;
    public string IconHash => _original.IconHash;
    public string Description => _original.Description;
    public NetCord.VerificationLevel VerificationLevel => _original.VerificationLevel;
    public NetCord.DefaultMessageNotificationLevel DefaultMessageNotificationLevel => _original.DefaultMessageNotificationLevel;
    public NetCord.ContentFilter ContentFilter => _original.ContentFilter;
    public string PreferredLocale => _original.PreferredLocale;
    public ulong? AfkChannelId => _original.AfkChannelId;
    public int AfkTimeout => _original.AfkTimeout;
    public ulong? SystemChannelId => _original.SystemChannelId;
    public NetCord.Rest.SystemChannelFlags SystemChannelFlags => _original.SystemChannelFlags;
    public IReadOnlyDictionary<ulong, IDiscordRole> Roles => _original.Roles.ToDictionary(kv => kv.Key, kv => (IDiscordRole)new DiscordRole(kv.Value));
    public IReadOnlyDictionary<ulong, IDiscordGuildChannel> Channels => _original.Channels.ToDictionary(kv => kv.Key, kv => (IDiscordGuildChannel)new DiscordGuildChannel(kv.Value));
}


public class DiscordGuildFromGuildTemplateProperties : IDiscordGuildFromGuildTemplateProperties
{
    private readonly NetCord.Rest.GuildFromGuildTemplateProperties _original;
    public DiscordGuildFromGuildTemplateProperties(NetCord.Rest.GuildFromGuildTemplateProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildFromGuildTemplateProperties Original => _original;
    public string Name => _original.Name;
    public NetCord.Rest.ImageProperties? Icon => _original.Icon;
    public IDiscordGuildFromGuildTemplateProperties WithName(string name) => new DiscordGuildFromGuildTemplateProperties(_original.WithName(name));
    public IDiscordGuildFromGuildTemplateProperties WithIcon(NetCord.Rest.ImageProperties? icon) => new DiscordGuildFromGuildTemplateProperties(_original.WithIcon(icon));
}


public class DiscordApplicationCommandOption : IDiscordApplicationCommandOption
{
    private readonly NetCord.Rest.ApplicationCommandOption _original;
    public DiscordApplicationCommandOption(NetCord.Rest.ApplicationCommandOption original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationCommandOption Original => _original;
    public NetCord.ApplicationCommandOptionType Type => _original.Type;
    public string Name => _original.Name;
    public IReadOnlyDictionary<string, string> NameLocalizations => _original.NameLocalizations;
    public string Description => _original.Description;
    public IReadOnlyDictionary<string, string> DescriptionLocalizations => _original.DescriptionLocalizations;
    public bool Required => _original.Required;
    public IReadOnlyList<IDiscordApplicationCommandOptionChoice> Choices => _original.Choices.Select(x => new DiscordApplicationCommandOptionChoice(x)).ToList();
    public IReadOnlyList<IDiscordApplicationCommandOption> Options => _original.Options.Select(x => new DiscordApplicationCommandOption(x)).ToList();
    public IReadOnlyList<NetCord.ChannelType> ChannelTypes => _original.ChannelTypes;
    public double? MinValue => _original.MinValue;
    public double? MaxValue => _original.MaxValue;
    public int? MinLength => _original.MinLength;
    public int? MaxLength => _original.MaxLength;
    public bool Autocomplete => _original.Autocomplete;
}


public class DiscordApplicationCommand : IDiscordApplicationCommand
{
    private readonly NetCord.Rest.ApplicationCommand _original;
    public DiscordApplicationCommand(NetCord.Rest.ApplicationCommand original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationCommand Original => _original;
    public ulong Id => _original.Id;
    public NetCord.ApplicationCommandType Type => _original.Type;
    public ulong ApplicationId => _original.ApplicationId;
    public string Name => _original.Name;
    public IReadOnlyDictionary<string, string> NameLocalizations => _original.NameLocalizations;
    public string Description => _original.Description;
    public IReadOnlyDictionary<string, string> DescriptionLocalizations => _original.DescriptionLocalizations;
    public NetCord.Permissions? DefaultGuildUserPermissions => _original.DefaultGuildUserPermissions;
    public bool DMPermission => _original.DMPermission;
    public IReadOnlyList<IDiscordApplicationCommandOption> Options => _original.Options.Select(x => new DiscordApplicationCommandOption(x)).ToList();
    public bool DefaultPermission => _original.DefaultPermission;
    public bool Nsfw => _original.Nsfw;
    public IReadOnlyList<NetCord.ApplicationIntegrationType> IntegrationTypes => _original.IntegrationTypes;
    public IReadOnlyList<NetCord.InteractionContextType> Contexts => _original.Contexts;
    public ulong Version => _original.Version;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordApplicationCommand> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationCommand(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordApplicationCommand> ModifyAsync(Action<IDiscordApplicationCommandOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationCommand(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAsync(properties.Original, cancellationToken);
    public async Task<IDiscordApplicationCommandGuildPermissions> GetGuildPermissionsAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationCommandGuildPermissions(await _original.GetGuildPermissionsAsync(guildId, properties.Original, cancellationToken));
    public async Task<IDiscordApplicationCommandGuildPermissions> OverwriteGuildPermissionsAsync(ulong guildId, IEnumerable<IDiscordApplicationCommandGuildPermissionProperties> newPermissions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationCommandGuildPermissions(await _original.OverwriteGuildPermissionsAsync(guildId, newPermissions?.Select(x => x.Original), properties.Original, cancellationToken));
}


public class DiscordApplicationCommandOptionProperties : IDiscordApplicationCommandOptionProperties
{
    private readonly NetCord.Rest.ApplicationCommandOptionProperties _original;
    public DiscordApplicationCommandOptionProperties(NetCord.Rest.ApplicationCommandOptionProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationCommandOptionProperties Original => _original;
    public NetCord.ApplicationCommandOptionType Type => _original.Type;
    public string Name => _original.Name;
    public IReadOnlyDictionary<string, string> NameLocalizations => _original.NameLocalizations;
    public string Description => _original.Description;
    public IReadOnlyDictionary<string, string> DescriptionLocalizations => _original.DescriptionLocalizations;
    public bool? Required => _original.Required;
    public IEnumerable<IDiscordApplicationCommandOptionChoiceProperties> Choices => _original.Choices.Select(x => new DiscordApplicationCommandOptionChoiceProperties(x));
    public IEnumerable<IDiscordApplicationCommandOptionProperties> Options => _original.Options.Select(x => new DiscordApplicationCommandOptionProperties(x));
    public IEnumerable<NetCord.ChannelType> ChannelTypes => _original.ChannelTypes;
    public double? MinValue => _original.MinValue;
    public double? MaxValue => _original.MaxValue;
    public int? MinLength => _original.MinLength;
    public int? MaxLength => _original.MaxLength;
    public bool? Autocomplete => _original.Autocomplete;
    public IDiscordApplicationCommandOptionProperties WithType(NetCord.ApplicationCommandOptionType type) => new DiscordApplicationCommandOptionProperties(_original.WithType(type));
    public IDiscordApplicationCommandOptionProperties WithName(string name) => new DiscordApplicationCommandOptionProperties(_original.WithName(name));
    public IDiscordApplicationCommandOptionProperties WithNameLocalizations(IReadOnlyDictionary<string, string> nameLocalizations) => new DiscordApplicationCommandOptionProperties(_original.WithNameLocalizations(nameLocalizations));
    public IDiscordApplicationCommandOptionProperties WithDescription(string description) => new DiscordApplicationCommandOptionProperties(_original.WithDescription(description));
    public IDiscordApplicationCommandOptionProperties WithDescriptionLocalizations(IReadOnlyDictionary<string, string> descriptionLocalizations) => new DiscordApplicationCommandOptionProperties(_original.WithDescriptionLocalizations(descriptionLocalizations));
    public IDiscordApplicationCommandOptionProperties WithRequired(bool? required = true) => new DiscordApplicationCommandOptionProperties(_original.WithRequired(required));
    public IDiscordApplicationCommandOptionProperties WithChoices(IEnumerable<IDiscordApplicationCommandOptionChoiceProperties> choices) => new DiscordApplicationCommandOptionProperties(_original.WithChoices(choices?.Select(x => x.Original)));
    public IDiscordApplicationCommandOptionProperties AddChoices(IEnumerable<IDiscordApplicationCommandOptionChoiceProperties> choices) => new DiscordApplicationCommandOptionProperties(_original.AddChoices(choices?.Select(x => x.Original)));
    public IDiscordApplicationCommandOptionProperties AddChoices(IDiscordApplicationCommandOptionChoiceProperties[] choices) => new DiscordApplicationCommandOptionProperties(_original.AddChoices(choices.Original));
    public IDiscordApplicationCommandOptionProperties WithOptions(IEnumerable<IDiscordApplicationCommandOptionProperties> options) => new DiscordApplicationCommandOptionProperties(_original.WithOptions(options?.Select(x => x.Original)));
    public IDiscordApplicationCommandOptionProperties AddOptions(IEnumerable<IDiscordApplicationCommandOptionProperties> options) => new DiscordApplicationCommandOptionProperties(_original.AddOptions(options?.Select(x => x.Original)));
    public IDiscordApplicationCommandOptionProperties AddOptions(IDiscordApplicationCommandOptionProperties[] options) => new DiscordApplicationCommandOptionProperties(_original.AddOptions(options.Original));
    public IDiscordApplicationCommandOptionProperties WithChannelTypes(IEnumerable<NetCord.ChannelType> channelTypes) => new DiscordApplicationCommandOptionProperties(_original.WithChannelTypes(channelTypes));
    public IDiscordApplicationCommandOptionProperties AddChannelTypes(IEnumerable<NetCord.ChannelType> channelTypes) => new DiscordApplicationCommandOptionProperties(_original.AddChannelTypes(channelTypes));
    public IDiscordApplicationCommandOptionProperties AddChannelTypes(NetCord.ChannelType[] channelTypes) => new DiscordApplicationCommandOptionProperties(_original.AddChannelTypes(channelTypes));
    public IDiscordApplicationCommandOptionProperties WithMinValue(double? minValue) => new DiscordApplicationCommandOptionProperties(_original.WithMinValue(minValue));
    public IDiscordApplicationCommandOptionProperties WithMaxValue(double? maxValue) => new DiscordApplicationCommandOptionProperties(_original.WithMaxValue(maxValue));
    public IDiscordApplicationCommandOptionProperties WithMinLength(int? minLength) => new DiscordApplicationCommandOptionProperties(_original.WithMinLength(minLength));
    public IDiscordApplicationCommandOptionProperties WithMaxLength(int? maxLength) => new DiscordApplicationCommandOptionProperties(_original.WithMaxLength(maxLength));
    public IDiscordApplicationCommandOptionProperties WithAutocomplete(bool? autocomplete = true) => new DiscordApplicationCommandOptionProperties(_original.WithAutocomplete(autocomplete));
}


public class DiscordApplicationCommandPermission : IDiscordApplicationCommandPermission
{
    private readonly NetCord.ApplicationCommandPermission _original;
    public DiscordApplicationCommandPermission(NetCord.ApplicationCommandPermission original)
    {
        _original = original;
    }
    public NetCord.ApplicationCommandPermission Original => _original;
    public ulong Id => _original.Id;
    public NetCord.ApplicationCommandGuildPermissionType Type => _original.Type;
    public bool Permission => _original.Permission;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordGuildUsersSearchQuery : IDiscordGuildUsersSearchQuery
{
    private readonly NetCord.Rest.IGuildUsersSearchQuery _original;
    public DiscordGuildUsersSearchQuery(NetCord.Rest.IGuildUsersSearchQuery original)
    {
        _original = original;
    }
    public NetCord.Rest.IGuildUsersSearchQuery Original => _original;
    public System.Void Serialize(Utf8JsonWriter writer) => _original.Serialize(writer);
}


public class DiscordWebhookOptions : IDiscordWebhookOptions
{
    private readonly NetCord.Rest.WebhookOptions _original;
    public DiscordWebhookOptions(NetCord.Rest.WebhookOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.WebhookOptions Original => _original;
    public string Name => _original.Name;
    public NetCord.Rest.ImageProperties? Avatar => _original.Avatar;
    public ulong? ChannelId => _original.ChannelId;
    public IDiscordWebhookOptions WithName(string name) => new DiscordWebhookOptions(_original.WithName(name));
    public IDiscordWebhookOptions WithAvatar(NetCord.Rest.ImageProperties? avatar) => new DiscordWebhookOptions(_original.WithAvatar(avatar));
    public IDiscordWebhookOptions WithChannelId(ulong? channelId) => new DiscordWebhookOptions(_original.WithChannelId(channelId));
}


public class DiscordNonceProperties : IDiscordNonceProperties
{
    private readonly NetCord.Rest.NonceProperties _original;
    public DiscordNonceProperties(NetCord.Rest.NonceProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.NonceProperties Original => _original;
    public bool Unique => _original.Unique;
    public IDiscordNonceProperties WithUnique(bool unique = true) => new DiscordNonceProperties(_original.WithUnique(unique));
}


public class DiscordMessageReferenceProperties : IDiscordMessageReferenceProperties
{
    private readonly NetCord.Rest.MessageReferenceProperties _original;
    public DiscordMessageReferenceProperties(NetCord.Rest.MessageReferenceProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.MessageReferenceProperties Original => _original;
    public NetCord.MessageReferenceType Type => _original.Type;
    public ulong? ChannelId => _original.ChannelId;
    public ulong MessageId => _original.MessageId;
    public bool FailIfNotExists => _original.FailIfNotExists;
    public IDiscordMessageReferenceProperties WithType(NetCord.MessageReferenceType type) => new DiscordMessageReferenceProperties(_original.WithType(type));
    public IDiscordMessageReferenceProperties WithChannelId(ulong? channelId) => new DiscordMessageReferenceProperties(_original.WithChannelId(channelId));
    public IDiscordMessageReferenceProperties WithMessageId(ulong messageId) => new DiscordMessageReferenceProperties(_original.WithMessageId(messageId));
    public IDiscordMessageReferenceProperties WithFailIfNotExists(bool failIfNotExists = true) => new DiscordMessageReferenceProperties(_original.WithFailIfNotExists(failIfNotExists));
}


public class DiscordAttachmentExpirationInfo : IDiscordAttachmentExpirationInfo
{
    private readonly NetCord.AttachmentExpirationInfo _original;
    public DiscordAttachmentExpirationInfo(NetCord.AttachmentExpirationInfo original)
    {
        _original = original;
    }
    public NetCord.AttachmentExpirationInfo Original => _original;
    public System.DateTimeOffset ExpiresAt => _original.ExpiresAt;
    public System.DateTimeOffset IssuedAt => _original.IssuedAt;
    public string Signature => _original.Signature;
}


public class DiscordEmbedFooter : IDiscordEmbedFooter
{
    private readonly NetCord.EmbedFooter _original;
    public DiscordEmbedFooter(NetCord.EmbedFooter original)
    {
        _original = original;
    }
    public NetCord.EmbedFooter Original => _original;
    public string Text => _original.Text;
    public string IconUrl => _original.IconUrl;
    public string ProxyIconUrl => _original.ProxyIconUrl;
}


public class DiscordEmbedImage : IDiscordEmbedImage
{
    private readonly NetCord.EmbedImage _original;
    public DiscordEmbedImage(NetCord.EmbedImage original)
    {
        _original = original;
    }
    public NetCord.EmbedImage Original => _original;
    public string Url => _original.Url;
    public string ProxyUrl => _original.ProxyUrl;
    public int? Height => _original.Height;
    public int? Width => _original.Width;
}


public class DiscordEmbedThumbnail : IDiscordEmbedThumbnail
{
    private readonly NetCord.EmbedThumbnail _original;
    public DiscordEmbedThumbnail(NetCord.EmbedThumbnail original)
    {
        _original = original;
    }
    public NetCord.EmbedThumbnail Original => _original;
    public string Url => _original.Url;
    public string ProxyUrl => _original.ProxyUrl;
    public int? Height => _original.Height;
    public int? Width => _original.Width;
}


public class DiscordEmbedVideo : IDiscordEmbedVideo
{
    private readonly NetCord.EmbedVideo _original;
    public DiscordEmbedVideo(NetCord.EmbedVideo original)
    {
        _original = original;
    }
    public NetCord.EmbedVideo Original => _original;
    public string Url => _original.Url;
    public string ProxyUrl => _original.ProxyUrl;
    public int? Height => _original.Height;
    public int? Width => _original.Width;
}


public class DiscordEmbedProvider : IDiscordEmbedProvider
{
    private readonly NetCord.EmbedProvider _original;
    public DiscordEmbedProvider(NetCord.EmbedProvider original)
    {
        _original = original;
    }
    public NetCord.EmbedProvider Original => _original;
    public string Name => _original.Name;
    public string Url => _original.Url;
}


public class DiscordEmbedAuthor : IDiscordEmbedAuthor
{
    private readonly NetCord.EmbedAuthor _original;
    public DiscordEmbedAuthor(NetCord.EmbedAuthor original)
    {
        _original = original;
    }
    public NetCord.EmbedAuthor Original => _original;
    public string Name => _original.Name;
    public string Url => _original.Url;
    public string IconUrl => _original.IconUrl;
    public string ProxyIconUrl => _original.ProxyIconUrl;
}


public class DiscordEmbedField : IDiscordEmbedField
{
    private readonly NetCord.EmbedField _original;
    public DiscordEmbedField(NetCord.EmbedField original)
    {
        _original = original;
    }
    public NetCord.EmbedField Original => _original;
    public string Name => _original.Name;
    public string Value => _original.Value;
    public bool Inline => _original.Inline;
}


public class DiscordMessageReactionCountDetails : IDiscordMessageReactionCountDetails
{
    private readonly NetCord.MessageReactionCountDetails _original;
    public DiscordMessageReactionCountDetails(NetCord.MessageReactionCountDetails original)
    {
        _original = original;
    }
    public NetCord.MessageReactionCountDetails Original => _original;
    public int Burst => _original.Burst;
    public int Normal => _original.Normal;
}


public class DiscordMessageReactionEmoji : IDiscordMessageReactionEmoji
{
    private readonly NetCord.MessageReactionEmoji _original;
    public DiscordMessageReactionEmoji(NetCord.MessageReactionEmoji original)
    {
        _original = original;
    }
    public NetCord.MessageReactionEmoji Original => _original;
    public ulong? Id => _original.Id;
    public string Name => _original.Name;
    public bool Animated => _original.Animated;
}


public class DiscordTeam : IDiscordTeam
{
    private readonly NetCord.Team _original;
    public DiscordTeam(NetCord.Team original)
    {
        _original = original;
    }
    public NetCord.Team Original => _original;
    public ulong Id => _original.Id;
    public string IconHash => _original.IconHash;
    public IReadOnlyList<IDiscordTeamUser> Users => _original.Users.Select(x => new DiscordTeamUser(x)).ToList();
    public string Name => _original.Name;
    public ulong OwnerId => _original.OwnerId;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordImageUrl GetIconUrl(NetCord.ImageFormat format) => new DiscordImageUrl(_original.GetIconUrl(format));
}


public class DiscordApplicationInstallParams : IDiscordApplicationInstallParams
{
    private readonly NetCord.ApplicationInstallParams _original;
    public DiscordApplicationInstallParams(NetCord.ApplicationInstallParams original)
    {
        _original = original;
    }
    public NetCord.ApplicationInstallParams Original => _original;
    public IReadOnlyList<string> Scopes => _original.Scopes;
    public NetCord.Permissions Permissions => _original.Permissions;
}


public class DiscordApplicationIntegrationTypeConfiguration : IDiscordApplicationIntegrationTypeConfiguration
{
    private readonly NetCord.ApplicationIntegrationTypeConfiguration _original;
    public DiscordApplicationIntegrationTypeConfiguration(NetCord.ApplicationIntegrationTypeConfiguration original)
    {
        _original = original;
    }
    public NetCord.ApplicationIntegrationTypeConfiguration Original => _original;
    public IDiscordApplicationInstallParams OAuth2InstallParams => new DiscordApplicationInstallParams(_original.OAuth2InstallParams);
}


public class DiscordApplicationEmoji : IDiscordApplicationEmoji
{
    private readonly NetCord.ApplicationEmoji _original;
    public DiscordApplicationEmoji(NetCord.ApplicationEmoji original)
    {
        _original = original;
    }
    public NetCord.ApplicationEmoji Original => _original;
    public ulong ApplicationId => _original.ApplicationId;
    public ulong Id => _original.Id;
    public IDiscordUser Creator => new DiscordUser(_original.Creator);
    public bool? RequireColons => _original.RequireColons;
    public bool? Managed => _original.Managed;
    public bool? Available => _original.Available;
    public string Name => _original.Name;
    public bool Animated => _original.Animated;
    public async Task<IDiscordApplicationEmoji> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationEmoji(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordApplicationEmoji> ModifyAsync(Action<IDiscordApplicationEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationEmoji(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAsync(properties.Original, cancellationToken);
    public IDiscordImageUrl GetImageUrl(NetCord.ImageFormat format) => new DiscordImageUrl(_original.GetImageUrl(format));
}


public class DiscordApplicationEmojiProperties : IDiscordApplicationEmojiProperties
{
    private readonly NetCord.Rest.ApplicationEmojiProperties _original;
    public DiscordApplicationEmojiProperties(NetCord.Rest.ApplicationEmojiProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationEmojiProperties Original => _original;
    public string Name => _original.Name;
    public NetCord.Rest.ImageProperties Image => _original.Image;
    public IDiscordApplicationEmojiProperties WithName(string name) => new DiscordApplicationEmojiProperties(_original.WithName(name));
    public IDiscordApplicationEmojiProperties WithImage(NetCord.Rest.ImageProperties image) => new DiscordApplicationEmojiProperties(_original.WithImage(image));
}


public class DiscordApplicationEmojiOptions : IDiscordApplicationEmojiOptions
{
    private readonly NetCord.Rest.ApplicationEmojiOptions _original;
    public DiscordApplicationEmojiOptions(NetCord.Rest.ApplicationEmojiOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationEmojiOptions Original => _original;
    public string Name => _original.Name;
    public IDiscordApplicationEmojiOptions WithName(string name) => new DiscordApplicationEmojiOptions(_original.WithName(name));
}


public class DiscordMessageSnapshotMessage : IDiscordMessageSnapshotMessage
{
    private readonly NetCord.MessageSnapshotMessage _original;
    public DiscordMessageSnapshotMessage(NetCord.MessageSnapshotMessage original)
    {
        _original = original;
    }
    public NetCord.MessageSnapshotMessage Original => _original;
    public NetCord.MessageType Type => _original.Type;
    public string Content => _original.Content;
    public IReadOnlyList<IDiscordEmbed> Embeds => _original.Embeds.Select(x => new DiscordEmbed(x)).ToList();
    public IReadOnlyList<IDiscordAttachment> Attachments => _original.Attachments.Select(x => new DiscordAttachment(x)).ToList();
    public System.DateTimeOffset? EditedAt => _original.EditedAt;
    public NetCord.MessageFlags Flags => _original.Flags;
    public IReadOnlyList<IDiscordUser> MentionedUsers => _original.MentionedUsers.Select(x => new DiscordUser(x)).ToList();
    public IReadOnlyList<ulong> MentionedRoleIds => _original.MentionedRoleIds;
}


public class DiscordMessagePollMedia : IDiscordMessagePollMedia
{
    private readonly NetCord.MessagePollMedia _original;
    public DiscordMessagePollMedia(NetCord.MessagePollMedia original)
    {
        _original = original;
    }
    public NetCord.MessagePollMedia Original => _original;
    public string Text => _original.Text;
    public IDiscordEmojiReference Emoji => new DiscordEmojiReference(_original.Emoji);
}


public class DiscordMessagePollAnswer : IDiscordMessagePollAnswer
{
    private readonly NetCord.MessagePollAnswer _original;
    public DiscordMessagePollAnswer(NetCord.MessagePollAnswer original)
    {
        _original = original;
    }
    public NetCord.MessagePollAnswer Original => _original;
    public int AnswerId => _original.AnswerId;
    public IDiscordMessagePollMedia PollMedia => new DiscordMessagePollMedia(_original.PollMedia);
}


public class DiscordMessagePollResults : IDiscordMessagePollResults
{
    private readonly NetCord.MessagePollResults _original;
    public DiscordMessagePollResults(NetCord.MessagePollResults original)
    {
        _original = original;
    }
    public NetCord.MessagePollResults Original => _original;
    public bool IsFinalized => _original.IsFinalized;
    public IReadOnlyList<IDiscordMessagePollAnswerCount> Answers => _original.Answers.Select(x => new DiscordMessagePollAnswerCount(x)).ToList();
}


public class DiscordEmbedFooterProperties : IDiscordEmbedFooterProperties
{
    private readonly NetCord.Rest.EmbedFooterProperties _original;
    public DiscordEmbedFooterProperties(NetCord.Rest.EmbedFooterProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.EmbedFooterProperties Original => _original;
    public string Text => _original.Text;
    public string IconUrl => _original.IconUrl;
    public IDiscordEmbedFooterProperties WithText(string text) => new DiscordEmbedFooterProperties(_original.WithText(text));
    public IDiscordEmbedFooterProperties WithIconUrl(string iconUrl) => new DiscordEmbedFooterProperties(_original.WithIconUrl(iconUrl));
}


public class DiscordEmbedImageProperties : IDiscordEmbedImageProperties
{
    private readonly NetCord.Rest.EmbedImageProperties _original;
    public DiscordEmbedImageProperties(NetCord.Rest.EmbedImageProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.EmbedImageProperties Original => _original;
    public string Url => _original.Url;
    public IDiscordEmbedImageProperties WithUrl(string url) => new DiscordEmbedImageProperties(_original.WithUrl(url));
}


public class DiscordEmbedThumbnailProperties : IDiscordEmbedThumbnailProperties
{
    private readonly NetCord.Rest.EmbedThumbnailProperties _original;
    public DiscordEmbedThumbnailProperties(NetCord.Rest.EmbedThumbnailProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.EmbedThumbnailProperties Original => _original;
    public string Url => _original.Url;
    public IDiscordEmbedThumbnailProperties WithUrl(string url) => new DiscordEmbedThumbnailProperties(_original.WithUrl(url));
}


public class DiscordEmbedAuthorProperties : IDiscordEmbedAuthorProperties
{
    private readonly NetCord.Rest.EmbedAuthorProperties _original;
    public DiscordEmbedAuthorProperties(NetCord.Rest.EmbedAuthorProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.EmbedAuthorProperties Original => _original;
    public string Name => _original.Name;
    public string Url => _original.Url;
    public string IconUrl => _original.IconUrl;
    public IDiscordEmbedAuthorProperties WithName(string name) => new DiscordEmbedAuthorProperties(_original.WithName(name));
    public IDiscordEmbedAuthorProperties WithUrl(string url) => new DiscordEmbedAuthorProperties(_original.WithUrl(url));
    public IDiscordEmbedAuthorProperties WithIconUrl(string iconUrl) => new DiscordEmbedAuthorProperties(_original.WithIconUrl(iconUrl));
}


public class DiscordEmbedFieldProperties : IDiscordEmbedFieldProperties
{
    private readonly NetCord.Rest.EmbedFieldProperties _original;
    public DiscordEmbedFieldProperties(NetCord.Rest.EmbedFieldProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.EmbedFieldProperties Original => _original;
    public string Name => _original.Name;
    public string Value => _original.Value;
    public bool Inline => _original.Inline;
    public IDiscordEmbedFieldProperties WithName(string name) => new DiscordEmbedFieldProperties(_original.WithName(name));
    public IDiscordEmbedFieldProperties WithValue(string value) => new DiscordEmbedFieldProperties(_original.WithValue(value));
    public IDiscordEmbedFieldProperties WithInline(bool inline = true) => new DiscordEmbedFieldProperties(_original.WithInline(inline));
}


public class DiscordMessagePollMediaProperties : IDiscordMessagePollMediaProperties
{
    private readonly NetCord.MessagePollMediaProperties _original;
    public DiscordMessagePollMediaProperties(NetCord.MessagePollMediaProperties original)
    {
        _original = original;
    }
    public NetCord.MessagePollMediaProperties Original => _original;
    public string Text => _original.Text;
    public IDiscordEmojiProperties Emoji => new DiscordEmojiProperties(_original.Emoji);
    public IDiscordMessagePollMediaProperties WithText(string text) => new DiscordMessagePollMediaProperties(_original.WithText(text));
    public IDiscordMessagePollMediaProperties WithEmoji(IDiscordEmojiProperties emoji) => new DiscordMessagePollMediaProperties(_original.WithEmoji(emoji.Original));
}


public class DiscordMessagePollAnswerProperties : IDiscordMessagePollAnswerProperties
{
    private readonly NetCord.MessagePollAnswerProperties _original;
    public DiscordMessagePollAnswerProperties(NetCord.MessagePollAnswerProperties original)
    {
        _original = original;
    }
    public NetCord.MessagePollAnswerProperties Original => _original;
    public IDiscordMessagePollMediaProperties PollMedia => new DiscordMessagePollMediaProperties(_original.PollMedia);
    public IDiscordMessagePollAnswerProperties WithPollMedia(IDiscordMessagePollMediaProperties pollMedia) => new DiscordMessagePollAnswerProperties(_original.WithPollMedia(pollMedia.Original));
}


public class DiscordWebhookClient : IDiscordWebhookClient
{
    private readonly NetCord.Rest.WebhookClient _original;
    public DiscordWebhookClient(NetCord.Rest.WebhookClient original)
    {
        _original = original;
    }
    public NetCord.Rest.WebhookClient Original => _original;
    public ulong Id => _original.Id;
    public string Token => _original.Token;
    public System.Void Dispose() => _original.Dispose();
    public async Task<IDiscordWebhook> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordWebhook(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordWebhook> ModifyAsync(Action<IDiscordWebhookOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordWebhook(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAsync(properties.Original, cancellationToken);
    public async Task<IDiscordRestMessage> ExecuteAsync(IDiscordWebhookMessageProperties message, bool wait = false, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.ExecuteAsync(message.Original, wait, threadId, withComponents, properties.Original, cancellationToken));
    public async Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.GetMessageAsync(messageId, properties.Original, cancellationToken));
    public async Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.ModifyMessageAsync(messageId, action, threadId, withComponents, properties.Original, cancellationToken));
    public Task DeleteMessageAsync(ulong messageId, ulong? threadId = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessageAsync(messageId, threadId, properties.Original, cancellationToken);
}


public class DiscordWebhookClientConfiguration : IDiscordWebhookClientConfiguration
{
    private readonly NetCord.Rest.WebhookClientConfiguration _original;
    public DiscordWebhookClientConfiguration(NetCord.Rest.WebhookClientConfiguration original)
    {
        _original = original;
    }
    public NetCord.Rest.WebhookClientConfiguration Original => _original;
    public IDiscordRestClient Client => new DiscordRestClient(_original.Client);
}


public class DiscordWebhookMessageProperties : IDiscordWebhookMessageProperties
{
    private readonly NetCord.Rest.WebhookMessageProperties _original;
    public DiscordWebhookMessageProperties(NetCord.Rest.WebhookMessageProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.WebhookMessageProperties Original => _original;
    public string Content => _original.Content;
    public string Username => _original.Username;
    public string AvatarUrl => _original.AvatarUrl;
    public bool Tts => _original.Tts;
    public IEnumerable<IDiscordEmbedProperties> Embeds => _original.Embeds.Select(x => new DiscordEmbedProperties(x));
    public IDiscordAllowedMentionsProperties AllowedMentions => new DiscordAllowedMentionsProperties(_original.AllowedMentions);
    public IEnumerable<IDiscordComponentProperties> Components => _original.Components.Select(x => new DiscordComponentProperties(x));
    public IEnumerable<IDiscordAttachmentProperties> Attachments => _original.Attachments.Select(x => new DiscordAttachmentProperties(x));
    public NetCord.MessageFlags? Flags => _original.Flags;
    public string ThreadName => _original.ThreadName;
    public IEnumerable<ulong> AppliedTags => _original.AppliedTags;
    public IDiscordMessagePollProperties Poll => new DiscordMessagePollProperties(_original.Poll);
    public HttpContent Serialize() => _original.Serialize();
    public IDiscordWebhookMessageProperties WithContent(string content) => new DiscordWebhookMessageProperties(_original.WithContent(content));
    public IDiscordWebhookMessageProperties WithUsername(string username) => new DiscordWebhookMessageProperties(_original.WithUsername(username));
    public IDiscordWebhookMessageProperties WithAvatarUrl(string avatarUrl) => new DiscordWebhookMessageProperties(_original.WithAvatarUrl(avatarUrl));
    public IDiscordWebhookMessageProperties WithTts(bool tts = true) => new DiscordWebhookMessageProperties(_original.WithTts(tts));
    public IDiscordWebhookMessageProperties WithEmbeds(IEnumerable<IDiscordEmbedProperties> embeds) => new DiscordWebhookMessageProperties(_original.WithEmbeds(embeds?.Select(x => x.Original)));
    public IDiscordWebhookMessageProperties AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds) => new DiscordWebhookMessageProperties(_original.AddEmbeds(embeds?.Select(x => x.Original)));
    public IDiscordWebhookMessageProperties AddEmbeds(IDiscordEmbedProperties[] embeds) => new DiscordWebhookMessageProperties(_original.AddEmbeds(embeds.Original));
    public IDiscordWebhookMessageProperties WithAllowedMentions(IDiscordAllowedMentionsProperties allowedMentions) => new DiscordWebhookMessageProperties(_original.WithAllowedMentions(allowedMentions.Original));
    public IDiscordWebhookMessageProperties WithComponents(IEnumerable<IDiscordComponentProperties> components) => new DiscordWebhookMessageProperties(_original.WithComponents(components?.Select(x => x.Original)));
    public IDiscordWebhookMessageProperties AddComponents(IEnumerable<IDiscordComponentProperties> components) => new DiscordWebhookMessageProperties(_original.AddComponents(components?.Select(x => x.Original)));
    public IDiscordWebhookMessageProperties AddComponents(IDiscordComponentProperties[] components) => new DiscordWebhookMessageProperties(_original.AddComponents(components.Original));
    public IDiscordWebhookMessageProperties WithAttachments(IEnumerable<IDiscordAttachmentProperties> attachments) => new DiscordWebhookMessageProperties(_original.WithAttachments(attachments?.Select(x => x.Original)));
    public IDiscordWebhookMessageProperties AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments) => new DiscordWebhookMessageProperties(_original.AddAttachments(attachments?.Select(x => x.Original)));
    public IDiscordWebhookMessageProperties AddAttachments(IDiscordAttachmentProperties[] attachments) => new DiscordWebhookMessageProperties(_original.AddAttachments(attachments.Original));
    public IDiscordWebhookMessageProperties WithFlags(NetCord.MessageFlags? flags) => new DiscordWebhookMessageProperties(_original.WithFlags(flags));
    public IDiscordWebhookMessageProperties WithThreadName(string threadName) => new DiscordWebhookMessageProperties(_original.WithThreadName(threadName));
    public IDiscordWebhookMessageProperties WithAppliedTags(IEnumerable<ulong> appliedTags) => new DiscordWebhookMessageProperties(_original.WithAppliedTags(appliedTags));
    public IDiscordWebhookMessageProperties AddAppliedTags(IEnumerable<ulong> appliedTags) => new DiscordWebhookMessageProperties(_original.AddAppliedTags(appliedTags));
    public IDiscordWebhookMessageProperties AddAppliedTags(ulong[] appliedTags) => new DiscordWebhookMessageProperties(_original.AddAppliedTags(appliedTags));
    public IDiscordWebhookMessageProperties WithPoll(IDiscordMessagePollProperties poll) => new DiscordWebhookMessageProperties(_original.WithPoll(poll.Original));
}


public class DiscordUserActivityTimestamps : IDiscordUserActivityTimestamps
{
    private readonly NetCord.Gateway.UserActivityTimestamps _original;
    public DiscordUserActivityTimestamps(NetCord.Gateway.UserActivityTimestamps original)
    {
        _original = original;
    }
    public NetCord.Gateway.UserActivityTimestamps Original => _original;
    public System.DateTimeOffset? StartTime => _original.StartTime;
    public System.DateTimeOffset? EndTime => _original.EndTime;
}


public class DiscordEmoji : IDiscordEmoji
{
    private readonly NetCord.Emoji _original;
    public DiscordEmoji(NetCord.Emoji original)
    {
        _original = original;
    }
    public NetCord.Emoji Original => _original;
    public string Name => _original.Name;
    public bool Animated => _original.Animated;
}


public class DiscordParty : IDiscordParty
{
    private readonly NetCord.Gateway.Party _original;
    public DiscordParty(NetCord.Gateway.Party original)
    {
        _original = original;
    }
    public NetCord.Gateway.Party Original => _original;
    public string Id => _original.Id;
    public int? CurrentSize => _original.CurrentSize;
    public int? MaxSize => _original.MaxSize;
}


public class DiscordUserActivityAssets : IDiscordUserActivityAssets
{
    private readonly NetCord.Gateway.UserActivityAssets _original;
    public DiscordUserActivityAssets(NetCord.Gateway.UserActivityAssets original)
    {
        _original = original;
    }
    public NetCord.Gateway.UserActivityAssets Original => _original;
    public string LargeImageId => _original.LargeImageId;
    public string LargeText => _original.LargeText;
    public string SmallImageId => _original.SmallImageId;
    public string SmallText => _original.SmallText;
}


public class DiscordUserActivitySecrets : IDiscordUserActivitySecrets
{
    private readonly NetCord.Gateway.UserActivitySecrets _original;
    public DiscordUserActivitySecrets(NetCord.Gateway.UserActivitySecrets original)
    {
        _original = original;
    }
    public NetCord.Gateway.UserActivitySecrets Original => _original;
    public string Join => _original.Join;
    public string Spectate => _original.Spectate;
    public string Match => _original.Match;
}


public class DiscordUserActivityButton : IDiscordUserActivityButton
{
    private readonly NetCord.Gateway.UserActivityButton _original;
    public DiscordUserActivityButton(NetCord.Gateway.UserActivityButton original)
    {
        _original = original;
    }
    public NetCord.Gateway.UserActivityButton Original => _original;
    public string Label => _original.Label;
}


public class DiscordGuildScheduledEventRecurrenceRuleNWeekday : IDiscordGuildScheduledEventRecurrenceRuleNWeekday
{
    private readonly NetCord.GuildScheduledEventRecurrenceRuleNWeekday _original;
    public DiscordGuildScheduledEventRecurrenceRuleNWeekday(NetCord.GuildScheduledEventRecurrenceRuleNWeekday original)
    {
        _original = original;
    }
    public NetCord.GuildScheduledEventRecurrenceRuleNWeekday Original => _original;
    public int N => _original.N;
    public NetCord.GuildScheduledEventRecurrenceRuleWeekday Day => _original.Day;
}


public class DiscordAutoModerationActionMetadata : IDiscordAutoModerationActionMetadata
{
    private readonly NetCord.AutoModerationActionMetadata _original;
    public DiscordAutoModerationActionMetadata(NetCord.AutoModerationActionMetadata original)
    {
        _original = original;
    }
    public NetCord.AutoModerationActionMetadata Original => _original;
    public ulong? ChannelId => _original.ChannelId;
    public int? DurationSeconds => _original.DurationSeconds;
    public string CustomMessage => _original.CustomMessage;
}


public class DiscordAutoModerationActionMetadataProperties : IDiscordAutoModerationActionMetadataProperties
{
    private readonly NetCord.AutoModerationActionMetadataProperties _original;
    public DiscordAutoModerationActionMetadataProperties(NetCord.AutoModerationActionMetadataProperties original)
    {
        _original = original;
    }
    public NetCord.AutoModerationActionMetadataProperties Original => _original;
    public ulong? ChannelId => _original.ChannelId;
    public int? DurationSeconds => _original.DurationSeconds;
    public string CustomMessage => _original.CustomMessage;
    public IDiscordAutoModerationActionMetadataProperties WithChannelId(ulong? channelId) => new DiscordAutoModerationActionMetadataProperties(_original.WithChannelId(channelId));
    public IDiscordAutoModerationActionMetadataProperties WithDurationSeconds(int? durationSeconds) => new DiscordAutoModerationActionMetadataProperties(_original.WithDurationSeconds(durationSeconds));
    public IDiscordAutoModerationActionMetadataProperties WithCustomMessage(string customMessage) => new DiscordAutoModerationActionMetadataProperties(_original.WithCustomMessage(customMessage));
}


public class DiscordEmojiProperties : IDiscordEmojiProperties
{
    private readonly NetCord.EmojiProperties _original;
    public DiscordEmojiProperties(NetCord.EmojiProperties original)
    {
        _original = original;
    }
    public NetCord.EmojiProperties Original => _original;
    public ulong? Id => _original.Id;
    public string Unicode => _original.Unicode;
    public IDiscordEmojiProperties WithId(ulong? id) => new DiscordEmojiProperties(_original.WithId(id));
    public IDiscordEmojiProperties WithUnicode(string unicode) => new DiscordEmojiProperties(_original.WithUnicode(unicode));
}


public class DiscordGuildOnboardingPromptOption : IDiscordGuildOnboardingPromptOption
{
    private readonly NetCord.Rest.GuildOnboardingPromptOption _original;
    public DiscordGuildOnboardingPromptOption(NetCord.Rest.GuildOnboardingPromptOption original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildOnboardingPromptOption Original => _original;
    public ulong Id => _original.Id;
    public IReadOnlyList<ulong> ChannelIds => _original.ChannelIds;
    public IReadOnlyList<ulong> RoleIds => _original.RoleIds;
    public IDiscordEmoji Emoji => new DiscordEmoji(_original.Emoji);
    public string Title => _original.Title;
    public string Description => _original.Description;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordGuildOnboardingPromptOptionProperties : IDiscordGuildOnboardingPromptOptionProperties
{
    private readonly NetCord.Rest.GuildOnboardingPromptOptionProperties _original;
    public DiscordGuildOnboardingPromptOptionProperties(NetCord.Rest.GuildOnboardingPromptOptionProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildOnboardingPromptOptionProperties Original => _original;
    public ulong? Id => _original.Id;
    public IEnumerable<ulong> ChannelIds => _original.ChannelIds;
    public IEnumerable<ulong> RoleIds => _original.RoleIds;
    public ulong? EmojiId => _original.EmojiId;
    public string EmojiName => _original.EmojiName;
    public bool? EmojiAnimated => _original.EmojiAnimated;
    public string Title => _original.Title;
    public string Description => _original.Description;
    public IDiscordGuildOnboardingPromptOptionProperties WithId(ulong? id) => new DiscordGuildOnboardingPromptOptionProperties(_original.WithId(id));
    public IDiscordGuildOnboardingPromptOptionProperties WithChannelIds(IEnumerable<ulong> channelIds) => new DiscordGuildOnboardingPromptOptionProperties(_original.WithChannelIds(channelIds));
    public IDiscordGuildOnboardingPromptOptionProperties AddChannelIds(IEnumerable<ulong> channelIds) => new DiscordGuildOnboardingPromptOptionProperties(_original.AddChannelIds(channelIds));
    public IDiscordGuildOnboardingPromptOptionProperties AddChannelIds(ulong[] channelIds) => new DiscordGuildOnboardingPromptOptionProperties(_original.AddChannelIds(channelIds));
    public IDiscordGuildOnboardingPromptOptionProperties WithRoleIds(IEnumerable<ulong> roleIds) => new DiscordGuildOnboardingPromptOptionProperties(_original.WithRoleIds(roleIds));
    public IDiscordGuildOnboardingPromptOptionProperties AddRoleIds(IEnumerable<ulong> roleIds) => new DiscordGuildOnboardingPromptOptionProperties(_original.AddRoleIds(roleIds));
    public IDiscordGuildOnboardingPromptOptionProperties AddRoleIds(ulong[] roleIds) => new DiscordGuildOnboardingPromptOptionProperties(_original.AddRoleIds(roleIds));
    public IDiscordGuildOnboardingPromptOptionProperties WithEmojiId(ulong? emojiId) => new DiscordGuildOnboardingPromptOptionProperties(_original.WithEmojiId(emojiId));
    public IDiscordGuildOnboardingPromptOptionProperties WithEmojiName(string emojiName) => new DiscordGuildOnboardingPromptOptionProperties(_original.WithEmojiName(emojiName));
    public IDiscordGuildOnboardingPromptOptionProperties WithEmojiAnimated(bool? emojiAnimated = true) => new DiscordGuildOnboardingPromptOptionProperties(_original.WithEmojiAnimated(emojiAnimated));
    public IDiscordGuildOnboardingPromptOptionProperties WithTitle(string title) => new DiscordGuildOnboardingPromptOptionProperties(_original.WithTitle(title));
    public IDiscordGuildOnboardingPromptOptionProperties WithDescription(string description) => new DiscordGuildOnboardingPromptOptionProperties(_original.WithDescription(description));
}


public class DiscordApplicationCommandOptionChoice : IDiscordApplicationCommandOptionChoice
{
    private readonly NetCord.Rest.ApplicationCommandOptionChoice _original;
    public DiscordApplicationCommandOptionChoice(NetCord.Rest.ApplicationCommandOptionChoice original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationCommandOptionChoice Original => _original;
    public string Name => _original.Name;
    public IReadOnlyDictionary<string, string> NameLocalizations => _original.NameLocalizations;
    public string ValueString => _original.ValueString;
    public double? ValueNumeric => _original.ValueNumeric;
    public NetCord.Rest.ApplicationCommandOptionChoiceValueType ValueType => _original.ValueType;
}


public class DiscordApplicationCommandOptionChoiceProperties : IDiscordApplicationCommandOptionChoiceProperties
{
    private readonly NetCord.Rest.ApplicationCommandOptionChoiceProperties _original;
    public DiscordApplicationCommandOptionChoiceProperties(NetCord.Rest.ApplicationCommandOptionChoiceProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationCommandOptionChoiceProperties Original => _original;
    public string Name => _original.Name;
    public IReadOnlyDictionary<string, string> NameLocalizations => _original.NameLocalizations;
    public string StringValue => _original.StringValue;
    public double? NumericValue => _original.NumericValue;
    public NetCord.Rest.ApplicationCommandOptionChoiceValueType ValueType => _original.ValueType;
    public IDiscordApplicationCommandOptionChoiceProperties WithName(string name) => new DiscordApplicationCommandOptionChoiceProperties(_original.WithName(name));
    public IDiscordApplicationCommandOptionChoiceProperties WithNameLocalizations(IReadOnlyDictionary<string, string> nameLocalizations) => new DiscordApplicationCommandOptionChoiceProperties(_original.WithNameLocalizations(nameLocalizations));
    public IDiscordApplicationCommandOptionChoiceProperties WithStringValue(string stringValue) => new DiscordApplicationCommandOptionChoiceProperties(_original.WithStringValue(stringValue));
    public IDiscordApplicationCommandOptionChoiceProperties WithNumericValue(double? numericValue) => new DiscordApplicationCommandOptionChoiceProperties(_original.WithNumericValue(numericValue));
    public IDiscordApplicationCommandOptionChoiceProperties WithValueType(NetCord.Rest.ApplicationCommandOptionChoiceValueType valueType) => new DiscordApplicationCommandOptionChoiceProperties(_original.WithValueType(valueType));
}


public class DiscordTeamUser : IDiscordTeamUser
{
    private readonly NetCord.TeamUser _original;
    public DiscordTeamUser(NetCord.TeamUser original)
    {
        _original = original;
    }
    public NetCord.TeamUser Original => _original;
    public NetCord.MembershipState MembershipState => _original.MembershipState;
    public ulong TeamId => _original.TeamId;
    public NetCord.TeamRole Role => _original.Role;
    public ulong Id => _original.Id;
    public string Username => _original.Username;
    public ushort Discriminator => _original.Discriminator;
    public string GlobalName => _original.GlobalName;
    public string AvatarHash => _original.AvatarHash;
    public bool IsBot => _original.IsBot;
    public bool? IsSystemUser => _original.IsSystemUser;
    public bool? MfaEnabled => _original.MfaEnabled;
    public string BannerHash => _original.BannerHash;
    public NetCord.Color? AccentColor => _original.AccentColor;
    public string Locale => _original.Locale;
    public bool? Verified => _original.Verified;
    public string Email => _original.Email;
    public NetCord.UserFlags? Flags => _original.Flags;
    public NetCord.PremiumType? PremiumType => _original.PremiumType;
    public NetCord.UserFlags? PublicFlags => _original.PublicFlags;
    public IDiscordAvatarDecorationData AvatarDecorationData => new DiscordAvatarDecorationData(_original.AvatarDecorationData);
    public bool HasAvatar => _original.HasAvatar;
    public bool HasBanner => _original.HasBanner;
    public bool HasAvatarDecoration => _original.HasAvatarDecoration;
    public IDiscordImageUrl DefaultAvatarUrl => new DiscordImageUrl(_original.DefaultAvatarUrl);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordImageUrl GetAvatarUrl(NetCord.ImageFormat? format = default) => new DiscordImageUrl(_original.GetAvatarUrl(format));
    public IDiscordImageUrl GetBannerUrl(NetCord.ImageFormat? format = default) => new DiscordImageUrl(_original.GetBannerUrl(format));
    public IDiscordImageUrl GetAvatarDecorationUrl() => new DiscordImageUrl(_original.GetAvatarDecorationUrl());
    public async Task<IDiscordUser> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordUser(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordDMChannel> GetDMChannelAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordDMChannel(await _original.GetDMChannelAsync(properties.Original, cancellationToken));
}


public class DiscordEmojiReference : IDiscordEmojiReference
{
    private readonly NetCord.EmojiReference _original;
    public DiscordEmojiReference(NetCord.EmojiReference original)
    {
        _original = original;
    }
    public NetCord.EmojiReference Original => _original;
    public ulong? Id => _original.Id;
    public string Name => _original.Name;
    public bool Animated => _original.Animated;
}


public class DiscordMessagePollAnswerCount : IDiscordMessagePollAnswerCount
{
    private readonly NetCord.MessagePollAnswerCount _original;
    public DiscordMessagePollAnswerCount(NetCord.MessagePollAnswerCount original)
    {
        _original = original;
    }
    public NetCord.MessagePollAnswerCount Original => _original;
    public int AnswerId => _original.AnswerId;
    public int Count => _original.Count;
    public bool MeVoted => _original.MeVoted;
}


public class DiscordRestClient : IDiscordRestClient
{
    private readonly NetCord.Rest.RestClient _original;
    public DiscordRestClient(NetCord.Rest.RestClient original)
    {
        _original = original;
    }
    public NetCord.Rest.RestClient Original => _original;
    public IDiscordToken Token => new DiscordToken(_original.Token);
    public async Task<IDiscordCurrentApplication> GetCurrentApplicationAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordCurrentApplication(await _original.GetCurrentApplicationAsync(properties.Original, cancellationToken));
    public async Task<IDiscordCurrentApplication> ModifyCurrentApplicationAsync(Action<IDiscordCurrentApplicationOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordCurrentApplication(await _original.ModifyCurrentApplicationAsync(action, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordApplicationRoleConnectionMetadata>> GetApplicationRoleConnectionMetadataRecordsAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetApplicationRoleConnectionMetadataRecordsAsync(applicationId, properties.Original, cancellationToken)).Select(x => new DiscordApplicationRoleConnectionMetadata(x)).ToList();
    public async Task<IReadOnlyList<IDiscordApplicationRoleConnectionMetadata>> UpdateApplicationRoleConnectionMetadataRecordsAsync(ulong applicationId, IEnumerable<IDiscordApplicationRoleConnectionMetadataProperties> applicationRoleConnectionMetadataProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.UpdateApplicationRoleConnectionMetadataRecordsAsync(applicationId, applicationRoleConnectionMetadataProperties?.Select(x => x.Original), properties.Original, cancellationToken)).Select(x => new DiscordApplicationRoleConnectionMetadata(x)).ToList();
    public async IAsyncEnumerable<IDiscordRestAuditLogEntry> GetGuildAuditLogAsync(ulong guildId, IDiscordGuildAuditLogPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetGuildAuditLogAsync(guildId, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordRestAuditLogEntry(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordAutoModerationRule>> GetAutoModerationRulesAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetAutoModerationRulesAsync(guildId, properties.Original, cancellationToken)).Select(x => new DiscordAutoModerationRule(x)).ToList();
    public async Task<IDiscordAutoModerationRule> GetAutoModerationRuleAsync(ulong guildId, ulong autoModerationRuleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordAutoModerationRule(await _original.GetAutoModerationRuleAsync(guildId, autoModerationRuleId, properties.Original, cancellationToken));
    public async Task<IDiscordAutoModerationRule> CreateAutoModerationRuleAsync(ulong guildId, IDiscordAutoModerationRuleProperties autoModerationRuleProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordAutoModerationRule(await _original.CreateAutoModerationRuleAsync(guildId, autoModerationRuleProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordAutoModerationRule> ModifyAutoModerationRuleAsync(ulong guildId, ulong autoModerationRuleId, Action<IDiscordAutoModerationRuleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordAutoModerationRule(await _original.ModifyAutoModerationRuleAsync(guildId, autoModerationRuleId, action, properties.Original, cancellationToken));
    public Task DeleteAutoModerationRuleAsync(ulong guildId, ulong autoModerationRuleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAutoModerationRuleAsync(guildId, autoModerationRuleId, properties.Original, cancellationToken);
    public async Task<IDiscordChannel> GetChannelAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordChannel(await _original.GetChannelAsync(channelId, properties.Original, cancellationToken));
    public async Task<IDiscordChannel> ModifyGroupDMChannelAsync(ulong channelId, Action<IDiscordGroupDMChannelOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordChannel(await _original.ModifyGroupDMChannelAsync(channelId, action, properties.Original, cancellationToken));
    public async Task<IDiscordChannel> ModifyGuildChannelAsync(ulong channelId, Action<IDiscordGuildChannelOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordChannel(await _original.ModifyGuildChannelAsync(channelId, action, properties.Original, cancellationToken));
    public async Task<IDiscordChannel> DeleteChannelAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordChannel(await _original.DeleteChannelAsync(channelId, properties.Original, cancellationToken));
    public async IAsyncEnumerable<IDiscordRestMessage> GetMessagesAsync(ulong channelId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetMessagesAsync(channelId, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordRestMessage(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetMessagesAroundAsync(ulong channelId, ulong messageId, int? limit = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetMessagesAroundAsync(channelId, messageId, limit, properties.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    public async Task<IDiscordRestMessage> GetMessageAsync(ulong channelId, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.GetMessageAsync(channelId, messageId, properties.Original, cancellationToken));
    public async Task<IDiscordRestMessage> SendMessageAsync(ulong channelId, IDiscordMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.SendMessageAsync(channelId, message.Original, properties.Original, cancellationToken));
    public async Task<IDiscordRestMessage> CrosspostMessageAsync(ulong channelId, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.CrosspostMessageAsync(channelId, messageId, properties.Original, cancellationToken));
    public Task AddMessageReactionAsync(ulong channelId, ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.AddMessageReactionAsync(channelId, messageId, emoji.Original, properties.Original, cancellationToken);
    public Task DeleteMessageReactionAsync(ulong channelId, ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessageReactionAsync(channelId, messageId, emoji.Original, properties.Original, cancellationToken);
    public Task DeleteMessageReactionAsync(ulong channelId, ulong messageId, IDiscordReactionEmojiProperties emoji, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessageReactionAsync(channelId, messageId, emoji.Original, userId, properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordUser> GetMessageReactionsAsync(ulong channelId, ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordMessageReactionsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetMessageReactionsAsync(channelId, messageId, emoji.Original, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public Task DeleteAllMessageReactionsAsync(ulong channelId, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAllMessageReactionsAsync(channelId, messageId, properties.Original, cancellationToken);
    public Task DeleteAllMessageReactionsAsync(ulong channelId, ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAllMessageReactionsAsync(channelId, messageId, emoji.Original, properties.Original, cancellationToken);
    public async Task<IDiscordRestMessage> ModifyMessageAsync(ulong channelId, ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.ModifyMessageAsync(channelId, messageId, action, properties.Original, cancellationToken));
    public Task DeleteMessageAsync(ulong channelId, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessageAsync(channelId, messageId, properties.Original, cancellationToken);
    public Task DeleteMessagesAsync(ulong channelId, IEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessagesAsync(channelId, messageIds, properties.Original, cancellationToken);
    public Task DeleteMessagesAsync(ulong channelId, IAsyncEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessagesAsync(channelId, messageIds, properties.Original, cancellationToken);
    public Task ModifyGuildChannelPermissionsAsync(ulong channelId, IDiscordPermissionOverwriteProperties permissionOverwrite, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.ModifyGuildChannelPermissionsAsync(channelId, permissionOverwrite.Original, properties.Original, cancellationToken);
    public async Task<IEnumerable<IDiscordRestInvite>> GetGuildChannelInvitesAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetGuildChannelInvitesAsync(channelId, properties.Original, cancellationToken)).Select(x => new DiscordRestInvite(x));
    public async Task<IDiscordRestInvite> CreateGuildChannelInviteAsync(ulong channelId, IDiscordInviteProperties? inviteProperties = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestInvite(await _original.CreateGuildChannelInviteAsync(channelId, inviteProperties.Original, properties.Original, cancellationToken));
    public Task DeleteGuildChannelPermissionAsync(ulong channelId, ulong overwriteId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteGuildChannelPermissionAsync(channelId, overwriteId, properties.Original, cancellationToken);
    public async Task<IDiscordFollowedChannel> FollowAnnouncementGuildChannelAsync(ulong channelId, ulong webhookChannelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordFollowedChannel(await _original.FollowAnnouncementGuildChannelAsync(channelId, webhookChannelId, properties.Original, cancellationToken));
    public Task TriggerTypingStateAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.TriggerTypingStateAsync(channelId, properties.Original, cancellationToken);
    public Task<IDisposable> EnterTypingStateAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.EnterTypingStateAsync(channelId, properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetPinnedMessagesAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetPinnedMessagesAsync(channelId, properties.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    public Task PinMessageAsync(ulong channelId, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.PinMessageAsync(channelId, messageId, properties.Original, cancellationToken);
    public Task UnpinMessageAsync(ulong channelId, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.UnpinMessageAsync(channelId, messageId, properties.Original, cancellationToken);
    public Task GroupDMChannelAddUserAsync(ulong channelId, ulong userId, IDiscordGroupDMChannelUserAddProperties groupDMChannelUserAddProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.GroupDMChannelAddUserAsync(channelId, userId, groupDMChannelUserAddProperties.Original, properties.Original, cancellationToken);
    public Task GroupDMChannelDeleteUserAsync(ulong channelId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.GroupDMChannelDeleteUserAsync(channelId, userId, properties.Original, cancellationToken);
    public async Task<IDiscordGuildThread> CreateGuildThreadAsync(ulong channelId, ulong messageId, IDiscordGuildThreadFromMessageProperties threadFromMessageProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildThread(await _original.CreateGuildThreadAsync(channelId, messageId, threadFromMessageProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordGuildThread> CreateGuildThreadAsync(ulong channelId, IDiscordGuildThreadProperties threadProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildThread(await _original.CreateGuildThreadAsync(channelId, threadProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordForumGuildThread> CreateForumGuildThreadAsync(ulong channelId, IDiscordForumGuildThreadProperties threadProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordForumGuildThread(await _original.CreateForumGuildThreadAsync(channelId, threadProperties.Original, properties.Original, cancellationToken));
    public Task JoinGuildThreadAsync(ulong threadId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.JoinGuildThreadAsync(threadId, properties.Original, cancellationToken);
    public Task AddGuildThreadUserAsync(ulong threadId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.AddGuildThreadUserAsync(threadId, userId, properties.Original, cancellationToken);
    public Task LeaveGuildThreadAsync(ulong threadId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.LeaveGuildThreadAsync(threadId, properties.Original, cancellationToken);
    public Task DeleteGuildThreadUserAsync(ulong threadId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteGuildThreadUserAsync(threadId, userId, properties.Original, cancellationToken);
    public async Task<IDiscordThreadUser> GetGuildThreadUserAsync(ulong threadId, ulong userId, bool withGuildUser = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordThreadUser(await _original.GetGuildThreadUserAsync(threadId, userId, withGuildUser, properties.Original, cancellationToken));
    public async IAsyncEnumerable<IDiscordThreadUser> GetGuildThreadUsersAsync(ulong threadId, IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetGuildThreadUsersAsync(threadId, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordThreadUser(original);
        }
    }
    public async IAsyncEnumerable<IDiscordGuildThread> GetPublicArchivedGuildThreadsAsync(ulong channelId, IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetPublicArchivedGuildThreadsAsync(channelId, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordGuildThread(original);
        }
    }
    public async IAsyncEnumerable<IDiscordGuildThread> GetPrivateArchivedGuildThreadsAsync(ulong channelId, IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetPrivateArchivedGuildThreadsAsync(channelId, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordGuildThread(original);
        }
    }
    public async IAsyncEnumerable<IDiscordGuildThread> GetJoinedPrivateArchivedGuildThreadsAsync(ulong channelId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetJoinedPrivateArchivedGuildThreadsAsync(channelId, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordGuildThread(original);
        }
    }
    public Task<Stream> SendRequestAsync(HttpMethod method, FormattableString route, string? query = null, NetCord.Rest.TopLevelResourceInfo? resourceInfo = default, IDiscordRestRequestProperties? properties = null, bool global = true, System.Threading.CancellationToken cancellationToken = default) => _original.SendRequestAsync(method, route, query, resourceInfo, properties.Original, global, cancellationToken);
    public Task<Stream> SendRequestAsync(HttpMethod method, HttpContent content, FormattableString route, string? query = null, NetCord.Rest.TopLevelResourceInfo? resourceInfo = default, IDiscordRestRequestProperties? properties = null, bool global = true, System.Threading.CancellationToken cancellationToken = default) => _original.SendRequestAsync(method, content, route, query, resourceInfo, properties.Original, global, cancellationToken);
    public System.Void Dispose() => _original.Dispose();
    public async Task<IReadOnlyList<IDiscordGuildEmoji>> GetGuildEmojisAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetGuildEmojisAsync(guildId, properties.Original, cancellationToken)).Select(x => new DiscordGuildEmoji(x)).ToList();
    public async Task<IDiscordGuildEmoji> GetGuildEmojiAsync(ulong guildId, ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildEmoji(await _original.GetGuildEmojiAsync(guildId, emojiId, properties.Original, cancellationToken));
    public async Task<IDiscordGuildEmoji> CreateGuildEmojiAsync(ulong guildId, IDiscordGuildEmojiProperties guildEmojiProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildEmoji(await _original.CreateGuildEmojiAsync(guildId, guildEmojiProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordGuildEmoji> ModifyGuildEmojiAsync(ulong guildId, ulong emojiId, Action<IDiscordGuildEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildEmoji(await _original.ModifyGuildEmojiAsync(guildId, emojiId, action, properties.Original, cancellationToken));
    public Task DeleteGuildEmojiAsync(ulong guildId, ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteGuildEmojiAsync(guildId, emojiId, properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordApplicationEmoji>> GetApplicationEmojisAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetApplicationEmojisAsync(applicationId, properties.Original, cancellationToken)).Select(x => new DiscordApplicationEmoji(x)).ToList();
    public async Task<IDiscordApplicationEmoji> GetApplicationEmojiAsync(ulong applicationId, ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationEmoji(await _original.GetApplicationEmojiAsync(applicationId, emojiId, properties.Original, cancellationToken));
    public async Task<IDiscordApplicationEmoji> CreateApplicationEmojiAsync(ulong applicationId, IDiscordApplicationEmojiProperties applicationEmojiProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationEmoji(await _original.CreateApplicationEmojiAsync(applicationId, applicationEmojiProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordApplicationEmoji> ModifyApplicationEmojiAsync(ulong applicationId, ulong emojiId, Action<IDiscordApplicationEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationEmoji(await _original.ModifyApplicationEmojiAsync(applicationId, emojiId, action, properties.Original, cancellationToken));
    public Task DeleteApplicationEmojiAsync(ulong applicationId, ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteApplicationEmojiAsync(applicationId, emojiId, properties.Original, cancellationToken);
    public Task<string> GetGatewayAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.GetGatewayAsync(properties.Original, cancellationToken);
    public async Task<IDiscordGatewayBot> GetGatewayBotAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGatewayBot(await _original.GetGatewayBotAsync(properties.Original, cancellationToken));
    public async Task<IDiscordRestGuild> CreateGuildAsync(IDiscordGuildProperties guildProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestGuild(await _original.CreateGuildAsync(guildProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordRestGuild> GetGuildAsync(ulong guildId, bool withCounts = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestGuild(await _original.GetGuildAsync(guildId, withCounts, properties.Original, cancellationToken));
    public async Task<IDiscordGuildPreview> GetGuildPreviewAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildPreview(await _original.GetGuildPreviewAsync(guildId, properties.Original, cancellationToken));
    public async Task<IDiscordRestGuild> ModifyGuildAsync(ulong guildId, Action<IDiscordGuildOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestGuild(await _original.ModifyGuildAsync(guildId, action, properties.Original, cancellationToken));
    public Task DeleteGuildAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteGuildAsync(guildId, properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordGuildChannel>> GetGuildChannelsAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetGuildChannelsAsync(guildId, properties.Original, cancellationToken)).Select(x => new DiscordGuildChannel(x)).ToList();
    public async Task<IDiscordGuildChannel> CreateGuildChannelAsync(ulong guildId, IDiscordGuildChannelProperties channelProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildChannel(await _original.CreateGuildChannelAsync(guildId, channelProperties.Original, properties.Original, cancellationToken));
    public Task ModifyGuildChannelPositionsAsync(ulong guildId, IEnumerable<IDiscordGuildChannelPositionProperties> positions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.ModifyGuildChannelPositionsAsync(guildId, positions?.Select(x => x.Original), properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordGuildThread>> GetActiveGuildThreadsAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetActiveGuildThreadsAsync(guildId, properties.Original, cancellationToken)).Select(x => new DiscordGuildThread(x)).ToList();
    public async Task<IDiscordGuildUser> GetGuildUserAsync(ulong guildId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildUser(await _original.GetGuildUserAsync(guildId, userId, properties.Original, cancellationToken));
    public async IAsyncEnumerable<IDiscordGuildUser> GetGuildUsersAsync(ulong guildId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetGuildUsersAsync(guildId, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordGuildUser(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordGuildUser>> FindGuildUserAsync(ulong guildId, string name, int limit, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.FindGuildUserAsync(guildId, name, limit, properties.Original, cancellationToken)).Select(x => new DiscordGuildUser(x)).ToList();
    public async Task<IDiscordGuildUser> AddGuildUserAsync(ulong guildId, ulong userId, IDiscordGuildUserProperties userProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildUser(await _original.AddGuildUserAsync(guildId, userId, userProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordGuildUser> ModifyGuildUserAsync(ulong guildId, ulong userId, Action<IDiscordGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildUser(await _original.ModifyGuildUserAsync(guildId, userId, action, properties.Original, cancellationToken));
    public async Task<IDiscordGuildUser> ModifyCurrentGuildUserAsync(ulong guildId, Action<IDiscordCurrentGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildUser(await _original.ModifyCurrentGuildUserAsync(guildId, action, properties.Original, cancellationToken));
    public Task AddGuildUserRoleAsync(ulong guildId, ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.AddGuildUserRoleAsync(guildId, userId, roleId, properties.Original, cancellationToken);
    public Task RemoveGuildUserRoleAsync(ulong guildId, ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.RemoveGuildUserRoleAsync(guildId, userId, roleId, properties.Original, cancellationToken);
    public Task KickGuildUserAsync(ulong guildId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.KickGuildUserAsync(guildId, userId, properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordGuildBan> GetGuildBansAsync(ulong guildId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetGuildBansAsync(guildId, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordGuildBan(original);
        }
    }
    public async Task<IDiscordGuildBan> GetGuildBanAsync(ulong guildId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildBan(await _original.GetGuildBanAsync(guildId, userId, properties.Original, cancellationToken));
    public Task BanGuildUserAsync(ulong guildId, ulong userId, int deleteMessageSeconds = 0, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.BanGuildUserAsync(guildId, userId, deleteMessageSeconds, properties.Original, cancellationToken);
    public async Task<IDiscordGuildBulkBan> BanGuildUsersAsync(ulong guildId, IEnumerable<ulong> userIds, int deleteMessageSeconds = 0, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildBulkBan(await _original.BanGuildUsersAsync(guildId, userIds, deleteMessageSeconds, properties.Original, cancellationToken));
    public Task UnbanGuildUserAsync(ulong guildId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.UnbanGuildUserAsync(guildId, userId, properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordRole>> GetGuildRolesAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetGuildRolesAsync(guildId, properties.Original, cancellationToken)).Select(x => new DiscordRole(x)).ToList();
    public async Task<IDiscordRole> GetGuildRoleAsync(ulong guildId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRole(await _original.GetGuildRoleAsync(guildId, roleId, properties.Original, cancellationToken));
    public async Task<IDiscordRole> CreateGuildRoleAsync(ulong guildId, IDiscordRoleProperties guildRoleProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRole(await _original.CreateGuildRoleAsync(guildId, guildRoleProperties.Original, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordRole>> ModifyGuildRolePositionsAsync(ulong guildId, IEnumerable<IDiscordRolePositionProperties> positions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.ModifyGuildRolePositionsAsync(guildId, positions?.Select(x => x.Original), properties.Original, cancellationToken)).Select(x => new DiscordRole(x)).ToList();
    public async Task<IDiscordRole> ModifyGuildRoleAsync(ulong guildId, ulong roleId, Action<IDiscordRoleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRole(await _original.ModifyGuildRoleAsync(guildId, roleId, action, properties.Original, cancellationToken));
    public Task DeleteGuildRoleAsync(ulong guildId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteGuildRoleAsync(guildId, roleId, properties.Original, cancellationToken);
    public Task<NetCord.MfaLevel> ModifyGuildMfaLevelAsync(ulong guildId, NetCord.MfaLevel mfaLevel, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.ModifyGuildMfaLevelAsync(guildId, mfaLevel, properties.Original, cancellationToken);
    public Task<int> GetGuildPruneCountAsync(ulong guildId, int days, IEnumerable<ulong>? roles = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.GetGuildPruneCountAsync(guildId, days, roles, properties.Original, cancellationToken);
    public Task<int?> GuildPruneAsync(ulong guildId, IDiscordGuildPruneProperties pruneProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.GuildPruneAsync(guildId, pruneProperties.Original, properties.Original, cancellationToken);
    public async Task<IEnumerable<IDiscordVoiceRegion>> GetGuildVoiceRegionsAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetGuildVoiceRegionsAsync(guildId, properties.Original, cancellationToken)).Select(x => new DiscordVoiceRegion(x));
    public async Task<IEnumerable<IDiscordRestInvite>> GetGuildInvitesAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetGuildInvitesAsync(guildId, properties.Original, cancellationToken)).Select(x => new DiscordRestInvite(x));
    public async Task<IReadOnlyList<IDiscordIntegration>> GetGuildIntegrationsAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetGuildIntegrationsAsync(guildId, properties.Original, cancellationToken)).Select(x => new DiscordIntegration(x)).ToList();
    public Task DeleteGuildIntegrationAsync(ulong guildId, ulong integrationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteGuildIntegrationAsync(guildId, integrationId, properties.Original, cancellationToken);
    public async Task<IDiscordGuildWidgetSettings> GetGuildWidgetSettingsAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildWidgetSettings(await _original.GetGuildWidgetSettingsAsync(guildId, properties.Original, cancellationToken));
    public async Task<IDiscordGuildWidgetSettings> ModifyGuildWidgetSettingsAsync(ulong guildId, Action<IDiscordGuildWidgetSettingsOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildWidgetSettings(await _original.ModifyGuildWidgetSettingsAsync(guildId, action, properties.Original, cancellationToken));
    public async Task<IDiscordGuildWidget> GetGuildWidgetAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildWidget(await _original.GetGuildWidgetAsync(guildId, properties.Original, cancellationToken));
    public async Task<IDiscordGuildVanityInvite> GetGuildVanityInviteAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildVanityInvite(await _original.GetGuildVanityInviteAsync(guildId, properties.Original, cancellationToken));
    public async Task<IDiscordGuildWelcomeScreen> GetGuildWelcomeScreenAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildWelcomeScreen(await _original.GetGuildWelcomeScreenAsync(guildId, properties.Original, cancellationToken));
    public async Task<IDiscordGuildWelcomeScreen> ModifyGuildWelcomeScreenAsync(ulong guildId, Action<IDiscordGuildWelcomeScreenOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildWelcomeScreen(await _original.ModifyGuildWelcomeScreenAsync(guildId, action, properties.Original, cancellationToken));
    public async Task<IDiscordGuildOnboarding> GetGuildOnboardingAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildOnboarding(await _original.GetGuildOnboardingAsync(guildId, properties.Original, cancellationToken));
    public async Task<IDiscordGuildOnboarding> ModifyGuildOnboardingAsync(ulong guildId, Action<IDiscordGuildOnboardingOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildOnboarding(await _original.ModifyGuildOnboardingAsync(guildId, action, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordGuildScheduledEvent>> GetGuildScheduledEventsAsync(ulong guildId, bool withUserCount = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetGuildScheduledEventsAsync(guildId, withUserCount, properties.Original, cancellationToken)).Select(x => new DiscordGuildScheduledEvent(x)).ToList();
    public async Task<IDiscordGuildScheduledEvent> CreateGuildScheduledEventAsync(ulong guildId, IDiscordGuildScheduledEventProperties guildScheduledEventProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildScheduledEvent(await _original.CreateGuildScheduledEventAsync(guildId, guildScheduledEventProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordGuildScheduledEvent> GetGuildScheduledEventAsync(ulong guildId, ulong scheduledEventId, bool withUserCount = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildScheduledEvent(await _original.GetGuildScheduledEventAsync(guildId, scheduledEventId, withUserCount, properties.Original, cancellationToken));
    public async Task<IDiscordGuildScheduledEvent> ModifyGuildScheduledEventAsync(ulong guildId, ulong scheduledEventId, Action<IDiscordGuildScheduledEventOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildScheduledEvent(await _original.ModifyGuildScheduledEventAsync(guildId, scheduledEventId, action, properties.Original, cancellationToken));
    public Task DeleteGuildScheduledEventAsync(ulong guildId, ulong scheduledEventId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteGuildScheduledEventAsync(guildId, scheduledEventId, properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordGuildScheduledEventUser> GetGuildScheduledEventUsersAsync(ulong guildId, ulong scheduledEventId, IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetGuildScheduledEventUsersAsync(guildId, scheduledEventId, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordGuildScheduledEventUser(original);
        }
    }
    public async Task<IDiscordGuildTemplate> GetGuildTemplateAsync(string templateCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildTemplate(await _original.GetGuildTemplateAsync(templateCode, properties.Original, cancellationToken));
    public async Task<IDiscordRestGuild> CreateGuildFromGuildTemplateAsync(string templateCode, IDiscordGuildFromGuildTemplateProperties guildProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestGuild(await _original.CreateGuildFromGuildTemplateAsync(templateCode, guildProperties.Original, properties.Original, cancellationToken));
    public async Task<IEnumerable<IDiscordGuildTemplate>> GetGuildTemplatesAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetGuildTemplatesAsync(guildId, properties.Original, cancellationToken)).Select(x => new DiscordGuildTemplate(x));
    public async Task<IDiscordGuildTemplate> CreateGuildTemplateAsync(ulong guildId, IDiscordGuildTemplateProperties guildTemplateProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildTemplate(await _original.CreateGuildTemplateAsync(guildId, guildTemplateProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordGuildTemplate> SyncGuildTemplateAsync(ulong guildId, string templateCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildTemplate(await _original.SyncGuildTemplateAsync(guildId, templateCode, properties.Original, cancellationToken));
    public async Task<IDiscordGuildTemplate> ModifyGuildTemplateAsync(ulong guildId, string templateCode, Action<IDiscordGuildTemplateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildTemplate(await _original.ModifyGuildTemplateAsync(guildId, templateCode, action, properties.Original, cancellationToken));
    public async Task<IDiscordGuildTemplate> DeleteGuildTemplateAsync(ulong guildId, string templateCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildTemplate(await _original.DeleteGuildTemplateAsync(guildId, templateCode, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordApplicationCommand>> GetGlobalApplicationCommandsAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetGlobalApplicationCommandsAsync(applicationId, properties.Original, cancellationToken)).Select(x => new DiscordApplicationCommand(x)).ToList();
    public async Task<IDiscordApplicationCommand> CreateGlobalApplicationCommandAsync(ulong applicationId, IDiscordApplicationCommandProperties applicationCommandProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationCommand(await _original.CreateGlobalApplicationCommandAsync(applicationId, applicationCommandProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordApplicationCommand> GetGlobalApplicationCommandAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationCommand(await _original.GetGlobalApplicationCommandAsync(applicationId, commandId, properties.Original, cancellationToken));
    public async Task<IDiscordApplicationCommand> ModifyGlobalApplicationCommandAsync(ulong applicationId, ulong commandId, Action<IDiscordApplicationCommandOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationCommand(await _original.ModifyGlobalApplicationCommandAsync(applicationId, commandId, action, properties.Original, cancellationToken));
    public Task DeleteGlobalApplicationCommandAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteGlobalApplicationCommandAsync(applicationId, commandId, properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordApplicationCommand>> BulkOverwriteGlobalApplicationCommandsAsync(ulong applicationId, IEnumerable<IDiscordApplicationCommandProperties> commands, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.BulkOverwriteGlobalApplicationCommandsAsync(applicationId, commands?.Select(x => x.Original), properties.Original, cancellationToken)).Select(x => new DiscordApplicationCommand(x)).ToList();
    public async Task<IReadOnlyList<IDiscordGuildApplicationCommand>> GetGuildApplicationCommandsAsync(ulong applicationId, ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetGuildApplicationCommandsAsync(applicationId, guildId, properties.Original, cancellationToken)).Select(x => new DiscordGuildApplicationCommand(x)).ToList();
    public async Task<IDiscordGuildApplicationCommand> CreateGuildApplicationCommandAsync(ulong applicationId, ulong guildId, IDiscordApplicationCommandProperties applicationCommandProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildApplicationCommand(await _original.CreateGuildApplicationCommandAsync(applicationId, guildId, applicationCommandProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordGuildApplicationCommand> GetGuildApplicationCommandAsync(ulong applicationId, ulong guildId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildApplicationCommand(await _original.GetGuildApplicationCommandAsync(applicationId, guildId, commandId, properties.Original, cancellationToken));
    public async Task<IDiscordGuildApplicationCommand> ModifyGuildApplicationCommandAsync(ulong applicationId, ulong guildId, ulong commandId, Action<IDiscordApplicationCommandOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildApplicationCommand(await _original.ModifyGuildApplicationCommandAsync(applicationId, guildId, commandId, action, properties.Original, cancellationToken));
    public Task DeleteGuildApplicationCommandAsync(ulong applicationId, ulong guildId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteGuildApplicationCommandAsync(applicationId, guildId, commandId, properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordGuildApplicationCommand>> BulkOverwriteGuildApplicationCommandsAsync(ulong applicationId, ulong guildId, IEnumerable<IDiscordApplicationCommandProperties> commands, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.BulkOverwriteGuildApplicationCommandsAsync(applicationId, guildId, commands?.Select(x => x.Original), properties.Original, cancellationToken)).Select(x => new DiscordGuildApplicationCommand(x)).ToList();
    public async Task<IReadOnlyList<IDiscordApplicationCommandGuildPermissions>> GetApplicationCommandsGuildPermissionsAsync(ulong applicationId, ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetApplicationCommandsGuildPermissionsAsync(applicationId, guildId, properties.Original, cancellationToken)).Select(x => new DiscordApplicationCommandGuildPermissions(x)).ToList();
    public async Task<IDiscordApplicationCommandGuildPermissions> GetApplicationCommandGuildPermissionsAsync(ulong applicationId, ulong guildId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationCommandGuildPermissions(await _original.GetApplicationCommandGuildPermissionsAsync(applicationId, guildId, commandId, properties.Original, cancellationToken));
    public async Task<IDiscordApplicationCommandGuildPermissions> OverwriteApplicationCommandGuildPermissionsAsync(ulong applicationId, ulong guildId, ulong commandId, IEnumerable<IDiscordApplicationCommandGuildPermissionProperties> newPermissions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationCommandGuildPermissions(await _original.OverwriteApplicationCommandGuildPermissionsAsync(applicationId, guildId, commandId, newPermissions?.Select(x => x.Original), properties.Original, cancellationToken));
    public Task SendInteractionResponseAsync(ulong interactionId, string interactionToken, IDiscordInteractionCallback callback, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.SendInteractionResponseAsync(interactionId, interactionToken, callback.Original, properties.Original, cancellationToken);
    public async Task<IDiscordRestMessage> GetInteractionResponseAsync(ulong applicationId, string interactionToken, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.GetInteractionResponseAsync(applicationId, interactionToken, properties.Original, cancellationToken));
    public async Task<IDiscordRestMessage> ModifyInteractionResponseAsync(ulong applicationId, string interactionToken, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.ModifyInteractionResponseAsync(applicationId, interactionToken, action, properties.Original, cancellationToken));
    public Task DeleteInteractionResponseAsync(ulong applicationId, string interactionToken, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteInteractionResponseAsync(applicationId, interactionToken, properties.Original, cancellationToken);
    public async Task<IDiscordRestMessage> SendInteractionFollowupMessageAsync(ulong applicationId, string interactionToken, IDiscordInteractionMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.SendInteractionFollowupMessageAsync(applicationId, interactionToken, message.Original, properties.Original, cancellationToken));
    public async Task<IDiscordRestMessage> GetInteractionFollowupMessageAsync(ulong applicationId, string interactionToken, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.GetInteractionFollowupMessageAsync(applicationId, interactionToken, messageId, properties.Original, cancellationToken));
    public async Task<IDiscordRestMessage> ModifyInteractionFollowupMessageAsync(ulong applicationId, string interactionToken, ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.ModifyInteractionFollowupMessageAsync(applicationId, interactionToken, messageId, action, properties.Original, cancellationToken));
    public Task DeleteInteractionFollowupMessageAsync(ulong applicationId, string interactionToken, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteInteractionFollowupMessageAsync(applicationId, interactionToken, messageId, properties.Original, cancellationToken);
    public async Task<IDiscordRestInvite> GetGuildInviteAsync(string inviteCode, bool withCounts = false, bool withExpiration = false, ulong? guildScheduledEventId = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestInvite(await _original.GetGuildInviteAsync(inviteCode, withCounts, withExpiration, guildScheduledEventId, properties.Original, cancellationToken));
    public async Task<IDiscordRestInvite> DeleteGuildInviteAsync(string inviteCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestInvite(await _original.DeleteGuildInviteAsync(inviteCode, properties.Original, cancellationToken));
    public async IAsyncEnumerable<IDiscordEntitlement> GetEntitlementsAsync(ulong applicationId, IDiscordEntitlementsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetEntitlementsAsync(applicationId, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordEntitlement(original);
        }
    }
    public async Task<IDiscordEntitlement> GetEntitlementAsync(ulong applicationId, ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordEntitlement(await _original.GetEntitlementAsync(applicationId, entitlementId, properties.Original, cancellationToken));
    public Task ConsumeEntitlementAsync(ulong applicationId, ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.ConsumeEntitlementAsync(applicationId, entitlementId, properties.Original, cancellationToken);
    public async Task<IDiscordEntitlement> CreateTestEntitlementAsync(ulong applicationId, IDiscordTestEntitlementProperties testEntitlementProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordEntitlement(await _original.CreateTestEntitlementAsync(applicationId, testEntitlementProperties.Original, properties.Original, cancellationToken));
    public Task DeleteTestEntitlementAsync(ulong applicationId, ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteTestEntitlementAsync(applicationId, entitlementId, properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordSku>> GetSkusAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetSkusAsync(applicationId, properties.Original, cancellationToken)).Select(x => new DiscordSku(x)).ToList();
    public async Task<IDiscordCurrentApplication> GetCurrentBotApplicationInformationAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordCurrentApplication(await _original.GetCurrentBotApplicationInformationAsync(properties.Original, cancellationToken));
    public async Task<IDiscordAuthorizationInformation> GetCurrentAuthorizationInformationAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordAuthorizationInformation(await _original.GetCurrentAuthorizationInformationAsync(properties.Original, cancellationToken));
    public async IAsyncEnumerable<IDiscordUser> GetMessagePollAnswerVotersAsync(ulong channelId, ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetMessagePollAnswerVotersAsync(channelId, messageId, answerId, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public async Task<IDiscordRestMessage> EndMessagePollAsync(ulong channelId, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.EndMessagePollAsync(channelId, messageId, properties.Original, cancellationToken));
    public async Task<IDiscordStageInstance> CreateStageInstanceAsync(IDiscordStageInstanceProperties stageInstanceProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordStageInstance(await _original.CreateStageInstanceAsync(stageInstanceProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordStageInstance> GetStageInstanceAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordStageInstance(await _original.GetStageInstanceAsync(channelId, properties.Original, cancellationToken));
    public async Task<IDiscordStageInstance> ModifyStageInstanceAsync(ulong channelId, Action<IDiscordStageInstanceOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordStageInstance(await _original.ModifyStageInstanceAsync(channelId, action, properties.Original, cancellationToken));
    public Task DeleteStageInstanceAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteStageInstanceAsync(channelId, properties.Original, cancellationToken);
    public async Task<IDiscordStandardSticker> GetStickerAsync(ulong stickerId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordStandardSticker(await _original.GetStickerAsync(stickerId, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordStickerPack>> GetStickerPacksAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetStickerPacksAsync(properties.Original, cancellationToken)).Select(x => new DiscordStickerPack(x)).ToList();
    public async Task<IDiscordStickerPack> GetStickerPackAsync(ulong stickerPackId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordStickerPack(await _original.GetStickerPackAsync(stickerPackId, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordGuildSticker>> GetGuildStickersAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetGuildStickersAsync(guildId, properties.Original, cancellationToken)).Select(x => new DiscordGuildSticker(x)).ToList();
    public async Task<IDiscordGuildSticker> GetGuildStickerAsync(ulong guildId, ulong stickerId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildSticker(await _original.GetGuildStickerAsync(guildId, stickerId, properties.Original, cancellationToken));
    public async Task<IDiscordGuildSticker> CreateGuildStickerAsync(ulong guildId, IDiscordGuildStickerProperties sticker, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildSticker(await _original.CreateGuildStickerAsync(guildId, sticker.Original, properties.Original, cancellationToken));
    public async Task<IDiscordGuildSticker> ModifyGuildStickerAsync(ulong guildId, ulong stickerId, Action<IDiscordGuildStickerOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildSticker(await _original.ModifyGuildStickerAsync(guildId, stickerId, action, properties.Original, cancellationToken));
    public Task DeleteGuildStickerAsync(ulong guildId, ulong stickerId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteGuildStickerAsync(guildId, stickerId, properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordSubscription> GetSkuSubscriptionsAsync(ulong skuId, IDiscordSubscriptionPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetSkuSubscriptionsAsync(skuId, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordSubscription(original);
        }
    }
    public async Task<IDiscordSubscription> GetSkuSubscriptionAsync(ulong skuId, ulong subscriptionId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordSubscription(await _original.GetSkuSubscriptionAsync(skuId, subscriptionId, properties.Original, cancellationToken));
    public async Task<IDiscordApplication> GetApplicationAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplication(await _original.GetApplicationAsync(applicationId, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(ulong channelId, IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.CreateGoogleCloudPlatformStorageBucketsAsync(channelId, buckets?.Select(x => x.Original), properties.Original, cancellationToken)).Select(x => new DiscordGoogleCloudPlatformStorageBucket(x)).ToList();
    public async IAsyncEnumerable<IDiscordGuildUserInfo> SearchGuildUsersAsync(ulong guildId, IDiscordGuildUsersSearchPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.SearchGuildUsersAsync(guildId, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordGuildUserInfo(original);
        }
    }
    public async Task<IDiscordCurrentUser> GetCurrentUserAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordCurrentUser(await _original.GetCurrentUserAsync(properties.Original, cancellationToken));
    public async Task<IDiscordUser> GetUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordUser(await _original.GetUserAsync(userId, properties.Original, cancellationToken));
    public async Task<IDiscordCurrentUser> ModifyCurrentUserAsync(Action<IDiscordCurrentUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordCurrentUser(await _original.ModifyCurrentUserAsync(action, properties.Original, cancellationToken));
    public async IAsyncEnumerable<IDiscordRestGuild> GetCurrentUserGuildsAsync(IDiscordGuildsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetCurrentUserGuildsAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordRestGuild(original);
        }
    }
    public async Task<IDiscordGuildUser> GetCurrentUserGuildUserAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildUser(await _original.GetCurrentUserGuildUserAsync(guildId, properties.Original, cancellationToken));
    public Task LeaveGuildAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.LeaveGuildAsync(guildId, properties.Original, cancellationToken);
    public async Task<IDiscordDMChannel> GetDMChannelAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordDMChannel(await _original.GetDMChannelAsync(userId, properties.Original, cancellationToken));
    public async Task<IDiscordGroupDMChannel> CreateGroupDMChannelAsync(IDiscordGroupDMChannelProperties groupDMChannelProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGroupDMChannel(await _original.CreateGroupDMChannelAsync(groupDMChannelProperties.Original, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordConnection>> GetCurrentUserConnectionsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetCurrentUserConnectionsAsync(properties.Original, cancellationToken)).Select(x => new DiscordConnection(x)).ToList();
    public async Task<IDiscordApplicationRoleConnection> GetCurrentUserApplicationRoleConnectionAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationRoleConnection(await _original.GetCurrentUserApplicationRoleConnectionAsync(applicationId, properties.Original, cancellationToken));
    public async Task<IDiscordApplicationRoleConnection> UpdateCurrentUserApplicationRoleConnectionAsync(ulong applicationId, IDiscordApplicationRoleConnectionProperties applicationRoleConnectionProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationRoleConnection(await _original.UpdateCurrentUserApplicationRoleConnectionAsync(applicationId, applicationRoleConnectionProperties.Original, properties.Original, cancellationToken));
    public async Task<IEnumerable<IDiscordVoiceRegion>> GetVoiceRegionsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetVoiceRegionsAsync(properties.Original, cancellationToken)).Select(x => new DiscordVoiceRegion(x));
    public async Task<IDiscordVoiceState> GetCurrentGuildUserVoiceStateAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordVoiceState(await _original.GetCurrentGuildUserVoiceStateAsync(guildId, properties.Original, cancellationToken));
    public async Task<IDiscordVoiceState> GetGuildUserVoiceStateAsync(ulong guildId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordVoiceState(await _original.GetGuildUserVoiceStateAsync(guildId, userId, properties.Original, cancellationToken));
    public Task ModifyCurrentGuildUserVoiceStateAsync(ulong guildId, Action<IDiscordCurrentUserVoiceStateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.ModifyCurrentGuildUserVoiceStateAsync(guildId, action, properties.Original, cancellationToken);
    public Task ModifyGuildUserVoiceStateAsync(ulong guildId, ulong channelId, ulong userId, Action<IDiscordVoiceStateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.ModifyGuildUserVoiceStateAsync(guildId, channelId, userId, action, properties.Original, cancellationToken);
    public async Task<IDiscordIncomingWebhook> CreateWebhookAsync(ulong channelId, IDiscordWebhookProperties webhookProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordIncomingWebhook(await _original.CreateWebhookAsync(channelId, webhookProperties.Original, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordWebhook>> GetChannelWebhooksAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetChannelWebhooksAsync(channelId, properties.Original, cancellationToken)).Select(x => new DiscordWebhook(x)).ToList();
    public async Task<IReadOnlyList<IDiscordWebhook>> GetGuildWebhooksAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetGuildWebhooksAsync(guildId, properties.Original, cancellationToken)).Select(x => new DiscordWebhook(x)).ToList();
    public async Task<IDiscordWebhook> GetWebhookAsync(ulong webhookId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordWebhook(await _original.GetWebhookAsync(webhookId, properties.Original, cancellationToken));
    public async Task<IDiscordWebhook> GetWebhookWithTokenAsync(ulong webhookId, string webhookToken, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordWebhook(await _original.GetWebhookWithTokenAsync(webhookId, webhookToken, properties.Original, cancellationToken));
    public async Task<IDiscordWebhook> ModifyWebhookAsync(ulong webhookId, Action<IDiscordWebhookOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordWebhook(await _original.ModifyWebhookAsync(webhookId, action, properties.Original, cancellationToken));
    public async Task<IDiscordWebhook> ModifyWebhookWithTokenAsync(ulong webhookId, string webhookToken, Action<IDiscordWebhookOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordWebhook(await _original.ModifyWebhookWithTokenAsync(webhookId, webhookToken, action, properties.Original, cancellationToken));
    public Task DeleteWebhookAsync(ulong webhookId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteWebhookAsync(webhookId, properties.Original, cancellationToken);
    public Task DeleteWebhookWithTokenAsync(ulong webhookId, string webhookToken, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteWebhookWithTokenAsync(webhookId, webhookToken, properties.Original, cancellationToken);
    public async Task<IDiscordRestMessage> ExecuteWebhookAsync(ulong webhookId, string webhookToken, IDiscordWebhookMessageProperties message, bool wait = false, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.ExecuteWebhookAsync(webhookId, webhookToken, message.Original, wait, threadId, withComponents, properties.Original, cancellationToken));
    public async Task<IDiscordRestMessage> GetWebhookMessageAsync(ulong webhookId, string webhookToken, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.GetWebhookMessageAsync(webhookId, webhookToken, messageId, properties.Original, cancellationToken));
    public async Task<IDiscordRestMessage> ModifyWebhookMessageAsync(ulong webhookId, string webhookToken, ulong messageId, Action<IDiscordMessageOptions> action, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.ModifyWebhookMessageAsync(webhookId, webhookToken, messageId, action, threadId, withComponents, properties.Original, cancellationToken));
    public Task DeleteWebhookMessageAsync(ulong webhookId, string webhookToken, ulong messageId, ulong? threadId = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteWebhookMessageAsync(webhookId, webhookToken, messageId, threadId, properties.Original, cancellationToken);
}


public class DiscordToken : IDiscordToken
{
    private readonly NetCord.IToken _original;
    public DiscordToken(NetCord.IToken original)
    {
        _original = original;
    }
    public NetCord.IToken Original => _original;
    public string RawToken => _original.RawToken;
    public string HttpHeaderValue => _original.HttpHeaderValue;
}


public class DiscordCurrentApplication : IDiscordCurrentApplication
{
    private readonly NetCord.CurrentApplication _original;
    public DiscordCurrentApplication(NetCord.CurrentApplication original)
    {
        _original = original;
    }
    public NetCord.CurrentApplication Original => _original;
    public ulong Id => _original.Id;
    public string Name => _original.Name;
    public string IconHash => _original.IconHash;
    public string Description => _original.Description;
    public IReadOnlyList<string> RpcOrigins => _original.RpcOrigins;
    public bool? BotPublic => _original.BotPublic;
    public bool? BotRequireCodeGrant => _original.BotRequireCodeGrant;
    public IDiscordUser Bot => new DiscordUser(_original.Bot);
    public string TermsOfServiceUrl => _original.TermsOfServiceUrl;
    public string PrivacyPolicyUrl => _original.PrivacyPolicyUrl;
    public IDiscordUser Owner => new DiscordUser(_original.Owner);
    public string VerifyKey => _original.VerifyKey;
    public IDiscordTeam Team => new DiscordTeam(_original.Team);
    public ulong? GuildId => _original.GuildId;
    public IDiscordRestGuild Guild => new DiscordRestGuild(_original.Guild);
    public ulong? PrimarySkuId => _original.PrimarySkuId;
    public string Slug => _original.Slug;
    public string CoverImageHash => _original.CoverImageHash;
    public NetCord.ApplicationFlags? Flags => _original.Flags;
    public int? ApproximateGuildCount => _original.ApproximateGuildCount;
    public int? ApproximateUserInstallCount => _original.ApproximateUserInstallCount;
    public IReadOnlyList<string> RedirectUris => _original.RedirectUris;
    public string InteractionsEndpointUrl => _original.InteractionsEndpointUrl;
    public string RoleConnectionsVerificationUrl => _original.RoleConnectionsVerificationUrl;
    public IReadOnlyList<string> Tags => _original.Tags;
    public IDiscordApplicationInstallParams InstallParams => new DiscordApplicationInstallParams(_original.InstallParams);
    public IReadOnlyDictionary<NetCord.ApplicationIntegrationType, IDiscordApplicationIntegrationTypeConfiguration> IntegrationTypesConfiguration => _original.IntegrationTypesConfiguration.ToDictionary(kv => kv.Key, kv => (IDiscordApplicationIntegrationTypeConfiguration)new DiscordApplicationIntegrationTypeConfiguration(kv.Value));
    public string CustomInstallUrl => _original.CustomInstallUrl;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordApplication> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplication(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordCurrentApplication> ModifyAsync(Action<IDiscordCurrentApplicationOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordCurrentApplication(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordApplicationRoleConnectionMetadata>> GetRoleConnectionMetadataRecordsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetRoleConnectionMetadataRecordsAsync(properties.Original, cancellationToken)).Select(x => new DiscordApplicationRoleConnectionMetadata(x)).ToList();
    public async Task<IReadOnlyList<IDiscordApplicationRoleConnectionMetadata>> UpdateRoleConnectionMetadataRecordsAsync(IEnumerable<IDiscordApplicationRoleConnectionMetadataProperties> applicationRoleConnectionMetadataProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.UpdateRoleConnectionMetadataRecordsAsync(applicationRoleConnectionMetadataProperties?.Select(x => x.Original), properties.Original, cancellationToken)).Select(x => new DiscordApplicationRoleConnectionMetadata(x)).ToList();
    public async IAsyncEnumerable<IDiscordEntitlement> GetEntitlementsAsync(IDiscordEntitlementsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetEntitlementsAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordEntitlement(original);
        }
    }
    public async Task<IDiscordEntitlement> GetEntitlementAsync(ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordEntitlement(await _original.GetEntitlementAsync(entitlementId, properties.Original, cancellationToken));
    public Task ConsumeEntitlementAsync(ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.ConsumeEntitlementAsync(entitlementId, properties.Original, cancellationToken);
    public async Task<IDiscordEntitlement> CreateTestEntitlementAsync(IDiscordTestEntitlementProperties testEntitlementProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordEntitlement(await _original.CreateTestEntitlementAsync(testEntitlementProperties.Original, properties.Original, cancellationToken));
    public Task DeleteTestEntitlementAsync(ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteTestEntitlementAsync(entitlementId, properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordSku>> GetSkusAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetSkusAsync(properties.Original, cancellationToken)).Select(x => new DiscordSku(x)).ToList();
    public IDiscordImageUrl GetIconUrl(NetCord.ImageFormat format) => new DiscordImageUrl(_original.GetIconUrl(format));
    public IDiscordImageUrl GetCoverUrl(NetCord.ImageFormat format) => new DiscordImageUrl(_original.GetCoverUrl(format));
    public IDiscordImageUrl GetAssetUrl(ulong assetId, NetCord.ImageFormat format) => new DiscordImageUrl(_original.GetAssetUrl(assetId, format));
    public IDiscordImageUrl GetAchievementIconUrl(ulong achievementId, string iconHash, NetCord.ImageFormat format) => new DiscordImageUrl(_original.GetAchievementIconUrl(achievementId, iconHash, format));
    public IDiscordImageUrl GetStorePageAssetUrl(ulong assetId, NetCord.ImageFormat format) => new DiscordImageUrl(_original.GetStorePageAssetUrl(assetId, format));
    public async Task<IReadOnlyList<IDiscordApplicationEmoji>> GetEmojisAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetEmojisAsync(properties.Original, cancellationToken)).Select(x => new DiscordApplicationEmoji(x)).ToList();
    public async Task<IDiscordApplicationEmoji> GetEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationEmoji(await _original.GetEmojiAsync(emojiId, properties.Original, cancellationToken));
    public async Task<IDiscordApplicationEmoji> CreateEmojiAsync(IDiscordApplicationEmojiProperties applicationEmojiProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationEmoji(await _original.CreateEmojiAsync(applicationEmojiProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordApplicationEmoji> ModifyEmojiAsync(ulong emojiId, Action<IDiscordApplicationEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationEmoji(await _original.ModifyEmojiAsync(emojiId, action, properties.Original, cancellationToken));
    public Task DeleteEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteEmojiAsync(emojiId, properties.Original, cancellationToken);
}


public class DiscordCurrentApplicationOptions : IDiscordCurrentApplicationOptions
{
    private readonly NetCord.Rest.CurrentApplicationOptions _original;
    public DiscordCurrentApplicationOptions(NetCord.Rest.CurrentApplicationOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.CurrentApplicationOptions Original => _original;
    public string CustomInstallUrl => _original.CustomInstallUrl;
    public string Description => _original.Description;
    public string RoleConnectionsVerificationUrl => _original.RoleConnectionsVerificationUrl;
    public IDiscordApplicationInstallParamsProperties InstallParams => new DiscordApplicationInstallParamsProperties(_original.InstallParams);
    public IReadOnlyDictionary<NetCord.ApplicationIntegrationType, IDiscordApplicationIntegrationTypeConfigurationProperties> IntegrationTypesConfiguration => _original.IntegrationTypesConfiguration.ToDictionary(kv => kv.Key, kv => (IDiscordApplicationIntegrationTypeConfigurationProperties)new DiscordApplicationIntegrationTypeConfigurationProperties(kv.Value));
    public NetCord.ApplicationFlags? Flags => _original.Flags;
    public NetCord.Rest.ImageProperties? Icon => _original.Icon;
    public NetCord.Rest.ImageProperties? CoverImage => _original.CoverImage;
    public string InteractionsEndpointUrl => _original.InteractionsEndpointUrl;
    public IEnumerable<string> Tags => _original.Tags;
    public IDiscordCurrentApplicationOptions WithCustomInstallUrl(string customInstallUrl) => new DiscordCurrentApplicationOptions(_original.WithCustomInstallUrl(customInstallUrl));
    public IDiscordCurrentApplicationOptions WithDescription(string description) => new DiscordCurrentApplicationOptions(_original.WithDescription(description));
    public IDiscordCurrentApplicationOptions WithRoleConnectionsVerificationUrl(string roleConnectionsVerificationUrl) => new DiscordCurrentApplicationOptions(_original.WithRoleConnectionsVerificationUrl(roleConnectionsVerificationUrl));
    public IDiscordCurrentApplicationOptions WithInstallParams(IDiscordApplicationInstallParamsProperties installParams) => new DiscordCurrentApplicationOptions(_original.WithInstallParams(installParams.Original));
    public IDiscordCurrentApplicationOptions WithIntegrationTypesConfiguration(IReadOnlyDictionary<NetCord.ApplicationIntegrationType, IDiscordApplicationIntegrationTypeConfigurationProperties> integrationTypesConfiguration) => new DiscordCurrentApplicationOptions(_original.WithIntegrationTypesConfiguration(integrationTypesConfiguration));
    public IDiscordCurrentApplicationOptions WithFlags(NetCord.ApplicationFlags? flags) => new DiscordCurrentApplicationOptions(_original.WithFlags(flags));
    public IDiscordCurrentApplicationOptions WithIcon(NetCord.Rest.ImageProperties? icon) => new DiscordCurrentApplicationOptions(_original.WithIcon(icon));
    public IDiscordCurrentApplicationOptions WithCoverImage(NetCord.Rest.ImageProperties? coverImage) => new DiscordCurrentApplicationOptions(_original.WithCoverImage(coverImage));
    public IDiscordCurrentApplicationOptions WithInteractionsEndpointUrl(string interactionsEndpointUrl) => new DiscordCurrentApplicationOptions(_original.WithInteractionsEndpointUrl(interactionsEndpointUrl));
    public IDiscordCurrentApplicationOptions WithTags(IEnumerable<string> tags) => new DiscordCurrentApplicationOptions(_original.WithTags(tags));
    public IDiscordCurrentApplicationOptions AddTags(IEnumerable<string> tags) => new DiscordCurrentApplicationOptions(_original.AddTags(tags));
    public IDiscordCurrentApplicationOptions AddTags(string[] tags) => new DiscordCurrentApplicationOptions(_original.AddTags(tags));
}


public class DiscordApplicationRoleConnectionMetadata : IDiscordApplicationRoleConnectionMetadata
{
    private readonly NetCord.Rest.ApplicationRoleConnectionMetadata _original;
    public DiscordApplicationRoleConnectionMetadata(NetCord.Rest.ApplicationRoleConnectionMetadata original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationRoleConnectionMetadata Original => _original;
    public NetCord.Rest.ApplicationRoleConnectionMetadataType Type => _original.Type;
    public string Key => _original.Key;
    public string Name => _original.Name;
    public IReadOnlyDictionary<string, string> NameLocalizations => _original.NameLocalizations;
    public string Description => _original.Description;
    public IReadOnlyDictionary<string, string> DescriptionLocalizations => _original.DescriptionLocalizations;
}


public class DiscordApplicationRoleConnectionMetadataProperties : IDiscordApplicationRoleConnectionMetadataProperties
{
    private readonly NetCord.Rest.ApplicationRoleConnectionMetadataProperties _original;
    public DiscordApplicationRoleConnectionMetadataProperties(NetCord.Rest.ApplicationRoleConnectionMetadataProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationRoleConnectionMetadataProperties Original => _original;
    public NetCord.Rest.ApplicationRoleConnectionMetadataType Type => _original.Type;
    public string Key => _original.Key;
    public string Name => _original.Name;
    public IReadOnlyDictionary<string, string> NameLocalizations => _original.NameLocalizations;
    public string Description => _original.Description;
    public IReadOnlyDictionary<string, string> DescriptionLocalizations => _original.DescriptionLocalizations;
    public IDiscordApplicationRoleConnectionMetadataProperties WithType(NetCord.Rest.ApplicationRoleConnectionMetadataType type) => new DiscordApplicationRoleConnectionMetadataProperties(_original.WithType(type));
    public IDiscordApplicationRoleConnectionMetadataProperties WithKey(string key) => new DiscordApplicationRoleConnectionMetadataProperties(_original.WithKey(key));
    public IDiscordApplicationRoleConnectionMetadataProperties WithName(string name) => new DiscordApplicationRoleConnectionMetadataProperties(_original.WithName(name));
    public IDiscordApplicationRoleConnectionMetadataProperties WithNameLocalizations(IReadOnlyDictionary<string, string> nameLocalizations) => new DiscordApplicationRoleConnectionMetadataProperties(_original.WithNameLocalizations(nameLocalizations));
    public IDiscordApplicationRoleConnectionMetadataProperties WithDescription(string description) => new DiscordApplicationRoleConnectionMetadataProperties(_original.WithDescription(description));
    public IDiscordApplicationRoleConnectionMetadataProperties WithDescriptionLocalizations(IReadOnlyDictionary<string, string> descriptionLocalizations) => new DiscordApplicationRoleConnectionMetadataProperties(_original.WithDescriptionLocalizations(descriptionLocalizations));
}


public class DiscordGroupDMChannelOptions : IDiscordGroupDMChannelOptions
{
    private readonly NetCord.Rest.GroupDMChannelOptions _original;
    public DiscordGroupDMChannelOptions(NetCord.Rest.GroupDMChannelOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.GroupDMChannelOptions Original => _original;
    public string Name => _original.Name;
    public NetCord.Rest.ImageProperties? Icon => _original.Icon;
    public IDiscordGroupDMChannelOptions WithName(string name) => new DiscordGroupDMChannelOptions(_original.WithName(name));
    public IDiscordGroupDMChannelOptions WithIcon(NetCord.Rest.ImageProperties? icon) => new DiscordGroupDMChannelOptions(_original.WithIcon(icon));
}


public class DiscordFollowedChannel : IDiscordFollowedChannel
{
    private readonly NetCord.Rest.FollowedChannel _original;
    public DiscordFollowedChannel(NetCord.Rest.FollowedChannel original)
    {
        _original = original;
    }
    public NetCord.Rest.FollowedChannel Original => _original;
    public ulong Id => _original.Id;
    public ulong WebhookId => _original.WebhookId;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordGroupDMChannelUserAddProperties : IDiscordGroupDMChannelUserAddProperties
{
    private readonly NetCord.Rest.GroupDMChannelUserAddProperties _original;
    public DiscordGroupDMChannelUserAddProperties(NetCord.Rest.GroupDMChannelUserAddProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GroupDMChannelUserAddProperties Original => _original;
    public string AccessToken => _original.AccessToken;
    public string Nickname => _original.Nickname;
    public IDiscordGroupDMChannelUserAddProperties WithAccessToken(string accessToken) => new DiscordGroupDMChannelUserAddProperties(_original.WithAccessToken(accessToken));
    public IDiscordGroupDMChannelUserAddProperties WithNickname(string nickname) => new DiscordGroupDMChannelUserAddProperties(_original.WithNickname(nickname));
}


public class DiscordForumGuildThread : IDiscordForumGuildThread
{
    private readonly NetCord.ForumGuildThread _original;
    public DiscordForumGuildThread(NetCord.ForumGuildThread original)
    {
        _original = original;
    }
    public NetCord.ForumGuildThread Original => _original;
    public IDiscordRestMessage Message => new DiscordRestMessage(_original.Message);
    public IReadOnlyList<ulong> AppliedTags => _original.AppliedTags;
    public ulong OwnerId => _original.OwnerId;
    public int MessageCount => _original.MessageCount;
    public int UserCount => _original.UserCount;
    public IDiscordGuildThreadMetadata Metadata => new DiscordGuildThreadMetadata(_original.Metadata);
    public IDiscordThreadCurrentUser CurrentUser => new DiscordThreadCurrentUser(_original.CurrentUser);
    public int TotalMessageSent => _original.TotalMessageSent;
    public ulong GuildId => _original.GuildId;
    public int? Position => _original.Position;
    public IReadOnlyDictionary<ulong, IDiscordPermissionOverwrite> PermissionOverwrites => _original.PermissionOverwrites.ToDictionary(kv => kv.Key, kv => (IDiscordPermissionOverwrite)new DiscordPermissionOverwrite(kv.Value));
    public string Name => _original.Name;
    public string Topic => _original.Topic;
    public bool Nsfw => _original.Nsfw;
    public int Slowmode => _original.Slowmode;
    public ulong? ParentId => _original.ParentId;
    public NetCord.ThreadArchiveDuration? DefaultAutoArchiveDuration => _original.DefaultAutoArchiveDuration;
    public int DefaultThreadSlowmode => _original.DefaultThreadSlowmode;
    public ulong? LastMessageId => _original.LastMessageId;
    public System.DateTimeOffset? LastPin => _original.LastPin;
    public ulong Id => _original.Id;
    public NetCord.ChannelFlags Flags => _original.Flags;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordForumGuildThread> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordForumGuildThread(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordForumGuildThread> ModifyAsync(Action<IDiscordGuildChannelOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordForumGuildThread(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public async Task<IDiscordForumGuildThread> DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordForumGuildThread(await _original.DeleteAsync(properties.Original, cancellationToken));
    public Task JoinAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.JoinAsync(properties.Original, cancellationToken);
    public Task AddUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.AddUserAsync(userId, properties.Original, cancellationToken);
    public Task LeaveAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.LeaveAsync(properties.Original, cancellationToken);
    public Task DeleteUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteUserAsync(userId, properties.Original, cancellationToken);
    public async Task<IDiscordThreadUser> GetUserAsync(ulong userId, bool withGuildUser = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordThreadUser(await _original.GetUserAsync(userId, withGuildUser, properties.Original, cancellationToken));
    public async IAsyncEnumerable<IDiscordThreadUser> GetUsersAsync(IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetUsersAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordThreadUser(original);
        }
    }
    public Task ModifyPermissionsAsync(IDiscordPermissionOverwriteProperties permissionOverwrite, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.ModifyPermissionsAsync(permissionOverwrite.Original, properties.Original, cancellationToken);
    public async Task<IEnumerable<IDiscordRestInvite>> GetInvitesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetInvitesAsync(properties.Original, cancellationToken)).Select(x => new DiscordRestInvite(x));
    public async Task<IDiscordRestInvite> CreateInviteAsync(IDiscordInviteProperties? inviteProperties = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestInvite(await _original.CreateInviteAsync(inviteProperties.Original, properties.Original, cancellationToken));
    public Task DeletePermissionAsync(ulong overwriteId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeletePermissionAsync(overwriteId, properties.Original, cancellationToken);
    public async Task<IDiscordGuildThread> CreateGuildThreadAsync(ulong messageId, IDiscordGuildThreadFromMessageProperties threadFromMessageProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildThread(await _original.CreateGuildThreadAsync(messageId, threadFromMessageProperties.Original, properties.Original, cancellationToken));
    public async Task<IDiscordGuildThread> CreateGuildThreadAsync(IDiscordGuildThreadProperties threadProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildThread(await _original.CreateGuildThreadAsync(threadProperties.Original, properties.Original, cancellationToken));
    public async IAsyncEnumerable<IDiscordGuildThread> GetPublicArchivedGuildThreadsAsync(IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetPublicArchivedGuildThreadsAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordGuildThread(original);
        }
    }
    public async IAsyncEnumerable<IDiscordGuildThread> GetPrivateArchivedGuildThreadsAsync(IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetPrivateArchivedGuildThreadsAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordGuildThread(original);
        }
    }
    public async IAsyncEnumerable<IDiscordGuildThread> GetJoinedPrivateArchivedGuildThreadsAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetJoinedPrivateArchivedGuildThreadsAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordGuildThread(original);
        }
    }
    public async Task<IDiscordIncomingWebhook> CreateWebhookAsync(IDiscordWebhookProperties webhookProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordIncomingWebhook(await _original.CreateWebhookAsync(webhookProperties.Original, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordWebhook>> GetChannelWebhooksAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetChannelWebhooksAsync(properties.Original, cancellationToken)).Select(x => new DiscordWebhook(x)).ToList();
    public async IAsyncEnumerable<IDiscordRestMessage> GetMessagesAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetMessagesAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordRestMessage(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetMessagesAroundAsync(ulong messageId, int? limit = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetMessagesAroundAsync(messageId, limit, properties.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    public async Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.GetMessageAsync(messageId, properties.Original, cancellationToken));
    public async Task<IDiscordRestMessage> SendMessageAsync(IDiscordMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.SendMessageAsync(message.Original, properties.Original, cancellationToken));
    public Task AddMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.AddMessageReactionAsync(messageId, emoji.Original, properties.Original, cancellationToken);
    public Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessageReactionAsync(messageId, emoji.Original, properties.Original, cancellationToken);
    public Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessageReactionAsync(messageId, emoji.Original, userId, properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordUser> GetMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordMessageReactionsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetMessageReactionsAsync(messageId, emoji.Original, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAllMessageReactionsAsync(messageId, properties.Original, cancellationToken);
    public Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAllMessageReactionsAsync(messageId, emoji.Original, properties.Original, cancellationToken);
    public async Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.ModifyMessageAsync(messageId, action, properties.Original, cancellationToken));
    public Task DeleteMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessageAsync(messageId, properties.Original, cancellationToken);
    public Task DeleteMessagesAsync(IEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessagesAsync(messageIds, properties.Original, cancellationToken);
    public Task DeleteMessagesAsync(IAsyncEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessagesAsync(messageIds, properties.Original, cancellationToken);
    public Task TriggerTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.TriggerTypingStateAsync(properties.Original, cancellationToken);
    public Task<IDisposable> EnterTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.EnterTypingStateAsync(properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetPinnedMessagesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetPinnedMessagesAsync(properties.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    public Task PinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.PinMessageAsync(messageId, properties.Original, cancellationToken);
    public Task UnpinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.UnpinMessageAsync(messageId, properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordUser> GetMessagePollAnswerVotersAsync(ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetMessagePollAnswerVotersAsync(messageId, answerId, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public async Task<IDiscordRestMessage> EndMessagePollAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.EndMessagePollAsync(messageId, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.CreateGoogleCloudPlatformStorageBucketsAsync(buckets?.Select(x => x.Original), properties.Original, cancellationToken)).Select(x => new DiscordGoogleCloudPlatformStorageBucket(x)).ToList();
}


public class DiscordForumGuildThreadProperties : IDiscordForumGuildThreadProperties
{
    private readonly NetCord.Rest.ForumGuildThreadProperties _original;
    public DiscordForumGuildThreadProperties(NetCord.Rest.ForumGuildThreadProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ForumGuildThreadProperties Original => _original;
    public IDiscordForumGuildThreadMessageProperties Message => new DiscordForumGuildThreadMessageProperties(_original.Message);
    public IEnumerable<ulong> AppliedTags => _original.AppliedTags;
    public string Name => _original.Name;
    public NetCord.ThreadArchiveDuration? AutoArchiveDuration => _original.AutoArchiveDuration;
    public int? Slowmode => _original.Slowmode;
    public HttpContent Serialize() => _original.Serialize();
    public IDiscordForumGuildThreadProperties WithMessage(IDiscordForumGuildThreadMessageProperties message) => new DiscordForumGuildThreadProperties(_original.WithMessage(message.Original));
    public IDiscordForumGuildThreadProperties WithAppliedTags(IEnumerable<ulong> appliedTags) => new DiscordForumGuildThreadProperties(_original.WithAppliedTags(appliedTags));
    public IDiscordForumGuildThreadProperties AddAppliedTags(IEnumerable<ulong> appliedTags) => new DiscordForumGuildThreadProperties(_original.AddAppliedTags(appliedTags));
    public IDiscordForumGuildThreadProperties AddAppliedTags(ulong[] appliedTags) => new DiscordForumGuildThreadProperties(_original.AddAppliedTags(appliedTags));
    public IDiscordForumGuildThreadProperties WithName(string name) => new DiscordForumGuildThreadProperties(_original.WithName(name));
    public IDiscordForumGuildThreadProperties WithAutoArchiveDuration(NetCord.ThreadArchiveDuration? autoArchiveDuration) => new DiscordForumGuildThreadProperties(_original.WithAutoArchiveDuration(autoArchiveDuration));
    public IDiscordForumGuildThreadProperties WithSlowmode(int? slowmode) => new DiscordForumGuildThreadProperties(_original.WithSlowmode(slowmode));
}


public class DiscordGatewayBot : IDiscordGatewayBot
{
    private readonly NetCord.Rest.GatewayBot _original;
    public DiscordGatewayBot(NetCord.Rest.GatewayBot original)
    {
        _original = original;
    }
    public NetCord.Rest.GatewayBot Original => _original;
    public string Url => _original.Url;
    public int ShardCount => _original.ShardCount;
    public IDiscordGatewaySessionStartLimit SessionStartLimit => new DiscordGatewaySessionStartLimit(_original.SessionStartLimit);
}


public class DiscordGuildProperties : IDiscordGuildProperties
{
    private readonly NetCord.Rest.GuildProperties _original;
    public DiscordGuildProperties(NetCord.Rest.GuildProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildProperties Original => _original;
    public string Name => _original.Name;
    public NetCord.Rest.ImageProperties? Icon => _original.Icon;
    public NetCord.VerificationLevel? VerificationLevel => _original.VerificationLevel;
    public NetCord.DefaultMessageNotificationLevel? DefaultMessageNotificationLevel => _original.DefaultMessageNotificationLevel;
    public NetCord.ContentFilter? ContentFilter => _original.ContentFilter;
    public IEnumerable<IDiscordRoleProperties> Roles => _original.Roles.Select(x => new DiscordRoleProperties(x));
    public IEnumerable<IDiscordGuildChannelProperties> Channels => _original.Channels.Select(x => new DiscordGuildChannelProperties(x));
    public ulong? AfkChannelId => _original.AfkChannelId;
    public int? AfkTimeout => _original.AfkTimeout;
    public ulong? SystemChannelId => _original.SystemChannelId;
    public NetCord.Rest.SystemChannelFlags? SystemChannelFlags => _original.SystemChannelFlags;
    public IDiscordGuildProperties WithName(string name) => new DiscordGuildProperties(_original.WithName(name));
    public IDiscordGuildProperties WithIcon(NetCord.Rest.ImageProperties? icon) => new DiscordGuildProperties(_original.WithIcon(icon));
    public IDiscordGuildProperties WithVerificationLevel(NetCord.VerificationLevel? verificationLevel) => new DiscordGuildProperties(_original.WithVerificationLevel(verificationLevel));
    public IDiscordGuildProperties WithDefaultMessageNotificationLevel(NetCord.DefaultMessageNotificationLevel? defaultMessageNotificationLevel) => new DiscordGuildProperties(_original.WithDefaultMessageNotificationLevel(defaultMessageNotificationLevel));
    public IDiscordGuildProperties WithContentFilter(NetCord.ContentFilter? contentFilter) => new DiscordGuildProperties(_original.WithContentFilter(contentFilter));
    public IDiscordGuildProperties WithRoles(IEnumerable<IDiscordRoleProperties> roles) => new DiscordGuildProperties(_original.WithRoles(roles?.Select(x => x.Original)));
    public IDiscordGuildProperties AddRoles(IEnumerable<IDiscordRoleProperties> roles) => new DiscordGuildProperties(_original.AddRoles(roles?.Select(x => x.Original)));
    public IDiscordGuildProperties AddRoles(IDiscordRoleProperties[] roles) => new DiscordGuildProperties(_original.AddRoles(roles.Original));
    public IDiscordGuildProperties WithChannels(IEnumerable<IDiscordGuildChannelProperties> channels) => new DiscordGuildProperties(_original.WithChannels(channels?.Select(x => x.Original)));
    public IDiscordGuildProperties AddChannels(IEnumerable<IDiscordGuildChannelProperties> channels) => new DiscordGuildProperties(_original.AddChannels(channels?.Select(x => x.Original)));
    public IDiscordGuildProperties AddChannels(IDiscordGuildChannelProperties[] channels) => new DiscordGuildProperties(_original.AddChannels(channels.Original));
    public IDiscordGuildProperties WithAfkChannelId(ulong? afkChannelId) => new DiscordGuildProperties(_original.WithAfkChannelId(afkChannelId));
    public IDiscordGuildProperties WithAfkTimeout(int? afkTimeout) => new DiscordGuildProperties(_original.WithAfkTimeout(afkTimeout));
    public IDiscordGuildProperties WithSystemChannelId(ulong? systemChannelId) => new DiscordGuildProperties(_original.WithSystemChannelId(systemChannelId));
    public IDiscordGuildProperties WithSystemChannelFlags(NetCord.Rest.SystemChannelFlags? systemChannelFlags) => new DiscordGuildProperties(_original.WithSystemChannelFlags(systemChannelFlags));
}


public class DiscordEntitlementsPaginationProperties : IDiscordEntitlementsPaginationProperties
{
    private readonly NetCord.Rest.EntitlementsPaginationProperties _original;
    public DiscordEntitlementsPaginationProperties(NetCord.Rest.EntitlementsPaginationProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.EntitlementsPaginationProperties Original => _original;
    public ulong? UserId => _original.UserId;
    public IEnumerable<ulong> SkuIds => _original.SkuIds;
    public ulong? GuildId => _original.GuildId;
    public bool? ExcludeEnded => _original.ExcludeEnded;
    public ulong? From => _original.From;
    public NetCord.Rest.PaginationDirection? Direction => _original.Direction;
    public int? BatchSize => _original.BatchSize;
    public IDiscordEntitlementsPaginationProperties WithUserId(ulong? userId) => new DiscordEntitlementsPaginationProperties(_original.WithUserId(userId));
    public IDiscordEntitlementsPaginationProperties WithSkuIds(IEnumerable<ulong> skuIds) => new DiscordEntitlementsPaginationProperties(_original.WithSkuIds(skuIds));
    public IDiscordEntitlementsPaginationProperties AddSkuIds(IEnumerable<ulong> skuIds) => new DiscordEntitlementsPaginationProperties(_original.AddSkuIds(skuIds));
    public IDiscordEntitlementsPaginationProperties AddSkuIds(ulong[] skuIds) => new DiscordEntitlementsPaginationProperties(_original.AddSkuIds(skuIds));
    public IDiscordEntitlementsPaginationProperties WithGuildId(ulong? guildId) => new DiscordEntitlementsPaginationProperties(_original.WithGuildId(guildId));
    public IDiscordEntitlementsPaginationProperties WithExcludeEnded(bool? excludeEnded = true) => new DiscordEntitlementsPaginationProperties(_original.WithExcludeEnded(excludeEnded));
    public IDiscordEntitlementsPaginationProperties WithFrom(ulong? from) => new DiscordEntitlementsPaginationProperties(_original.WithFrom(from));
    public IDiscordEntitlementsPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction) => new DiscordEntitlementsPaginationProperties(_original.WithDirection(direction));
    public IDiscordEntitlementsPaginationProperties WithBatchSize(int? batchSize) => new DiscordEntitlementsPaginationProperties(_original.WithBatchSize(batchSize));
}


public class DiscordTestEntitlementProperties : IDiscordTestEntitlementProperties
{
    private readonly NetCord.Rest.TestEntitlementProperties _original;
    public DiscordTestEntitlementProperties(NetCord.Rest.TestEntitlementProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.TestEntitlementProperties Original => _original;
    public ulong SkuId => _original.SkuId;
    public ulong OwnerId => _original.OwnerId;
    public NetCord.Rest.TestEntitlementOwnerType OwnerType => _original.OwnerType;
    public IDiscordTestEntitlementProperties WithSkuId(ulong skuId) => new DiscordTestEntitlementProperties(_original.WithSkuId(skuId));
    public IDiscordTestEntitlementProperties WithOwnerId(ulong ownerId) => new DiscordTestEntitlementProperties(_original.WithOwnerId(ownerId));
    public IDiscordTestEntitlementProperties WithOwnerType(NetCord.Rest.TestEntitlementOwnerType ownerType) => new DiscordTestEntitlementProperties(_original.WithOwnerType(ownerType));
}


public class DiscordSku : IDiscordSku
{
    private readonly NetCord.Rest.Sku _original;
    public DiscordSku(NetCord.Rest.Sku original)
    {
        _original = original;
    }
    public NetCord.Rest.Sku Original => _original;
    public ulong Id => _original.Id;
    public NetCord.Rest.SkuType Type => _original.Type;
    public ulong ApplicationId => _original.ApplicationId;
    public string Name => _original.Name;
    public string Slug => _original.Slug;
    public NetCord.Rest.SkuFlags Flags => _original.Flags;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async IAsyncEnumerable<IDiscordSubscription> GetSubscriptionsAsync(IDiscordSubscriptionPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetSubscriptionsAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordSubscription(original);
        }
    }
    public async Task<IDiscordSubscription> GetSubscriptionAsync(ulong subscriptionId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordSubscription(await _original.GetSubscriptionAsync(subscriptionId, properties.Original, cancellationToken));
}


public class DiscordAuthorizationInformation : IDiscordAuthorizationInformation
{
    private readonly NetCord.Rest.AuthorizationInformation _original;
    public DiscordAuthorizationInformation(NetCord.Rest.AuthorizationInformation original)
    {
        _original = original;
    }
    public NetCord.Rest.AuthorizationInformation Original => _original;
    public IDiscordApplication Application => new DiscordApplication(_original.Application);
    public IReadOnlyList<string> Scopes => _original.Scopes;
    public System.DateTimeOffset ExpiresAt => _original.ExpiresAt;
    public IDiscordUser User => new DiscordUser(_original.User);
}


public class DiscordStageInstanceProperties : IDiscordStageInstanceProperties
{
    private readonly NetCord.Rest.StageInstanceProperties _original;
    public DiscordStageInstanceProperties(NetCord.Rest.StageInstanceProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.StageInstanceProperties Original => _original;
    public ulong ChannelId => _original.ChannelId;
    public string Topic => _original.Topic;
    public NetCord.StageInstancePrivacyLevel? PrivacyLevel => _original.PrivacyLevel;
    public bool? SendStartNotification => _original.SendStartNotification;
    public ulong? GuildScheduledEventId => _original.GuildScheduledEventId;
    public IDiscordStageInstanceProperties WithChannelId(ulong channelId) => new DiscordStageInstanceProperties(_original.WithChannelId(channelId));
    public IDiscordStageInstanceProperties WithTopic(string topic) => new DiscordStageInstanceProperties(_original.WithTopic(topic));
    public IDiscordStageInstanceProperties WithPrivacyLevel(NetCord.StageInstancePrivacyLevel? privacyLevel) => new DiscordStageInstanceProperties(_original.WithPrivacyLevel(privacyLevel));
    public IDiscordStageInstanceProperties WithSendStartNotification(bool? sendStartNotification = true) => new DiscordStageInstanceProperties(_original.WithSendStartNotification(sendStartNotification));
    public IDiscordStageInstanceProperties WithGuildScheduledEventId(ulong? guildScheduledEventId) => new DiscordStageInstanceProperties(_original.WithGuildScheduledEventId(guildScheduledEventId));
}


public class DiscordStandardSticker : IDiscordStandardSticker
{
    private readonly NetCord.StandardSticker _original;
    public DiscordStandardSticker(NetCord.StandardSticker original)
    {
        _original = original;
    }
    public NetCord.StandardSticker Original => _original;
    public ulong PackId => _original.PackId;
    public int? SortValue => _original.SortValue;
    public ulong Id => _original.Id;
    public string Name => _original.Name;
    public string Description => _original.Description;
    public IReadOnlyList<string> Tags => _original.Tags;
    public NetCord.StickerFormat Format => _original.Format;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordImageUrl GetImageUrl(NetCord.ImageFormat format) => new DiscordImageUrl(_original.GetImageUrl(format));
}


public class DiscordStickerPack : IDiscordStickerPack
{
    private readonly NetCord.Rest.StickerPack _original;
    public DiscordStickerPack(NetCord.Rest.StickerPack original)
    {
        _original = original;
    }
    public NetCord.Rest.StickerPack Original => _original;
    public IReadOnlyList<IDiscordSticker> Stickers => _original.Stickers.Select(x => new DiscordSticker(x)).ToList();
    public string Name => _original.Name;
    public ulong SkuId => _original.SkuId;
    public ulong? CoverStickerId => _original.CoverStickerId;
    public string Description => _original.Description;
    public ulong? BannerAssetId => _original.BannerAssetId;
}


public class DiscordSubscription : IDiscordSubscription
{
    private readonly NetCord.Subscription _original;
    public DiscordSubscription(NetCord.Subscription original)
    {
        _original = original;
    }
    public NetCord.Subscription Original => _original;
    public ulong Id => _original.Id;
    public ulong UserId => _original.UserId;
    public IReadOnlyList<ulong> SkuIds => _original.SkuIds;
    public IReadOnlyList<ulong> EntitlementIds => _original.EntitlementIds;
    public IReadOnlyList<ulong> RenewalSkuIds => _original.RenewalSkuIds;
    public System.DateTimeOffset CurrentPeriodStart => _original.CurrentPeriodStart;
    public System.DateTimeOffset CurrentPeriodEnd => _original.CurrentPeriodEnd;
    public NetCord.SubscriptionStatus Status => _original.Status;
    public System.DateTimeOffset? CanceledAt => _original.CanceledAt;
    public string Country => _original.Country;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordSubscriptionPaginationProperties : IDiscordSubscriptionPaginationProperties
{
    private readonly NetCord.Rest.SubscriptionPaginationProperties _original;
    public DiscordSubscriptionPaginationProperties(NetCord.Rest.SubscriptionPaginationProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.SubscriptionPaginationProperties Original => _original;
    public ulong? UserId => _original.UserId;
    public ulong? From => _original.From;
    public NetCord.Rest.PaginationDirection? Direction => _original.Direction;
    public int? BatchSize => _original.BatchSize;
    public IDiscordSubscriptionPaginationProperties WithUserId(ulong? userId) => new DiscordSubscriptionPaginationProperties(_original.WithUserId(userId));
    public IDiscordSubscriptionPaginationProperties WithFrom(ulong? from) => new DiscordSubscriptionPaginationProperties(_original.WithFrom(from));
    public IDiscordSubscriptionPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction) => new DiscordSubscriptionPaginationProperties(_original.WithDirection(direction));
    public IDiscordSubscriptionPaginationProperties WithBatchSize(int? batchSize) => new DiscordSubscriptionPaginationProperties(_original.WithBatchSize(batchSize));
}


public class DiscordCurrentUser : IDiscordCurrentUser
{
    private readonly NetCord.CurrentUser _original;
    public DiscordCurrentUser(NetCord.CurrentUser original)
    {
        _original = original;
    }
    public NetCord.CurrentUser Original => _original;
    public ulong Id => _original.Id;
    public string Username => _original.Username;
    public ushort Discriminator => _original.Discriminator;
    public string GlobalName => _original.GlobalName;
    public string AvatarHash => _original.AvatarHash;
    public bool IsBot => _original.IsBot;
    public bool? IsSystemUser => _original.IsSystemUser;
    public bool? MfaEnabled => _original.MfaEnabled;
    public string BannerHash => _original.BannerHash;
    public NetCord.Color? AccentColor => _original.AccentColor;
    public string Locale => _original.Locale;
    public bool? Verified => _original.Verified;
    public string Email => _original.Email;
    public NetCord.UserFlags? Flags => _original.Flags;
    public NetCord.PremiumType? PremiumType => _original.PremiumType;
    public NetCord.UserFlags? PublicFlags => _original.PublicFlags;
    public IDiscordAvatarDecorationData AvatarDecorationData => new DiscordAvatarDecorationData(_original.AvatarDecorationData);
    public bool HasAvatar => _original.HasAvatar;
    public bool HasBanner => _original.HasBanner;
    public bool HasAvatarDecoration => _original.HasAvatarDecoration;
    public IDiscordImageUrl DefaultAvatarUrl => new DiscordImageUrl(_original.DefaultAvatarUrl);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordCurrentUser> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordCurrentUser(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordCurrentUser> ModifyAsync(Action<IDiscordCurrentUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordCurrentUser(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public async IAsyncEnumerable<IDiscordRestGuild> GetGuildsAsync(IDiscordGuildsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetGuildsAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordRestGuild(original);
        }
    }
    public async Task<IDiscordGuildUser> GetGuildUserAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGuildUser(await _original.GetGuildUserAsync(guildId, properties.Original, cancellationToken));
    public Task LeaveGuildAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.LeaveGuildAsync(guildId, properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordConnection>> GetConnectionsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetConnectionsAsync(properties.Original, cancellationToken)).Select(x => new DiscordConnection(x)).ToList();
    public async Task<IDiscordApplicationRoleConnection> GetApplicationRoleConnectionAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationRoleConnection(await _original.GetApplicationRoleConnectionAsync(applicationId, properties.Original, cancellationToken));
    public async Task<IDiscordApplicationRoleConnection> UpdateApplicationRoleConnectionAsync(ulong applicationId, IDiscordApplicationRoleConnectionProperties applicationRoleConnectionProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordApplicationRoleConnection(await _original.UpdateApplicationRoleConnectionAsync(applicationId, applicationRoleConnectionProperties.Original, properties.Original, cancellationToken));
    public IDiscordImageUrl GetAvatarUrl(NetCord.ImageFormat? format = default) => new DiscordImageUrl(_original.GetAvatarUrl(format));
    public IDiscordImageUrl GetBannerUrl(NetCord.ImageFormat? format = default) => new DiscordImageUrl(_original.GetBannerUrl(format));
    public IDiscordImageUrl GetAvatarDecorationUrl() => new DiscordImageUrl(_original.GetAvatarDecorationUrl());
    public async Task<IDiscordDMChannel> GetDMChannelAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordDMChannel(await _original.GetDMChannelAsync(properties.Original, cancellationToken));
}


public class DiscordCurrentUserOptions : IDiscordCurrentUserOptions
{
    private readonly NetCord.Rest.CurrentUserOptions _original;
    public DiscordCurrentUserOptions(NetCord.Rest.CurrentUserOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.CurrentUserOptions Original => _original;
    public string Username => _original.Username;
    public NetCord.Rest.ImageProperties? Avatar => _original.Avatar;
    public NetCord.Rest.ImageProperties? Banner => _original.Banner;
    public IDiscordCurrentUserOptions WithUsername(string username) => new DiscordCurrentUserOptions(_original.WithUsername(username));
    public IDiscordCurrentUserOptions WithAvatar(NetCord.Rest.ImageProperties? avatar) => new DiscordCurrentUserOptions(_original.WithAvatar(avatar));
    public IDiscordCurrentUserOptions WithBanner(NetCord.Rest.ImageProperties? banner) => new DiscordCurrentUserOptions(_original.WithBanner(banner));
}


public class DiscordGuildsPaginationProperties : IDiscordGuildsPaginationProperties
{
    private readonly NetCord.Rest.GuildsPaginationProperties _original;
    public DiscordGuildsPaginationProperties(NetCord.Rest.GuildsPaginationProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildsPaginationProperties Original => _original;
    public bool WithCounts => _original.WithCounts;
    public ulong? From => _original.From;
    public NetCord.Rest.PaginationDirection? Direction => _original.Direction;
    public int? BatchSize => _original.BatchSize;
    public IDiscordGuildsPaginationProperties WithWithCounts(bool withCounts = true) => new DiscordGuildsPaginationProperties(_original.WithWithCounts(withCounts));
    public IDiscordGuildsPaginationProperties WithFrom(ulong? from) => new DiscordGuildsPaginationProperties(_original.WithFrom(from));
    public IDiscordGuildsPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction) => new DiscordGuildsPaginationProperties(_original.WithDirection(direction));
    public IDiscordGuildsPaginationProperties WithBatchSize(int? batchSize) => new DiscordGuildsPaginationProperties(_original.WithBatchSize(batchSize));
}


public class DiscordGroupDMChannel : IDiscordGroupDMChannel
{
    private readonly NetCord.GroupDMChannel _original;
    public DiscordGroupDMChannel(NetCord.GroupDMChannel original)
    {
        _original = original;
    }
    public NetCord.GroupDMChannel Original => _original;
    public string Name => _original.Name;
    public string IconHash => _original.IconHash;
    public ulong OwnerId => _original.OwnerId;
    public ulong? ApplicationId => _original.ApplicationId;
    public bool Managed => _original.Managed;
    public IReadOnlyDictionary<ulong, IDiscordUser> Users => _original.Users.ToDictionary(kv => kv.Key, kv => (IDiscordUser)new DiscordUser(kv.Value));
    public ulong? LastMessageId => _original.LastMessageId;
    public System.DateTimeOffset? LastPin => _original.LastPin;
    public ulong Id => _original.Id;
    public NetCord.ChannelFlags Flags => _original.Flags;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordGroupDMChannel> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGroupDMChannel(await _original.GetAsync(properties.Original, cancellationToken));
    public async Task<IDiscordGroupDMChannel> ModifyAsync(Action<IDiscordGroupDMChannelOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGroupDMChannel(await _original.ModifyAsync(action, properties.Original, cancellationToken));
    public async Task<IDiscordGroupDMChannel> DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordGroupDMChannel(await _original.DeleteAsync(properties.Original, cancellationToken));
    public Task AddUserAsync(ulong userId, IDiscordGroupDMChannelUserAddProperties groupDMChannelUserAddProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.AddUserAsync(userId, groupDMChannelUserAddProperties.Original, properties.Original, cancellationToken);
    public Task DeleteUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteUserAsync(userId, properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordRestMessage> GetMessagesAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetMessagesAsync(paginationProperties.Original, properties.Original))
        {
            yield return new DiscordRestMessage(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetMessagesAroundAsync(ulong messageId, int? limit = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetMessagesAroundAsync(messageId, limit, properties.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    public async Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.GetMessageAsync(messageId, properties.Original, cancellationToken));
    public async Task<IDiscordRestMessage> SendMessageAsync(IDiscordMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.SendMessageAsync(message.Original, properties.Original, cancellationToken));
    public Task AddMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.AddMessageReactionAsync(messageId, emoji.Original, properties.Original, cancellationToken);
    public Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessageReactionAsync(messageId, emoji.Original, properties.Original, cancellationToken);
    public Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessageReactionAsync(messageId, emoji.Original, userId, properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordUser> GetMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordMessageReactionsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetMessageReactionsAsync(messageId, emoji.Original, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAllMessageReactionsAsync(messageId, properties.Original, cancellationToken);
    public Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteAllMessageReactionsAsync(messageId, emoji.Original, properties.Original, cancellationToken);
    public async Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.ModifyMessageAsync(messageId, action, properties.Original, cancellationToken));
    public Task DeleteMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessageAsync(messageId, properties.Original, cancellationToken);
    public Task DeleteMessagesAsync(IEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessagesAsync(messageIds, properties.Original, cancellationToken);
    public Task DeleteMessagesAsync(IAsyncEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.DeleteMessagesAsync(messageIds, properties.Original, cancellationToken);
    public Task TriggerTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.TriggerTypingStateAsync(properties.Original, cancellationToken);
    public Task<IDisposable> EnterTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.EnterTypingStateAsync(properties.Original, cancellationToken);
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetPinnedMessagesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.GetPinnedMessagesAsync(properties.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    public Task PinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.PinMessageAsync(messageId, properties.Original, cancellationToken);
    public Task UnpinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => _original.UnpinMessageAsync(messageId, properties.Original, cancellationToken);
    public async IAsyncEnumerable<IDiscordUser> GetMessagePollAnswerVotersAsync(ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null)
    {
        await foreach(var original in _original.GetMessagePollAnswerVotersAsync(messageId, answerId, paginationProperties.Original, properties.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public async Task<IDiscordRestMessage> EndMessagePollAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => new DiscordRestMessage(await _original.EndMessagePollAsync(messageId, properties.Original, cancellationToken));
    public async Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) => (await _original.CreateGoogleCloudPlatformStorageBucketsAsync(buckets?.Select(x => x.Original), properties.Original, cancellationToken)).Select(x => new DiscordGoogleCloudPlatformStorageBucket(x)).ToList();
}


public class DiscordGroupDMChannelProperties : IDiscordGroupDMChannelProperties
{
    private readonly NetCord.Rest.GroupDMChannelProperties _original;
    public DiscordGroupDMChannelProperties(NetCord.Rest.GroupDMChannelProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GroupDMChannelProperties Original => _original;
    public IEnumerable<string> AccessTokens => _original.AccessTokens;
    public IReadOnlyDictionary<ulong, string> Nicknames => _original.Nicknames;
    public IDiscordGroupDMChannelProperties WithAccessTokens(IEnumerable<string> accessTokens) => new DiscordGroupDMChannelProperties(_original.WithAccessTokens(accessTokens));
    public IDiscordGroupDMChannelProperties AddAccessTokens(IEnumerable<string> accessTokens) => new DiscordGroupDMChannelProperties(_original.AddAccessTokens(accessTokens));
    public IDiscordGroupDMChannelProperties AddAccessTokens(string[] accessTokens) => new DiscordGroupDMChannelProperties(_original.AddAccessTokens(accessTokens));
    public IDiscordGroupDMChannelProperties WithNicknames(IReadOnlyDictionary<ulong, string> nicknames) => new DiscordGroupDMChannelProperties(_original.WithNicknames(nicknames));
}


public class DiscordConnection : IDiscordConnection
{
    private readonly NetCord.Rest.Connection _original;
    public DiscordConnection(NetCord.Rest.Connection original)
    {
        _original = original;
    }
    public NetCord.Rest.Connection Original => _original;
    public string Id => _original.Id;
    public string Name => _original.Name;
    public NetCord.Rest.ConnectionType Type => _original.Type;
    public bool? Revoked => _original.Revoked;
    public IReadOnlyList<IDiscordIntegration> Integrations => _original.Integrations.Select(x => new DiscordIntegration(x)).ToList();
    public bool Verified => _original.Verified;
    public bool FriendSync => _original.FriendSync;
    public bool ShowActivity => _original.ShowActivity;
    public bool TwoWayLink => _original.TwoWayLink;
    public NetCord.Rest.ConnectionVisibility Visibility => _original.Visibility;
}


public class DiscordApplicationRoleConnection : IDiscordApplicationRoleConnection
{
    private readonly NetCord.Rest.ApplicationRoleConnection _original;
    public DiscordApplicationRoleConnection(NetCord.Rest.ApplicationRoleConnection original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationRoleConnection Original => _original;
    public string PlatformName => _original.PlatformName;
    public string PlatformUsername => _original.PlatformUsername;
    public IReadOnlyDictionary<string, string> Metadata => _original.Metadata;
}


public class DiscordApplicationRoleConnectionProperties : IDiscordApplicationRoleConnectionProperties
{
    private readonly NetCord.Rest.ApplicationRoleConnectionProperties _original;
    public DiscordApplicationRoleConnectionProperties(NetCord.Rest.ApplicationRoleConnectionProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationRoleConnectionProperties Original => _original;
    public string PlatformName => _original.PlatformName;
    public string PlatformUsername => _original.PlatformUsername;
    public IReadOnlyDictionary<string, string> Metadata => _original.Metadata;
    public IDiscordApplicationRoleConnectionProperties WithPlatformName(string platformName) => new DiscordApplicationRoleConnectionProperties(_original.WithPlatformName(platformName));
    public IDiscordApplicationRoleConnectionProperties WithPlatformUsername(string platformUsername) => new DiscordApplicationRoleConnectionProperties(_original.WithPlatformUsername(platformUsername));
    public IDiscordApplicationRoleConnectionProperties WithMetadata(IReadOnlyDictionary<string, string> metadata) => new DiscordApplicationRoleConnectionProperties(_original.WithMetadata(metadata));
}


public class DiscordApplicationInstallParamsProperties : IDiscordApplicationInstallParamsProperties
{
    private readonly NetCord.Rest.ApplicationInstallParamsProperties _original;
    public DiscordApplicationInstallParamsProperties(NetCord.Rest.ApplicationInstallParamsProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationInstallParamsProperties Original => _original;
    public IEnumerable<string> Scopes => _original.Scopes;
    public NetCord.Permissions? Permissions => _original.Permissions;
    public IDiscordApplicationInstallParamsProperties WithScopes(IEnumerable<string> scopes) => new DiscordApplicationInstallParamsProperties(_original.WithScopes(scopes));
    public IDiscordApplicationInstallParamsProperties AddScopes(IEnumerable<string> scopes) => new DiscordApplicationInstallParamsProperties(_original.AddScopes(scopes));
    public IDiscordApplicationInstallParamsProperties AddScopes(string[] scopes) => new DiscordApplicationInstallParamsProperties(_original.AddScopes(scopes));
    public IDiscordApplicationInstallParamsProperties WithPermissions(NetCord.Permissions? permissions) => new DiscordApplicationInstallParamsProperties(_original.WithPermissions(permissions));
}


public class DiscordApplicationIntegrationTypeConfigurationProperties : IDiscordApplicationIntegrationTypeConfigurationProperties
{
    private readonly NetCord.Rest.ApplicationIntegrationTypeConfigurationProperties _original;
    public DiscordApplicationIntegrationTypeConfigurationProperties(NetCord.Rest.ApplicationIntegrationTypeConfigurationProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationIntegrationTypeConfigurationProperties Original => _original;
    public IDiscordApplicationInstallParamsProperties OAuth2InstallParams => new DiscordApplicationInstallParamsProperties(_original.OAuth2InstallParams);
    public IDiscordApplicationIntegrationTypeConfigurationProperties WithOAuth2InstallParams(IDiscordApplicationInstallParamsProperties oAuth2InstallParams) => new DiscordApplicationIntegrationTypeConfigurationProperties(_original.WithOAuth2InstallParams(oAuth2InstallParams.Original));
}


public class DiscordForumGuildThreadMessageProperties : IDiscordForumGuildThreadMessageProperties
{
    private readonly NetCord.Rest.ForumGuildThreadMessageProperties _original;
    public DiscordForumGuildThreadMessageProperties(NetCord.Rest.ForumGuildThreadMessageProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ForumGuildThreadMessageProperties Original => _original;
    public string Content => _original.Content;
    public IEnumerable<IDiscordEmbedProperties> Embeds => _original.Embeds.Select(x => new DiscordEmbedProperties(x));
    public IDiscordAllowedMentionsProperties AllowedMentions => new DiscordAllowedMentionsProperties(_original.AllowedMentions);
    public IEnumerable<IDiscordComponentProperties> Components => _original.Components.Select(x => new DiscordComponentProperties(x));
    public IEnumerable<ulong> StickerIds => _original.StickerIds;
    public IEnumerable<IDiscordAttachmentProperties> Attachments => _original.Attachments.Select(x => new DiscordAttachmentProperties(x));
    public NetCord.MessageFlags? Flags => _original.Flags;
    public IDiscordForumGuildThreadMessageProperties WithContent(string content) => new DiscordForumGuildThreadMessageProperties(_original.WithContent(content));
    public IDiscordForumGuildThreadMessageProperties WithEmbeds(IEnumerable<IDiscordEmbedProperties> embeds) => new DiscordForumGuildThreadMessageProperties(_original.WithEmbeds(embeds?.Select(x => x.Original)));
    public IDiscordForumGuildThreadMessageProperties AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds) => new DiscordForumGuildThreadMessageProperties(_original.AddEmbeds(embeds?.Select(x => x.Original)));
    public IDiscordForumGuildThreadMessageProperties AddEmbeds(IDiscordEmbedProperties[] embeds) => new DiscordForumGuildThreadMessageProperties(_original.AddEmbeds(embeds.Original));
    public IDiscordForumGuildThreadMessageProperties WithAllowedMentions(IDiscordAllowedMentionsProperties allowedMentions) => new DiscordForumGuildThreadMessageProperties(_original.WithAllowedMentions(allowedMentions.Original));
    public IDiscordForumGuildThreadMessageProperties WithComponents(IEnumerable<IDiscordComponentProperties> components) => new DiscordForumGuildThreadMessageProperties(_original.WithComponents(components?.Select(x => x.Original)));
    public IDiscordForumGuildThreadMessageProperties AddComponents(IEnumerable<IDiscordComponentProperties> components) => new DiscordForumGuildThreadMessageProperties(_original.AddComponents(components?.Select(x => x.Original)));
    public IDiscordForumGuildThreadMessageProperties AddComponents(IDiscordComponentProperties[] components) => new DiscordForumGuildThreadMessageProperties(_original.AddComponents(components.Original));
    public IDiscordForumGuildThreadMessageProperties WithStickerIds(IEnumerable<ulong> stickerIds) => new DiscordForumGuildThreadMessageProperties(_original.WithStickerIds(stickerIds));
    public IDiscordForumGuildThreadMessageProperties AddStickerIds(IEnumerable<ulong> stickerIds) => new DiscordForumGuildThreadMessageProperties(_original.AddStickerIds(stickerIds));
    public IDiscordForumGuildThreadMessageProperties AddStickerIds(ulong[] stickerIds) => new DiscordForumGuildThreadMessageProperties(_original.AddStickerIds(stickerIds));
    public IDiscordForumGuildThreadMessageProperties WithAttachments(IEnumerable<IDiscordAttachmentProperties> attachments) => new DiscordForumGuildThreadMessageProperties(_original.WithAttachments(attachments?.Select(x => x.Original)));
    public IDiscordForumGuildThreadMessageProperties AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments) => new DiscordForumGuildThreadMessageProperties(_original.AddAttachments(attachments?.Select(x => x.Original)));
    public IDiscordForumGuildThreadMessageProperties AddAttachments(IDiscordAttachmentProperties[] attachments) => new DiscordForumGuildThreadMessageProperties(_original.AddAttachments(attachments.Original));
    public IDiscordForumGuildThreadMessageProperties WithFlags(NetCord.MessageFlags? flags) => new DiscordForumGuildThreadMessageProperties(_original.WithFlags(flags));
}


public class DiscordGatewaySessionStartLimit : IDiscordGatewaySessionStartLimit
{
    private readonly NetCord.Rest.GatewaySessionStartLimit _original;
    public DiscordGatewaySessionStartLimit(NetCord.Rest.GatewaySessionStartLimit original)
    {
        _original = original;
    }
    public NetCord.Rest.GatewaySessionStartLimit Original => _original;
    public int Total => _original.Total;
    public int Remaining => _original.Remaining;
    public System.TimeSpan ResetAfter => _original.ResetAfter;
    public int MaxConcurrency => _original.MaxConcurrency;
}


public class DiscordSticker : IDiscordSticker
{
    private readonly NetCord.Sticker _original;
    public DiscordSticker(NetCord.Sticker original)
    {
        _original = original;
    }
    public NetCord.Sticker Original => _original;
    public ulong Id => _original.Id;
    public string Name => _original.Name;
    public string Description => _original.Description;
    public IReadOnlyList<string> Tags => _original.Tags;
    public NetCord.StickerFormat Format => _original.Format;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordImageUrl GetImageUrl(NetCord.ImageFormat format) => new DiscordImageUrl(_original.GetImageUrl(format));
}


