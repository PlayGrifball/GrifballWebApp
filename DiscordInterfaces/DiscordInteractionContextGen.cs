using System.Linq;
using System.Linq.Expressions;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace DiscordInterface.Generated;

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
    IDiscordInteractionGuildReference? GuildReference { get; }
    IDiscordGuild? Guild { get; }
    IDiscordTextChannel Channel { get; }
    IDiscordUser User { get; }
    string Token { get; }
    NetCord.Permissions AppPermissions { get; }
    string UserLocale { get; }
    string? GuildLocale { get; }
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
    ImmutableDictionary<ulong, IDiscordVoiceState> VoiceStates { get; set; }
    ImmutableDictionary<ulong, IDiscordGuildUser> Users { get; set; }
    ImmutableDictionary<ulong, IDiscordGuildChannel> Channels { get; set; }
    ImmutableDictionary<ulong, IDiscordGuildThread> ActiveThreads { get; set; }
    ImmutableDictionary<ulong, IDiscordPresence> Presences { get; set; }
    ImmutableDictionary<ulong, IDiscordStageInstance> StageInstances { get; set; }
    ImmutableDictionary<ulong, IDiscordGuildScheduledEvent> ScheduledEvents { get; set; }
    bool IsOwner { get; }
    ulong Id { get; }
    string Name { get; }
    bool HasIcon { get; }
    string? IconHash { get; }
    bool HasSplash { get; }
    string? SplashHash { get; }
    bool HasDiscoverySplash { get; }
    string? DiscoverySplashHash { get; }
    ulong OwnerId { get; }
    NetCord.Permissions? Permissions { get; }
    ulong? AfkChannelId { get; }
    int AfkTimeout { get; }
    bool? WidgetEnabled { get; }
    ulong? WidgetChannelId { get; }
    NetCord.VerificationLevel VerificationLevel { get; }
    NetCord.DefaultMessageNotificationLevel DefaultMessageNotificationLevel { get; }
    NetCord.ContentFilter ContentFilter { get; }
    ImmutableDictionary<ulong, IDiscordRole> Roles { get; set; }
    ImmutableDictionary<ulong, IDiscordGuildEmoji> Emojis { get; set; }
    IReadOnlyList<string> Features { get; }
    NetCord.MfaLevel MfaLevel { get; }
    ulong? ApplicationId { get; }
    ulong? SystemChannelId { get; }
    NetCord.Rest.SystemChannelFlags SystemChannelFlags { get; }
    ulong? RulesChannelId { get; }
    int? MaxPresences { get; }
    int? MaxUsers { get; }
    string? VanityUrlCode { get; }
    string? Description { get; }
    bool HasBanner { get; }
    string? BannerHash { get; }
    int PremiumTier { get; }
    int? PremiumSubscriptionCount { get; }
    string PreferredLocale { get; }
    ulong? PublicUpdatesChannelId { get; }
    int? MaxVideoChannelUsers { get; }
    int? MaxStageVideoChannelUsers { get; }
    int? ApproximateUserCount { get; }
    int? ApproximatePresenceCount { get; }
    IDiscordGuildWelcomeScreen? WelcomeScreen { get; }
    NetCord.NsfwLevel NsfwLevel { get; }
    ImmutableDictionary<ulong, IDiscordGuildSticker> Stickers { get; set; }
    bool PremiumProgressBarEnabled { get; }
    ulong? SafetyAlertsChannelId { get; }
    IDiscordRole? EveryoneRole { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordGuild With(Action<IDiscordGuild> action);
    int? Compare(IDiscordPartialGuildUser x, IDiscordPartialGuildUser y);
    IDiscordImageUrl? GetIconUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl? GetSplashUrl(NetCord.ImageFormat format);
    IDiscordImageUrl? GetDiscoverySplashUrl(NetCord.ImageFormat format);
    IDiscordImageUrl? GetBannerUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl GetWidgetUrl(NetCord.GuildWidgetStyle? style = default, string? hostname = null, NetCord.ApiVersion? version = default);
    IAsyncEnumerable<IDiscordRestAuditLogEntry>? GetAuditLogAsync(IDiscordGuildAuditLogPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
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
    IAsyncEnumerable<IDiscordGuildUser>? GetUsersAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IReadOnlyList<IDiscordGuildUser>> FindUserAsync(string name, int limit, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildUser?> AddUserAsync(ulong userId, IDiscordGuildUserProperties userProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildUser> ModifyUserAsync(ulong userId, Action<IDiscordGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildUser> ModifyCurrentUserAsync(Action<IDiscordCurrentGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task AddUserRoleAsync(ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task RemoveUserRoleAsync(ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task KickUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordGuildBan>? GetBansAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
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
    Task<int>? GetPruneCountAsync(int days, IEnumerable<ulong>? roles = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
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
    IAsyncEnumerable<IDiscordGuildScheduledEventUser>? GetScheduledEventUsersAsync(ulong scheduledEventId, IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
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
    IAsyncEnumerable<IDiscordGuildUserInfo>? SearchUsersAsync(IDiscordGuildUsersSearchPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
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
    IAsyncEnumerable<IDiscordRestMessage>? GetMessagesAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
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
    IAsyncEnumerable<IDiscordUser>? GetMessagePollAnswerVotersAsync(ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordRestMessage> EndMessagePollAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordUser  
{
    NetCord.User Original { get; }
    ulong Id { get; }
    string Username { get; }
    ushort Discriminator { get; }
    string? GlobalName { get; }
    string? AvatarHash { get; }
    bool IsBot { get; }
    bool? IsSystemUser { get; }
    bool? MfaEnabled { get; }
    string? BannerHash { get; }
    NetCord.Color? AccentColor { get; }
    string? Locale { get; }
    bool? Verified { get; }
    string? Email { get; }
    NetCord.UserFlags? Flags { get; }
    NetCord.PremiumType? PremiumType { get; }
    NetCord.UserFlags? PublicFlags { get; }
    IDiscordAvatarDecorationData? AvatarDecorationData { get; }
    bool HasAvatar { get; }
    bool HasBanner { get; }
    bool HasAvatarDecoration { get; }
    IDiscordImageUrl DefaultAvatarUrl { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordImageUrl? GetAvatarUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl? GetBannerUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl? GetAvatarDecorationUrl();
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
    static IDiscordInteractionCallback Pong => new DiscordInteractionCallback(NetCord.Rest.InteractionCallback.Pong);
    static IDiscordInteractionCallback DeferredModifyMessage => new DiscordInteractionCallback(NetCord.Rest.InteractionCallback.DeferredModifyMessage);
    HttpContent Serialize();
}


public interface IDiscordRestRequestProperties  
{
    NetCord.Rest.RestRequestProperties Original { get; }
    NetCord.Rest.RestRateLimitHandling? RateLimitHandling { get; set; }
    string? AuditLogReason { get; set; }
    string? ErrorLocalization { get; set; }
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
    string? Nonce { get; }
    bool IsPinned { get; }
    ulong? WebhookId { get; }
    NetCord.MessageType Type { get; }
    IDiscordMessageActivity? Activity { get; }
    IDiscordApplication? Application { get; }
    ulong? ApplicationId { get; }
    NetCord.MessageFlags Flags { get; }
    IDiscordMessageReference? MessageReference { get; }
    IReadOnlyList<IDiscordMessageSnapshot> MessageSnapshots { get; }
    IDiscordRestMessage? ReferencedMessage { get; }
    IDiscordMessageInteractionMetadata? InteractionMetadata { get; }
    IDiscordGuildThread? StartedThread { get; }
    IReadOnlyList<IDiscordComponent> Components { get; }
    IReadOnlyList<IDiscordMessageSticker> Stickers { get; }
    int? Position { get; }
    IDiscordRoleSubscriptionData? RoleSubscriptionData { get; }
    IDiscordInteractionResolvedData? ResolvedData { get; }
    IDiscordMessagePoll? Poll { get; }
    IDiscordMessageCall? Call { get; }
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
    IAsyncEnumerable<IDiscordUser>? GetPollAnswerVotersAsync(int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordRestMessage> EndPollAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordMessageOptions  
{
    NetCord.Rest.MessageOptions Original { get; }
    string? Content { get; set; }
    IEnumerable<IDiscordEmbedProperties>? Embeds { get; set; }
    NetCord.MessageFlags? Flags { get; set; }
    IDiscordAllowedMentionsProperties? AllowedMentions { get; set; }
    IEnumerable<IDiscordComponentProperties>? Components { get; set; }
    IEnumerable<IDiscordAttachmentProperties>? Attachments { get; set; }
    HttpContent Serialize();
    IDiscordMessageOptions WithContent(string content);
    IDiscordMessageOptions WithEmbeds(IEnumerable<IDiscordEmbedProperties>? embeds);
    IDiscordMessageOptions AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds);
    IDiscordMessageOptions AddEmbeds(IDiscordEmbedProperties[] embeds);
    IDiscordMessageOptions WithFlags(NetCord.MessageFlags? flags);
    IDiscordMessageOptions WithAllowedMentions(IDiscordAllowedMentionsProperties? allowedMentions);
    IDiscordMessageOptions WithComponents(IEnumerable<IDiscordComponentProperties>? components);
    IDiscordMessageOptions AddComponents(IEnumerable<IDiscordComponentProperties> components);
    IDiscordMessageOptions AddComponents(IDiscordComponentProperties[] components);
    IDiscordMessageOptions WithAttachments(IEnumerable<IDiscordAttachmentProperties>? attachments);
    IDiscordMessageOptions AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments);
    IDiscordMessageOptions AddAttachments(IDiscordAttachmentProperties[] attachments);
}


public interface IDiscordInteractionMessageProperties  
{
    NetCord.Rest.InteractionMessageProperties Original { get; }
    bool Tts { get; set; }
    string? Content { get; set; }
    IEnumerable<IDiscordEmbedProperties>? Embeds { get; set; }
    IDiscordAllowedMentionsProperties? AllowedMentions { get; set; }
    NetCord.MessageFlags? Flags { get; set; }
    IEnumerable<IDiscordComponentProperties>? Components { get; set; }
    IEnumerable<IDiscordAttachmentProperties>? Attachments { get; set; }
    IDiscordMessagePollProperties? Poll { get; set; }
    HttpContent Serialize();
    IDiscordInteractionMessageProperties WithTts(bool tts = true);
    IDiscordInteractionMessageProperties WithContent(string content);
    IDiscordInteractionMessageProperties WithEmbeds(IEnumerable<IDiscordEmbedProperties>? embeds);
    IDiscordInteractionMessageProperties AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds);
    IDiscordInteractionMessageProperties AddEmbeds(IDiscordEmbedProperties[] embeds);
    IDiscordInteractionMessageProperties WithAllowedMentions(IDiscordAllowedMentionsProperties? allowedMentions);
    IDiscordInteractionMessageProperties WithFlags(NetCord.MessageFlags? flags);
    IDiscordInteractionMessageProperties WithComponents(IEnumerable<IDiscordComponentProperties>? components);
    IDiscordInteractionMessageProperties AddComponents(IEnumerable<IDiscordComponentProperties> components);
    IDiscordInteractionMessageProperties AddComponents(IDiscordComponentProperties[] components);
    IDiscordInteractionMessageProperties WithAttachments(IEnumerable<IDiscordAttachmentProperties>? attachments);
    IDiscordInteractionMessageProperties AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments);
    IDiscordInteractionMessageProperties AddAttachments(IDiscordAttachmentProperties[] attachments);
    IDiscordInteractionMessageProperties WithPoll(IDiscordMessagePollProperties? poll);
}


public interface IDiscordVoiceState  
{
    NetCord.Gateway.VoiceState Original { get; }
    ulong GuildId { get; }
    ulong? ChannelId { get; }
    ulong UserId { get; }
    IDiscordGuildUser? User { get; }
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
    string? Nickname { get; }
    string? GuildAvatarHash { get; }
    string? GuildBannerHash { get; }
    IReadOnlyList<ulong> RoleIds { get; }
    ulong? HoistedRoleId { get; }
    System.DateTimeOffset JoinedAt { get; }
    System.DateTimeOffset? GuildBoostStart { get; }
    bool Deafened { get; }
    bool Muted { get; }
    NetCord.GuildUserFlags GuildFlags { get; }
    bool? IsPending { get; }
    System.DateTimeOffset? TimeOutUntil { get; }
    IDiscordAvatarDecorationData? GuildAvatarDecorationData { get; }
    bool HasGuildAvatar { get; }
    bool HasGuildBanner { get; }
    bool HasGuildAvatarDecoration { get; }
    ulong Id { get; }
    string Username { get; }
    ushort Discriminator { get; }
    string? GlobalName { get; }
    string? AvatarHash { get; }
    bool IsBot { get; }
    bool? IsSystemUser { get; }
    bool? MfaEnabled { get; }
    string? BannerHash { get; }
    NetCord.Color? AccentColor { get; }
    string? Locale { get; }
    bool? Verified { get; }
    string? Email { get; }
    NetCord.UserFlags? Flags { get; }
    NetCord.PremiumType? PremiumType { get; }
    NetCord.UserFlags? PublicFlags { get; }
    IDiscordAvatarDecorationData? AvatarDecorationData { get; }
    bool HasAvatar { get; }
    bool HasBanner { get; }
    bool HasAvatarDecoration { get; }
    IDiscordImageUrl DefaultAvatarUrl { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordImageUrl? GetGuildAvatarUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl? GetGuildBannerUrl(NetCord.ImageFormat? format = default);
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
    IDiscordImageUrl? GetGuildAvatarDecorationUrl();
    IDiscordImageUrl? GetAvatarUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl? GetBannerUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl? GetAvatarDecorationUrl();
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
    Task<IDiscordRestInvite>? CreateInviteAsync(IDiscordInviteProperties? inviteProperties = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeletePermissionAsync(ulong overwriteId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordGuildThread  
{
    NetCord.GuildThread Original { get; }
    ulong OwnerId { get; }
    int MessageCount { get; }
    int UserCount { get; }
    IDiscordGuildThreadMetadata Metadata { get; }
    IDiscordThreadCurrentUser? CurrentUser { get; }
    int TotalMessageSent { get; }
    ulong GuildId { get; }
    int? Position { get; }
    IReadOnlyDictionary<ulong, IDiscordPermissionOverwrite> PermissionOverwrites { get; }
    string Name { get; }
    string? Topic { get; }
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
    IAsyncEnumerable<IDiscordThreadUser>? GetUsersAsync(IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task ModifyPermissionsAsync(IDiscordPermissionOverwriteProperties permissionOverwrite, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IEnumerable<IDiscordRestInvite>> GetInvitesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestInvite>? CreateInviteAsync(IDiscordInviteProperties? inviteProperties = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeletePermissionAsync(ulong overwriteId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildThread> CreateGuildThreadAsync(ulong messageId, IDiscordGuildThreadFromMessageProperties threadFromMessageProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildThread> CreateGuildThreadAsync(IDiscordGuildThreadProperties threadProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordGuildThread>? GetPublicArchivedGuildThreadsAsync(IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    IAsyncEnumerable<IDiscordGuildThread>? GetPrivateArchivedGuildThreadsAsync(IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    IAsyncEnumerable<IDiscordGuildThread>? GetJoinedPrivateArchivedGuildThreadsAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordIncomingWebhook> CreateWebhookAsync(IDiscordWebhookProperties webhookProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordWebhook>> GetChannelWebhooksAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordRestMessage>? GetMessagesAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
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
    IAsyncEnumerable<IDiscordUser>? GetMessagePollAnswerVotersAsync(ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
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
    string? Description { get; }
    System.DateTimeOffset ScheduledStartTime { get; }
    System.DateTimeOffset? ScheduledEndTime { get; }
    NetCord.GuildScheduledEventPrivacyLevel PrivacyLevel { get; }
    NetCord.GuildScheduledEventStatus Status { get; }
    NetCord.GuildScheduledEventEntityType EntityType { get; }
    ulong? EntityId { get; }
    string? Location { get; }
    IDiscordUser? Creator { get; }
    int? UserCount { get; }
    string? CoverImageHash { get; }
    IDiscordGuildScheduledEventRecurrenceRule? RecurrenceRule { get; }
    bool HasCoverImage { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordImageUrl? GetCoverImageUrl(NetCord.ImageFormat format);
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
    string? IconHash { get; }
    string? UnicodeEmoji { get; }
    int Position { get; }
    NetCord.Permissions Permissions { get; }
    bool Managed { get; }
    bool Mentionable { get; }
    IDiscordRoleTags? Tags { get; }
    NetCord.RoleFlags Flags { get; }
    ulong GuildId { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordImageUrl? GetIconUrl(NetCord.ImageFormat format);
    int? CompareTo(IDiscordRole other);
    Task<IDiscordRole> ModifyAsync(Action<IDiscordRoleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordGuildEmoji  
{
    NetCord.GuildEmoji Original { get; }
    IReadOnlyList<ulong>? AllowedRoles { get; }
    ulong GuildId { get; }
    ulong Id { get; }
    IDiscordUser? Creator { get; }
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
    string? Description { get; }
    ImmutableDictionary<ulong, IDiscordGuildWelcomeScreenChannel> WelcomeChannels { get; }
}


public interface IDiscordGuildSticker  
{
    NetCord.GuildSticker Original { get; }
    bool? Available { get; }
    ulong GuildId { get; }
    IDiscordUser? Creator { get; }
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
    string? Nickname { get; }
    string? GuildAvatarHash { get; }
    string? GuildBannerHash { get; }
    IReadOnlyList<ulong> RoleIds { get; }
    ulong? HoistedRoleId { get; }
    System.DateTimeOffset JoinedAt { get; }
    System.DateTimeOffset? GuildBoostStart { get; }
    bool Deafened { get; }
    bool Muted { get; }
    NetCord.GuildUserFlags GuildFlags { get; }
    bool? IsPending { get; }
    System.DateTimeOffset? TimeOutUntil { get; }
    IDiscordAvatarDecorationData? GuildAvatarDecorationData { get; }
    bool HasGuildAvatar { get; }
    bool HasGuildBanner { get; }
    bool HasGuildAvatarDecoration { get; }
    ulong Id { get; }
    string Username { get; }
    ushort Discriminator { get; }
    string? GlobalName { get; }
    string? AvatarHash { get; }
    bool IsBot { get; }
    bool? IsSystemUser { get; }
    bool? MfaEnabled { get; }
    string? BannerHash { get; }
    NetCord.Color? AccentColor { get; }
    string? Locale { get; }
    bool? Verified { get; }
    string? Email { get; }
    NetCord.UserFlags? Flags { get; }
    NetCord.PremiumType? PremiumType { get; }
    NetCord.UserFlags? PublicFlags { get; }
    IDiscordAvatarDecorationData? AvatarDecorationData { get; }
    bool HasAvatar { get; }
    bool HasBanner { get; }
    bool HasAvatarDecoration { get; }
    IDiscordImageUrl DefaultAvatarUrl { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordImageUrl? GetGuildAvatarDecorationUrl();
    IDiscordImageUrl? GetAvatarUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl? GetBannerUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl? GetAvatarDecorationUrl();
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
    IDiscordUser? User { get; }
    ulong Id { get; }
    ulong? TargetId { get; }
    IReadOnlyDictionary<string, IDiscordAuditLogChange> Changes { get; }
    ulong? UserId { get; }
    NetCord.AuditLogEvent ActionType { get; }
    IDiscordAuditLogEntryInfo? Options { get; }
    string? Reason { get; }
    ulong GuildId { get; }
    System.DateTimeOffset CreatedAt { get; }
    bool TryGetChange<TObjectParam, TValueParam>(Expression<Func<TObjectParam, TValueParam?>> expression, out IDiscordAuditLogChange change)where TObjectParam : NetCord.JsonModels.JsonEntity;
    IDiscordAuditLogChange<TValueParam> GetChange<TObjectParam, TValueParam>(Expression<Func<TObjectParam, TValueParam?>> expression)where TObjectParam : NetCord.JsonModels.JsonEntity;
    bool TryGetChange<TObjectParam, TValueParam>(Expression<Func<TObjectParam, TValueParam?>> expression, JsonTypeInfo<TValueParam> jsonTypeInfo, out IDiscordAuditLogChange change)where TObjectParam : NetCord.JsonModels.JsonEntity;
    IDiscordAuditLogChange<TValueParam> GetChange<TObjectParam, TValueParam>(Expression<Func<TObjectParam, TValueParam?>> expression, JsonTypeInfo<TValueParam> jsonTypeInfo)where TObjectParam : NetCord.JsonModels.JsonEntity;
}


public interface IDiscordGuildAuditLogPaginationProperties  
{
    NetCord.Rest.GuildAuditLogPaginationProperties Original { get; }
    ulong? UserId { get; set; }
    NetCord.AuditLogEvent? ActionType { get; set; }
    ulong? From { get; set; }
    NetCord.Rest.PaginationDirection? Direction { get; set; }
    int? BatchSize { get; set; }
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
    string Name { get; set; }
    NetCord.AutoModerationRuleEventType EventType { get; set; }
    NetCord.AutoModerationRuleTriggerType TriggerType { get; set; }
    IDiscordAutoModerationRuleTriggerMetadataProperties? TriggerMetadata { get; set; }
    IEnumerable<IDiscordAutoModerationActionProperties> Actions { get; set; }
    bool Enabled { get; set; }
    IEnumerable<ulong>? ExemptRoles { get; set; }
    IEnumerable<ulong>? ExemptChannels { get; set; }
    IDiscordAutoModerationRuleProperties WithName(string name);
    IDiscordAutoModerationRuleProperties WithEventType(NetCord.AutoModerationRuleEventType eventType);
    IDiscordAutoModerationRuleProperties WithTriggerType(NetCord.AutoModerationRuleTriggerType triggerType);
    IDiscordAutoModerationRuleProperties WithTriggerMetadata(IDiscordAutoModerationRuleTriggerMetadataProperties? triggerMetadata);
    IDiscordAutoModerationRuleProperties WithActions(IEnumerable<IDiscordAutoModerationActionProperties> actions);
    IDiscordAutoModerationRuleProperties AddActions(IEnumerable<IDiscordAutoModerationActionProperties> actions);
    IDiscordAutoModerationRuleProperties AddActions(IDiscordAutoModerationActionProperties[] actions);
    IDiscordAutoModerationRuleProperties WithEnabled(bool enabled = true);
    IDiscordAutoModerationRuleProperties WithExemptRoles(IEnumerable<ulong>? exemptRoles);
    IDiscordAutoModerationRuleProperties AddExemptRoles(IEnumerable<ulong> exemptRoles);
    IDiscordAutoModerationRuleProperties AddExemptRoles(ulong[] exemptRoles);
    IDiscordAutoModerationRuleProperties WithExemptChannels(IEnumerable<ulong>? exemptChannels);
    IDiscordAutoModerationRuleProperties AddExemptChannels(IEnumerable<ulong> exemptChannels);
    IDiscordAutoModerationRuleProperties AddExemptChannels(ulong[] exemptChannels);
}


public interface IDiscordAutoModerationRuleOptions  
{
    NetCord.AutoModerationRuleOptions Original { get; }
    string? Name { get; set; }
    NetCord.AutoModerationRuleEventType? EventType { get; set; }
    IDiscordAutoModerationRuleTriggerMetadataProperties? TriggerMetadata { get; set; }
    IEnumerable<IDiscordAutoModerationActionProperties>? Actions { get; set; }
    bool? Enabled { get; set; }
    IEnumerable<ulong>? ExemptRoles { get; set; }
    IEnumerable<ulong>? ExemptChannels { get; set; }
    IDiscordAutoModerationRuleOptions WithName(string name);
    IDiscordAutoModerationRuleOptions WithEventType(NetCord.AutoModerationRuleEventType? eventType);
    IDiscordAutoModerationRuleOptions WithTriggerMetadata(IDiscordAutoModerationRuleTriggerMetadataProperties? triggerMetadata);
    IDiscordAutoModerationRuleOptions WithActions(IEnumerable<IDiscordAutoModerationActionProperties>? actions);
    IDiscordAutoModerationRuleOptions AddActions(IEnumerable<IDiscordAutoModerationActionProperties> actions);
    IDiscordAutoModerationRuleOptions AddActions(IDiscordAutoModerationActionProperties[] actions);
    IDiscordAutoModerationRuleOptions WithEnabled(bool? enabled = true);
    IDiscordAutoModerationRuleOptions WithExemptRoles(IEnumerable<ulong>? exemptRoles);
    IDiscordAutoModerationRuleOptions AddExemptRoles(IEnumerable<ulong> exemptRoles);
    IDiscordAutoModerationRuleOptions AddExemptRoles(ulong[] exemptRoles);
    IDiscordAutoModerationRuleOptions WithExemptChannels(IEnumerable<ulong>? exemptChannels);
    IDiscordAutoModerationRuleOptions AddExemptChannels(IEnumerable<ulong> exemptChannels);
    IDiscordAutoModerationRuleOptions AddExemptChannels(ulong[] exemptChannels);
}


public interface IDiscordGuildEmojiProperties  
{
    NetCord.Rest.GuildEmojiProperties Original { get; }
    string Name { get; set; }
    NetCord.Rest.ImageProperties Image { get; set; }
    IEnumerable<ulong>? AllowedRoles { get; set; }
    IDiscordGuildEmojiProperties WithName(string name);
    IDiscordGuildEmojiProperties WithImage(NetCord.Rest.ImageProperties image);
    IDiscordGuildEmojiProperties WithAllowedRoles(IEnumerable<ulong>? allowedRoles);
    IDiscordGuildEmojiProperties AddAllowedRoles(IEnumerable<ulong> allowedRoles);
    IDiscordGuildEmojiProperties AddAllowedRoles(ulong[] allowedRoles);
}


public interface IDiscordGuildEmojiOptions  
{
    NetCord.Rest.GuildEmojiOptions Original { get; }
    string? Name { get; set; }
    IEnumerable<ulong>? AllowedRoles { get; set; }
    IDiscordGuildEmojiOptions WithName(string name);
    IDiscordGuildEmojiOptions WithAllowedRoles(IEnumerable<ulong>? allowedRoles);
    IDiscordGuildEmojiOptions AddAllowedRoles(IEnumerable<ulong> allowedRoles);
    IDiscordGuildEmojiOptions AddAllowedRoles(ulong[] allowedRoles);
}


public interface IDiscordRestGuild  
{
    NetCord.Rest.RestGuild Original { get; }
    ulong Id { get; }
    string Name { get; }
    bool HasIcon { get; }
    string? IconHash { get; }
    bool HasSplash { get; }
    string? SplashHash { get; }
    bool HasDiscoverySplash { get; }
    string? DiscoverySplashHash { get; }
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
    ImmutableDictionary<ulong, IDiscordRole> Roles { get; set; }
    ImmutableDictionary<ulong, IDiscordGuildEmoji> Emojis { get; set; }
    IReadOnlyList<string> Features { get; }
    NetCord.MfaLevel MfaLevel { get; }
    ulong? ApplicationId { get; }
    ulong? SystemChannelId { get; }
    NetCord.Rest.SystemChannelFlags SystemChannelFlags { get; }
    ulong? RulesChannelId { get; }
    int? MaxPresences { get; }
    int? MaxUsers { get; }
    string? VanityUrlCode { get; }
    string? Description { get; }
    bool HasBanner { get; }
    string? BannerHash { get; }
    int PremiumTier { get; }
    int? PremiumSubscriptionCount { get; }
    string PreferredLocale { get; }
    ulong? PublicUpdatesChannelId { get; }
    int? MaxVideoChannelUsers { get; }
    int? MaxStageVideoChannelUsers { get; }
    int? ApproximateUserCount { get; }
    int? ApproximatePresenceCount { get; }
    IDiscordGuildWelcomeScreen? WelcomeScreen { get; }
    NetCord.NsfwLevel NsfwLevel { get; }
    ImmutableDictionary<ulong, IDiscordGuildSticker> Stickers { get; set; }
    bool PremiumProgressBarEnabled { get; }
    ulong? SafetyAlertsChannelId { get; }
    IDiscordRole? EveryoneRole { get; }
    System.DateTimeOffset CreatedAt { get; }
    int? Compare(IDiscordPartialGuildUser x, IDiscordPartialGuildUser y);
    IDiscordImageUrl? GetIconUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl? GetSplashUrl(NetCord.ImageFormat format);
    IDiscordImageUrl? GetDiscoverySplashUrl(NetCord.ImageFormat format);
    IDiscordImageUrl? GetBannerUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl GetWidgetUrl(NetCord.GuildWidgetStyle? style = default, string? hostname = null, NetCord.ApiVersion? version = default);
    IAsyncEnumerable<IDiscordRestAuditLogEntry>? GetAuditLogAsync(IDiscordGuildAuditLogPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
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
    IAsyncEnumerable<IDiscordGuildUser>? GetUsersAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IReadOnlyList<IDiscordGuildUser>> FindUserAsync(string name, int limit, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildUser?> AddUserAsync(ulong userId, IDiscordGuildUserProperties userProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildUser> ModifyUserAsync(ulong userId, Action<IDiscordGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildUser> ModifyCurrentUserAsync(Action<IDiscordCurrentGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task AddUserRoleAsync(ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task RemoveUserRoleAsync(ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task KickUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordGuildBan>? GetBansAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
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
    Task<int>? GetPruneCountAsync(int days, IEnumerable<ulong>? roles = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
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
    IAsyncEnumerable<IDiscordGuildScheduledEventUser>? GetScheduledEventUsersAsync(ulong scheduledEventId, IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
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
    IAsyncEnumerable<IDiscordGuildUserInfo>? SearchUsersAsync(IDiscordGuildUsersSearchPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
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
    string? IconHash { get; }
    string? SplashHash { get; }
    string? DiscoverySplashHash { get; }
    ImmutableDictionary<ulong, IDiscordGuildEmoji> Emojis { get; }
    IReadOnlyList<string> Features { get; }
    int ApproximateUserCount { get; }
    int ApproximatePresenceCount { get; }
    string? Description { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordGuildOptions  
{
    NetCord.Rest.GuildOptions Original { get; }
    string? Name { get; set; }
    NetCord.VerificationLevel? VerificationLevel { get; set; }
    NetCord.DefaultMessageNotificationLevel? DefaultMessageNotificationLevel { get; set; }
    NetCord.ContentFilter? ContentFilter { get; set; }
    ulong? AfkChannelId { get; set; }
    int? AfkTimeout { get; set; }
    NetCord.Rest.ImageProperties? Icon { get; set; }
    ulong? OwnerId { get; set; }
    NetCord.Rest.ImageProperties? Splash { get; set; }
    NetCord.Rest.ImageProperties? DiscoverySplash { get; set; }
    NetCord.Rest.ImageProperties? Banner { get; set; }
    ulong? SystemChannelId { get; set; }
    NetCord.Rest.SystemChannelFlags? SystemChannelFlags { get; set; }
    ulong? RulesChannelId { get; set; }
    ulong? PublicUpdatesChannelId { get; set; }
    string? PreferredLocale { get; set; }
    IEnumerable<string>? Features { get; set; }
    string? Description { get; set; }
    bool? PremiumProgressBarEnabled { get; set; }
    ulong? SafetyAlertsChannelId { get; set; }
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
    IDiscordGuildOptions WithFeatures(IEnumerable<string>? features);
    IDiscordGuildOptions AddFeatures(IEnumerable<string> features);
    IDiscordGuildOptions AddFeatures(string[] features);
    IDiscordGuildOptions WithDescription(string description);
    IDiscordGuildOptions WithPremiumProgressBarEnabled(bool? premiumProgressBarEnabled = true);
    IDiscordGuildOptions WithSafetyAlertsChannelId(ulong? safetyAlertsChannelId);
}


public interface IDiscordGuildChannelProperties  
{
    NetCord.Rest.GuildChannelProperties Original { get; }
    string Name { get; set; }
    NetCord.ChannelType Type { get; set; }
    string? Topic { get; set; }
    int? Bitrate { get; set; }
    int? UserLimit { get; set; }
    int? Slowmode { get; set; }
    int? Position { get; set; }
    IEnumerable<IDiscordPermissionOverwriteProperties>? PermissionOverwrites { get; set; }
    ulong? ParentId { get; set; }
    bool? Nsfw { get; set; }
    string? RtcRegion { get; set; }
    NetCord.VideoQualityMode? VideoQualityMode { get; set; }
    NetCord.ThreadArchiveDuration? DefaultAutoArchiveDuration { get; set; }
    NetCord.Rest.ForumGuildChannelDefaultReactionProperties? DefaultReactionEmoji { get; set; }
    IEnumerable<IDiscordForumTagProperties>? AvailableTags { get; set; }
    NetCord.SortOrderType? DefaultSortOrder { get; set; }
    NetCord.ForumLayoutType? DefaultForumLayout { get; set; }
    int? DefaultThreadSlowmode { get; set; }
    IDiscordGuildChannelProperties WithName(string name);
    IDiscordGuildChannelProperties WithType(NetCord.ChannelType type);
    IDiscordGuildChannelProperties WithTopic(string topic);
    IDiscordGuildChannelProperties WithBitrate(int? bitrate);
    IDiscordGuildChannelProperties WithUserLimit(int? userLimit);
    IDiscordGuildChannelProperties WithSlowmode(int? slowmode);
    IDiscordGuildChannelProperties WithPosition(int? position);
    IDiscordGuildChannelProperties WithPermissionOverwrites(IEnumerable<IDiscordPermissionOverwriteProperties>? permissionOverwrites);
    IDiscordGuildChannelProperties AddPermissionOverwrites(IEnumerable<IDiscordPermissionOverwriteProperties> permissionOverwrites);
    IDiscordGuildChannelProperties AddPermissionOverwrites(IDiscordPermissionOverwriteProperties[] permissionOverwrites);
    IDiscordGuildChannelProperties WithParentId(ulong? parentId);
    IDiscordGuildChannelProperties WithNsfw(bool? nsfw = true);
    IDiscordGuildChannelProperties WithRtcRegion(string rtcRegion);
    IDiscordGuildChannelProperties WithVideoQualityMode(NetCord.VideoQualityMode? videoQualityMode);
    IDiscordGuildChannelProperties WithDefaultAutoArchiveDuration(NetCord.ThreadArchiveDuration? defaultAutoArchiveDuration);
    IDiscordGuildChannelProperties WithDefaultReactionEmoji(NetCord.Rest.ForumGuildChannelDefaultReactionProperties? defaultReactionEmoji);
    IDiscordGuildChannelProperties WithAvailableTags(IEnumerable<IDiscordForumTagProperties>? availableTags);
    IDiscordGuildChannelProperties AddAvailableTags(IEnumerable<IDiscordForumTagProperties> availableTags);
    IDiscordGuildChannelProperties AddAvailableTags(IDiscordForumTagProperties[] availableTags);
    IDiscordGuildChannelProperties WithDefaultSortOrder(NetCord.SortOrderType? defaultSortOrder);
    IDiscordGuildChannelProperties WithDefaultForumLayout(NetCord.ForumLayoutType? defaultForumLayout);
    IDiscordGuildChannelProperties WithDefaultThreadSlowmode(int? defaultThreadSlowmode);
}


public interface IDiscordGuildChannelPositionProperties  
{
    NetCord.Rest.GuildChannelPositionProperties Original { get; }
    ulong Id { get; set; }
    int? Position { get; set; }
    bool? LockPermissions { get; set; }
    ulong? ParentId { get; set; }
    IDiscordGuildChannelPositionProperties WithId(ulong id);
    IDiscordGuildChannelPositionProperties WithPosition(int? position);
    IDiscordGuildChannelPositionProperties WithLockPermissions(bool? lockPermissions = true);
    IDiscordGuildChannelPositionProperties WithParentId(ulong? parentId);
}


public interface IDiscordPaginationProperties<T>  where T : struct
{
    NetCord.Rest.PaginationProperties<T> Original { get; }
    T? From { get; set; }
    NetCord.Rest.PaginationDirection? Direction { get; set; }
    int? BatchSize { get; set; }
    IDiscordPaginationProperties<T> WithFrom(T? from);
    IDiscordPaginationProperties<T> WithDirection(NetCord.Rest.PaginationDirection? direction);
    IDiscordPaginationProperties<T> WithBatchSize(int? batchSize);
}


public interface IDiscordGuildUserProperties  
{
    NetCord.Rest.GuildUserProperties Original { get; }
    string AccessToken { get; set; }
    string? Nickname { get; set; }
    IEnumerable<ulong>? RolesIds { get; set; }
    bool? Muted { get; set; }
    bool? Deafened { get; set; }
    IDiscordGuildUserProperties WithAccessToken(string accessToken);
    IDiscordGuildUserProperties WithNickname(string nickname);
    IDiscordGuildUserProperties WithRolesIds(IEnumerable<ulong>? rolesIds);
    IDiscordGuildUserProperties AddRolesIds(IEnumerable<ulong> rolesIds);
    IDiscordGuildUserProperties AddRolesIds(ulong[] rolesIds);
    IDiscordGuildUserProperties WithMuted(bool? muted = true);
    IDiscordGuildUserProperties WithDeafened(bool? deafened = true);
}


public interface IDiscordGuildUserOptions  
{
    NetCord.Rest.GuildUserOptions Original { get; }
    IEnumerable<ulong>? RoleIds { get; set; }
    bool? Muted { get; set; }
    bool? Deafened { get; set; }
    ulong? ChannelId { get; set; }
    System.DateTimeOffset? TimeOutUntil { get; set; }
    NetCord.GuildUserFlags? GuildFlags { get; set; }
    string? Nickname { get; set; }
    IDiscordGuildUserOptions WithRoleIds(IEnumerable<ulong>? roleIds);
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
    string? Nickname { get; set; }
    IDiscordCurrentGuildUserOptions WithNickname(string nickname);
}


public interface IDiscordGuildBan  
{
    NetCord.Rest.GuildBan Original { get; }
    string? Reason { get; }
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
    string? Name { get; set; }
    NetCord.Permissions? Permissions { get; set; }
    NetCord.Color? Color { get; set; }
    bool? Hoist { get; set; }
    NetCord.Rest.ImageProperties? Icon { get; set; }
    string? UnicodeIcon { get; set; }
    bool? Mentionable { get; set; }
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
    ulong Id { get; set; }
    int? Position { get; set; }
    IDiscordRolePositionProperties WithId(ulong id);
    IDiscordRolePositionProperties WithPosition(int? position);
}


public interface IDiscordRoleOptions  
{
    NetCord.Rest.RoleOptions Original { get; }
    string? Name { get; set; }
    NetCord.Permissions? Permissions { get; set; }
    NetCord.Color? Color { get; set; }
    bool? Hoist { get; set; }
    NetCord.Rest.ImageProperties? Icon { get; set; }
    string? UnicodeIcon { get; set; }
    bool? Mentionable { get; set; }
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
    int Days { get; set; }
    bool ComputePruneCount { get; set; }
    IEnumerable<ulong>? Roles { get; set; }
    IDiscordGuildPruneProperties WithDays(int days);
    IDiscordGuildPruneProperties WithComputePruneCount(bool computePruneCount = true);
    IDiscordGuildPruneProperties WithRoles(IEnumerable<ulong>? roles);
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
    IDiscordRestGuild? Guild { get; }
    IDiscordChannel? Channel { get; }
    IDiscordUser? Inviter { get; }
    NetCord.InviteTargetType? TargetType { get; }
    IDiscordUser? TargetUser { get; }
    IDiscordApplication? TargetApplication { get; }
    int? ApproximatePresenceCount { get; }
    int? ApproximateUserCount { get; }
    System.DateTimeOffset? ExpiresAt { get; }
    IDiscordStageInstance? StageInstance { get; }
    IDiscordGuildScheduledEvent? GuildScheduledEvent { get; }
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
    IDiscordUser? User { get; }
    IDiscordAccount Account { get; }
    System.DateTimeOffset? SyncedAt { get; }
    int? SubscriberCount { get; }
    bool? Revoked { get; }
    IDiscordIntegrationApplication? Application { get; }
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
    bool Enabled { get; set; }
    ulong? ChannelId { get; set; }
    IDiscordGuildWidgetSettingsOptions WithEnabled(bool enabled = true);
    IDiscordGuildWidgetSettingsOptions WithChannelId(ulong? channelId);
}


public interface IDiscordGuildWidget  
{
    NetCord.Rest.GuildWidget Original { get; }
    ulong Id { get; }
    string Name { get; }
    string? InstantInvite { get; }
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
    bool? Enabled { get; set; }
    IEnumerable<IDiscordGuildWelcomeScreenChannelProperties>? WelcomeChannels { get; set; }
    string? Description { get; set; }
    IDiscordGuildWelcomeScreenOptions WithEnabled(bool? enabled = true);
    IDiscordGuildWelcomeScreenOptions WithWelcomeChannels(IEnumerable<IDiscordGuildWelcomeScreenChannelProperties>? welcomeChannels);
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
    IEnumerable<IDiscordGuildOnboardingPromptProperties>? Prompts { get; set; }
    IEnumerable<ulong>? DefaultChannelIds { get; set; }
    bool? Enabled { get; set; }
    NetCord.Rest.GuildOnboardingMode? Mode { get; set; }
    IDiscordGuildOnboardingOptions WithPrompts(IEnumerable<IDiscordGuildOnboardingPromptProperties>? prompts);
    IDiscordGuildOnboardingOptions AddPrompts(IEnumerable<IDiscordGuildOnboardingPromptProperties> prompts);
    IDiscordGuildOnboardingOptions AddPrompts(IDiscordGuildOnboardingPromptProperties[] prompts);
    IDiscordGuildOnboardingOptions WithDefaultChannelIds(IEnumerable<ulong>? defaultChannelIds);
    IDiscordGuildOnboardingOptions AddDefaultChannelIds(IEnumerable<ulong> defaultChannelIds);
    IDiscordGuildOnboardingOptions AddDefaultChannelIds(ulong[] defaultChannelIds);
    IDiscordGuildOnboardingOptions WithEnabled(bool? enabled = true);
    IDiscordGuildOnboardingOptions WithMode(NetCord.Rest.GuildOnboardingMode? mode);
}


public interface IDiscordGuildScheduledEventProperties  
{
    NetCord.Rest.GuildScheduledEventProperties Original { get; }
    ulong? ChannelId { get; set; }
    IDiscordGuildScheduledEventMetadataProperties? Metadata { get; set; }
    string Name { get; set; }
    NetCord.GuildScheduledEventPrivacyLevel PrivacyLevel { get; set; }
    System.DateTimeOffset ScheduledStartTime { get; set; }
    System.DateTimeOffset? ScheduledEndTime { get; set; }
    string? Description { get; set; }
    NetCord.GuildScheduledEventEntityType EntityType { get; set; }
    NetCord.Rest.ImageProperties? Image { get; set; }
    IDiscordGuildScheduledEventProperties WithChannelId(ulong? channelId);
    IDiscordGuildScheduledEventProperties WithMetadata(IDiscordGuildScheduledEventMetadataProperties? metadata);
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
    ulong? ChannelId { get; set; }
    IDiscordGuildScheduledEventMetadataProperties? Metadata { get; set; }
    string? Name { get; set; }
    NetCord.GuildScheduledEventPrivacyLevel? PrivacyLevel { get; set; }
    System.DateTimeOffset? ScheduledStartTime { get; set; }
    System.DateTimeOffset? ScheduledEndTime { get; set; }
    string? Description { get; set; }
    NetCord.GuildScheduledEventEntityType? EntityType { get; set; }
    NetCord.GuildScheduledEventStatus? Status { get; set; }
    NetCord.Rest.ImageProperties? Image { get; set; }
    IDiscordGuildScheduledEventOptions WithChannelId(ulong? channelId);
    IDiscordGuildScheduledEventOptions WithMetadata(IDiscordGuildScheduledEventMetadataProperties? metadata);
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
    bool WithGuildUsers { get; set; }
    ulong? From { get; set; }
    NetCord.Rest.PaginationDirection? Direction { get; set; }
    int? BatchSize { get; set; }
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
    string Name { get; set; }
    string? Description { get; set; }
    IDiscordGuildTemplateProperties WithName(string name);
    IDiscordGuildTemplateProperties WithDescription(string description);
}


public interface IDiscordGuildTemplateOptions  
{
    NetCord.Rest.GuildTemplateOptions Original { get; }
    string? Name { get; set; }
    string? Description { get; set; }
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
    IReadOnlyDictionary<string, string>? NameLocalizations { get; }
    string Description { get; }
    IReadOnlyDictionary<string, string>? DescriptionLocalizations { get; }
    NetCord.Permissions? DefaultGuildUserPermissions { get; }
    IReadOnlyList<IDiscordApplicationCommandOption> Options { get; }
    bool Nsfw { get; }
    IReadOnlyList<NetCord.ApplicationIntegrationType>? IntegrationTypes { get; }
    IReadOnlyList<NetCord.InteractionContextType>? Contexts { get; }
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
    string Name { get; set; }
    IReadOnlyDictionary<string, string>? NameLocalizations { get; set; }
    NetCord.Permissions? DefaultGuildUserPermissions { get; set; }
    IEnumerable<NetCord.ApplicationIntegrationType>? IntegrationTypes { get; set; }
    IEnumerable<NetCord.InteractionContextType>? Contexts { get; set; }
    bool Nsfw { get; set; }
    IDiscordApplicationCommandProperties WithName(string name);
    IDiscordApplicationCommandProperties WithNameLocalizations(IReadOnlyDictionary<string, string>? nameLocalizations);
    IDiscordApplicationCommandProperties WithDefaultGuildUserPermissions(NetCord.Permissions? defaultGuildUserPermissions);
    IDiscordApplicationCommandProperties WithIntegrationTypes(IEnumerable<NetCord.ApplicationIntegrationType>? integrationTypes);
    IDiscordApplicationCommandProperties AddIntegrationTypes(IEnumerable<NetCord.ApplicationIntegrationType> integrationTypes);
    IDiscordApplicationCommandProperties AddIntegrationTypes(NetCord.ApplicationIntegrationType[] integrationTypes);
    IDiscordApplicationCommandProperties WithContexts(IEnumerable<NetCord.InteractionContextType>? contexts);
    IDiscordApplicationCommandProperties AddContexts(IEnumerable<NetCord.InteractionContextType> contexts);
    IDiscordApplicationCommandProperties AddContexts(NetCord.InteractionContextType[] contexts);
    IDiscordApplicationCommandProperties WithNsfw(bool nsfw = true);
}


public interface IDiscordApplicationCommandOptions  
{
    NetCord.Rest.ApplicationCommandOptions Original { get; }
    string? Name { get; set; }
    IReadOnlyDictionary<string, string>? NameLocalizations { get; set; }
    string? Description { get; set; }
    IReadOnlyDictionary<string, string>? DescriptionLocalizations { get; set; }
    IEnumerable<IDiscordApplicationCommandOptionProperties>? Options { get; set; }
    NetCord.Permissions? DefaultGuildUserPermissions { get; set; }
    IEnumerable<NetCord.ApplicationIntegrationType>? IntegrationTypes { get; set; }
    IEnumerable<NetCord.InteractionContextType>? Contexts { get; set; }
    bool? Nsfw { get; set; }
    IDiscordApplicationCommandOptions WithName(string name);
    IDiscordApplicationCommandOptions WithNameLocalizations(IReadOnlyDictionary<string, string>? nameLocalizations);
    IDiscordApplicationCommandOptions WithDescription(string description);
    IDiscordApplicationCommandOptions WithDescriptionLocalizations(IReadOnlyDictionary<string, string>? descriptionLocalizations);
    IDiscordApplicationCommandOptions WithOptions(IEnumerable<IDiscordApplicationCommandOptionProperties>? options);
    IDiscordApplicationCommandOptions AddOptions(IEnumerable<IDiscordApplicationCommandOptionProperties> options);
    IDiscordApplicationCommandOptions AddOptions(IDiscordApplicationCommandOptionProperties[] options);
    IDiscordApplicationCommandOptions WithDefaultGuildUserPermissions(NetCord.Permissions? defaultGuildUserPermissions);
    IDiscordApplicationCommandOptions WithIntegrationTypes(IEnumerable<NetCord.ApplicationIntegrationType>? integrationTypes);
    IDiscordApplicationCommandOptions AddIntegrationTypes(IEnumerable<NetCord.ApplicationIntegrationType> integrationTypes);
    IDiscordApplicationCommandOptions AddIntegrationTypes(NetCord.ApplicationIntegrationType[] integrationTypes);
    IDiscordApplicationCommandOptions WithContexts(IEnumerable<NetCord.InteractionContextType>? contexts);
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
    ulong Id { get; set; }
    NetCord.ApplicationCommandGuildPermissionType Type { get; set; }
    bool Permission { get; set; }
    IDiscordApplicationCommandGuildPermissionProperties WithId(ulong id);
    IDiscordApplicationCommandGuildPermissionProperties WithType(NetCord.ApplicationCommandGuildPermissionType type);
    IDiscordApplicationCommandGuildPermissionProperties WithPermission(bool permission = true);
}


public interface IDiscordGuildStickerProperties  
{
    NetCord.Rest.GuildStickerProperties Original { get; }
    IDiscordAttachmentProperties Attachment { get; set; }
    NetCord.StickerFormat Format { get; set; }
    IEnumerable<string> Tags { get; set; }
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
    string? Name { get; set; }
    string? Description { get; set; }
    string? Tags { get; set; }
    IDiscordGuildStickerOptions WithName(string name);
    IDiscordGuildStickerOptions WithDescription(string description);
    IDiscordGuildStickerOptions WithTags(string tags);
}


public interface IDiscordGuildUserInfo  
{
    NetCord.Rest.GuildUserInfo Original { get; }
    IDiscordGuildUser User { get; }
    string? SourceInviteCode { get; }
    NetCord.Rest.GuildUserJoinSourceType JoinSourceType { get; }
    ulong? InviterId { get; }
}


public interface IDiscordGuildUsersSearchPaginationProperties  
{
    NetCord.Rest.GuildUsersSearchPaginationProperties Original { get; }
    IEnumerable<IDiscordGuildUsersSearchQuery>? OrQuery { get; set; }
    IEnumerable<IDiscordGuildUsersSearchQuery>? AndQuery { get; set; }
    NetCord.Rest.GuildUsersSearchTimestamp? From { get; set; }
    NetCord.Rest.PaginationDirection? Direction { get; set; }
    int? BatchSize { get; set; }
    IDiscordGuildUsersSearchPaginationProperties WithOrQuery(IEnumerable<IDiscordGuildUsersSearchQuery>? orQuery);
    IDiscordGuildUsersSearchPaginationProperties AddOrQuery(IEnumerable<IDiscordGuildUsersSearchQuery> orQuery);
    IDiscordGuildUsersSearchPaginationProperties AddOrQuery(IDiscordGuildUsersSearchQuery[] orQuery);
    IDiscordGuildUsersSearchPaginationProperties WithAndQuery(IEnumerable<IDiscordGuildUsersSearchQuery>? andQuery);
    IDiscordGuildUsersSearchPaginationProperties AddAndQuery(IEnumerable<IDiscordGuildUsersSearchQuery> andQuery);
    IDiscordGuildUsersSearchPaginationProperties AddAndQuery(IDiscordGuildUsersSearchQuery[] andQuery);
    IDiscordGuildUsersSearchPaginationProperties WithFrom(NetCord.Rest.GuildUsersSearchTimestamp? from);
    IDiscordGuildUsersSearchPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction);
    IDiscordGuildUsersSearchPaginationProperties WithBatchSize(int? batchSize);
}


public interface IDiscordCurrentUserVoiceStateOptions  
{
    NetCord.Rest.CurrentUserVoiceStateOptions Original { get; }
    ulong? ChannelId { get; set; }
    bool? Suppress { get; set; }
    System.DateTimeOffset? RequestToSpeakTimestamp { get; set; }
    IDiscordCurrentUserVoiceStateOptions WithChannelId(ulong? channelId);
    IDiscordCurrentUserVoiceStateOptions WithSuppress(bool? suppress = true);
    IDiscordCurrentUserVoiceStateOptions WithRequestToSpeakTimestamp(System.DateTimeOffset? requestToSpeakTimestamp);
}


public interface IDiscordVoiceStateOptions  
{
    NetCord.Rest.VoiceStateOptions Original { get; }
    ulong ChannelId { get; }
    bool? Suppress { get; set; }
    IDiscordVoiceStateOptions WithSuppress(bool? suppress = true);
}


public interface IDiscordWebhook  
{
    NetCord.Rest.Webhook Original { get; }
    ulong Id { get; }
    NetCord.Rest.WebhookType Type { get; }
    ulong? GuildId { get; }
    ulong? ChannelId { get; }
    IDiscordUser? Creator { get; }
    string? Name { get; }
    string? AvatarHash { get; }
    ulong? ApplicationId { get; }
    IDiscordRestGuild? Guild { get; }
    IDiscordChannel? Channel { get; }
    string? Url { get; }
    System.DateTimeOffset CreatedAt { get; }
    Task<IDiscordWebhook> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordWebhook> ModifyAsync(Action<IDiscordWebhookOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordMessageProperties  
{
    NetCord.Rest.MessageProperties Original { get; }
    string? Content { get; set; }
    IDiscordNonceProperties? Nonce { get; set; }
    bool Tts { get; set; }
    IEnumerable<IDiscordAttachmentProperties>? Attachments { get; set; }
    IEnumerable<IDiscordEmbedProperties>? Embeds { get; set; }
    IDiscordAllowedMentionsProperties? AllowedMentions { get; set; }
    IDiscordMessageReferenceProperties? MessageReference { get; set; }
    IEnumerable<IDiscordComponentProperties>? Components { get; set; }
    IEnumerable<ulong>? StickerIds { get; set; }
    NetCord.MessageFlags? Flags { get; set; }
    IDiscordMessagePollProperties? Poll { get; set; }
    HttpContent Serialize();
    IDiscordMessageProperties WithContent(string content);
    IDiscordMessageProperties WithNonce(IDiscordNonceProperties? nonce);
    IDiscordMessageProperties WithTts(bool tts = true);
    IDiscordMessageProperties WithAttachments(IEnumerable<IDiscordAttachmentProperties>? attachments);
    IDiscordMessageProperties AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments);
    IDiscordMessageProperties AddAttachments(IDiscordAttachmentProperties[] attachments);
    IDiscordMessageProperties WithEmbeds(IEnumerable<IDiscordEmbedProperties>? embeds);
    IDiscordMessageProperties AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds);
    IDiscordMessageProperties AddEmbeds(IDiscordEmbedProperties[] embeds);
    IDiscordMessageProperties WithAllowedMentions(IDiscordAllowedMentionsProperties? allowedMentions);
    IDiscordMessageProperties WithMessageReference(IDiscordMessageReferenceProperties? messageReference);
    IDiscordMessageProperties WithComponents(IEnumerable<IDiscordComponentProperties>? components);
    IDiscordMessageProperties AddComponents(IEnumerable<IDiscordComponentProperties> components);
    IDiscordMessageProperties AddComponents(IDiscordComponentProperties[] components);
    IDiscordMessageProperties WithStickerIds(IEnumerable<ulong>? stickerIds);
    IDiscordMessageProperties AddStickerIds(IEnumerable<ulong> stickerIds);
    IDiscordMessageProperties AddStickerIds(ulong[] stickerIds);
    IDiscordMessageProperties WithFlags(NetCord.MessageFlags? flags);
    IDiscordMessageProperties WithPoll(IDiscordMessagePollProperties? poll);
}


public interface IDiscordReactionEmojiProperties  
{
    NetCord.Rest.ReactionEmojiProperties Original { get; }
    string Name { get; set; }
    ulong? Id { get; set; }
    IDiscordReactionEmojiProperties WithName(string name);
    IDiscordReactionEmojiProperties WithId(ulong? id);
}


public interface IDiscordMessageReactionsPaginationProperties  
{
    NetCord.Rest.MessageReactionsPaginationProperties Original { get; }
    NetCord.ReactionType? Type { get; set; }
    ulong? From { get; set; }
    NetCord.Rest.PaginationDirection? Direction { get; set; }
    int? BatchSize { get; set; }
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
    string FileName { get; set; }
    long FileSize { get; set; }
    long? Id { get; set; }
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
    IAsyncEnumerable<IDiscordRestMessage>? GetMessagesAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
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
    IAsyncEnumerable<IDiscordUser>? GetMessagePollAnswerVotersAsync(ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
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
    string? Title { get; }
    string? Description { get; }
    string? ContentType { get; }
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
    string? Title { get; }
    NetCord.EmbedType? Type { get; }
    string? Description { get; }
    string? Url { get; }
    System.DateTimeOffset? Timestamp { get; }
    NetCord.Color? Color { get; }
    IDiscordEmbedFooter? Footer { get; }
    IDiscordEmbedImage? Image { get; }
    IDiscordEmbedThumbnail? Thumbnail { get; }
    IDiscordEmbedVideo? Video { get; }
    IDiscordEmbedProvider? Provider { get; }
    IDiscordEmbedAuthor? Author { get; }
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
    string? PartyId { get; }
}


public interface IDiscordApplication  
{
    NetCord.Application Original { get; }
    ulong Id { get; }
    string Name { get; }
    string? IconHash { get; }
    string Description { get; }
    IReadOnlyList<string> RpcOrigins { get; }
    bool? BotPublic { get; }
    bool? BotRequireCodeGrant { get; }
    IDiscordUser? Bot { get; }
    string? TermsOfServiceUrl { get; }
    string? PrivacyPolicyUrl { get; }
    IDiscordUser? Owner { get; }
    string VerifyKey { get; }
    IDiscordTeam? Team { get; }
    ulong? GuildId { get; }
    IDiscordRestGuild? Guild { get; }
    ulong? PrimarySkuId { get; }
    string? Slug { get; }
    string? CoverImageHash { get; }
    NetCord.ApplicationFlags? Flags { get; }
    int? ApproximateGuildCount { get; }
    int? ApproximateUserInstallCount { get; }
    IReadOnlyList<string>? RedirectUris { get; }
    string? InteractionsEndpointUrl { get; }
    string? RoleConnectionsVerificationUrl { get; }
    IReadOnlyList<string>? Tags { get; }
    IDiscordApplicationInstallParams? InstallParams { get; }
    IReadOnlyDictionary<NetCord.ApplicationIntegrationType, IDiscordApplicationIntegrationTypeConfiguration>? IntegrationTypesConfiguration { get; }
    string? CustomInstallUrl { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordImageUrl? GetIconUrl(NetCord.ImageFormat format);
    IDiscordImageUrl? GetCoverUrl(NetCord.ImageFormat format);
    IDiscordImageUrl? GetAssetUrl(ulong assetId, NetCord.ImageFormat format);
    IDiscordImageUrl? GetAchievementIconUrl(ulong achievementId, string iconHash, NetCord.ImageFormat format);
    IDiscordImageUrl? GetStorePageAssetUrl(ulong assetId, NetCord.ImageFormat format);
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
    IDiscordMessageInteractionMetadata? TriggeringInteractionMetadata { get; }
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
    IReadOnlyDictionary<ulong, IDiscordUser>? Users { get; }
    IReadOnlyDictionary<ulong, IDiscordRole>? Roles { get; }
    IReadOnlyDictionary<ulong, IDiscordChannel>? Channels { get; }
    IReadOnlyDictionary<ulong, IDiscordAttachment>? Attachments { get; }
}


public interface IDiscordMessagePoll  
{
    NetCord.MessagePoll Original { get; }
    IDiscordMessagePollMedia Question { get; }
    IReadOnlyList<IDiscordMessagePollAnswer> Answers { get; }
    System.DateTimeOffset? ExpiresAt { get; }
    bool AllowMultiselect { get; }
    NetCord.MessagePollLayoutType LayoutType { get; }
    IDiscordMessagePollResults? Results { get; }
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
    string? Content { get; set; }
    IDiscordNonceProperties? Nonce { get; set; }
    bool Tts { get; set; }
    IEnumerable<IDiscordAttachmentProperties>? Attachments { get; set; }
    IEnumerable<IDiscordEmbedProperties>? Embeds { get; set; }
    IDiscordAllowedMentionsProperties? AllowedMentions { get; set; }
    bool? FailIfNotExists { get; set; }
    IEnumerable<IDiscordComponentProperties>? Components { get; set; }
    IEnumerable<ulong>? StickerIds { get; set; }
    NetCord.MessageFlags? Flags { get; set; }
    IDiscordMessagePollProperties? Poll { get; set; }
    IDiscordMessageProperties ToMessageProperties(ulong messageReferenceId);
    IDiscordReplyMessageProperties WithContent(string content);
    IDiscordReplyMessageProperties WithNonce(IDiscordNonceProperties? nonce);
    IDiscordReplyMessageProperties WithTts(bool tts = true);
    IDiscordReplyMessageProperties WithAttachments(IEnumerable<IDiscordAttachmentProperties>? attachments);
    IDiscordReplyMessageProperties AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments);
    IDiscordReplyMessageProperties AddAttachments(IDiscordAttachmentProperties[] attachments);
    IDiscordReplyMessageProperties WithEmbeds(IEnumerable<IDiscordEmbedProperties>? embeds);
    IDiscordReplyMessageProperties AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds);
    IDiscordReplyMessageProperties AddEmbeds(IDiscordEmbedProperties[] embeds);
    IDiscordReplyMessageProperties WithAllowedMentions(IDiscordAllowedMentionsProperties? allowedMentions);
    IDiscordReplyMessageProperties WithFailIfNotExists(bool? failIfNotExists = true);
    IDiscordReplyMessageProperties WithComponents(IEnumerable<IDiscordComponentProperties>? components);
    IDiscordReplyMessageProperties AddComponents(IEnumerable<IDiscordComponentProperties> components);
    IDiscordReplyMessageProperties AddComponents(IDiscordComponentProperties[] components);
    IDiscordReplyMessageProperties WithStickerIds(IEnumerable<ulong>? stickerIds);
    IDiscordReplyMessageProperties AddStickerIds(IEnumerable<ulong> stickerIds);
    IDiscordReplyMessageProperties AddStickerIds(ulong[] stickerIds);
    IDiscordReplyMessageProperties WithFlags(NetCord.MessageFlags? flags);
    IDiscordReplyMessageProperties WithPoll(IDiscordMessagePollProperties? poll);
}


public interface IDiscordGuildThreadFromMessageProperties  
{
    NetCord.Rest.GuildThreadFromMessageProperties Original { get; }
    string Name { get; set; }
    NetCord.ThreadArchiveDuration? AutoArchiveDuration { get; set; }
    int? Slowmode { get; set; }
    IDiscordGuildThreadFromMessageProperties WithName(string name);
    IDiscordGuildThreadFromMessageProperties WithAutoArchiveDuration(NetCord.ThreadArchiveDuration? autoArchiveDuration);
    IDiscordGuildThreadFromMessageProperties WithSlowmode(int? slowmode);
}


public interface IDiscordEmbedProperties  
{
    NetCord.Rest.EmbedProperties Original { get; }
    string? Title { get; set; }
    string? Description { get; set; }
    string? Url { get; set; }
    System.DateTimeOffset? Timestamp { get; set; }
    NetCord.Color Color { get; set; }
    IDiscordEmbedFooterProperties? Footer { get; set; }
    IDiscordEmbedImageProperties? Image { get; set; }
    IDiscordEmbedThumbnailProperties? Thumbnail { get; set; }
    IDiscordEmbedAuthorProperties? Author { get; set; }
    IEnumerable<IDiscordEmbedFieldProperties>? Fields { get; set; }
    IDiscordEmbedProperties WithTitle(string title);
    IDiscordEmbedProperties WithDescription(string description);
    IDiscordEmbedProperties WithUrl(string url);
    IDiscordEmbedProperties WithTimestamp(System.DateTimeOffset? timestamp);
    IDiscordEmbedProperties WithColor(NetCord.Color color);
    IDiscordEmbedProperties WithFooter(IDiscordEmbedFooterProperties? footer);
    IDiscordEmbedProperties WithImage(IDiscordEmbedImageProperties? image);
    IDiscordEmbedProperties WithThumbnail(IDiscordEmbedThumbnailProperties? thumbnail);
    IDiscordEmbedProperties WithAuthor(IDiscordEmbedAuthorProperties? author);
    IDiscordEmbedProperties WithFields(IEnumerable<IDiscordEmbedFieldProperties>? fields);
    IDiscordEmbedProperties AddFields(IEnumerable<IDiscordEmbedFieldProperties> fields);
    IDiscordEmbedProperties AddFields(IDiscordEmbedFieldProperties[] fields);
}


public interface IDiscordAllowedMentionsProperties  
{
    NetCord.Rest.AllowedMentionsProperties Original { get; }
    bool Everyone { get; set; }
    IEnumerable<ulong>? AllowedRoles { get; set; }
    IEnumerable<ulong>? AllowedUsers { get; set; }
    bool ReplyMention { get; set; }
    static IDiscordAllowedMentionsProperties All => new DiscordAllowedMentionsProperties(NetCord.Rest.AllowedMentionsProperties.All);
    static IDiscordAllowedMentionsProperties None => new DiscordAllowedMentionsProperties(NetCord.Rest.AllowedMentionsProperties.None);
    IDiscordAllowedMentionsProperties WithEveryone(bool everyone = true);
    IDiscordAllowedMentionsProperties WithAllowedRoles(IEnumerable<ulong>? allowedRoles);
    IDiscordAllowedMentionsProperties AddAllowedRoles(IEnumerable<ulong> allowedRoles);
    IDiscordAllowedMentionsProperties AddAllowedRoles(ulong[] allowedRoles);
    IDiscordAllowedMentionsProperties WithAllowedUsers(IEnumerable<ulong>? allowedUsers);
    IDiscordAllowedMentionsProperties AddAllowedUsers(IEnumerable<ulong> allowedUsers);
    IDiscordAllowedMentionsProperties AddAllowedUsers(ulong[] allowedUsers);
    IDiscordAllowedMentionsProperties WithReplyMention(bool replyMention = true);
}


public interface IDiscordComponentProperties  
{
    NetCord.Rest.IComponentProperties Original { get; }
    int? Id { get; set; }
    NetCord.ComponentType ComponentType { get; }
    IDiscordComponentProperties WithId(int? id);
}


public interface IDiscordAttachmentProperties  
{
    NetCord.Rest.AttachmentProperties Original { get; }
    string FileName { get; set; }
    string? Title { get; set; }
    string? Description { get; set; }
    bool SupportsHttpSerialization { get; }
    HttpContent Serialize();
    IDiscordAttachmentProperties WithFileName(string fileName);
    IDiscordAttachmentProperties WithTitle(string title);
    IDiscordAttachmentProperties WithDescription(string description);
}


public interface IDiscordMessagePollProperties  
{
    NetCord.MessagePollProperties Original { get; }
    IDiscordMessagePollMediaProperties Question { get; set; }
    IEnumerable<IDiscordMessagePollAnswerProperties> Answers { get; set; }
    int? DurationInHours { get; set; }
    bool AllowMultiselect { get; set; }
    NetCord.MessagePollLayoutType? LayoutType { get; set; }
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
    string? Name { get; set; }
    NetCord.ChannelType? ChannelType { get; set; }
    int? Position { get; set; }
    string? Topic { get; set; }
    bool? Nsfw { get; set; }
    int? Slowmode { get; set; }
    int? Bitrate { get; set; }
    int? UserLimit { get; set; }
    IEnumerable<IDiscordPermissionOverwriteProperties>? PermissionOverwrites { get; set; }
    ulong? ParentId { get; set; }
    string? RtcRegion { get; set; }
    NetCord.VideoQualityMode? VideoQualityMode { get; set; }
    NetCord.ThreadArchiveDuration? DefaultAutoArchiveDuration { get; set; }
    IEnumerable<IDiscordForumTagProperties>? AvailableTags { get; set; }
    NetCord.Rest.ForumGuildChannelDefaultReactionProperties? DefaultReactionEmoji { get; set; }
    int? DefaultThreadSlowmode { get; set; }
    NetCord.ChannelFlags? Flags { get; set; }
    NetCord.SortOrderType? DefaultSortOrder { get; set; }
    NetCord.ForumLayoutType? DefaultForumLayout { get; set; }
    bool? Archived { get; set; }
    NetCord.ThreadArchiveDuration? AutoArchiveDuration { get; set; }
    bool? Locked { get; set; }
    bool? Invitable { get; set; }
    IEnumerable<ulong>? AppliedTags { get; set; }
    IDiscordGuildChannelOptions WithName(string name);
    IDiscordGuildChannelOptions WithChannelType(NetCord.ChannelType? channelType);
    IDiscordGuildChannelOptions WithPosition(int? position);
    IDiscordGuildChannelOptions WithTopic(string topic);
    IDiscordGuildChannelOptions WithNsfw(bool? nsfw = true);
    IDiscordGuildChannelOptions WithSlowmode(int? slowmode);
    IDiscordGuildChannelOptions WithBitrate(int? bitrate);
    IDiscordGuildChannelOptions WithUserLimit(int? userLimit);
    IDiscordGuildChannelOptions WithPermissionOverwrites(IEnumerable<IDiscordPermissionOverwriteProperties>? permissionOverwrites);
    IDiscordGuildChannelOptions AddPermissionOverwrites(IEnumerable<IDiscordPermissionOverwriteProperties> permissionOverwrites);
    IDiscordGuildChannelOptions AddPermissionOverwrites(IDiscordPermissionOverwriteProperties[] permissionOverwrites);
    IDiscordGuildChannelOptions WithParentId(ulong? parentId);
    IDiscordGuildChannelOptions WithRtcRegion(string rtcRegion);
    IDiscordGuildChannelOptions WithVideoQualityMode(NetCord.VideoQualityMode? videoQualityMode);
    IDiscordGuildChannelOptions WithDefaultAutoArchiveDuration(NetCord.ThreadArchiveDuration? defaultAutoArchiveDuration);
    IDiscordGuildChannelOptions WithAvailableTags(IEnumerable<IDiscordForumTagProperties>? availableTags);
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
    IDiscordGuildChannelOptions WithAppliedTags(IEnumerable<ulong>? appliedTags);
    IDiscordGuildChannelOptions AddAppliedTags(IEnumerable<ulong> appliedTags);
    IDiscordGuildChannelOptions AddAppliedTags(ulong[] appliedTags);
}


public interface IDiscordPermissionOverwriteProperties  
{
    NetCord.Rest.PermissionOverwriteProperties Original { get; }
    ulong Id { get; set; }
    NetCord.PermissionOverwriteType Type { get; set; }
    NetCord.Permissions? Allowed { get; set; }
    NetCord.Permissions? Denied { get; set; }
    IDiscordPermissionOverwriteProperties WithId(ulong id);
    IDiscordPermissionOverwriteProperties WithType(NetCord.PermissionOverwriteType type);
    IDiscordPermissionOverwriteProperties WithAllowed(NetCord.Permissions? allowed);
    IDiscordPermissionOverwriteProperties WithDenied(NetCord.Permissions? denied);
}


public interface IDiscordInviteProperties  
{
    NetCord.Rest.InviteProperties Original { get; }
    int? MaxAge { get; set; }
    int? MaxUses { get; set; }
    bool? Temporary { get; set; }
    bool? Unique { get; set; }
    NetCord.InviteTargetType? TargetType { get; set; }
    ulong? TargetUserId { get; set; }
    ulong? TargetApplicationId { get; set; }
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
    NetCord.ChannelType? ChannelType { get; set; }
    bool? Invitable { get; set; }
    string Name { get; set; }
    NetCord.ThreadArchiveDuration? AutoArchiveDuration { get; set; }
    int? Slowmode { get; set; }
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
    IDiscordUser? Creator { get; }
    string? Name { get; }
    string? AvatarHash { get; }
    ulong? ApplicationId { get; }
    IDiscordRestGuild? Guild { get; }
    IDiscordChannel? Channel { get; }
    string? Url { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordWebhookClient ToClient(IDiscordWebhookClientConfiguration? configuration = null);
    Task<IDiscordIncomingWebhook> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordIncomingWebhook> GetWithTokenAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordIncomingWebhook> ModifyAsync(Action<IDiscordWebhookOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordIncomingWebhook> ModifyWithTokenAsync(Action<IDiscordWebhookOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteWithTokenAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage?> ExecuteAsync(IDiscordWebhookMessageProperties message, bool wait = false, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessageAsync(ulong messageId, ulong? threadId = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordWebhookProperties  
{
    NetCord.Rest.WebhookProperties Original { get; }
    string Name { get; set; }
    NetCord.Rest.ImageProperties? Avatar { get; set; }
    IDiscordWebhookProperties WithName(string name);
    IDiscordWebhookProperties WithAvatar(NetCord.Rest.ImageProperties? avatar);
}


public interface IDiscordUserActivity  
{
    NetCord.Gateway.UserActivity Original { get; }
    string Name { get; }
    NetCord.Gateway.UserActivityType Type { get; }
    string? Url { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordUserActivityTimestamps? Timestamps { get; }
    ulong? ApplicationId { get; }
    string? Details { get; }
    string? State { get; }
    IDiscordEmoji? Emoji { get; }
    IDiscordParty? Party { get; }
    IDiscordUserActivityAssets? Assets { get; }
    IDiscordUserActivitySecrets? Secrets { get; }
    bool? Instance { get; }
    NetCord.Gateway.UserActivityFlags? Flags { get; }
    IReadOnlyList<IDiscordUserActivityButton> Buttons { get; }
    ulong GuildId { get; }
}


public interface IDiscordStageInstanceOptions  
{
    NetCord.Rest.StageInstanceOptions Original { get; }
    string? Topic { get; set; }
    NetCord.StageInstancePrivacyLevel? PrivacyLevel { get; set; }
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
    IReadOnlyList<NetCord.GuildScheduledEventRecurrenceRuleWeekday>? ByWeekday { get; }
    IReadOnlyList<IDiscordGuildScheduledEventRecurrenceRuleNWeekday>? ByNWeekday { get; }
    IReadOnlyList<NetCord.GuildScheduledEventRecurrenceRuleMonth>? ByMonth { get; }
    IReadOnlyList<int>? ByMonthDay { get; }
    IReadOnlyList<int>? ByYearDay { get; }
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
    string? EmojiName { get; }
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
    IDiscordAuditLogChange<TValueParam> WithValues<TValueParam>(JsonTypeInfo<TValueParam> jsonTypeInfo);
    IDiscordAuditLogChange<TValueParam> WithValues<TValueParam>();
}


public interface IDiscordAuditLogEntryInfo  
{
    NetCord.AuditLogEntryInfo Original { get; }
    ulong? ApplicationId { get; }
    string? AutoModerationRuleName { get; }
    NetCord.AutoModerationRuleTriggerType? AutoModerationRuleTriggerType { get; }
    ulong? ChannelId { get; }
    int? Count { get; }
    int? DeleteGuildUserDays { get; }
    ulong? Id { get; }
    int? GuildUsersRemoved { get; }
    ulong? MessageId { get; }
    string? RoleName { get; }
    NetCord.PermissionOverwriteType? Type { get; }
    NetCord.IntegrationType? IntegrationType { get; }
}


public interface IDiscordAuditLogChange<TValue>  
{
    NetCord.AuditLogChange<TValue> Original { get; }
    TValue? NewValue { get; }
    TValue? OldValue { get; }
    string Key { get; }
    bool HasNewValue { get; }
    bool HasOldValue { get; }
    IDiscordAuditLogChange<TValueParam> WithValues<TValueParam>(JsonTypeInfo<TValueParam> jsonTypeInfo);
    IDiscordAuditLogChange<TValueParam> WithValues<TValueParam>();
}


public interface IDiscordAutoModerationRuleTriggerMetadata  
{
    NetCord.AutoModerationRuleTriggerMetadata Original { get; }
    IReadOnlyList<string>? KeywordFilter { get; }
    IReadOnlyList<string>? RegexPatterns { get; }
    IReadOnlyList<NetCord.AutoModerationRuleKeywordPresetType>? Presets { get; }
    IReadOnlyList<string>? AllowList { get; }
    int? MentionTotalLimit { get; }
    bool MentionRaidProtectionEnabled { get; }
}


public interface IDiscordAutoModerationAction  
{
    NetCord.AutoModerationAction Original { get; }
    NetCord.AutoModerationActionType Type { get; }
    IDiscordAutoModerationActionMetadata? Metadata { get; }
}


public interface IDiscordAutoModerationRuleTriggerMetadataProperties  
{
    NetCord.AutoModerationRuleTriggerMetadataProperties Original { get; }
    IEnumerable<string>? KeywordFilter { get; set; }
    IEnumerable<string>? RegexPatterns { get; set; }
    IEnumerable<NetCord.AutoModerationRuleKeywordPresetType>? Presets { get; set; }
    IEnumerable<string>? AllowList { get; set; }
    int? MentionTotalLimit { get; set; }
    bool MentionRaidProtectionEnabled { get; set; }
    IDiscordAutoModerationRuleTriggerMetadataProperties WithKeywordFilter(IEnumerable<string>? keywordFilter);
    IDiscordAutoModerationRuleTriggerMetadataProperties AddKeywordFilter(IEnumerable<string> keywordFilter);
    IDiscordAutoModerationRuleTriggerMetadataProperties AddKeywordFilter(string[] keywordFilter);
    IDiscordAutoModerationRuleTriggerMetadataProperties WithRegexPatterns(IEnumerable<string>? regexPatterns);
    IDiscordAutoModerationRuleTriggerMetadataProperties AddRegexPatterns(IEnumerable<string> regexPatterns);
    IDiscordAutoModerationRuleTriggerMetadataProperties AddRegexPatterns(string[] regexPatterns);
    IDiscordAutoModerationRuleTriggerMetadataProperties WithPresets(IEnumerable<NetCord.AutoModerationRuleKeywordPresetType>? presets);
    IDiscordAutoModerationRuleTriggerMetadataProperties AddPresets(IEnumerable<NetCord.AutoModerationRuleKeywordPresetType> presets);
    IDiscordAutoModerationRuleTriggerMetadataProperties AddPresets(NetCord.AutoModerationRuleKeywordPresetType[] presets);
    IDiscordAutoModerationRuleTriggerMetadataProperties WithAllowList(IEnumerable<string>? allowList);
    IDiscordAutoModerationRuleTriggerMetadataProperties AddAllowList(IEnumerable<string> allowList);
    IDiscordAutoModerationRuleTriggerMetadataProperties AddAllowList(string[] allowList);
    IDiscordAutoModerationRuleTriggerMetadataProperties WithMentionTotalLimit(int? mentionTotalLimit);
    IDiscordAutoModerationRuleTriggerMetadataProperties WithMentionRaidProtectionEnabled(bool mentionRaidProtectionEnabled = true);
}


public interface IDiscordAutoModerationActionProperties  
{
    NetCord.AutoModerationActionProperties Original { get; }
    NetCord.AutoModerationActionType Type { get; set; }
    IDiscordAutoModerationActionMetadataProperties? Metadata { get; set; }
    IDiscordAutoModerationActionProperties WithType(NetCord.AutoModerationActionType type);
    IDiscordAutoModerationActionProperties WithMetadata(IDiscordAutoModerationActionMetadataProperties? metadata);
}


public interface IDiscordForumTagProperties  
{
    NetCord.Rest.ForumTagProperties Original { get; }
    ulong? Id { get; set; }
    string Name { get; set; }
    bool? Moderated { get; set; }
    ulong? EmojiId { get; set; }
    string? EmojiName { get; set; }
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
    string? IconHash { get; }
    string Description { get; }
    string Summary { get; }
    IDiscordUser? Bot { get; }
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
    ulong ChannelId { get; set; }
    string Description { get; set; }
    IDiscordEmojiProperties? Emoji { get; set; }
    IDiscordGuildWelcomeScreenChannelProperties WithChannelId(ulong channelId);
    IDiscordGuildWelcomeScreenChannelProperties WithDescription(string description);
    IDiscordGuildWelcomeScreenChannelProperties WithEmoji(IDiscordEmojiProperties? emoji);
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
    ulong? Id { get; set; }
    NetCord.Rest.GuildOnboardingPromptType Type { get; set; }
    IEnumerable<IDiscordGuildOnboardingPromptOptionProperties> Options { get; set; }
    string Title { get; set; }
    bool SingleSelect { get; set; }
    bool Required { get; set; }
    bool InOnboarding { get; set; }
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
    string Location { get; set; }
    IDiscordGuildScheduledEventMetadataProperties WithLocation(string location);
}


public interface IDiscordGuildTemplatePreview  
{
    NetCord.Rest.GuildTemplatePreview Original { get; }
    string Name { get; }
    string? IconHash { get; }
    string? Description { get; }
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
    string Name { get; set; }
    NetCord.Rest.ImageProperties? Icon { get; set; }
    IDiscordGuildFromGuildTemplateProperties WithName(string name);
    IDiscordGuildFromGuildTemplateProperties WithIcon(NetCord.Rest.ImageProperties? icon);
}


public interface IDiscordApplicationCommandOption  
{
    NetCord.Rest.ApplicationCommandOption Original { get; }
    NetCord.ApplicationCommandOptionType Type { get; }
    string Name { get; }
    IReadOnlyDictionary<string, string>? NameLocalizations { get; }
    string Description { get; }
    IReadOnlyDictionary<string, string>? DescriptionLocalizations { get; }
    bool Required { get; }
    IReadOnlyList<IDiscordApplicationCommandOptionChoice>? Choices { get; }
    IReadOnlyList<IDiscordApplicationCommandOption>? Options { get; }
    IReadOnlyList<NetCord.ChannelType>? ChannelTypes { get; }
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
    IReadOnlyDictionary<string, string>? NameLocalizations { get; }
    string Description { get; }
    IReadOnlyDictionary<string, string>? DescriptionLocalizations { get; }
    NetCord.Permissions? DefaultGuildUserPermissions { get; }
    IReadOnlyList<IDiscordApplicationCommandOption> Options { get; }
    bool Nsfw { get; }
    IReadOnlyList<NetCord.ApplicationIntegrationType>? IntegrationTypes { get; }
    IReadOnlyList<NetCord.InteractionContextType>? Contexts { get; }
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
    NetCord.ApplicationCommandOptionType Type { get; set; }
    string Name { get; set; }
    IReadOnlyDictionary<string, string>? NameLocalizations { get; set; }
    string Description { get; set; }
    IReadOnlyDictionary<string, string>? DescriptionLocalizations { get; set; }
    bool? Required { get; set; }
    IEnumerable<IDiscordApplicationCommandOptionChoiceProperties>? Choices { get; set; }
    IEnumerable<IDiscordApplicationCommandOptionProperties>? Options { get; set; }
    IEnumerable<NetCord.ChannelType>? ChannelTypes { get; set; }
    double? MinValue { get; set; }
    double? MaxValue { get; set; }
    int? MinLength { get; set; }
    int? MaxLength { get; set; }
    bool? Autocomplete { get; set; }
    IDiscordApplicationCommandOptionProperties WithType(NetCord.ApplicationCommandOptionType type);
    IDiscordApplicationCommandOptionProperties WithName(string name);
    IDiscordApplicationCommandOptionProperties WithNameLocalizations(IReadOnlyDictionary<string, string>? nameLocalizations);
    IDiscordApplicationCommandOptionProperties WithDescription(string description);
    IDiscordApplicationCommandOptionProperties WithDescriptionLocalizations(IReadOnlyDictionary<string, string>? descriptionLocalizations);
    IDiscordApplicationCommandOptionProperties WithRequired(bool? required = true);
    IDiscordApplicationCommandOptionProperties WithChoices(IEnumerable<IDiscordApplicationCommandOptionChoiceProperties>? choices);
    IDiscordApplicationCommandOptionProperties AddChoices(IEnumerable<IDiscordApplicationCommandOptionChoiceProperties> choices);
    IDiscordApplicationCommandOptionProperties AddChoices(IDiscordApplicationCommandOptionChoiceProperties[] choices);
    IDiscordApplicationCommandOptionProperties WithOptions(IEnumerable<IDiscordApplicationCommandOptionProperties>? options);
    IDiscordApplicationCommandOptionProperties AddOptions(IEnumerable<IDiscordApplicationCommandOptionProperties> options);
    IDiscordApplicationCommandOptionProperties AddOptions(IDiscordApplicationCommandOptionProperties[] options);
    IDiscordApplicationCommandOptionProperties WithChannelTypes(IEnumerable<NetCord.ChannelType>? channelTypes);
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
    void Serialize(Utf8JsonWriter writer);
}


public interface IDiscordWebhookOptions  
{
    NetCord.Rest.WebhookOptions Original { get; }
    string? Name { get; set; }
    NetCord.Rest.ImageProperties? Avatar { get; set; }
    ulong? ChannelId { get; set; }
    IDiscordWebhookOptions WithName(string name);
    IDiscordWebhookOptions WithAvatar(NetCord.Rest.ImageProperties? avatar);
    IDiscordWebhookOptions WithChannelId(ulong? channelId);
}


public interface IDiscordNonceProperties  
{
    NetCord.Rest.NonceProperties Original { get; }
    bool Unique { get; set; }
    IDiscordNonceProperties WithUnique(bool unique = true);
}


public interface IDiscordMessageReferenceProperties  
{
    NetCord.Rest.MessageReferenceProperties Original { get; }
    NetCord.MessageReferenceType Type { get; set; }
    ulong? ChannelId { get; set; }
    ulong MessageId { get; set; }
    bool FailIfNotExists { get; set; }
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
    string? IconUrl { get; }
    string? ProxyIconUrl { get; }
}


public interface IDiscordEmbedImage  
{
    NetCord.EmbedImage Original { get; }
    string? Url { get; }
    string? ProxyUrl { get; }
    int? Height { get; }
    int? Width { get; }
}


public interface IDiscordEmbedThumbnail  
{
    NetCord.EmbedThumbnail Original { get; }
    string? Url { get; }
    string? ProxyUrl { get; }
    int? Height { get; }
    int? Width { get; }
}


public interface IDiscordEmbedVideo  
{
    NetCord.EmbedVideo Original { get; }
    string? Url { get; }
    string? ProxyUrl { get; }
    int? Height { get; }
    int? Width { get; }
}


public interface IDiscordEmbedProvider  
{
    NetCord.EmbedProvider Original { get; }
    string? Name { get; }
    string? Url { get; }
}


public interface IDiscordEmbedAuthor  
{
    NetCord.EmbedAuthor Original { get; }
    string? Name { get; }
    string? Url { get; }
    string? IconUrl { get; }
    string? ProxyIconUrl { get; }
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
    string? Name { get; }
    bool Animated { get; }
}


public interface IDiscordTeam  
{
    NetCord.Team Original { get; }
    ulong Id { get; }
    string? IconHash { get; }
    IReadOnlyList<IDiscordTeamUser> Users { get; }
    string Name { get; }
    ulong OwnerId { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordImageUrl? GetIconUrl(NetCord.ImageFormat format);
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
    IDiscordApplicationInstallParams? OAuth2InstallParams { get; }
}


public interface IDiscordApplicationEmoji  
{
    NetCord.ApplicationEmoji Original { get; }
    ulong ApplicationId { get; }
    ulong Id { get; }
    IDiscordUser? Creator { get; }
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
    string Name { get; set; }
    NetCord.Rest.ImageProperties Image { get; set; }
    IDiscordApplicationEmojiProperties WithName(string name);
    IDiscordApplicationEmojiProperties WithImage(NetCord.Rest.ImageProperties image);
}


public interface IDiscordApplicationEmojiOptions  
{
    NetCord.Rest.ApplicationEmojiOptions Original { get; }
    string? Name { get; set; }
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
    string? Text { get; }
    IDiscordEmojiReference? Emoji { get; }
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
    string? Text { get; set; }
    string? IconUrl { get; set; }
    IDiscordEmbedFooterProperties WithText(string text);
    IDiscordEmbedFooterProperties WithIconUrl(string iconUrl);
}


public interface IDiscordEmbedImageProperties  
{
    NetCord.Rest.EmbedImageProperties Original { get; }
    string? Url { get; set; }
    IDiscordEmbedImageProperties WithUrl(string url);
}


public interface IDiscordEmbedThumbnailProperties  
{
    NetCord.Rest.EmbedThumbnailProperties Original { get; }
    string? Url { get; set; }
    IDiscordEmbedThumbnailProperties WithUrl(string url);
}


public interface IDiscordEmbedAuthorProperties  
{
    NetCord.Rest.EmbedAuthorProperties Original { get; }
    string? Name { get; set; }
    string? Url { get; set; }
    string? IconUrl { get; set; }
    IDiscordEmbedAuthorProperties WithName(string name);
    IDiscordEmbedAuthorProperties WithUrl(string url);
    IDiscordEmbedAuthorProperties WithIconUrl(string iconUrl);
}


public interface IDiscordEmbedFieldProperties  
{
    NetCord.Rest.EmbedFieldProperties Original { get; }
    string? Name { get; set; }
    string? Value { get; set; }
    bool Inline { get; set; }
    IDiscordEmbedFieldProperties WithName(string name);
    IDiscordEmbedFieldProperties WithValue(string value);
    IDiscordEmbedFieldProperties WithInline(bool inline = true);
}


public interface IDiscordMessagePollMediaProperties  
{
    NetCord.MessagePollMediaProperties Original { get; }
    string? Text { get; set; }
    IDiscordEmojiProperties? Emoji { get; set; }
    IDiscordMessagePollMediaProperties WithText(string text);
    IDiscordMessagePollMediaProperties WithEmoji(IDiscordEmojiProperties? emoji);
}


public interface IDiscordMessagePollAnswerProperties  
{
    NetCord.MessagePollAnswerProperties Original { get; }
    IDiscordMessagePollMediaProperties PollMedia { get; set; }
    IDiscordMessagePollAnswerProperties WithPollMedia(IDiscordMessagePollMediaProperties pollMedia);
}


public interface IDiscordWebhookClient  
{
    NetCord.Rest.WebhookClient Original { get; }
    ulong Id { get; }
    string Token { get; }
    void Dispose();
    Task<IDiscordWebhook> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordWebhook> ModifyAsync(Action<IDiscordWebhookOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage?> ExecuteAsync(IDiscordWebhookMessageProperties message, bool wait = false, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteMessageAsync(ulong messageId, ulong? threadId = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordWebhookClientConfiguration  
{
    NetCord.Rest.WebhookClientConfiguration Original { get; }
    IDiscordRestClient? Client { get; set; }
}


public interface IDiscordWebhookMessageProperties  
{
    NetCord.Rest.WebhookMessageProperties Original { get; }
    string? Content { get; set; }
    string? Username { get; set; }
    string? AvatarUrl { get; set; }
    bool Tts { get; set; }
    IEnumerable<IDiscordEmbedProperties>? Embeds { get; set; }
    IDiscordAllowedMentionsProperties? AllowedMentions { get; set; }
    IEnumerable<IDiscordComponentProperties>? Components { get; set; }
    IEnumerable<IDiscordAttachmentProperties>? Attachments { get; set; }
    NetCord.MessageFlags? Flags { get; set; }
    string? ThreadName { get; set; }
    IEnumerable<ulong>? AppliedTags { get; set; }
    IDiscordMessagePollProperties? Poll { get; set; }
    HttpContent Serialize();
    IDiscordWebhookMessageProperties WithContent(string content);
    IDiscordWebhookMessageProperties WithUsername(string username);
    IDiscordWebhookMessageProperties WithAvatarUrl(string avatarUrl);
    IDiscordWebhookMessageProperties WithTts(bool tts = true);
    IDiscordWebhookMessageProperties WithEmbeds(IEnumerable<IDiscordEmbedProperties>? embeds);
    IDiscordWebhookMessageProperties AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds);
    IDiscordWebhookMessageProperties AddEmbeds(IDiscordEmbedProperties[] embeds);
    IDiscordWebhookMessageProperties WithAllowedMentions(IDiscordAllowedMentionsProperties? allowedMentions);
    IDiscordWebhookMessageProperties WithComponents(IEnumerable<IDiscordComponentProperties>? components);
    IDiscordWebhookMessageProperties AddComponents(IEnumerable<IDiscordComponentProperties> components);
    IDiscordWebhookMessageProperties AddComponents(IDiscordComponentProperties[] components);
    IDiscordWebhookMessageProperties WithAttachments(IEnumerable<IDiscordAttachmentProperties>? attachments);
    IDiscordWebhookMessageProperties AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments);
    IDiscordWebhookMessageProperties AddAttachments(IDiscordAttachmentProperties[] attachments);
    IDiscordWebhookMessageProperties WithFlags(NetCord.MessageFlags? flags);
    IDiscordWebhookMessageProperties WithThreadName(string threadName);
    IDiscordWebhookMessageProperties WithAppliedTags(IEnumerable<ulong>? appliedTags);
    IDiscordWebhookMessageProperties AddAppliedTags(IEnumerable<ulong> appliedTags);
    IDiscordWebhookMessageProperties AddAppliedTags(ulong[] appliedTags);
    IDiscordWebhookMessageProperties WithPoll(IDiscordMessagePollProperties? poll);
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
    string? Id { get; }
    int? CurrentSize { get; }
    int? MaxSize { get; }
}


public interface IDiscordUserActivityAssets  
{
    NetCord.Gateway.UserActivityAssets Original { get; }
    string? LargeImageId { get; }
    string? LargeText { get; }
    string? SmallImageId { get; }
    string? SmallText { get; }
}


public interface IDiscordUserActivitySecrets  
{
    NetCord.Gateway.UserActivitySecrets Original { get; }
    string? Join { get; }
    string? Spectate { get; }
    string? Match { get; }
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
    string? CustomMessage { get; }
}


public interface IDiscordAutoModerationActionMetadataProperties  
{
    NetCord.AutoModerationActionMetadataProperties Original { get; }
    ulong? ChannelId { get; set; }
    int? DurationSeconds { get; set; }
    string? CustomMessage { get; set; }
    IDiscordAutoModerationActionMetadataProperties WithChannelId(ulong? channelId);
    IDiscordAutoModerationActionMetadataProperties WithDurationSeconds(int? durationSeconds);
    IDiscordAutoModerationActionMetadataProperties WithCustomMessage(string customMessage);
}


public interface IDiscordEmojiProperties  
{
    NetCord.EmojiProperties Original { get; }
    ulong? Id { get; set; }
    string? Unicode { get; set; }
    IDiscordEmojiProperties WithId(ulong? id);
    IDiscordEmojiProperties WithUnicode(string unicode);
}


public interface IDiscordGuildOnboardingPromptOption  
{
    NetCord.Rest.GuildOnboardingPromptOption Original { get; }
    ulong Id { get; }
    IReadOnlyList<ulong> ChannelIds { get; }
    IReadOnlyList<ulong> RoleIds { get; }
    IDiscordEmoji? Emoji { get; }
    string Title { get; }
    string? Description { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordGuildOnboardingPromptOptionProperties  
{
    NetCord.Rest.GuildOnboardingPromptOptionProperties Original { get; }
    ulong? Id { get; set; }
    IEnumerable<ulong>? ChannelIds { get; set; }
    IEnumerable<ulong>? RoleIds { get; set; }
    ulong? EmojiId { get; set; }
    string? EmojiName { get; set; }
    bool? EmojiAnimated { get; set; }
    string Title { get; set; }
    string? Description { get; set; }
    IDiscordGuildOnboardingPromptOptionProperties WithId(ulong? id);
    IDiscordGuildOnboardingPromptOptionProperties WithChannelIds(IEnumerable<ulong>? channelIds);
    IDiscordGuildOnboardingPromptOptionProperties AddChannelIds(IEnumerable<ulong> channelIds);
    IDiscordGuildOnboardingPromptOptionProperties AddChannelIds(ulong[] channelIds);
    IDiscordGuildOnboardingPromptOptionProperties WithRoleIds(IEnumerable<ulong>? roleIds);
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
    IReadOnlyDictionary<string, string>? NameLocalizations { get; }
    string? ValueString { get; }
    double? ValueNumeric { get; }
    NetCord.Rest.ApplicationCommandOptionChoiceValueType ValueType { get; }
}


public interface IDiscordApplicationCommandOptionChoiceProperties  
{
    NetCord.Rest.ApplicationCommandOptionChoiceProperties Original { get; }
    string Name { get; set; }
    IReadOnlyDictionary<string, string>? NameLocalizations { get; set; }
    string? StringValue { get; set; }
    double? NumericValue { get; set; }
    NetCord.Rest.ApplicationCommandOptionChoiceValueType ValueType { get; set; }
    IDiscordApplicationCommandOptionChoiceProperties WithName(string name);
    IDiscordApplicationCommandOptionChoiceProperties WithNameLocalizations(IReadOnlyDictionary<string, string>? nameLocalizations);
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
    string? GlobalName { get; }
    string? AvatarHash { get; }
    bool IsBot { get; }
    bool? IsSystemUser { get; }
    bool? MfaEnabled { get; }
    string? BannerHash { get; }
    NetCord.Color? AccentColor { get; }
    string? Locale { get; }
    bool? Verified { get; }
    string? Email { get; }
    NetCord.UserFlags? Flags { get; }
    NetCord.PremiumType? PremiumType { get; }
    NetCord.UserFlags? PublicFlags { get; }
    IDiscordAvatarDecorationData? AvatarDecorationData { get; }
    bool HasAvatar { get; }
    bool HasBanner { get; }
    bool HasAvatarDecoration { get; }
    IDiscordImageUrl DefaultAvatarUrl { get; }
    System.DateTimeOffset CreatedAt { get; }
    IDiscordImageUrl? GetAvatarUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl? GetBannerUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl? GetAvatarDecorationUrl();
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
    IDiscordToken? Token { get; }
    Task<IDiscordCurrentApplication> GetCurrentApplicationAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordCurrentApplication> ModifyCurrentApplicationAsync(Action<IDiscordCurrentApplicationOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordApplicationRoleConnectionMetadata>> GetApplicationRoleConnectionMetadataRecordsAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordApplicationRoleConnectionMetadata>> UpdateApplicationRoleConnectionMetadataRecordsAsync(ulong applicationId, IEnumerable<IDiscordApplicationRoleConnectionMetadataProperties> applicationRoleConnectionMetadataProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordRestAuditLogEntry>? GetGuildAuditLogAsync(ulong guildId, IDiscordGuildAuditLogPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IReadOnlyList<IDiscordAutoModerationRule>> GetAutoModerationRulesAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordAutoModerationRule> GetAutoModerationRuleAsync(ulong guildId, ulong autoModerationRuleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordAutoModerationRule> CreateAutoModerationRuleAsync(ulong guildId, IDiscordAutoModerationRuleProperties autoModerationRuleProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordAutoModerationRule> ModifyAutoModerationRuleAsync(ulong guildId, ulong autoModerationRuleId, Action<IDiscordAutoModerationRuleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteAutoModerationRuleAsync(ulong guildId, ulong autoModerationRuleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordChannel> GetChannelAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordChannel> ModifyGroupDMChannelAsync(ulong channelId, Action<IDiscordGroupDMChannelOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordChannel> ModifyGuildChannelAsync(ulong channelId, Action<IDiscordGuildChannelOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordChannel> DeleteChannelAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordRestMessage>? GetMessagesAsync(ulong channelId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
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
    Task<IDiscordRestInvite>? CreateGuildChannelInviteAsync(ulong channelId, IDiscordInviteProperties? inviteProperties = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
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
    IAsyncEnumerable<IDiscordThreadUser>? GetGuildThreadUsersAsync(ulong threadId, IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    IAsyncEnumerable<IDiscordGuildThread>? GetPublicArchivedGuildThreadsAsync(ulong channelId, IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    IAsyncEnumerable<IDiscordGuildThread>? GetPrivateArchivedGuildThreadsAsync(ulong channelId, IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    IAsyncEnumerable<IDiscordGuildThread>? GetJoinedPrivateArchivedGuildThreadsAsync(ulong channelId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<Stream> SendRequestAsync(HttpMethod method, FormattableString route, string? query = null, NetCord.Rest.TopLevelResourceInfo? resourceInfo = default, IDiscordRestRequestProperties? properties = null, bool global = true, System.Threading.CancellationToken cancellationToken = default);
    Task<Stream> SendRequestAsync(HttpMethod method, HttpContent content, FormattableString route, string? query = null, NetCord.Rest.TopLevelResourceInfo? resourceInfo = default, IDiscordRestRequestProperties? properties = null, bool global = true, System.Threading.CancellationToken cancellationToken = default);
    void Dispose();
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
    IAsyncEnumerable<IDiscordGuildUser>? GetGuildUsersAsync(ulong guildId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IReadOnlyList<IDiscordGuildUser>> FindGuildUserAsync(ulong guildId, string name, int limit, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildUser?> AddGuildUserAsync(ulong guildId, ulong userId, IDiscordGuildUserProperties userProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildUser> ModifyGuildUserAsync(ulong guildId, ulong userId, Action<IDiscordGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildUser> ModifyCurrentGuildUserAsync(ulong guildId, Action<IDiscordCurrentGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task AddGuildUserRoleAsync(ulong guildId, ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task RemoveGuildUserRoleAsync(ulong guildId, ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task KickGuildUserAsync(ulong guildId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordGuildBan>? GetGuildBansAsync(ulong guildId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
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
    Task<int>? GetGuildPruneCountAsync(ulong guildId, int days, IEnumerable<ulong>? roles = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
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
    IAsyncEnumerable<IDiscordGuildScheduledEventUser>? GetGuildScheduledEventUsersAsync(ulong guildId, ulong scheduledEventId, IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
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
    IAsyncEnumerable<IDiscordEntitlement>? GetEntitlementsAsync(ulong applicationId, IDiscordEntitlementsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordEntitlement> GetEntitlementAsync(ulong applicationId, ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task ConsumeEntitlementAsync(ulong applicationId, ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordEntitlement> CreateTestEntitlementAsync(ulong applicationId, IDiscordTestEntitlementProperties testEntitlementProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteTestEntitlementAsync(ulong applicationId, ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordSku>> GetSkusAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordCurrentApplication> GetCurrentBotApplicationInformationAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordAuthorizationInformation> GetCurrentAuthorizationInformationAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordUser>? GetMessagePollAnswerVotersAsync(ulong channelId, ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
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
    IAsyncEnumerable<IDiscordSubscription>? GetSkuSubscriptionsAsync(ulong skuId, IDiscordSubscriptionPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordSubscription> GetSkuSubscriptionAsync(ulong skuId, ulong subscriptionId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplication> GetApplicationAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(ulong channelId, IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordGuildUserInfo>? SearchGuildUsersAsync(ulong guildId, IDiscordGuildUsersSearchPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordCurrentUser> GetCurrentUserAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordUser> GetUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordCurrentUser> ModifyCurrentUserAsync(Action<IDiscordCurrentUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordRestGuild>? GetCurrentUserGuildsAsync(IDiscordGuildsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
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
    Task<IDiscordRestMessage?> ExecuteWebhookAsync(ulong webhookId, string webhookToken, IDiscordWebhookMessageProperties message, bool wait = false, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
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
    string? IconHash { get; }
    string Description { get; }
    IReadOnlyList<string> RpcOrigins { get; }
    bool? BotPublic { get; }
    bool? BotRequireCodeGrant { get; }
    IDiscordUser? Bot { get; }
    string? TermsOfServiceUrl { get; }
    string? PrivacyPolicyUrl { get; }
    IDiscordUser? Owner { get; }
    string VerifyKey { get; }
    IDiscordTeam? Team { get; }
    ulong? GuildId { get; }
    IDiscordRestGuild? Guild { get; }
    ulong? PrimarySkuId { get; }
    string? Slug { get; }
    string? CoverImageHash { get; }
    NetCord.ApplicationFlags? Flags { get; }
    int? ApproximateGuildCount { get; }
    int? ApproximateUserInstallCount { get; }
    IReadOnlyList<string>? RedirectUris { get; }
    string? InteractionsEndpointUrl { get; }
    string? RoleConnectionsVerificationUrl { get; }
    IReadOnlyList<string>? Tags { get; }
    IDiscordApplicationInstallParams? InstallParams { get; }
    IReadOnlyDictionary<NetCord.ApplicationIntegrationType, IDiscordApplicationIntegrationTypeConfiguration>? IntegrationTypesConfiguration { get; }
    string? CustomInstallUrl { get; }
    System.DateTimeOffset CreatedAt { get; }
    Task<IDiscordApplication> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordCurrentApplication> ModifyAsync(Action<IDiscordCurrentApplicationOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordApplicationRoleConnectionMetadata>> GetRoleConnectionMetadataRecordsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordApplicationRoleConnectionMetadata>> UpdateRoleConnectionMetadataRecordsAsync(IEnumerable<IDiscordApplicationRoleConnectionMetadataProperties> applicationRoleConnectionMetadataProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordEntitlement>? GetEntitlementsAsync(IDiscordEntitlementsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordEntitlement> GetEntitlementAsync(ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task ConsumeEntitlementAsync(ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordEntitlement> CreateTestEntitlementAsync(IDiscordTestEntitlementProperties testEntitlementProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteTestEntitlementAsync(ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordSku>> GetSkusAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IDiscordImageUrl? GetIconUrl(NetCord.ImageFormat format);
    IDiscordImageUrl? GetCoverUrl(NetCord.ImageFormat format);
    IDiscordImageUrl? GetAssetUrl(ulong assetId, NetCord.ImageFormat format);
    IDiscordImageUrl? GetAchievementIconUrl(ulong achievementId, string iconHash, NetCord.ImageFormat format);
    IDiscordImageUrl? GetStorePageAssetUrl(ulong assetId, NetCord.ImageFormat format);
    Task<IReadOnlyList<IDiscordApplicationEmoji>> GetEmojisAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationEmoji> GetEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationEmoji> CreateEmojiAsync(IDiscordApplicationEmojiProperties applicationEmojiProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationEmoji> ModifyEmojiAsync(ulong emojiId, Action<IDiscordApplicationEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeleteEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordCurrentApplicationOptions  
{
    NetCord.Rest.CurrentApplicationOptions Original { get; }
    string? CustomInstallUrl { get; set; }
    string? Description { get; set; }
    string? RoleConnectionsVerificationUrl { get; set; }
    IDiscordApplicationInstallParamsProperties? InstallParams { get; set; }
    IReadOnlyDictionary<NetCord.ApplicationIntegrationType, IDiscordApplicationIntegrationTypeConfigurationProperties>? IntegrationTypesConfiguration { get; set; }
    NetCord.ApplicationFlags? Flags { get; set; }
    NetCord.Rest.ImageProperties? Icon { get; set; }
    NetCord.Rest.ImageProperties? CoverImage { get; set; }
    string? InteractionsEndpointUrl { get; set; }
    IEnumerable<string>? Tags { get; set; }
    IDiscordCurrentApplicationOptions WithCustomInstallUrl(string customInstallUrl);
    IDiscordCurrentApplicationOptions WithDescription(string description);
    IDiscordCurrentApplicationOptions WithRoleConnectionsVerificationUrl(string roleConnectionsVerificationUrl);
    IDiscordCurrentApplicationOptions WithInstallParams(IDiscordApplicationInstallParamsProperties? installParams);
    IDiscordCurrentApplicationOptions WithIntegrationTypesConfiguration(IReadOnlyDictionary<NetCord.ApplicationIntegrationType, IDiscordApplicationIntegrationTypeConfigurationProperties>? integrationTypesConfiguration);
    IDiscordCurrentApplicationOptions WithFlags(NetCord.ApplicationFlags? flags);
    IDiscordCurrentApplicationOptions WithIcon(NetCord.Rest.ImageProperties? icon);
    IDiscordCurrentApplicationOptions WithCoverImage(NetCord.Rest.ImageProperties? coverImage);
    IDiscordCurrentApplicationOptions WithInteractionsEndpointUrl(string interactionsEndpointUrl);
    IDiscordCurrentApplicationOptions WithTags(IEnumerable<string>? tags);
    IDiscordCurrentApplicationOptions AddTags(IEnumerable<string> tags);
    IDiscordCurrentApplicationOptions AddTags(string[] tags);
}


public interface IDiscordApplicationRoleConnectionMetadata  
{
    NetCord.Rest.ApplicationRoleConnectionMetadata Original { get; }
    NetCord.Rest.ApplicationRoleConnectionMetadataType Type { get; }
    string Key { get; }
    string Name { get; }
    IReadOnlyDictionary<string, string>? NameLocalizations { get; }
    string Description { get; }
    IReadOnlyDictionary<string, string>? DescriptionLocalizations { get; }
}


public interface IDiscordApplicationRoleConnectionMetadataProperties  
{
    NetCord.Rest.ApplicationRoleConnectionMetadataProperties Original { get; }
    NetCord.Rest.ApplicationRoleConnectionMetadataType Type { get; set; }
    string Key { get; set; }
    string Name { get; set; }
    IReadOnlyDictionary<string, string>? NameLocalizations { get; set; }
    string Description { get; set; }
    IReadOnlyDictionary<string, string>? DescriptionLocalizations { get; set; }
    IDiscordApplicationRoleConnectionMetadataProperties WithType(NetCord.Rest.ApplicationRoleConnectionMetadataType type);
    IDiscordApplicationRoleConnectionMetadataProperties WithKey(string key);
    IDiscordApplicationRoleConnectionMetadataProperties WithName(string name);
    IDiscordApplicationRoleConnectionMetadataProperties WithNameLocalizations(IReadOnlyDictionary<string, string>? nameLocalizations);
    IDiscordApplicationRoleConnectionMetadataProperties WithDescription(string description);
    IDiscordApplicationRoleConnectionMetadataProperties WithDescriptionLocalizations(IReadOnlyDictionary<string, string>? descriptionLocalizations);
}


public interface IDiscordGroupDMChannelOptions  
{
    NetCord.Rest.GroupDMChannelOptions Original { get; }
    string? Name { get; set; }
    NetCord.Rest.ImageProperties? Icon { get; set; }
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
    string AccessToken { get; set; }
    string? Nickname { get; set; }
    IDiscordGroupDMChannelUserAddProperties WithAccessToken(string accessToken);
    IDiscordGroupDMChannelUserAddProperties WithNickname(string nickname);
}


public interface IDiscordForumGuildThread  
{
    NetCord.ForumGuildThread Original { get; }
    IDiscordRestMessage Message { get; }
    IReadOnlyList<ulong>? AppliedTags { get; }
    ulong OwnerId { get; }
    int MessageCount { get; }
    int UserCount { get; }
    IDiscordGuildThreadMetadata Metadata { get; }
    IDiscordThreadCurrentUser? CurrentUser { get; }
    int TotalMessageSent { get; }
    ulong GuildId { get; }
    int? Position { get; }
    IReadOnlyDictionary<ulong, IDiscordPermissionOverwrite> PermissionOverwrites { get; }
    string Name { get; }
    string? Topic { get; }
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
    IAsyncEnumerable<IDiscordThreadUser>? GetUsersAsync(IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task ModifyPermissionsAsync(IDiscordPermissionOverwriteProperties permissionOverwrite, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IEnumerable<IDiscordRestInvite>> GetInvitesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordRestInvite>? CreateInviteAsync(IDiscordInviteProperties? inviteProperties = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task DeletePermissionAsync(ulong overwriteId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildThread> CreateGuildThreadAsync(ulong messageId, IDiscordGuildThreadFromMessageProperties threadFromMessageProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordGuildThread> CreateGuildThreadAsync(IDiscordGuildThreadProperties threadProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordGuildThread>? GetPublicArchivedGuildThreadsAsync(IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    IAsyncEnumerable<IDiscordGuildThread>? GetPrivateArchivedGuildThreadsAsync(IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    IAsyncEnumerable<IDiscordGuildThread>? GetJoinedPrivateArchivedGuildThreadsAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordIncomingWebhook> CreateWebhookAsync(IDiscordWebhookProperties webhookProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordWebhook>> GetChannelWebhooksAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordRestMessage>? GetMessagesAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
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
    IAsyncEnumerable<IDiscordUser>? GetMessagePollAnswerVotersAsync(ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordRestMessage> EndMessagePollAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordForumGuildThreadProperties  
{
    NetCord.Rest.ForumGuildThreadProperties Original { get; }
    IDiscordForumGuildThreadMessageProperties Message { get; set; }
    IEnumerable<ulong>? AppliedTags { get; set; }
    string Name { get; set; }
    NetCord.ThreadArchiveDuration? AutoArchiveDuration { get; set; }
    int? Slowmode { get; set; }
    HttpContent Serialize();
    IDiscordForumGuildThreadProperties WithMessage(IDiscordForumGuildThreadMessageProperties message);
    IDiscordForumGuildThreadProperties WithAppliedTags(IEnumerable<ulong>? appliedTags);
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
    string Name { get; set; }
    NetCord.Rest.ImageProperties? Icon { get; set; }
    NetCord.VerificationLevel? VerificationLevel { get; set; }
    NetCord.DefaultMessageNotificationLevel? DefaultMessageNotificationLevel { get; set; }
    NetCord.ContentFilter? ContentFilter { get; set; }
    IEnumerable<IDiscordRoleProperties>? Roles { get; set; }
    IEnumerable<IDiscordGuildChannelProperties>? Channels { get; set; }
    ulong? AfkChannelId { get; set; }
    int? AfkTimeout { get; set; }
    ulong? SystemChannelId { get; set; }
    NetCord.Rest.SystemChannelFlags? SystemChannelFlags { get; set; }
    IDiscordGuildProperties WithName(string name);
    IDiscordGuildProperties WithIcon(NetCord.Rest.ImageProperties? icon);
    IDiscordGuildProperties WithVerificationLevel(NetCord.VerificationLevel? verificationLevel);
    IDiscordGuildProperties WithDefaultMessageNotificationLevel(NetCord.DefaultMessageNotificationLevel? defaultMessageNotificationLevel);
    IDiscordGuildProperties WithContentFilter(NetCord.ContentFilter? contentFilter);
    IDiscordGuildProperties WithRoles(IEnumerable<IDiscordRoleProperties>? roles);
    IDiscordGuildProperties AddRoles(IEnumerable<IDiscordRoleProperties> roles);
    IDiscordGuildProperties AddRoles(IDiscordRoleProperties[] roles);
    IDiscordGuildProperties WithChannels(IEnumerable<IDiscordGuildChannelProperties>? channels);
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
    ulong? UserId { get; set; }
    IEnumerable<ulong>? SkuIds { get; set; }
    ulong? GuildId { get; set; }
    bool? ExcludeEnded { get; set; }
    ulong? From { get; set; }
    NetCord.Rest.PaginationDirection? Direction { get; set; }
    int? BatchSize { get; set; }
    IDiscordEntitlementsPaginationProperties WithUserId(ulong? userId);
    IDiscordEntitlementsPaginationProperties WithSkuIds(IEnumerable<ulong>? skuIds);
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
    ulong SkuId { get; set; }
    ulong OwnerId { get; set; }
    NetCord.Rest.TestEntitlementOwnerType OwnerType { get; set; }
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
    IAsyncEnumerable<IDiscordSubscription>? GetSubscriptionsAsync(IDiscordSubscriptionPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordSubscription> GetSubscriptionAsync(ulong subscriptionId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordAuthorizationInformation  
{
    NetCord.Rest.AuthorizationInformation Original { get; }
    IDiscordApplication Application { get; }
    IReadOnlyList<string> Scopes { get; }
    System.DateTimeOffset ExpiresAt { get; }
    IDiscordUser? User { get; }
}


public interface IDiscordStageInstanceProperties  
{
    NetCord.Rest.StageInstanceProperties Original { get; }
    ulong ChannelId { get; set; }
    string Topic { get; set; }
    NetCord.StageInstancePrivacyLevel? PrivacyLevel { get; set; }
    bool? SendStartNotification { get; set; }
    ulong? GuildScheduledEventId { get; set; }
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
    IReadOnlyList<ulong>? RenewalSkuIds { get; }
    System.DateTimeOffset CurrentPeriodStart { get; }
    System.DateTimeOffset CurrentPeriodEnd { get; }
    NetCord.SubscriptionStatus Status { get; }
    System.DateTimeOffset? CanceledAt { get; }
    string? Country { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordSubscriptionPaginationProperties  
{
    NetCord.Rest.SubscriptionPaginationProperties Original { get; }
    ulong? UserId { get; set; }
    ulong? From { get; set; }
    NetCord.Rest.PaginationDirection? Direction { get; set; }
    int? BatchSize { get; set; }
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
    string? GlobalName { get; }
    string? AvatarHash { get; }
    bool IsBot { get; }
    bool? IsSystemUser { get; }
    bool? MfaEnabled { get; }
    string? BannerHash { get; }
    NetCord.Color? AccentColor { get; }
    string? Locale { get; }
    bool? Verified { get; }
    string? Email { get; }
    NetCord.UserFlags? Flags { get; }
    NetCord.PremiumType? PremiumType { get; }
    NetCord.UserFlags? PublicFlags { get; }
    IDiscordAvatarDecorationData? AvatarDecorationData { get; }
    bool HasAvatar { get; }
    bool HasBanner { get; }
    bool HasAvatarDecoration { get; }
    IDiscordImageUrl DefaultAvatarUrl { get; }
    System.DateTimeOffset CreatedAt { get; }
    Task<IDiscordCurrentUser> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordCurrentUser> ModifyAsync(Action<IDiscordCurrentUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IAsyncEnumerable<IDiscordRestGuild>? GetGuildsAsync(IDiscordGuildsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordGuildUser> GetGuildUserAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task LeaveGuildAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordConnection>> GetConnectionsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationRoleConnection> GetApplicationRoleConnectionAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IDiscordApplicationRoleConnection> UpdateApplicationRoleConnectionAsync(ulong applicationId, IDiscordApplicationRoleConnectionProperties applicationRoleConnectionProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    IDiscordImageUrl? GetAvatarUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl? GetBannerUrl(NetCord.ImageFormat? format = default);
    IDiscordImageUrl? GetAvatarDecorationUrl();
    Task<IDiscordDMChannel> GetDMChannelAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordCurrentUserOptions  
{
    NetCord.Rest.CurrentUserOptions Original { get; }
    string? Username { get; set; }
    NetCord.Rest.ImageProperties? Avatar { get; set; }
    NetCord.Rest.ImageProperties? Banner { get; set; }
    IDiscordCurrentUserOptions WithUsername(string username);
    IDiscordCurrentUserOptions WithAvatar(NetCord.Rest.ImageProperties? avatar);
    IDiscordCurrentUserOptions WithBanner(NetCord.Rest.ImageProperties? banner);
}


public interface IDiscordGuildsPaginationProperties  
{
    NetCord.Rest.GuildsPaginationProperties Original { get; }
    bool WithCounts { get; set; }
    ulong? From { get; set; }
    NetCord.Rest.PaginationDirection? Direction { get; set; }
    int? BatchSize { get; set; }
    IDiscordGuildsPaginationProperties WithWithCounts(bool withCounts = true);
    IDiscordGuildsPaginationProperties WithFrom(ulong? from);
    IDiscordGuildsPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction);
    IDiscordGuildsPaginationProperties WithBatchSize(int? batchSize);
}


public interface IDiscordGroupDMChannel  
{
    NetCord.GroupDMChannel Original { get; }
    string Name { get; }
    string? IconHash { get; }
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
    IAsyncEnumerable<IDiscordRestMessage>? GetMessagesAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
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
    IAsyncEnumerable<IDiscordUser>? GetMessagePollAnswerVotersAsync(ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null);
    Task<IDiscordRestMessage> EndMessagePollAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default);
}


public interface IDiscordGroupDMChannelProperties  
{
    NetCord.Rest.GroupDMChannelProperties Original { get; }
    IEnumerable<string> AccessTokens { get; set; }
    IReadOnlyDictionary<ulong, string>? Nicknames { get; set; }
    IDiscordGroupDMChannelProperties WithAccessTokens(IEnumerable<string> accessTokens);
    IDiscordGroupDMChannelProperties AddAccessTokens(IEnumerable<string> accessTokens);
    IDiscordGroupDMChannelProperties AddAccessTokens(string[] accessTokens);
    IDiscordGroupDMChannelProperties WithNicknames(IReadOnlyDictionary<ulong, string>? nicknames);
}


public interface IDiscordConnection  
{
    NetCord.Rest.Connection Original { get; }
    string Id { get; }
    string Name { get; }
    NetCord.Rest.ConnectionType Type { get; }
    bool? Revoked { get; }
    IReadOnlyList<IDiscordIntegration>? Integrations { get; }
    bool Verified { get; }
    bool FriendSync { get; }
    bool ShowActivity { get; }
    bool TwoWayLink { get; }
    NetCord.Rest.ConnectionVisibility Visibility { get; }
}


public interface IDiscordApplicationRoleConnection  
{
    NetCord.Rest.ApplicationRoleConnection Original { get; }
    string? PlatformName { get; }
    string? PlatformUsername { get; }
    IReadOnlyDictionary<string, string> Metadata { get; }
}


public interface IDiscordApplicationRoleConnectionProperties  
{
    NetCord.Rest.ApplicationRoleConnectionProperties Original { get; }
    string? PlatformName { get; set; }
    string? PlatformUsername { get; set; }
    IReadOnlyDictionary<string, string>? Metadata { get; set; }
    IDiscordApplicationRoleConnectionProperties WithPlatformName(string platformName);
    IDiscordApplicationRoleConnectionProperties WithPlatformUsername(string platformUsername);
    IDiscordApplicationRoleConnectionProperties WithMetadata(IReadOnlyDictionary<string, string>? metadata);
}


public interface IDiscordApplicationInstallParamsProperties  
{
    NetCord.Rest.ApplicationInstallParamsProperties Original { get; }
    IEnumerable<string>? Scopes { get; set; }
    NetCord.Permissions? Permissions { get; set; }
    IDiscordApplicationInstallParamsProperties WithScopes(IEnumerable<string>? scopes);
    IDiscordApplicationInstallParamsProperties AddScopes(IEnumerable<string> scopes);
    IDiscordApplicationInstallParamsProperties AddScopes(string[] scopes);
    IDiscordApplicationInstallParamsProperties WithPermissions(NetCord.Permissions? permissions);
}


public interface IDiscordApplicationIntegrationTypeConfigurationProperties  
{
    NetCord.Rest.ApplicationIntegrationTypeConfigurationProperties Original { get; }
    IDiscordApplicationInstallParamsProperties? OAuth2InstallParams { get; set; }
    IDiscordApplicationIntegrationTypeConfigurationProperties WithOAuth2InstallParams(IDiscordApplicationInstallParamsProperties? oAuth2InstallParams);
}


public interface IDiscordForumGuildThreadMessageProperties  
{
    NetCord.Rest.ForumGuildThreadMessageProperties Original { get; }
    string? Content { get; set; }
    IEnumerable<IDiscordEmbedProperties>? Embeds { get; set; }
    IDiscordAllowedMentionsProperties? AllowedMentions { get; set; }
    IEnumerable<IDiscordComponentProperties>? Components { get; set; }
    IEnumerable<ulong>? StickerIds { get; set; }
    IEnumerable<IDiscordAttachmentProperties>? Attachments { get; set; }
    NetCord.MessageFlags? Flags { get; set; }
    IDiscordForumGuildThreadMessageProperties WithContent(string content);
    IDiscordForumGuildThreadMessageProperties WithEmbeds(IEnumerable<IDiscordEmbedProperties>? embeds);
    IDiscordForumGuildThreadMessageProperties AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds);
    IDiscordForumGuildThreadMessageProperties AddEmbeds(IDiscordEmbedProperties[] embeds);
    IDiscordForumGuildThreadMessageProperties WithAllowedMentions(IDiscordAllowedMentionsProperties? allowedMentions);
    IDiscordForumGuildThreadMessageProperties WithComponents(IEnumerable<IDiscordComponentProperties>? components);
    IDiscordForumGuildThreadMessageProperties AddComponents(IEnumerable<IDiscordComponentProperties> components);
    IDiscordForumGuildThreadMessageProperties AddComponents(IDiscordComponentProperties[] components);
    IDiscordForumGuildThreadMessageProperties WithStickerIds(IEnumerable<ulong>? stickerIds);
    IDiscordForumGuildThreadMessageProperties AddStickerIds(IEnumerable<ulong> stickerIds);
    IDiscordForumGuildThreadMessageProperties AddStickerIds(ulong[] stickerIds);
    IDiscordForumGuildThreadMessageProperties WithAttachments(IEnumerable<IDiscordAttachmentProperties>? attachments);
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
    public IDiscordInteractionGuildReference? GuildReference => _original.GuildReference is null ? null : new DiscordInteractionGuildReference(_original.GuildReference);
    public IDiscordGuild? Guild => _original.Guild is null ? null : new DiscordGuild(_original.Guild);
    public IDiscordTextChannel Channel => new DiscordTextChannel(_original.Channel);
    public IDiscordUser User => new DiscordUser(_original.User);
    public string Token => _original.Token;
    public NetCord.Permissions AppPermissions => _original.AppPermissions;
    public string UserLocale => _original.UserLocale;
    public string? GuildLocale => _original.GuildLocale;
    public IReadOnlyList<IDiscordEntitlement> Entitlements => _original.Entitlements.Select(x => new DiscordEntitlement(x)).ToList();
    public IReadOnlyDictionary<NetCord.ApplicationIntegrationType, ulong> AuthorizingIntegrationOwners => _original.AuthorizingIntegrationOwners;
    public NetCord.InteractionContextType Context => _original.Context;
    public IDiscordInteractionData Data => new DiscordInteractionData(_original.Data);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public Task SendResponseAsync(IDiscordInteractionCallback callback, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.SendResponseAsync(callback.Original, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordRestMessage> GetResponseAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.GetResponseAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestMessage> ModifyResponseAsync(Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.ModifyResponseAsync(x => action(new DiscordMessageOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteResponseAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteResponseAsync(properties?.Original, cancellationToken);
    }
    public async Task<IDiscordRestMessage> SendFollowupMessageAsync(IDiscordInteractionMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.SendFollowupMessageAsync(message.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestMessage> GetFollowupMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.GetFollowupMessageAsync(messageId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestMessage> ModifyFollowupMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.ModifyFollowupMessageAsync(messageId, x => action(new DiscordMessageOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteFollowupMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteFollowupMessageAsync(messageId, properties?.Original, cancellationToken);
    }
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
    public ImmutableDictionary<ulong, IDiscordVoiceState> VoiceStates { get { return _original.VoiceStates.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordVoiceState)new DiscordVoiceState(kv.Value)); } set { _original.VoiceStates = value.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.Original); } }
    public ImmutableDictionary<ulong, IDiscordGuildUser> Users { get { return _original.Users.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordGuildUser)new DiscordGuildUser(kv.Value)); } set { _original.Users = value.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.Original); } }
    public ImmutableDictionary<ulong, IDiscordGuildChannel> Channels { get { return _original.Channels.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordGuildChannel)new DiscordGuildChannel(kv.Value)); } set { _original.Channels = value.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.Original); } }
    public ImmutableDictionary<ulong, IDiscordGuildThread> ActiveThreads { get { return _original.ActiveThreads.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordGuildThread)new DiscordGuildThread(kv.Value)); } set { _original.ActiveThreads = value.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.Original); } }
    public ImmutableDictionary<ulong, IDiscordPresence> Presences { get { return _original.Presences.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordPresence)new DiscordPresence(kv.Value)); } set { _original.Presences = value.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.Original); } }
    public ImmutableDictionary<ulong, IDiscordStageInstance> StageInstances { get { return _original.StageInstances.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordStageInstance)new DiscordStageInstance(kv.Value)); } set { _original.StageInstances = value.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.Original); } }
    public ImmutableDictionary<ulong, IDiscordGuildScheduledEvent> ScheduledEvents { get { return _original.ScheduledEvents.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordGuildScheduledEvent)new DiscordGuildScheduledEvent(kv.Value)); } set { _original.ScheduledEvents = value.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.Original); } }
    public bool IsOwner => _original.IsOwner;
    public ulong Id => _original.Id;
    public string Name => _original.Name;
    public bool HasIcon => _original.HasIcon;
    public string? IconHash => _original.IconHash;
    public bool HasSplash => _original.HasSplash;
    public string? SplashHash => _original.SplashHash;
    public bool HasDiscoverySplash => _original.HasDiscoverySplash;
    public string? DiscoverySplashHash => _original.DiscoverySplashHash;
    public ulong OwnerId => _original.OwnerId;
    public NetCord.Permissions? Permissions => _original.Permissions;
    public ulong? AfkChannelId => _original.AfkChannelId;
    public int AfkTimeout => _original.AfkTimeout;
    public bool? WidgetEnabled => _original.WidgetEnabled;
    public ulong? WidgetChannelId => _original.WidgetChannelId;
    public NetCord.VerificationLevel VerificationLevel => _original.VerificationLevel;
    public NetCord.DefaultMessageNotificationLevel DefaultMessageNotificationLevel => _original.DefaultMessageNotificationLevel;
    public NetCord.ContentFilter ContentFilter => _original.ContentFilter;
    public ImmutableDictionary<ulong, IDiscordRole> Roles { get { return _original.Roles.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordRole)new DiscordRole(kv.Value)); } set { _original.Roles = value.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.Original); } }
    public ImmutableDictionary<ulong, IDiscordGuildEmoji> Emojis { get { return _original.Emojis.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordGuildEmoji)new DiscordGuildEmoji(kv.Value)); } set { _original.Emojis = value.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.Original); } }
    public IReadOnlyList<string> Features => _original.Features;
    public NetCord.MfaLevel MfaLevel => _original.MfaLevel;
    public ulong? ApplicationId => _original.ApplicationId;
    public ulong? SystemChannelId => _original.SystemChannelId;
    public NetCord.Rest.SystemChannelFlags SystemChannelFlags => _original.SystemChannelFlags;
    public ulong? RulesChannelId => _original.RulesChannelId;
    public int? MaxPresences => _original.MaxPresences;
    public int? MaxUsers => _original.MaxUsers;
    public string? VanityUrlCode => _original.VanityUrlCode;
    public string? Description => _original.Description;
    public bool HasBanner => _original.HasBanner;
    public string? BannerHash => _original.BannerHash;
    public int PremiumTier => _original.PremiumTier;
    public int? PremiumSubscriptionCount => _original.PremiumSubscriptionCount;
    public string PreferredLocale => _original.PreferredLocale;
    public ulong? PublicUpdatesChannelId => _original.PublicUpdatesChannelId;
    public int? MaxVideoChannelUsers => _original.MaxVideoChannelUsers;
    public int? MaxStageVideoChannelUsers => _original.MaxStageVideoChannelUsers;
    public int? ApproximateUserCount => _original.ApproximateUserCount;
    public int? ApproximatePresenceCount => _original.ApproximatePresenceCount;
    public IDiscordGuildWelcomeScreen? WelcomeScreen => _original.WelcomeScreen is null ? null : new DiscordGuildWelcomeScreen(_original.WelcomeScreen);
    public NetCord.NsfwLevel NsfwLevel => _original.NsfwLevel;
    public ImmutableDictionary<ulong, IDiscordGuildSticker> Stickers { get { return _original.Stickers.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordGuildSticker)new DiscordGuildSticker(kv.Value)); } set { _original.Stickers = value.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.Original); } }
    public bool PremiumProgressBarEnabled => _original.PremiumProgressBarEnabled;
    public ulong? SafetyAlertsChannelId => _original.SafetyAlertsChannelId;
    public IDiscordRole? EveryoneRole => _original.EveryoneRole is null ? null : new DiscordRole(_original.EveryoneRole);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordGuild With(Action<IDiscordGuild> action) 
    {
        return new DiscordGuild(_original.With(x => action(new DiscordGuild(x))));
    }
    public int? Compare(IDiscordPartialGuildUser x, IDiscordPartialGuildUser y) 
    {
        return _original.Compare(x.Original, y.Original);
    }
    public IDiscordImageUrl? GetIconUrl(NetCord.ImageFormat? format = default) 
    {
        var temp = _original.GetIconUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetSplashUrl(NetCord.ImageFormat format) 
    {
        var temp = _original.GetSplashUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetDiscoverySplashUrl(NetCord.ImageFormat format) 
    {
        var temp = _original.GetDiscoverySplashUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetBannerUrl(NetCord.ImageFormat? format = default) 
    {
        var temp = _original.GetBannerUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl GetWidgetUrl(NetCord.GuildWidgetStyle? style = default, string? hostname = null, NetCord.ApiVersion? version = default) 
    {
        return new DiscordImageUrl(_original.GetWidgetUrl(style, hostname, version));
    }
    public async IAsyncEnumerable<IDiscordRestAuditLogEntry> GetAuditLogAsync(IDiscordGuildAuditLogPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetAuditLogAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordRestAuditLogEntry(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordAutoModerationRule>> GetAutoModerationRulesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetAutoModerationRulesAsync(properties?.Original, cancellationToken)).Select(x => new DiscordAutoModerationRule(x)).ToList();
    }
    public async Task<IDiscordAutoModerationRule> GetAutoModerationRuleAsync(ulong autoModerationRuleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordAutoModerationRule(await _original.GetAutoModerationRuleAsync(autoModerationRuleId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordAutoModerationRule> CreateAutoModerationRuleAsync(IDiscordAutoModerationRuleProperties autoModerationRuleProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordAutoModerationRule(await _original.CreateAutoModerationRuleAsync(autoModerationRuleProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordAutoModerationRule> ModifyAutoModerationRuleAsync(ulong autoModerationRuleId, Action<IDiscordAutoModerationRuleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordAutoModerationRule(await _original.ModifyAutoModerationRuleAsync(autoModerationRuleId, x => action(new DiscordAutoModerationRuleOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteAutoModerationRuleAsync(ulong autoModerationRuleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAutoModerationRuleAsync(autoModerationRuleId, properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordGuildEmoji>> GetEmojisAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetEmojisAsync(properties?.Original, cancellationToken)).Select(x => new DiscordGuildEmoji(x)).ToList();
    }
    public async Task<IDiscordGuildEmoji> GetEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildEmoji(await _original.GetEmojiAsync(emojiId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildEmoji> CreateEmojiAsync(IDiscordGuildEmojiProperties guildEmojiProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildEmoji(await _original.CreateEmojiAsync(guildEmojiProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildEmoji> ModifyEmojiAsync(ulong emojiId, Action<IDiscordGuildEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildEmoji(await _original.ModifyEmojiAsync(emojiId, x => action(new DiscordGuildEmojiOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteEmojiAsync(emojiId, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordRestGuild> GetAsync(bool withCounts = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestGuild(await _original.GetAsync(withCounts, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildPreview> GetPreviewAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildPreview(await _original.GetPreviewAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestGuild> ModifyAsync(Action<IDiscordGuildOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestGuild(await _original.ModifyAsync(x => action(new DiscordGuildOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAsync(properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordGuildChannel>> GetChannelsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetChannelsAsync(properties?.Original, cancellationToken)).Select(x => new DiscordGuildChannel(x)).ToList();
    }
    public async Task<IDiscordGuildChannel> CreateChannelAsync(IDiscordGuildChannelProperties channelProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildChannel(await _original.CreateChannelAsync(channelProperties.Original, properties?.Original, cancellationToken));
    }
    public Task ModifyChannelPositionsAsync(IEnumerable<IDiscordGuildChannelPositionProperties> positions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.ModifyChannelPositionsAsync(positions.Select(x => x.Original), properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordGuildThread>> GetActiveThreadsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetActiveThreadsAsync(properties?.Original, cancellationToken)).Select(x => new DiscordGuildThread(x)).ToList();
    }
    public async Task<IDiscordGuildUser> GetUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildUser(await _original.GetUserAsync(userId, properties?.Original, cancellationToken));
    }
    public async IAsyncEnumerable<IDiscordGuildUser> GetUsersAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetUsersAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordGuildUser(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordGuildUser>> FindUserAsync(string name, int limit, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.FindUserAsync(name, limit, properties?.Original, cancellationToken)).Select(x => new DiscordGuildUser(x)).ToList();
    }
    public async Task<IDiscordGuildUser?> AddUserAsync(ulong userId, IDiscordGuildUserProperties userProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        var temp = await _original.AddUserAsync(userId, userProperties.Original, properties?.Original, cancellationToken);
        return temp is null ? null : new DiscordGuildUser(temp);
    }
    public async Task<IDiscordGuildUser> ModifyUserAsync(ulong userId, Action<IDiscordGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildUser(await _original.ModifyUserAsync(userId, x => action(new DiscordGuildUserOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildUser> ModifyCurrentUserAsync(Action<IDiscordCurrentGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildUser(await _original.ModifyCurrentUserAsync(x => action(new DiscordCurrentGuildUserOptions(x)), properties?.Original, cancellationToken));
    }
    public Task AddUserRoleAsync(ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.AddUserRoleAsync(userId, roleId, properties?.Original, cancellationToken);
    }
    public Task RemoveUserRoleAsync(ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.RemoveUserRoleAsync(userId, roleId, properties?.Original, cancellationToken);
    }
    public Task KickUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.KickUserAsync(userId, properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordGuildBan> GetBansAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetBansAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordGuildBan(original);
        }
    }
    public async Task<IDiscordGuildBan> GetBanAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildBan(await _original.GetBanAsync(userId, properties?.Original, cancellationToken));
    }
    public Task BanUserAsync(ulong userId, int deleteMessageSeconds = 0, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.BanUserAsync(userId, deleteMessageSeconds, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordGuildBulkBan> BanUsersAsync(IEnumerable<ulong> userIds, int deleteMessageSeconds = 0, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildBulkBan(await _original.BanUsersAsync(userIds, deleteMessageSeconds, properties?.Original, cancellationToken));
    }
    public Task UnbanUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.UnbanUserAsync(userId, properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordRole>> GetRolesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetRolesAsync(properties?.Original, cancellationToken)).Select(x => new DiscordRole(x)).ToList();
    }
    public async Task<IDiscordRole> GetRoleAsync(ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRole(await _original.GetRoleAsync(roleId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRole> CreateRoleAsync(IDiscordRoleProperties guildRoleProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRole(await _original.CreateRoleAsync(guildRoleProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordRole>> ModifyRolePositionsAsync(IEnumerable<IDiscordRolePositionProperties> positions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.ModifyRolePositionsAsync(positions.Select(x => x.Original), properties?.Original, cancellationToken)).Select(x => new DiscordRole(x)).ToList();
    }
    public async Task<IDiscordRole> ModifyRoleAsync(ulong roleId, Action<IDiscordRoleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRole(await _original.ModifyRoleAsync(roleId, x => action(new DiscordRoleOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteRoleAsync(ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteRoleAsync(roleId, properties?.Original, cancellationToken);
    }
    public Task<NetCord.MfaLevel> ModifyMfaLevelAsync(NetCord.MfaLevel mfaLevel, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.ModifyMfaLevelAsync(mfaLevel, properties?.Original, cancellationToken);
    }
    public Task<int> GetPruneCountAsync(int days, IEnumerable<ulong>? roles = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.GetPruneCountAsync(days, roles, properties?.Original, cancellationToken);
    }
    public Task<int?> PruneAsync(IDiscordGuildPruneProperties pruneProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.PruneAsync(pruneProperties.Original, properties?.Original, cancellationToken);
    }
    public async Task<IEnumerable<IDiscordVoiceRegion>> GetVoiceRegionsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetVoiceRegionsAsync(properties?.Original, cancellationToken)).Select(x => new DiscordVoiceRegion(x));
    }
    public async Task<IEnumerable<IDiscordRestInvite>> GetInvitesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetInvitesAsync(properties?.Original, cancellationToken)).Select(x => new DiscordRestInvite(x));
    }
    public async Task<IReadOnlyList<IDiscordIntegration>> GetIntegrationsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetIntegrationsAsync(properties?.Original, cancellationToken)).Select(x => new DiscordIntegration(x)).ToList();
    }
    public Task DeleteIntegrationAsync(ulong integrationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteIntegrationAsync(integrationId, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordGuildWidgetSettings> GetWidgetSettingsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildWidgetSettings(await _original.GetWidgetSettingsAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildWidgetSettings> ModifyWidgetSettingsAsync(Action<IDiscordGuildWidgetSettingsOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildWidgetSettings(await _original.ModifyWidgetSettingsAsync(x => action(new DiscordGuildWidgetSettingsOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildWidget> GetWidgetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildWidget(await _original.GetWidgetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildVanityInvite> GetVanityInviteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildVanityInvite(await _original.GetVanityInviteAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildWelcomeScreen> GetWelcomeScreenAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildWelcomeScreen(await _original.GetWelcomeScreenAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildWelcomeScreen> ModifyWelcomeScreenAsync(Action<IDiscordGuildWelcomeScreenOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildWelcomeScreen(await _original.ModifyWelcomeScreenAsync(x => action(new DiscordGuildWelcomeScreenOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildOnboarding> GetOnboardingAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildOnboarding(await _original.GetOnboardingAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildOnboarding> ModifyOnboardingAsync(Action<IDiscordGuildOnboardingOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildOnboarding(await _original.ModifyOnboardingAsync(x => action(new DiscordGuildOnboardingOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordGuildScheduledEvent>> GetScheduledEventsAsync(bool withUserCount = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetScheduledEventsAsync(withUserCount, properties?.Original, cancellationToken)).Select(x => new DiscordGuildScheduledEvent(x)).ToList();
    }
    public async Task<IDiscordGuildScheduledEvent> CreateScheduledEventAsync(IDiscordGuildScheduledEventProperties guildScheduledEventProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildScheduledEvent(await _original.CreateScheduledEventAsync(guildScheduledEventProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildScheduledEvent> GetScheduledEventAsync(ulong scheduledEventId, bool withUserCount = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildScheduledEvent(await _original.GetScheduledEventAsync(scheduledEventId, withUserCount, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildScheduledEvent> ModifyScheduledEventAsync(ulong scheduledEventId, Action<IDiscordGuildScheduledEventOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildScheduledEvent(await _original.ModifyScheduledEventAsync(scheduledEventId, x => action(new DiscordGuildScheduledEventOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteScheduledEventAsync(ulong scheduledEventId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteScheduledEventAsync(scheduledEventId, properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordGuildScheduledEventUser> GetScheduledEventUsersAsync(ulong scheduledEventId, IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetScheduledEventUsersAsync(scheduledEventId, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordGuildScheduledEventUser(original);
        }
    }
    public async Task<IEnumerable<IDiscordGuildTemplate>> GetTemplatesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetTemplatesAsync(properties?.Original, cancellationToken)).Select(x => new DiscordGuildTemplate(x));
    }
    public async Task<IDiscordGuildTemplate> CreateTemplateAsync(IDiscordGuildTemplateProperties guildTemplateProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildTemplate(await _original.CreateTemplateAsync(guildTemplateProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildTemplate> SyncTemplateAsync(string templateCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildTemplate(await _original.SyncTemplateAsync(templateCode, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildTemplate> ModifyTemplateAsync(string templateCode, Action<IDiscordGuildTemplateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildTemplate(await _original.ModifyTemplateAsync(templateCode, x => action(new DiscordGuildTemplateOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildTemplate> DeleteTemplateAsync(string templateCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildTemplate(await _original.DeleteTemplateAsync(templateCode, properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordGuildApplicationCommand>> GetApplicationCommandsAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetApplicationCommandsAsync(applicationId, properties?.Original, cancellationToken)).Select(x => new DiscordGuildApplicationCommand(x)).ToList();
    }
    public async Task<IDiscordGuildApplicationCommand> CreateApplicationCommandAsync(ulong applicationId, IDiscordApplicationCommandProperties applicationCommandProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildApplicationCommand(await _original.CreateApplicationCommandAsync(applicationId, applicationCommandProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildApplicationCommand> GetApplicationCommandAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildApplicationCommand(await _original.GetApplicationCommandAsync(applicationId, commandId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildApplicationCommand> ModifyApplicationCommandAsync(ulong applicationId, ulong commandId, Action<IDiscordApplicationCommandOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildApplicationCommand(await _original.ModifyApplicationCommandAsync(applicationId, commandId, x => action(new DiscordApplicationCommandOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteApplicationCommandAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteApplicationCommandAsync(applicationId, commandId, properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordGuildApplicationCommand>> BulkOverwriteApplicationCommandsAsync(ulong applicationId, IEnumerable<IDiscordApplicationCommandProperties> commands, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.BulkOverwriteApplicationCommandsAsync(applicationId, commands.Select(x => x.Original), properties?.Original, cancellationToken)).Select(x => new DiscordGuildApplicationCommand(x)).ToList();
    }
    public async Task<IReadOnlyList<IDiscordApplicationCommandGuildPermissions>> GetApplicationCommandsPermissionsAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetApplicationCommandsPermissionsAsync(applicationId, properties?.Original, cancellationToken)).Select(x => new DiscordApplicationCommandGuildPermissions(x)).ToList();
    }
    public async Task<IDiscordApplicationCommandGuildPermissions> GetApplicationCommandPermissionsAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationCommandGuildPermissions(await _original.GetApplicationCommandPermissionsAsync(applicationId, commandId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordApplicationCommandGuildPermissions> OverwriteApplicationCommandPermissionsAsync(ulong applicationId, ulong commandId, IEnumerable<IDiscordApplicationCommandGuildPermissionProperties> newPermissions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationCommandGuildPermissions(await _original.OverwriteApplicationCommandPermissionsAsync(applicationId, commandId, newPermissions.Select(x => x.Original), properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordGuildSticker>> GetStickersAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetStickersAsync(properties?.Original, cancellationToken)).Select(x => new DiscordGuildSticker(x)).ToList();
    }
    public async Task<IDiscordGuildSticker> GetStickerAsync(ulong stickerId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildSticker(await _original.GetStickerAsync(stickerId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildSticker> CreateStickerAsync(IDiscordGuildStickerProperties sticker, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildSticker(await _original.CreateStickerAsync(sticker.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildSticker> ModifyStickerAsync(ulong stickerId, Action<IDiscordGuildStickerOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildSticker(await _original.ModifyStickerAsync(stickerId, x => action(new DiscordGuildStickerOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteStickerAsync(ulong stickerId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteStickerAsync(stickerId, properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordGuildUserInfo> SearchUsersAsync(IDiscordGuildUsersSearchPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.SearchUsersAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordGuildUserInfo(original);
        }
    }
    public async Task<IDiscordGuildUser> GetCurrentUserGuildUserAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildUser(await _original.GetCurrentUserGuildUserAsync(properties?.Original, cancellationToken));
    }
    public Task LeaveAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.LeaveAsync(properties?.Original, cancellationToken);
    }
    public async Task<IDiscordVoiceState> GetCurrentUserVoiceStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordVoiceState(await _original.GetCurrentUserVoiceStateAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordVoiceState> GetUserVoiceStateAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordVoiceState(await _original.GetUserVoiceStateAsync(userId, properties?.Original, cancellationToken));
    }
    public Task ModifyCurrentUserVoiceStateAsync(Action<IDiscordCurrentUserVoiceStateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.ModifyCurrentUserVoiceStateAsync(x => action(new DiscordCurrentUserVoiceStateOptions(x)), properties?.Original, cancellationToken);
    }
    public Task ModifyUserVoiceStateAsync(ulong channelId, ulong userId, Action<IDiscordVoiceStateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.ModifyUserVoiceStateAsync(channelId, userId, x => action(new DiscordVoiceStateOptions(x)), properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordWebhook>> GetWebhooksAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetWebhooksAsync(properties?.Original, cancellationToken)).Select(x => new DiscordWebhook(x)).ToList();
    }
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
    public async Task<IDiscordTextChannel> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordTextChannel(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordTextChannel> DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordTextChannel(await _original.DeleteAsync(properties?.Original, cancellationToken));
    }
    public async IAsyncEnumerable<IDiscordRestMessage> GetMessagesAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetMessagesAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordRestMessage(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetMessagesAroundAsync(ulong messageId, int? limit = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetMessagesAroundAsync(messageId, limit, properties?.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    }
    public async Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.GetMessageAsync(messageId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestMessage> SendMessageAsync(IDiscordMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.SendMessageAsync(message.Original, properties?.Original, cancellationToken));
    }
    public Task AddMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.AddMessageReactionAsync(messageId, emoji.Original, properties?.Original, cancellationToken);
    }
    public Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessageReactionAsync(messageId, emoji.Original, properties?.Original, cancellationToken);
    }
    public Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessageReactionAsync(messageId, emoji.Original, userId, properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordUser> GetMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordMessageReactionsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetMessageReactionsAsync(messageId, emoji.Original, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAllMessageReactionsAsync(messageId, properties?.Original, cancellationToken);
    }
    public Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAllMessageReactionsAsync(messageId, emoji.Original, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.ModifyMessageAsync(messageId, x => action(new DiscordMessageOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessageAsync(messageId, properties?.Original, cancellationToken);
    }
    public Task DeleteMessagesAsync(IEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessagesAsync(messageIds, properties?.Original, cancellationToken);
    }
    public Task DeleteMessagesAsync(IAsyncEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessagesAsync(messageIds, properties?.Original, cancellationToken);
    }
    public Task TriggerTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.TriggerTypingStateAsync(properties?.Original, cancellationToken);
    }
    public Task<IDisposable> EnterTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.EnterTypingStateAsync(properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetPinnedMessagesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetPinnedMessagesAsync(properties?.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    }
    public Task PinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.PinMessageAsync(messageId, properties?.Original, cancellationToken);
    }
    public Task UnpinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.UnpinMessageAsync(messageId, properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordUser> GetMessagePollAnswerVotersAsync(ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetMessagePollAnswerVotersAsync(messageId, answerId, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public async Task<IDiscordRestMessage> EndMessagePollAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.EndMessagePollAsync(messageId, properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.CreateGoogleCloudPlatformStorageBucketsAsync(buckets.Select(x => x.Original), properties?.Original, cancellationToken)).Select(x => new DiscordGoogleCloudPlatformStorageBucket(x)).ToList();
    }
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
    public string? GlobalName => _original.GlobalName;
    public string? AvatarHash => _original.AvatarHash;
    public bool IsBot => _original.IsBot;
    public bool? IsSystemUser => _original.IsSystemUser;
    public bool? MfaEnabled => _original.MfaEnabled;
    public string? BannerHash => _original.BannerHash;
    public NetCord.Color? AccentColor => _original.AccentColor;
    public string? Locale => _original.Locale;
    public bool? Verified => _original.Verified;
    public string? Email => _original.Email;
    public NetCord.UserFlags? Flags => _original.Flags;
    public NetCord.PremiumType? PremiumType => _original.PremiumType;
    public NetCord.UserFlags? PublicFlags => _original.PublicFlags;
    public IDiscordAvatarDecorationData? AvatarDecorationData => _original.AvatarDecorationData is null ? null : new DiscordAvatarDecorationData(_original.AvatarDecorationData);
    public bool HasAvatar => _original.HasAvatar;
    public bool HasBanner => _original.HasBanner;
    public bool HasAvatarDecoration => _original.HasAvatarDecoration;
    public IDiscordImageUrl DefaultAvatarUrl => new DiscordImageUrl(_original.DefaultAvatarUrl);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordImageUrl? GetAvatarUrl(NetCord.ImageFormat? format = default) 
    {
        var temp = _original.GetAvatarUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetBannerUrl(NetCord.ImageFormat? format = default) 
    {
        var temp = _original.GetBannerUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetAvatarDecorationUrl() 
    {
        var temp = _original.GetAvatarDecorationUrl();
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public async Task<IDiscordUser> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordUser(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordDMChannel> GetDMChannelAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordDMChannel(await _original.GetDMChannelAsync(properties?.Original, cancellationToken));
    }
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
    public async Task<IDiscordEntitlement> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordEntitlement(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public Task ConsumeAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.ConsumeAsync(properties?.Original, cancellationToken);
    }
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
    public HttpContent Serialize() 
    {
        return _original.Serialize();
    }
}


public class DiscordRestRequestProperties : IDiscordRestRequestProperties 
{
    private readonly NetCord.Rest.RestRequestProperties _original;
    public DiscordRestRequestProperties(NetCord.Rest.RestRequestProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.RestRequestProperties Original => _original;
    public NetCord.Rest.RestRateLimitHandling? RateLimitHandling { get { return _original.RateLimitHandling; } set { _original.RateLimitHandling = value; } }
    public string? AuditLogReason { get { return _original.AuditLogReason; } set { _original.AuditLogReason = value; } }
    public string? ErrorLocalization { get { return _original.ErrorLocalization; } set { _original.ErrorLocalization = value; } }
    public IDiscordRestRequestProperties WithRateLimitHandling(NetCord.Rest.RestRateLimitHandling? rateLimitHandling) 
    {
        return new DiscordRestRequestProperties(_original.WithRateLimitHandling(rateLimitHandling));
    }
    public IDiscordRestRequestProperties WithAuditLogReason(string auditLogReason) 
    {
        return new DiscordRestRequestProperties(_original.WithAuditLogReason(auditLogReason));
    }
    public IDiscordRestRequestProperties WithErrorLocalization(string errorLocalization) 
    {
        return new DiscordRestRequestProperties(_original.WithErrorLocalization(errorLocalization));
    }
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
    public string? Nonce => _original.Nonce;
    public bool IsPinned => _original.IsPinned;
    public ulong? WebhookId => _original.WebhookId;
    public NetCord.MessageType Type => _original.Type;
    public IDiscordMessageActivity? Activity => _original.Activity is null ? null : new DiscordMessageActivity(_original.Activity);
    public IDiscordApplication? Application => _original.Application is null ? null : new DiscordApplication(_original.Application);
    public ulong? ApplicationId => _original.ApplicationId;
    public NetCord.MessageFlags Flags => _original.Flags;
    public IDiscordMessageReference? MessageReference => _original.MessageReference is null ? null : new DiscordMessageReference(_original.MessageReference);
    public IReadOnlyList<IDiscordMessageSnapshot> MessageSnapshots => _original.MessageSnapshots.Select(x => new DiscordMessageSnapshot(x)).ToList();
    public IDiscordRestMessage? ReferencedMessage => _original.ReferencedMessage is null ? null : new DiscordRestMessage(_original.ReferencedMessage);
    public IDiscordMessageInteractionMetadata? InteractionMetadata => _original.InteractionMetadata is null ? null : new DiscordMessageInteractionMetadata(_original.InteractionMetadata);
    public IDiscordGuildThread? StartedThread => _original.StartedThread is null ? null : new DiscordGuildThread(_original.StartedThread);
    public IReadOnlyList<IDiscordComponent> Components => _original.Components.Select(x => new DiscordComponent(x)).ToList();
    public IReadOnlyList<IDiscordMessageSticker> Stickers => _original.Stickers.Select(x => new DiscordMessageSticker(x)).ToList();
    public int? Position => _original.Position;
    public IDiscordRoleSubscriptionData? RoleSubscriptionData => _original.RoleSubscriptionData is null ? null : new DiscordRoleSubscriptionData(_original.RoleSubscriptionData);
    public IDiscordInteractionResolvedData? ResolvedData => _original.ResolvedData is null ? null : new DiscordInteractionResolvedData(_original.ResolvedData);
    public IDiscordMessagePoll? Poll => _original.Poll is null ? null : new DiscordMessagePoll(_original.Poll);
    public IDiscordMessageCall? Call => _original.Call is null ? null : new DiscordMessageCall(_original.Call);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordRestMessage> ReplyAsync(IDiscordReplyMessageProperties replyMessage, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.ReplyAsync(replyMessage.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestMessage> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestMessage> SendAsync(IDiscordMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.SendAsync(message.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestMessage> CrosspostAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.CrosspostAsync(properties?.Original, cancellationToken));
    }
    public Task AddReactionAsync(IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.AddReactionAsync(emoji.Original, properties?.Original, cancellationToken);
    }
    public Task DeleteReactionAsync(IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteReactionAsync(emoji.Original, properties?.Original, cancellationToken);
    }
    public Task DeleteReactionAsync(IDiscordReactionEmojiProperties emoji, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteReactionAsync(emoji.Original, userId, properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordUser> GetReactionsAsync(IDiscordReactionEmojiProperties emoji, IDiscordMessageReactionsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetReactionsAsync(emoji.Original, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public Task DeleteAllReactionsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAllReactionsAsync(properties?.Original, cancellationToken);
    }
    public Task DeleteAllReactionsAsync(IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAllReactionsAsync(emoji.Original, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordRestMessage> ModifyAsync(Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.ModifyAsync(x => action(new DiscordMessageOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAsync(properties?.Original, cancellationToken);
    }
    public Task PinAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.PinAsync(properties?.Original, cancellationToken);
    }
    public Task UnpinAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.UnpinAsync(properties?.Original, cancellationToken);
    }
    public async Task<IDiscordGuildThread> CreateGuildThreadAsync(IDiscordGuildThreadFromMessageProperties threadFromMessageProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildThread(await _original.CreateGuildThreadAsync(threadFromMessageProperties.Original, properties?.Original, cancellationToken));
    }
    public async IAsyncEnumerable<IDiscordUser> GetPollAnswerVotersAsync(int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetPollAnswerVotersAsync(answerId, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public async Task<IDiscordRestMessage> EndPollAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.EndPollAsync(properties?.Original, cancellationToken));
    }
}


public class DiscordMessageOptions : IDiscordMessageOptions 
{
    private readonly NetCord.Rest.MessageOptions _original;
    public DiscordMessageOptions(NetCord.Rest.MessageOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.MessageOptions Original => _original;
    public string? Content { get { return _original.Content; } set { _original.Content = value; } }
    public IEnumerable<IDiscordEmbedProperties>? Embeds { get { return _original.Embeds is null ? null : _original.Embeds.Select(x => new DiscordEmbedProperties(x)); } set { _original.Embeds = value?.Select(x => x.Original); } }
    public NetCord.MessageFlags? Flags { get { return _original.Flags; } set { _original.Flags = value; } }
    public IDiscordAllowedMentionsProperties? AllowedMentions { get { return _original.AllowedMentions is null ? null : new DiscordAllowedMentionsProperties(_original.AllowedMentions); } set { _original.AllowedMentions = value?.Original; } }
    public IEnumerable<IDiscordComponentProperties>? Components { get { return _original.Components is null ? null : _original.Components.Select(x => new DiscordComponentProperties(x)); } set { _original.Components = value?.Select(x => x.Original); } }
    public IEnumerable<IDiscordAttachmentProperties>? Attachments { get { return _original.Attachments is null ? null : _original.Attachments.Select(x => new DiscordAttachmentProperties(x)); } set { _original.Attachments = value?.Select(x => x.Original); } }
    public HttpContent Serialize() 
    {
        return _original.Serialize();
    }
    public IDiscordMessageOptions WithContent(string content) 
    {
        return new DiscordMessageOptions(_original.WithContent(content));
    }
    public IDiscordMessageOptions WithEmbeds(IEnumerable<IDiscordEmbedProperties>? embeds) 
    {
        return new DiscordMessageOptions(_original.WithEmbeds(embeds?.Select(x => x.Original)));
    }
    public IDiscordMessageOptions AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds) 
    {
        return new DiscordMessageOptions(_original.AddEmbeds(embeds.Select(x => x.Original)));
    }
    public IDiscordMessageOptions AddEmbeds(IDiscordEmbedProperties[] embeds) 
    {
        return new DiscordMessageOptions(_original.AddEmbeds(embeds.Select(x => x.Original).ToArray()));
    }
    public IDiscordMessageOptions WithFlags(NetCord.MessageFlags? flags) 
    {
        return new DiscordMessageOptions(_original.WithFlags(flags));
    }
    public IDiscordMessageOptions WithAllowedMentions(IDiscordAllowedMentionsProperties? allowedMentions) 
    {
        return new DiscordMessageOptions(_original.WithAllowedMentions(allowedMentions?.Original));
    }
    public IDiscordMessageOptions WithComponents(IEnumerable<IDiscordComponentProperties>? components) 
    {
        return new DiscordMessageOptions(_original.WithComponents(components?.Select(x => x.Original)));
    }
    public IDiscordMessageOptions AddComponents(IEnumerable<IDiscordComponentProperties> components) 
    {
        return new DiscordMessageOptions(_original.AddComponents(components.Select(x => x.Original)));
    }
    public IDiscordMessageOptions AddComponents(IDiscordComponentProperties[] components) 
    {
        return new DiscordMessageOptions(_original.AddComponents(components.Select(x => x.Original).ToArray()));
    }
    public IDiscordMessageOptions WithAttachments(IEnumerable<IDiscordAttachmentProperties>? attachments) 
    {
        return new DiscordMessageOptions(_original.WithAttachments(attachments?.Select(x => x.Original)));
    }
    public IDiscordMessageOptions AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments) 
    {
        return new DiscordMessageOptions(_original.AddAttachments(attachments.Select(x => x.Original)));
    }
    public IDiscordMessageOptions AddAttachments(IDiscordAttachmentProperties[] attachments) 
    {
        return new DiscordMessageOptions(_original.AddAttachments(attachments.Select(x => x.Original).ToArray()));
    }
}


public class DiscordInteractionMessageProperties : IDiscordInteractionMessageProperties 
{
    private readonly NetCord.Rest.InteractionMessageProperties _original;
    public DiscordInteractionMessageProperties(NetCord.Rest.InteractionMessageProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.InteractionMessageProperties Original => _original;
    public bool Tts { get { return _original.Tts; } set { _original.Tts = value; } }
    public string? Content { get { return _original.Content; } set { _original.Content = value; } }
    public IEnumerable<IDiscordEmbedProperties>? Embeds { get { return _original.Embeds is null ? null : _original.Embeds.Select(x => new DiscordEmbedProperties(x)); } set { _original.Embeds = value?.Select(x => x.Original); } }
    public IDiscordAllowedMentionsProperties? AllowedMentions { get { return _original.AllowedMentions is null ? null : new DiscordAllowedMentionsProperties(_original.AllowedMentions); } set { _original.AllowedMentions = value?.Original; } }
    public NetCord.MessageFlags? Flags { get { return _original.Flags; } set { _original.Flags = value; } }
    public IEnumerable<IDiscordComponentProperties>? Components { get { return _original.Components is null ? null : _original.Components.Select(x => new DiscordComponentProperties(x)); } set { _original.Components = value?.Select(x => x.Original); } }
    public IEnumerable<IDiscordAttachmentProperties>? Attachments { get { return _original.Attachments is null ? null : _original.Attachments.Select(x => new DiscordAttachmentProperties(x)); } set { _original.Attachments = value?.Select(x => x.Original); } }
    public IDiscordMessagePollProperties? Poll { get { return _original.Poll is null ? null : new DiscordMessagePollProperties(_original.Poll); } set { _original.Poll = value?.Original; } }
    public HttpContent Serialize() 
    {
        return _original.Serialize();
    }
    public IDiscordInteractionMessageProperties WithTts(bool tts = true) 
    {
        return new DiscordInteractionMessageProperties(_original.WithTts(tts));
    }
    public IDiscordInteractionMessageProperties WithContent(string content) 
    {
        return new DiscordInteractionMessageProperties(_original.WithContent(content));
    }
    public IDiscordInteractionMessageProperties WithEmbeds(IEnumerable<IDiscordEmbedProperties>? embeds) 
    {
        return new DiscordInteractionMessageProperties(_original.WithEmbeds(embeds?.Select(x => x.Original)));
    }
    public IDiscordInteractionMessageProperties AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds) 
    {
        return new DiscordInteractionMessageProperties(_original.AddEmbeds(embeds.Select(x => x.Original)));
    }
    public IDiscordInteractionMessageProperties AddEmbeds(IDiscordEmbedProperties[] embeds) 
    {
        return new DiscordInteractionMessageProperties(_original.AddEmbeds(embeds.Select(x => x.Original).ToArray()));
    }
    public IDiscordInteractionMessageProperties WithAllowedMentions(IDiscordAllowedMentionsProperties? allowedMentions) 
    {
        return new DiscordInteractionMessageProperties(_original.WithAllowedMentions(allowedMentions?.Original));
    }
    public IDiscordInteractionMessageProperties WithFlags(NetCord.MessageFlags? flags) 
    {
        return new DiscordInteractionMessageProperties(_original.WithFlags(flags));
    }
    public IDiscordInteractionMessageProperties WithComponents(IEnumerable<IDiscordComponentProperties>? components) 
    {
        return new DiscordInteractionMessageProperties(_original.WithComponents(components?.Select(x => x.Original)));
    }
    public IDiscordInteractionMessageProperties AddComponents(IEnumerable<IDiscordComponentProperties> components) 
    {
        return new DiscordInteractionMessageProperties(_original.AddComponents(components.Select(x => x.Original)));
    }
    public IDiscordInteractionMessageProperties AddComponents(IDiscordComponentProperties[] components) 
    {
        return new DiscordInteractionMessageProperties(_original.AddComponents(components.Select(x => x.Original).ToArray()));
    }
    public IDiscordInteractionMessageProperties WithAttachments(IEnumerable<IDiscordAttachmentProperties>? attachments) 
    {
        return new DiscordInteractionMessageProperties(_original.WithAttachments(attachments?.Select(x => x.Original)));
    }
    public IDiscordInteractionMessageProperties AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments) 
    {
        return new DiscordInteractionMessageProperties(_original.AddAttachments(attachments.Select(x => x.Original)));
    }
    public IDiscordInteractionMessageProperties AddAttachments(IDiscordAttachmentProperties[] attachments) 
    {
        return new DiscordInteractionMessageProperties(_original.AddAttachments(attachments.Select(x => x.Original).ToArray()));
    }
    public IDiscordInteractionMessageProperties WithPoll(IDiscordMessagePollProperties? poll) 
    {
        return new DiscordInteractionMessageProperties(_original.WithPoll(poll?.Original));
    }
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
    public IDiscordGuildUser? User => _original.User is null ? null : new DiscordGuildUser(_original.User);
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
    public string? Nickname => _original.Nickname;
    public string? GuildAvatarHash => _original.GuildAvatarHash;
    public string? GuildBannerHash => _original.GuildBannerHash;
    public IReadOnlyList<ulong> RoleIds => _original.RoleIds;
    public ulong? HoistedRoleId => _original.HoistedRoleId;
    public System.DateTimeOffset JoinedAt => _original.JoinedAt;
    public System.DateTimeOffset? GuildBoostStart => _original.GuildBoostStart;
    public bool Deafened => _original.Deafened;
    public bool Muted => _original.Muted;
    public NetCord.GuildUserFlags GuildFlags => _original.GuildFlags;
    public bool? IsPending => _original.IsPending;
    public System.DateTimeOffset? TimeOutUntil => _original.TimeOutUntil;
    public IDiscordAvatarDecorationData? GuildAvatarDecorationData => _original.GuildAvatarDecorationData is null ? null : new DiscordAvatarDecorationData(_original.GuildAvatarDecorationData);
    public bool HasGuildAvatar => _original.HasGuildAvatar;
    public bool HasGuildBanner => _original.HasGuildBanner;
    public bool HasGuildAvatarDecoration => _original.HasGuildAvatarDecoration;
    public ulong Id => _original.Id;
    public string Username => _original.Username;
    public ushort Discriminator => _original.Discriminator;
    public string? GlobalName => _original.GlobalName;
    public string? AvatarHash => _original.AvatarHash;
    public bool IsBot => _original.IsBot;
    public bool? IsSystemUser => _original.IsSystemUser;
    public bool? MfaEnabled => _original.MfaEnabled;
    public string? BannerHash => _original.BannerHash;
    public NetCord.Color? AccentColor => _original.AccentColor;
    public string? Locale => _original.Locale;
    public bool? Verified => _original.Verified;
    public string? Email => _original.Email;
    public NetCord.UserFlags? Flags => _original.Flags;
    public NetCord.PremiumType? PremiumType => _original.PremiumType;
    public NetCord.UserFlags? PublicFlags => _original.PublicFlags;
    public IDiscordAvatarDecorationData? AvatarDecorationData => _original.AvatarDecorationData is null ? null : new DiscordAvatarDecorationData(_original.AvatarDecorationData);
    public bool HasAvatar => _original.HasAvatar;
    public bool HasBanner => _original.HasBanner;
    public bool HasAvatarDecoration => _original.HasAvatarDecoration;
    public IDiscordImageUrl DefaultAvatarUrl => new DiscordImageUrl(_original.DefaultAvatarUrl);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordImageUrl? GetGuildAvatarUrl(NetCord.ImageFormat? format = default) 
    {
        var temp = _original.GetGuildAvatarUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetGuildBannerUrl(NetCord.ImageFormat? format = default) 
    {
        var temp = _original.GetGuildBannerUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public async Task<IDiscordGuildUser> TimeOutAsync(System.DateTimeOffset until, IDiscordRestRequestProperties? properties = null) 
    {
        return new DiscordGuildUser(await _original.TimeOutAsync(until, properties?.Original));
    }
    public async Task<IDiscordGuildUserInfo> GetInfoAsync(IDiscordRestRequestProperties? properties = null) 
    {
        return new DiscordGuildUserInfo(await _original.GetInfoAsync(properties?.Original));
    }
    public async Task<IDiscordGuildUser> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildUser(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildUser> ModifyAsync(Action<IDiscordGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildUser(await _original.ModifyAsync(x => action(new DiscordGuildUserOptions(x)), properties?.Original, cancellationToken));
    }
    public Task AddRoleAsync(ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.AddRoleAsync(roleId, properties?.Original, cancellationToken);
    }
    public Task RemoveRoleAsync(ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.RemoveRoleAsync(roleId, properties?.Original, cancellationToken);
    }
    public Task KickAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.KickAsync(properties?.Original, cancellationToken);
    }
    public Task BanAsync(int deleteMessageSeconds = 0, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.BanAsync(deleteMessageSeconds, properties?.Original, cancellationToken);
    }
    public Task UnbanAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.UnbanAsync(properties?.Original, cancellationToken);
    }
    public async Task<IDiscordVoiceState> GetVoiceStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordVoiceState(await _original.GetVoiceStateAsync(properties?.Original, cancellationToken));
    }
    public Task ModifyVoiceStateAsync(ulong channelId, Action<IDiscordVoiceStateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.ModifyVoiceStateAsync(channelId, x => action(new DiscordVoiceStateOptions(x)), properties?.Original, cancellationToken);
    }
    public IDiscordImageUrl? GetGuildAvatarDecorationUrl() 
    {
        var temp = _original.GetGuildAvatarDecorationUrl();
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetAvatarUrl(NetCord.ImageFormat? format = default) 
    {
        var temp = _original.GetAvatarUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetBannerUrl(NetCord.ImageFormat? format = default) 
    {
        var temp = _original.GetBannerUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetAvatarDecorationUrl() 
    {
        var temp = _original.GetAvatarDecorationUrl();
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public async Task<IDiscordDMChannel> GetDMChannelAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordDMChannel(await _original.GetDMChannelAsync(properties?.Original, cancellationToken));
    }
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
    public async Task<IDiscordGuildChannel> ModifyAsync(Action<IDiscordGuildChannelOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildChannel(await _original.ModifyAsync(x => action(new DiscordGuildChannelOptions(x)), properties?.Original, cancellationToken));
    }
    public Task ModifyPermissionsAsync(IDiscordPermissionOverwriteProperties permissionOverwrite, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.ModifyPermissionsAsync(permissionOverwrite.Original, properties?.Original, cancellationToken);
    }
    public async Task<IEnumerable<IDiscordRestInvite>> GetInvitesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetInvitesAsync(properties?.Original, cancellationToken)).Select(x => new DiscordRestInvite(x));
    }
    public async Task<IDiscordRestInvite> CreateInviteAsync(IDiscordInviteProperties? inviteProperties = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestInvite(await _original.CreateInviteAsync(inviteProperties?.Original, properties?.Original, cancellationToken));
    }
    public Task DeletePermissionAsync(ulong overwriteId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeletePermissionAsync(overwriteId, properties?.Original, cancellationToken);
    }
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
    public IDiscordThreadCurrentUser? CurrentUser => _original.CurrentUser is null ? null : new DiscordThreadCurrentUser(_original.CurrentUser);
    public int TotalMessageSent => _original.TotalMessageSent;
    public ulong GuildId => _original.GuildId;
    public int? Position => _original.Position;
    public IReadOnlyDictionary<ulong, IDiscordPermissionOverwrite> PermissionOverwrites => _original.PermissionOverwrites.ToDictionary(kv => kv.Key, kv => (IDiscordPermissionOverwrite)new DiscordPermissionOverwrite(kv.Value));
    public string Name => _original.Name;
    public string? Topic => _original.Topic;
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
    public async Task<IDiscordGuildThread> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildThread(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildThread> ModifyAsync(Action<IDiscordGuildChannelOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildThread(await _original.ModifyAsync(x => action(new DiscordGuildChannelOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildThread> DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildThread(await _original.DeleteAsync(properties?.Original, cancellationToken));
    }
    public Task JoinAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.JoinAsync(properties?.Original, cancellationToken);
    }
    public Task AddUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.AddUserAsync(userId, properties?.Original, cancellationToken);
    }
    public Task LeaveAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.LeaveAsync(properties?.Original, cancellationToken);
    }
    public Task DeleteUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteUserAsync(userId, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordThreadUser> GetUserAsync(ulong userId, bool withGuildUser = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordThreadUser(await _original.GetUserAsync(userId, withGuildUser, properties?.Original, cancellationToken));
    }
    public async IAsyncEnumerable<IDiscordThreadUser> GetUsersAsync(IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetUsersAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordThreadUser(original);
        }
    }
    public Task ModifyPermissionsAsync(IDiscordPermissionOverwriteProperties permissionOverwrite, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.ModifyPermissionsAsync(permissionOverwrite.Original, properties?.Original, cancellationToken);
    }
    public async Task<IEnumerable<IDiscordRestInvite>> GetInvitesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetInvitesAsync(properties?.Original, cancellationToken)).Select(x => new DiscordRestInvite(x));
    }
    public async Task<IDiscordRestInvite> CreateInviteAsync(IDiscordInviteProperties? inviteProperties = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestInvite(await _original.CreateInviteAsync(inviteProperties?.Original, properties?.Original, cancellationToken));
    }
    public Task DeletePermissionAsync(ulong overwriteId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeletePermissionAsync(overwriteId, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordGuildThread> CreateGuildThreadAsync(ulong messageId, IDiscordGuildThreadFromMessageProperties threadFromMessageProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildThread(await _original.CreateGuildThreadAsync(messageId, threadFromMessageProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildThread> CreateGuildThreadAsync(IDiscordGuildThreadProperties threadProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildThread(await _original.CreateGuildThreadAsync(threadProperties.Original, properties?.Original, cancellationToken));
    }
    public async IAsyncEnumerable<IDiscordGuildThread> GetPublicArchivedGuildThreadsAsync(IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetPublicArchivedGuildThreadsAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordGuildThread(original);
        }
    }
    public async IAsyncEnumerable<IDiscordGuildThread> GetPrivateArchivedGuildThreadsAsync(IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetPrivateArchivedGuildThreadsAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordGuildThread(original);
        }
    }
    public async IAsyncEnumerable<IDiscordGuildThread> GetJoinedPrivateArchivedGuildThreadsAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetJoinedPrivateArchivedGuildThreadsAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordGuildThread(original);
        }
    }
    public async Task<IDiscordIncomingWebhook> CreateWebhookAsync(IDiscordWebhookProperties webhookProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordIncomingWebhook(await _original.CreateWebhookAsync(webhookProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordWebhook>> GetChannelWebhooksAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetChannelWebhooksAsync(properties?.Original, cancellationToken)).Select(x => new DiscordWebhook(x)).ToList();
    }
    public async IAsyncEnumerable<IDiscordRestMessage> GetMessagesAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetMessagesAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordRestMessage(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetMessagesAroundAsync(ulong messageId, int? limit = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetMessagesAroundAsync(messageId, limit, properties?.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    }
    public async Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.GetMessageAsync(messageId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestMessage> SendMessageAsync(IDiscordMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.SendMessageAsync(message.Original, properties?.Original, cancellationToken));
    }
    public Task AddMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.AddMessageReactionAsync(messageId, emoji.Original, properties?.Original, cancellationToken);
    }
    public Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessageReactionAsync(messageId, emoji.Original, properties?.Original, cancellationToken);
    }
    public Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessageReactionAsync(messageId, emoji.Original, userId, properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordUser> GetMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordMessageReactionsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetMessageReactionsAsync(messageId, emoji.Original, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAllMessageReactionsAsync(messageId, properties?.Original, cancellationToken);
    }
    public Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAllMessageReactionsAsync(messageId, emoji.Original, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.ModifyMessageAsync(messageId, x => action(new DiscordMessageOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessageAsync(messageId, properties?.Original, cancellationToken);
    }
    public Task DeleteMessagesAsync(IEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessagesAsync(messageIds, properties?.Original, cancellationToken);
    }
    public Task DeleteMessagesAsync(IAsyncEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessagesAsync(messageIds, properties?.Original, cancellationToken);
    }
    public Task TriggerTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.TriggerTypingStateAsync(properties?.Original, cancellationToken);
    }
    public Task<IDisposable> EnterTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.EnterTypingStateAsync(properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetPinnedMessagesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetPinnedMessagesAsync(properties?.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    }
    public Task PinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.PinMessageAsync(messageId, properties?.Original, cancellationToken);
    }
    public Task UnpinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.UnpinMessageAsync(messageId, properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordUser> GetMessagePollAnswerVotersAsync(ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetMessagePollAnswerVotersAsync(messageId, answerId, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public async Task<IDiscordRestMessage> EndMessagePollAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.EndMessagePollAsync(messageId, properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.CreateGoogleCloudPlatformStorageBucketsAsync(buckets.Select(x => x.Original), properties?.Original, cancellationToken)).Select(x => new DiscordGoogleCloudPlatformStorageBucket(x)).ToList();
    }
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
    public async Task<IDiscordStageInstance> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordStageInstance(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordStageInstance> ModifyAsync(Action<IDiscordStageInstanceOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordStageInstance(await _original.ModifyAsync(x => action(new DiscordStageInstanceOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAsync(properties?.Original, cancellationToken);
    }
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
    public string? Description => _original.Description;
    public System.DateTimeOffset ScheduledStartTime => _original.ScheduledStartTime;
    public System.DateTimeOffset? ScheduledEndTime => _original.ScheduledEndTime;
    public NetCord.GuildScheduledEventPrivacyLevel PrivacyLevel => _original.PrivacyLevel;
    public NetCord.GuildScheduledEventStatus Status => _original.Status;
    public NetCord.GuildScheduledEventEntityType EntityType => _original.EntityType;
    public ulong? EntityId => _original.EntityId;
    public string? Location => _original.Location;
    public IDiscordUser? Creator => _original.Creator is null ? null : new DiscordUser(_original.Creator);
    public int? UserCount => _original.UserCount;
    public string? CoverImageHash => _original.CoverImageHash;
    public IDiscordGuildScheduledEventRecurrenceRule? RecurrenceRule => _original.RecurrenceRule is null ? null : new DiscordGuildScheduledEventRecurrenceRule(_original.RecurrenceRule);
    public bool HasCoverImage => _original.HasCoverImage;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordImageUrl? GetCoverImageUrl(NetCord.ImageFormat format) 
    {
        var temp = _original.GetCoverImageUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public async Task<IDiscordGuildScheduledEvent> GetAsync(bool withUserCount = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildScheduledEvent(await _original.GetAsync(withUserCount, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildScheduledEvent> ModifyAsync(Action<IDiscordGuildScheduledEventOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildScheduledEvent(await _original.ModifyAsync(x => action(new DiscordGuildScheduledEventOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAsync(properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordGuildScheduledEventUser> GetUsersAsync(IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetUsersAsync(paginationProperties?.Original, properties?.Original))
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
    public string? IconHash => _original.IconHash;
    public string? UnicodeEmoji => _original.UnicodeEmoji;
    public int Position => _original.Position;
    public NetCord.Permissions Permissions => _original.Permissions;
    public bool Managed => _original.Managed;
    public bool Mentionable => _original.Mentionable;
    public IDiscordRoleTags? Tags => _original.Tags is null ? null : new DiscordRoleTags(_original.Tags);
    public NetCord.RoleFlags Flags => _original.Flags;
    public ulong GuildId => _original.GuildId;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordImageUrl? GetIconUrl(NetCord.ImageFormat format) 
    {
        var temp = _original.GetIconUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public int? CompareTo(IDiscordRole other) 
    {
        return _original.CompareTo(other.Original);
    }
    public async Task<IDiscordRole> ModifyAsync(Action<IDiscordRoleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRole(await _original.ModifyAsync(x => action(new DiscordRoleOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAsync(properties?.Original, cancellationToken);
    }
}


public class DiscordGuildEmoji : IDiscordGuildEmoji 
{
    private readonly NetCord.GuildEmoji _original;
    public DiscordGuildEmoji(NetCord.GuildEmoji original)
    {
        _original = original;
    }
    public NetCord.GuildEmoji Original => _original;
    public IReadOnlyList<ulong>? AllowedRoles => _original.AllowedRoles;
    public ulong GuildId => _original.GuildId;
    public ulong Id => _original.Id;
    public IDiscordUser? Creator => _original.Creator is null ? null : new DiscordUser(_original.Creator);
    public bool? RequireColons => _original.RequireColons;
    public bool? Managed => _original.Managed;
    public bool? Available => _original.Available;
    public string Name => _original.Name;
    public bool Animated => _original.Animated;
    public async Task<IDiscordGuildEmoji> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildEmoji(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildEmoji> ModifyAsync(Action<IDiscordGuildEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildEmoji(await _original.ModifyAsync(x => action(new DiscordGuildEmojiOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAsync(properties?.Original, cancellationToken);
    }
    public IDiscordImageUrl GetImageUrl(NetCord.ImageFormat format) 
    {
        return new DiscordImageUrl(_original.GetImageUrl(format));
    }
}


public class DiscordGuildWelcomeScreen : IDiscordGuildWelcomeScreen 
{
    private readonly NetCord.GuildWelcomeScreen _original;
    public DiscordGuildWelcomeScreen(NetCord.GuildWelcomeScreen original)
    {
        _original = original;
    }
    public NetCord.GuildWelcomeScreen Original => _original;
    public string? Description => _original.Description;
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
    public IDiscordUser? Creator => _original.Creator is null ? null : new DiscordUser(_original.Creator);
    public ulong Id => _original.Id;
    public string Name => _original.Name;
    public string Description => _original.Description;
    public IReadOnlyList<string> Tags => _original.Tags;
    public NetCord.StickerFormat Format => _original.Format;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordGuildSticker> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildSticker(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildSticker> ModifyAsync(Action<IDiscordGuildStickerOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildSticker(await _original.ModifyAsync(x => action(new DiscordGuildStickerOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAsync(properties?.Original, cancellationToken);
    }
    public IDiscordImageUrl GetImageUrl(NetCord.ImageFormat format) 
    {
        return new DiscordImageUrl(_original.GetImageUrl(format));
    }
}


public class DiscordPartialGuildUser : IDiscordPartialGuildUser 
{
    private readonly NetCord.PartialGuildUser _original;
    public DiscordPartialGuildUser(NetCord.PartialGuildUser original)
    {
        _original = original;
    }
    public NetCord.PartialGuildUser Original => _original;
    public string? Nickname => _original.Nickname;
    public string? GuildAvatarHash => _original.GuildAvatarHash;
    public string? GuildBannerHash => _original.GuildBannerHash;
    public IReadOnlyList<ulong> RoleIds => _original.RoleIds;
    public ulong? HoistedRoleId => _original.HoistedRoleId;
    public System.DateTimeOffset JoinedAt => _original.JoinedAt;
    public System.DateTimeOffset? GuildBoostStart => _original.GuildBoostStart;
    public bool Deafened => _original.Deafened;
    public bool Muted => _original.Muted;
    public NetCord.GuildUserFlags GuildFlags => _original.GuildFlags;
    public bool? IsPending => _original.IsPending;
    public System.DateTimeOffset? TimeOutUntil => _original.TimeOutUntil;
    public IDiscordAvatarDecorationData? GuildAvatarDecorationData => _original.GuildAvatarDecorationData is null ? null : new DiscordAvatarDecorationData(_original.GuildAvatarDecorationData);
    public bool HasGuildAvatar => _original.HasGuildAvatar;
    public bool HasGuildBanner => _original.HasGuildBanner;
    public bool HasGuildAvatarDecoration => _original.HasGuildAvatarDecoration;
    public ulong Id => _original.Id;
    public string Username => _original.Username;
    public ushort Discriminator => _original.Discriminator;
    public string? GlobalName => _original.GlobalName;
    public string? AvatarHash => _original.AvatarHash;
    public bool IsBot => _original.IsBot;
    public bool? IsSystemUser => _original.IsSystemUser;
    public bool? MfaEnabled => _original.MfaEnabled;
    public string? BannerHash => _original.BannerHash;
    public NetCord.Color? AccentColor => _original.AccentColor;
    public string? Locale => _original.Locale;
    public bool? Verified => _original.Verified;
    public string? Email => _original.Email;
    public NetCord.UserFlags? Flags => _original.Flags;
    public NetCord.PremiumType? PremiumType => _original.PremiumType;
    public NetCord.UserFlags? PublicFlags => _original.PublicFlags;
    public IDiscordAvatarDecorationData? AvatarDecorationData => _original.AvatarDecorationData is null ? null : new DiscordAvatarDecorationData(_original.AvatarDecorationData);
    public bool HasAvatar => _original.HasAvatar;
    public bool HasBanner => _original.HasBanner;
    public bool HasAvatarDecoration => _original.HasAvatarDecoration;
    public IDiscordImageUrl DefaultAvatarUrl => new DiscordImageUrl(_original.DefaultAvatarUrl);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordImageUrl? GetGuildAvatarDecorationUrl() 
    {
        var temp = _original.GetGuildAvatarDecorationUrl();
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetAvatarUrl(NetCord.ImageFormat? format = default) 
    {
        var temp = _original.GetAvatarUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetBannerUrl(NetCord.ImageFormat? format = default) 
    {
        var temp = _original.GetBannerUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetAvatarDecorationUrl() 
    {
        var temp = _original.GetAvatarDecorationUrl();
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public async Task<IDiscordUser> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordUser(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordDMChannel> GetDMChannelAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordDMChannel(await _original.GetDMChannelAsync(properties?.Original, cancellationToken));
    }
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
    public IDiscordUser? User => _original.User is null ? null : new DiscordUser(_original.User);
    public ulong Id => _original.Id;
    public ulong? TargetId => _original.TargetId;
    public IReadOnlyDictionary<string, IDiscordAuditLogChange> Changes => _original.Changes.ToDictionary(kv => kv.Key, kv => (IDiscordAuditLogChange)new DiscordAuditLogChange(kv.Value));
    public ulong? UserId => _original.UserId;
    public NetCord.AuditLogEvent ActionType => _original.ActionType;
    public IDiscordAuditLogEntryInfo? Options => _original.Options is null ? null : new DiscordAuditLogEntryInfo(_original.Options);
    public string? Reason => _original.Reason;
    public ulong GuildId => _original.GuildId;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public bool TryGetChange<TObjectParam, TValueParam>(Expression<Func<TObjectParam, TValueParam?>> expression, out IDiscordAuditLogChange change) where TObjectParam : NetCord.JsonModels.JsonEntity
    {
        var result = _original.TryGetChange<TObjectParam, TValueParam>(expression, out var changeTemp);
        change = new DiscordAuditLogChange(changeTemp);
        return result;
    }
    public IDiscordAuditLogChange<TValueParam> GetChange<TObjectParam, TValueParam>(Expression<Func<TObjectParam, TValueParam?>> expression) where TObjectParam : NetCord.JsonModels.JsonEntity
    {
        return new DiscordAuditLogChange<TValueParam>(_original.GetChange<TObjectParam, TValueParam>(expression));
    }
    public bool TryGetChange<TObjectParam, TValueParam>(Expression<Func<TObjectParam, TValueParam?>> expression, JsonTypeInfo<TValueParam> jsonTypeInfo, out IDiscordAuditLogChange change) where TObjectParam : NetCord.JsonModels.JsonEntity
    {
        var result = _original.TryGetChange<TObjectParam, TValueParam>(expression, jsonTypeInfo, out var changeTemp);
        change = new DiscordAuditLogChange(changeTemp);
        return result;
    }
    public IDiscordAuditLogChange<TValueParam> GetChange<TObjectParam, TValueParam>(Expression<Func<TObjectParam, TValueParam?>> expression, JsonTypeInfo<TValueParam> jsonTypeInfo) where TObjectParam : NetCord.JsonModels.JsonEntity
    {
        return new DiscordAuditLogChange<TValueParam>(_original.GetChange<TObjectParam, TValueParam>(expression, jsonTypeInfo));
    }
}


public class DiscordGuildAuditLogPaginationProperties : IDiscordGuildAuditLogPaginationProperties 
{
    private readonly NetCord.Rest.GuildAuditLogPaginationProperties _original;
    public DiscordGuildAuditLogPaginationProperties(NetCord.Rest.GuildAuditLogPaginationProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildAuditLogPaginationProperties Original => _original;
    public ulong? UserId { get { return _original.UserId; } set { _original.UserId = value; } }
    public NetCord.AuditLogEvent? ActionType { get { return _original.ActionType; } set { _original.ActionType = value; } }
    public ulong? From { get { return _original.From; } set { _original.From = value; } }
    public NetCord.Rest.PaginationDirection? Direction { get { return _original.Direction; } set { _original.Direction = value; } }
    public int? BatchSize { get { return _original.BatchSize; } set { _original.BatchSize = value; } }
    public IDiscordGuildAuditLogPaginationProperties WithUserId(ulong? userId) 
    {
        return new DiscordGuildAuditLogPaginationProperties(_original.WithUserId(userId));
    }
    public IDiscordGuildAuditLogPaginationProperties WithActionType(NetCord.AuditLogEvent? actionType) 
    {
        return new DiscordGuildAuditLogPaginationProperties(_original.WithActionType(actionType));
    }
    public IDiscordGuildAuditLogPaginationProperties WithFrom(ulong? from) 
    {
        return new DiscordGuildAuditLogPaginationProperties(_original.WithFrom(from));
    }
    public IDiscordGuildAuditLogPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction) 
    {
        return new DiscordGuildAuditLogPaginationProperties(_original.WithDirection(direction));
    }
    public IDiscordGuildAuditLogPaginationProperties WithBatchSize(int? batchSize) 
    {
        return new DiscordGuildAuditLogPaginationProperties(_original.WithBatchSize(batchSize));
    }
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
    public async Task<IDiscordAutoModerationRule> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordAutoModerationRule(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordAutoModerationRule> ModifyAsync(Action<IDiscordAutoModerationRuleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordAutoModerationRule(await _original.ModifyAsync(x => action(new DiscordAutoModerationRuleOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAsync(properties?.Original, cancellationToken);
    }
}


public class DiscordAutoModerationRuleProperties : IDiscordAutoModerationRuleProperties 
{
    private readonly NetCord.AutoModerationRuleProperties _original;
    public DiscordAutoModerationRuleProperties(NetCord.AutoModerationRuleProperties original)
    {
        _original = original;
    }
    public NetCord.AutoModerationRuleProperties Original => _original;
    public string Name { get { return _original.Name; } set { _original.Name = value; } }
    public NetCord.AutoModerationRuleEventType EventType { get { return _original.EventType; } set { _original.EventType = value; } }
    public NetCord.AutoModerationRuleTriggerType TriggerType { get { return _original.TriggerType; } set { _original.TriggerType = value; } }
    public IDiscordAutoModerationRuleTriggerMetadataProperties? TriggerMetadata { get { return _original.TriggerMetadata is null ? null : new DiscordAutoModerationRuleTriggerMetadataProperties(_original.TriggerMetadata); } set { _original.TriggerMetadata = value?.Original; } }
    public IEnumerable<IDiscordAutoModerationActionProperties> Actions { get { return _original.Actions.Select(x => new DiscordAutoModerationActionProperties(x)); } set { _original.Actions = value.Select(x => x.Original); } }
    public bool Enabled { get { return _original.Enabled; } set { _original.Enabled = value; } }
    public IEnumerable<ulong>? ExemptRoles { get { return _original.ExemptRoles; } set { _original.ExemptRoles = value; } }
    public IEnumerable<ulong>? ExemptChannels { get { return _original.ExemptChannels; } set { _original.ExemptChannels = value; } }
    public IDiscordAutoModerationRuleProperties WithName(string name) 
    {
        return new DiscordAutoModerationRuleProperties(_original.WithName(name));
    }
    public IDiscordAutoModerationRuleProperties WithEventType(NetCord.AutoModerationRuleEventType eventType) 
    {
        return new DiscordAutoModerationRuleProperties(_original.WithEventType(eventType));
    }
    public IDiscordAutoModerationRuleProperties WithTriggerType(NetCord.AutoModerationRuleTriggerType triggerType) 
    {
        return new DiscordAutoModerationRuleProperties(_original.WithTriggerType(triggerType));
    }
    public IDiscordAutoModerationRuleProperties WithTriggerMetadata(IDiscordAutoModerationRuleTriggerMetadataProperties? triggerMetadata) 
    {
        return new DiscordAutoModerationRuleProperties(_original.WithTriggerMetadata(triggerMetadata?.Original));
    }
    public IDiscordAutoModerationRuleProperties WithActions(IEnumerable<IDiscordAutoModerationActionProperties> actions) 
    {
        return new DiscordAutoModerationRuleProperties(_original.WithActions(actions.Select(x => x.Original)));
    }
    public IDiscordAutoModerationRuleProperties AddActions(IEnumerable<IDiscordAutoModerationActionProperties> actions) 
    {
        return new DiscordAutoModerationRuleProperties(_original.AddActions(actions.Select(x => x.Original)));
    }
    public IDiscordAutoModerationRuleProperties AddActions(IDiscordAutoModerationActionProperties[] actions) 
    {
        return new DiscordAutoModerationRuleProperties(_original.AddActions(actions.Select(x => x.Original).ToArray()));
    }
    public IDiscordAutoModerationRuleProperties WithEnabled(bool enabled = true) 
    {
        return new DiscordAutoModerationRuleProperties(_original.WithEnabled(enabled));
    }
    public IDiscordAutoModerationRuleProperties WithExemptRoles(IEnumerable<ulong>? exemptRoles) 
    {
        return new DiscordAutoModerationRuleProperties(_original.WithExemptRoles(exemptRoles));
    }
    public IDiscordAutoModerationRuleProperties AddExemptRoles(IEnumerable<ulong> exemptRoles) 
    {
        return new DiscordAutoModerationRuleProperties(_original.AddExemptRoles(exemptRoles));
    }
    public IDiscordAutoModerationRuleProperties AddExemptRoles(ulong[] exemptRoles) 
    {
        return new DiscordAutoModerationRuleProperties(_original.AddExemptRoles(exemptRoles));
    }
    public IDiscordAutoModerationRuleProperties WithExemptChannels(IEnumerable<ulong>? exemptChannels) 
    {
        return new DiscordAutoModerationRuleProperties(_original.WithExemptChannels(exemptChannels));
    }
    public IDiscordAutoModerationRuleProperties AddExemptChannels(IEnumerable<ulong> exemptChannels) 
    {
        return new DiscordAutoModerationRuleProperties(_original.AddExemptChannels(exemptChannels));
    }
    public IDiscordAutoModerationRuleProperties AddExemptChannels(ulong[] exemptChannels) 
    {
        return new DiscordAutoModerationRuleProperties(_original.AddExemptChannels(exemptChannels));
    }
}


public class DiscordAutoModerationRuleOptions : IDiscordAutoModerationRuleOptions 
{
    private readonly NetCord.AutoModerationRuleOptions _original;
    public DiscordAutoModerationRuleOptions(NetCord.AutoModerationRuleOptions original)
    {
        _original = original;
    }
    public NetCord.AutoModerationRuleOptions Original => _original;
    public string? Name { get { return _original.Name; } set { _original.Name = value; } }
    public NetCord.AutoModerationRuleEventType? EventType { get { return _original.EventType; } set { _original.EventType = value; } }
    public IDiscordAutoModerationRuleTriggerMetadataProperties? TriggerMetadata { get { return _original.TriggerMetadata is null ? null : new DiscordAutoModerationRuleTriggerMetadataProperties(_original.TriggerMetadata); } set { _original.TriggerMetadata = value?.Original; } }
    public IEnumerable<IDiscordAutoModerationActionProperties>? Actions { get { return _original.Actions is null ? null : _original.Actions.Select(x => new DiscordAutoModerationActionProperties(x)); } set { _original.Actions = value?.Select(x => x.Original); } }
    public bool? Enabled { get { return _original.Enabled; } set { _original.Enabled = value; } }
    public IEnumerable<ulong>? ExemptRoles { get { return _original.ExemptRoles; } set { _original.ExemptRoles = value; } }
    public IEnumerable<ulong>? ExemptChannels { get { return _original.ExemptChannels; } set { _original.ExemptChannels = value; } }
    public IDiscordAutoModerationRuleOptions WithName(string name) 
    {
        return new DiscordAutoModerationRuleOptions(_original.WithName(name));
    }
    public IDiscordAutoModerationRuleOptions WithEventType(NetCord.AutoModerationRuleEventType? eventType) 
    {
        return new DiscordAutoModerationRuleOptions(_original.WithEventType(eventType));
    }
    public IDiscordAutoModerationRuleOptions WithTriggerMetadata(IDiscordAutoModerationRuleTriggerMetadataProperties? triggerMetadata) 
    {
        return new DiscordAutoModerationRuleOptions(_original.WithTriggerMetadata(triggerMetadata?.Original));
    }
    public IDiscordAutoModerationRuleOptions WithActions(IEnumerable<IDiscordAutoModerationActionProperties>? actions) 
    {
        return new DiscordAutoModerationRuleOptions(_original.WithActions(actions?.Select(x => x.Original)));
    }
    public IDiscordAutoModerationRuleOptions AddActions(IEnumerable<IDiscordAutoModerationActionProperties> actions) 
    {
        return new DiscordAutoModerationRuleOptions(_original.AddActions(actions.Select(x => x.Original)));
    }
    public IDiscordAutoModerationRuleOptions AddActions(IDiscordAutoModerationActionProperties[] actions) 
    {
        return new DiscordAutoModerationRuleOptions(_original.AddActions(actions.Select(x => x.Original).ToArray()));
    }
    public IDiscordAutoModerationRuleOptions WithEnabled(bool? enabled = true) 
    {
        return new DiscordAutoModerationRuleOptions(_original.WithEnabled(enabled));
    }
    public IDiscordAutoModerationRuleOptions WithExemptRoles(IEnumerable<ulong>? exemptRoles) 
    {
        return new DiscordAutoModerationRuleOptions(_original.WithExemptRoles(exemptRoles));
    }
    public IDiscordAutoModerationRuleOptions AddExemptRoles(IEnumerable<ulong> exemptRoles) 
    {
        return new DiscordAutoModerationRuleOptions(_original.AddExemptRoles(exemptRoles));
    }
    public IDiscordAutoModerationRuleOptions AddExemptRoles(ulong[] exemptRoles) 
    {
        return new DiscordAutoModerationRuleOptions(_original.AddExemptRoles(exemptRoles));
    }
    public IDiscordAutoModerationRuleOptions WithExemptChannels(IEnumerable<ulong>? exemptChannels) 
    {
        return new DiscordAutoModerationRuleOptions(_original.WithExemptChannels(exemptChannels));
    }
    public IDiscordAutoModerationRuleOptions AddExemptChannels(IEnumerable<ulong> exemptChannels) 
    {
        return new DiscordAutoModerationRuleOptions(_original.AddExemptChannels(exemptChannels));
    }
    public IDiscordAutoModerationRuleOptions AddExemptChannels(ulong[] exemptChannels) 
    {
        return new DiscordAutoModerationRuleOptions(_original.AddExemptChannels(exemptChannels));
    }
}


public class DiscordGuildEmojiProperties : IDiscordGuildEmojiProperties 
{
    private readonly NetCord.Rest.GuildEmojiProperties _original;
    public DiscordGuildEmojiProperties(NetCord.Rest.GuildEmojiProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildEmojiProperties Original => _original;
    public string Name { get { return _original.Name; } set { _original.Name = value; } }
    public NetCord.Rest.ImageProperties Image { get { return _original.Image; } set { _original.Image = value; } }
    public IEnumerable<ulong>? AllowedRoles { get { return _original.AllowedRoles; } set { _original.AllowedRoles = value; } }
    public IDiscordGuildEmojiProperties WithName(string name) 
    {
        return new DiscordGuildEmojiProperties(_original.WithName(name));
    }
    public IDiscordGuildEmojiProperties WithImage(NetCord.Rest.ImageProperties image) 
    {
        return new DiscordGuildEmojiProperties(_original.WithImage(image));
    }
    public IDiscordGuildEmojiProperties WithAllowedRoles(IEnumerable<ulong>? allowedRoles) 
    {
        return new DiscordGuildEmojiProperties(_original.WithAllowedRoles(allowedRoles));
    }
    public IDiscordGuildEmojiProperties AddAllowedRoles(IEnumerable<ulong> allowedRoles) 
    {
        return new DiscordGuildEmojiProperties(_original.AddAllowedRoles(allowedRoles));
    }
    public IDiscordGuildEmojiProperties AddAllowedRoles(ulong[] allowedRoles) 
    {
        return new DiscordGuildEmojiProperties(_original.AddAllowedRoles(allowedRoles));
    }
}


public class DiscordGuildEmojiOptions : IDiscordGuildEmojiOptions 
{
    private readonly NetCord.Rest.GuildEmojiOptions _original;
    public DiscordGuildEmojiOptions(NetCord.Rest.GuildEmojiOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildEmojiOptions Original => _original;
    public string? Name { get { return _original.Name; } set { _original.Name = value; } }
    public IEnumerable<ulong>? AllowedRoles { get { return _original.AllowedRoles; } set { _original.AllowedRoles = value; } }
    public IDiscordGuildEmojiOptions WithName(string name) 
    {
        return new DiscordGuildEmojiOptions(_original.WithName(name));
    }
    public IDiscordGuildEmojiOptions WithAllowedRoles(IEnumerable<ulong>? allowedRoles) 
    {
        return new DiscordGuildEmojiOptions(_original.WithAllowedRoles(allowedRoles));
    }
    public IDiscordGuildEmojiOptions AddAllowedRoles(IEnumerable<ulong> allowedRoles) 
    {
        return new DiscordGuildEmojiOptions(_original.AddAllowedRoles(allowedRoles));
    }
    public IDiscordGuildEmojiOptions AddAllowedRoles(ulong[] allowedRoles) 
    {
        return new DiscordGuildEmojiOptions(_original.AddAllowedRoles(allowedRoles));
    }
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
    public string? IconHash => _original.IconHash;
    public bool HasSplash => _original.HasSplash;
    public string? SplashHash => _original.SplashHash;
    public bool HasDiscoverySplash => _original.HasDiscoverySplash;
    public string? DiscoverySplashHash => _original.DiscoverySplashHash;
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
    public ImmutableDictionary<ulong, IDiscordRole> Roles { get { return _original.Roles.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordRole)new DiscordRole(kv.Value)); } set { _original.Roles = value.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.Original); } }
    public ImmutableDictionary<ulong, IDiscordGuildEmoji> Emojis { get { return _original.Emojis.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordGuildEmoji)new DiscordGuildEmoji(kv.Value)); } set { _original.Emojis = value.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.Original); } }
    public IReadOnlyList<string> Features => _original.Features;
    public NetCord.MfaLevel MfaLevel => _original.MfaLevel;
    public ulong? ApplicationId => _original.ApplicationId;
    public ulong? SystemChannelId => _original.SystemChannelId;
    public NetCord.Rest.SystemChannelFlags SystemChannelFlags => _original.SystemChannelFlags;
    public ulong? RulesChannelId => _original.RulesChannelId;
    public int? MaxPresences => _original.MaxPresences;
    public int? MaxUsers => _original.MaxUsers;
    public string? VanityUrlCode => _original.VanityUrlCode;
    public string? Description => _original.Description;
    public bool HasBanner => _original.HasBanner;
    public string? BannerHash => _original.BannerHash;
    public int PremiumTier => _original.PremiumTier;
    public int? PremiumSubscriptionCount => _original.PremiumSubscriptionCount;
    public string PreferredLocale => _original.PreferredLocale;
    public ulong? PublicUpdatesChannelId => _original.PublicUpdatesChannelId;
    public int? MaxVideoChannelUsers => _original.MaxVideoChannelUsers;
    public int? MaxStageVideoChannelUsers => _original.MaxStageVideoChannelUsers;
    public int? ApproximateUserCount => _original.ApproximateUserCount;
    public int? ApproximatePresenceCount => _original.ApproximatePresenceCount;
    public IDiscordGuildWelcomeScreen? WelcomeScreen => _original.WelcomeScreen is null ? null : new DiscordGuildWelcomeScreen(_original.WelcomeScreen);
    public NetCord.NsfwLevel NsfwLevel => _original.NsfwLevel;
    public ImmutableDictionary<ulong, IDiscordGuildSticker> Stickers { get { return _original.Stickers.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordGuildSticker)new DiscordGuildSticker(kv.Value)); } set { _original.Stickers = value.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.Original); } }
    public bool PremiumProgressBarEnabled => _original.PremiumProgressBarEnabled;
    public ulong? SafetyAlertsChannelId => _original.SafetyAlertsChannelId;
    public IDiscordRole? EveryoneRole => _original.EveryoneRole is null ? null : new DiscordRole(_original.EveryoneRole);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public int? Compare(IDiscordPartialGuildUser x, IDiscordPartialGuildUser y) 
    {
        return _original.Compare(x.Original, y.Original);
    }
    public IDiscordImageUrl? GetIconUrl(NetCord.ImageFormat? format = default) 
    {
        var temp = _original.GetIconUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetSplashUrl(NetCord.ImageFormat format) 
    {
        var temp = _original.GetSplashUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetDiscoverySplashUrl(NetCord.ImageFormat format) 
    {
        var temp = _original.GetDiscoverySplashUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetBannerUrl(NetCord.ImageFormat? format = default) 
    {
        var temp = _original.GetBannerUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl GetWidgetUrl(NetCord.GuildWidgetStyle? style = default, string? hostname = null, NetCord.ApiVersion? version = default) 
    {
        return new DiscordImageUrl(_original.GetWidgetUrl(style, hostname, version));
    }
    public async IAsyncEnumerable<IDiscordRestAuditLogEntry> GetAuditLogAsync(IDiscordGuildAuditLogPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetAuditLogAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordRestAuditLogEntry(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordAutoModerationRule>> GetAutoModerationRulesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetAutoModerationRulesAsync(properties?.Original, cancellationToken)).Select(x => new DiscordAutoModerationRule(x)).ToList();
    }
    public async Task<IDiscordAutoModerationRule> GetAutoModerationRuleAsync(ulong autoModerationRuleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordAutoModerationRule(await _original.GetAutoModerationRuleAsync(autoModerationRuleId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordAutoModerationRule> CreateAutoModerationRuleAsync(IDiscordAutoModerationRuleProperties autoModerationRuleProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordAutoModerationRule(await _original.CreateAutoModerationRuleAsync(autoModerationRuleProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordAutoModerationRule> ModifyAutoModerationRuleAsync(ulong autoModerationRuleId, Action<IDiscordAutoModerationRuleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordAutoModerationRule(await _original.ModifyAutoModerationRuleAsync(autoModerationRuleId, x => action(new DiscordAutoModerationRuleOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteAutoModerationRuleAsync(ulong autoModerationRuleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAutoModerationRuleAsync(autoModerationRuleId, properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordGuildEmoji>> GetEmojisAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetEmojisAsync(properties?.Original, cancellationToken)).Select(x => new DiscordGuildEmoji(x)).ToList();
    }
    public async Task<IDiscordGuildEmoji> GetEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildEmoji(await _original.GetEmojiAsync(emojiId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildEmoji> CreateEmojiAsync(IDiscordGuildEmojiProperties guildEmojiProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildEmoji(await _original.CreateEmojiAsync(guildEmojiProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildEmoji> ModifyEmojiAsync(ulong emojiId, Action<IDiscordGuildEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildEmoji(await _original.ModifyEmojiAsync(emojiId, x => action(new DiscordGuildEmojiOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteEmojiAsync(emojiId, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordRestGuild> GetAsync(bool withCounts = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestGuild(await _original.GetAsync(withCounts, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildPreview> GetPreviewAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildPreview(await _original.GetPreviewAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestGuild> ModifyAsync(Action<IDiscordGuildOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestGuild(await _original.ModifyAsync(x => action(new DiscordGuildOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAsync(properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordGuildChannel>> GetChannelsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetChannelsAsync(properties?.Original, cancellationToken)).Select(x => new DiscordGuildChannel(x)).ToList();
    }
    public async Task<IDiscordGuildChannel> CreateChannelAsync(IDiscordGuildChannelProperties channelProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildChannel(await _original.CreateChannelAsync(channelProperties.Original, properties?.Original, cancellationToken));
    }
    public Task ModifyChannelPositionsAsync(IEnumerable<IDiscordGuildChannelPositionProperties> positions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.ModifyChannelPositionsAsync(positions.Select(x => x.Original), properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordGuildThread>> GetActiveThreadsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetActiveThreadsAsync(properties?.Original, cancellationToken)).Select(x => new DiscordGuildThread(x)).ToList();
    }
    public async Task<IDiscordGuildUser> GetUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildUser(await _original.GetUserAsync(userId, properties?.Original, cancellationToken));
    }
    public async IAsyncEnumerable<IDiscordGuildUser> GetUsersAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetUsersAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordGuildUser(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordGuildUser>> FindUserAsync(string name, int limit, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.FindUserAsync(name, limit, properties?.Original, cancellationToken)).Select(x => new DiscordGuildUser(x)).ToList();
    }
    public async Task<IDiscordGuildUser?> AddUserAsync(ulong userId, IDiscordGuildUserProperties userProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        var temp = await _original.AddUserAsync(userId, userProperties.Original, properties?.Original, cancellationToken);
        return temp is null ? null : new DiscordGuildUser(temp);
    }
    public async Task<IDiscordGuildUser> ModifyUserAsync(ulong userId, Action<IDiscordGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildUser(await _original.ModifyUserAsync(userId, x => action(new DiscordGuildUserOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildUser> ModifyCurrentUserAsync(Action<IDiscordCurrentGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildUser(await _original.ModifyCurrentUserAsync(x => action(new DiscordCurrentGuildUserOptions(x)), properties?.Original, cancellationToken));
    }
    public Task AddUserRoleAsync(ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.AddUserRoleAsync(userId, roleId, properties?.Original, cancellationToken);
    }
    public Task RemoveUserRoleAsync(ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.RemoveUserRoleAsync(userId, roleId, properties?.Original, cancellationToken);
    }
    public Task KickUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.KickUserAsync(userId, properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordGuildBan> GetBansAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetBansAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordGuildBan(original);
        }
    }
    public async Task<IDiscordGuildBan> GetBanAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildBan(await _original.GetBanAsync(userId, properties?.Original, cancellationToken));
    }
    public Task BanUserAsync(ulong userId, int deleteMessageSeconds = 0, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.BanUserAsync(userId, deleteMessageSeconds, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordGuildBulkBan> BanUsersAsync(IEnumerable<ulong> userIds, int deleteMessageSeconds = 0, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildBulkBan(await _original.BanUsersAsync(userIds, deleteMessageSeconds, properties?.Original, cancellationToken));
    }
    public Task UnbanUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.UnbanUserAsync(userId, properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordRole>> GetRolesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetRolesAsync(properties?.Original, cancellationToken)).Select(x => new DiscordRole(x)).ToList();
    }
    public async Task<IDiscordRole> GetRoleAsync(ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRole(await _original.GetRoleAsync(roleId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRole> CreateRoleAsync(IDiscordRoleProperties guildRoleProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRole(await _original.CreateRoleAsync(guildRoleProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordRole>> ModifyRolePositionsAsync(IEnumerable<IDiscordRolePositionProperties> positions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.ModifyRolePositionsAsync(positions.Select(x => x.Original), properties?.Original, cancellationToken)).Select(x => new DiscordRole(x)).ToList();
    }
    public async Task<IDiscordRole> ModifyRoleAsync(ulong roleId, Action<IDiscordRoleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRole(await _original.ModifyRoleAsync(roleId, x => action(new DiscordRoleOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteRoleAsync(ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteRoleAsync(roleId, properties?.Original, cancellationToken);
    }
    public Task<NetCord.MfaLevel> ModifyMfaLevelAsync(NetCord.MfaLevel mfaLevel, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.ModifyMfaLevelAsync(mfaLevel, properties?.Original, cancellationToken);
    }
    public Task<int> GetPruneCountAsync(int days, IEnumerable<ulong>? roles = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.GetPruneCountAsync(days, roles, properties?.Original, cancellationToken);
    }
    public Task<int?> PruneAsync(IDiscordGuildPruneProperties pruneProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.PruneAsync(pruneProperties.Original, properties?.Original, cancellationToken);
    }
    public async Task<IEnumerable<IDiscordVoiceRegion>> GetVoiceRegionsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetVoiceRegionsAsync(properties?.Original, cancellationToken)).Select(x => new DiscordVoiceRegion(x));
    }
    public async Task<IEnumerable<IDiscordRestInvite>> GetInvitesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetInvitesAsync(properties?.Original, cancellationToken)).Select(x => new DiscordRestInvite(x));
    }
    public async Task<IReadOnlyList<IDiscordIntegration>> GetIntegrationsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetIntegrationsAsync(properties?.Original, cancellationToken)).Select(x => new DiscordIntegration(x)).ToList();
    }
    public Task DeleteIntegrationAsync(ulong integrationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteIntegrationAsync(integrationId, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordGuildWidgetSettings> GetWidgetSettingsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildWidgetSettings(await _original.GetWidgetSettingsAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildWidgetSettings> ModifyWidgetSettingsAsync(Action<IDiscordGuildWidgetSettingsOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildWidgetSettings(await _original.ModifyWidgetSettingsAsync(x => action(new DiscordGuildWidgetSettingsOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildWidget> GetWidgetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildWidget(await _original.GetWidgetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildVanityInvite> GetVanityInviteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildVanityInvite(await _original.GetVanityInviteAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildWelcomeScreen> GetWelcomeScreenAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildWelcomeScreen(await _original.GetWelcomeScreenAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildWelcomeScreen> ModifyWelcomeScreenAsync(Action<IDiscordGuildWelcomeScreenOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildWelcomeScreen(await _original.ModifyWelcomeScreenAsync(x => action(new DiscordGuildWelcomeScreenOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildOnboarding> GetOnboardingAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildOnboarding(await _original.GetOnboardingAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildOnboarding> ModifyOnboardingAsync(Action<IDiscordGuildOnboardingOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildOnboarding(await _original.ModifyOnboardingAsync(x => action(new DiscordGuildOnboardingOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordGuildScheduledEvent>> GetScheduledEventsAsync(bool withUserCount = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetScheduledEventsAsync(withUserCount, properties?.Original, cancellationToken)).Select(x => new DiscordGuildScheduledEvent(x)).ToList();
    }
    public async Task<IDiscordGuildScheduledEvent> CreateScheduledEventAsync(IDiscordGuildScheduledEventProperties guildScheduledEventProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildScheduledEvent(await _original.CreateScheduledEventAsync(guildScheduledEventProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildScheduledEvent> GetScheduledEventAsync(ulong scheduledEventId, bool withUserCount = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildScheduledEvent(await _original.GetScheduledEventAsync(scheduledEventId, withUserCount, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildScheduledEvent> ModifyScheduledEventAsync(ulong scheduledEventId, Action<IDiscordGuildScheduledEventOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildScheduledEvent(await _original.ModifyScheduledEventAsync(scheduledEventId, x => action(new DiscordGuildScheduledEventOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteScheduledEventAsync(ulong scheduledEventId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteScheduledEventAsync(scheduledEventId, properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordGuildScheduledEventUser> GetScheduledEventUsersAsync(ulong scheduledEventId, IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetScheduledEventUsersAsync(scheduledEventId, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordGuildScheduledEventUser(original);
        }
    }
    public async Task<IEnumerable<IDiscordGuildTemplate>> GetTemplatesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetTemplatesAsync(properties?.Original, cancellationToken)).Select(x => new DiscordGuildTemplate(x));
    }
    public async Task<IDiscordGuildTemplate> CreateTemplateAsync(IDiscordGuildTemplateProperties guildTemplateProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildTemplate(await _original.CreateTemplateAsync(guildTemplateProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildTemplate> SyncTemplateAsync(string templateCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildTemplate(await _original.SyncTemplateAsync(templateCode, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildTemplate> ModifyTemplateAsync(string templateCode, Action<IDiscordGuildTemplateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildTemplate(await _original.ModifyTemplateAsync(templateCode, x => action(new DiscordGuildTemplateOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildTemplate> DeleteTemplateAsync(string templateCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildTemplate(await _original.DeleteTemplateAsync(templateCode, properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordGuildApplicationCommand>> GetApplicationCommandsAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetApplicationCommandsAsync(applicationId, properties?.Original, cancellationToken)).Select(x => new DiscordGuildApplicationCommand(x)).ToList();
    }
    public async Task<IDiscordGuildApplicationCommand> CreateApplicationCommandAsync(ulong applicationId, IDiscordApplicationCommandProperties applicationCommandProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildApplicationCommand(await _original.CreateApplicationCommandAsync(applicationId, applicationCommandProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildApplicationCommand> GetApplicationCommandAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildApplicationCommand(await _original.GetApplicationCommandAsync(applicationId, commandId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildApplicationCommand> ModifyApplicationCommandAsync(ulong applicationId, ulong commandId, Action<IDiscordApplicationCommandOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildApplicationCommand(await _original.ModifyApplicationCommandAsync(applicationId, commandId, x => action(new DiscordApplicationCommandOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteApplicationCommandAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteApplicationCommandAsync(applicationId, commandId, properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordGuildApplicationCommand>> BulkOverwriteApplicationCommandsAsync(ulong applicationId, IEnumerable<IDiscordApplicationCommandProperties> commands, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.BulkOverwriteApplicationCommandsAsync(applicationId, commands.Select(x => x.Original), properties?.Original, cancellationToken)).Select(x => new DiscordGuildApplicationCommand(x)).ToList();
    }
    public async Task<IReadOnlyList<IDiscordApplicationCommandGuildPermissions>> GetApplicationCommandsPermissionsAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetApplicationCommandsPermissionsAsync(applicationId, properties?.Original, cancellationToken)).Select(x => new DiscordApplicationCommandGuildPermissions(x)).ToList();
    }
    public async Task<IDiscordApplicationCommandGuildPermissions> GetApplicationCommandPermissionsAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationCommandGuildPermissions(await _original.GetApplicationCommandPermissionsAsync(applicationId, commandId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordApplicationCommandGuildPermissions> OverwriteApplicationCommandPermissionsAsync(ulong applicationId, ulong commandId, IEnumerable<IDiscordApplicationCommandGuildPermissionProperties> newPermissions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationCommandGuildPermissions(await _original.OverwriteApplicationCommandPermissionsAsync(applicationId, commandId, newPermissions.Select(x => x.Original), properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordGuildSticker>> GetStickersAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetStickersAsync(properties?.Original, cancellationToken)).Select(x => new DiscordGuildSticker(x)).ToList();
    }
    public async Task<IDiscordGuildSticker> GetStickerAsync(ulong stickerId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildSticker(await _original.GetStickerAsync(stickerId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildSticker> CreateStickerAsync(IDiscordGuildStickerProperties sticker, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildSticker(await _original.CreateStickerAsync(sticker.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildSticker> ModifyStickerAsync(ulong stickerId, Action<IDiscordGuildStickerOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildSticker(await _original.ModifyStickerAsync(stickerId, x => action(new DiscordGuildStickerOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteStickerAsync(ulong stickerId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteStickerAsync(stickerId, properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordGuildUserInfo> SearchUsersAsync(IDiscordGuildUsersSearchPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.SearchUsersAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordGuildUserInfo(original);
        }
    }
    public async Task<IDiscordGuildUser> GetCurrentUserGuildUserAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildUser(await _original.GetCurrentUserGuildUserAsync(properties?.Original, cancellationToken));
    }
    public Task LeaveAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.LeaveAsync(properties?.Original, cancellationToken);
    }
    public async Task<IDiscordVoiceState> GetCurrentUserVoiceStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordVoiceState(await _original.GetCurrentUserVoiceStateAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordVoiceState> GetUserVoiceStateAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordVoiceState(await _original.GetUserVoiceStateAsync(userId, properties?.Original, cancellationToken));
    }
    public Task ModifyCurrentUserVoiceStateAsync(Action<IDiscordCurrentUserVoiceStateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.ModifyCurrentUserVoiceStateAsync(x => action(new DiscordCurrentUserVoiceStateOptions(x)), properties?.Original, cancellationToken);
    }
    public Task ModifyUserVoiceStateAsync(ulong channelId, ulong userId, Action<IDiscordVoiceStateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.ModifyUserVoiceStateAsync(channelId, userId, x => action(new DiscordVoiceStateOptions(x)), properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordWebhook>> GetWebhooksAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetWebhooksAsync(properties?.Original, cancellationToken)).Select(x => new DiscordWebhook(x)).ToList();
    }
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
    public string? IconHash => _original.IconHash;
    public string? SplashHash => _original.SplashHash;
    public string? DiscoverySplashHash => _original.DiscoverySplashHash;
    public ImmutableDictionary<ulong, IDiscordGuildEmoji> Emojis => _original.Emojis.ToImmutableDictionary(kv => kv.Key, kv => (IDiscordGuildEmoji)new DiscordGuildEmoji(kv.Value));
    public IReadOnlyList<string> Features => _original.Features;
    public int ApproximateUserCount => _original.ApproximateUserCount;
    public int ApproximatePresenceCount => _original.ApproximatePresenceCount;
    public string? Description => _original.Description;
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
    public string? Name { get { return _original.Name; } set { _original.Name = value; } }
    public NetCord.VerificationLevel? VerificationLevel { get { return _original.VerificationLevel; } set { _original.VerificationLevel = value; } }
    public NetCord.DefaultMessageNotificationLevel? DefaultMessageNotificationLevel { get { return _original.DefaultMessageNotificationLevel; } set { _original.DefaultMessageNotificationLevel = value; } }
    public NetCord.ContentFilter? ContentFilter { get { return _original.ContentFilter; } set { _original.ContentFilter = value; } }
    public ulong? AfkChannelId { get { return _original.AfkChannelId; } set { _original.AfkChannelId = value; } }
    public int? AfkTimeout { get { return _original.AfkTimeout; } set { _original.AfkTimeout = value; } }
    public NetCord.Rest.ImageProperties? Icon { get { return _original.Icon; } set { _original.Icon = value; } }
    public ulong? OwnerId { get { return _original.OwnerId; } set { _original.OwnerId = value; } }
    public NetCord.Rest.ImageProperties? Splash { get { return _original.Splash; } set { _original.Splash = value; } }
    public NetCord.Rest.ImageProperties? DiscoverySplash { get { return _original.DiscoverySplash; } set { _original.DiscoverySplash = value; } }
    public NetCord.Rest.ImageProperties? Banner { get { return _original.Banner; } set { _original.Banner = value; } }
    public ulong? SystemChannelId { get { return _original.SystemChannelId; } set { _original.SystemChannelId = value; } }
    public NetCord.Rest.SystemChannelFlags? SystemChannelFlags { get { return _original.SystemChannelFlags; } set { _original.SystemChannelFlags = value; } }
    public ulong? RulesChannelId { get { return _original.RulesChannelId; } set { _original.RulesChannelId = value; } }
    public ulong? PublicUpdatesChannelId { get { return _original.PublicUpdatesChannelId; } set { _original.PublicUpdatesChannelId = value; } }
    public string? PreferredLocale { get { return _original.PreferredLocale; } set { _original.PreferredLocale = value; } }
    public IEnumerable<string>? Features { get { return _original.Features; } set { _original.Features = value; } }
    public string? Description { get { return _original.Description; } set { _original.Description = value; } }
    public bool? PremiumProgressBarEnabled { get { return _original.PremiumProgressBarEnabled; } set { _original.PremiumProgressBarEnabled = value; } }
    public ulong? SafetyAlertsChannelId { get { return _original.SafetyAlertsChannelId; } set { _original.SafetyAlertsChannelId = value; } }
    public IDiscordGuildOptions WithName(string name) 
    {
        return new DiscordGuildOptions(_original.WithName(name));
    }
    public IDiscordGuildOptions WithVerificationLevel(NetCord.VerificationLevel? verificationLevel) 
    {
        return new DiscordGuildOptions(_original.WithVerificationLevel(verificationLevel));
    }
    public IDiscordGuildOptions WithDefaultMessageNotificationLevel(NetCord.DefaultMessageNotificationLevel? defaultMessageNotificationLevel) 
    {
        return new DiscordGuildOptions(_original.WithDefaultMessageNotificationLevel(defaultMessageNotificationLevel));
    }
    public IDiscordGuildOptions WithContentFilter(NetCord.ContentFilter? contentFilter) 
    {
        return new DiscordGuildOptions(_original.WithContentFilter(contentFilter));
    }
    public IDiscordGuildOptions WithAfkChannelId(ulong? afkChannelId) 
    {
        return new DiscordGuildOptions(_original.WithAfkChannelId(afkChannelId));
    }
    public IDiscordGuildOptions WithAfkTimeout(int? afkTimeout) 
    {
        return new DiscordGuildOptions(_original.WithAfkTimeout(afkTimeout));
    }
    public IDiscordGuildOptions WithIcon(NetCord.Rest.ImageProperties? icon) 
    {
        return new DiscordGuildOptions(_original.WithIcon(icon));
    }
    public IDiscordGuildOptions WithOwnerId(ulong? ownerId) 
    {
        return new DiscordGuildOptions(_original.WithOwnerId(ownerId));
    }
    public IDiscordGuildOptions WithSplash(NetCord.Rest.ImageProperties? splash) 
    {
        return new DiscordGuildOptions(_original.WithSplash(splash));
    }
    public IDiscordGuildOptions WithDiscoverySplash(NetCord.Rest.ImageProperties? discoverySplash) 
    {
        return new DiscordGuildOptions(_original.WithDiscoverySplash(discoverySplash));
    }
    public IDiscordGuildOptions WithBanner(NetCord.Rest.ImageProperties? banner) 
    {
        return new DiscordGuildOptions(_original.WithBanner(banner));
    }
    public IDiscordGuildOptions WithSystemChannelId(ulong? systemChannelId) 
    {
        return new DiscordGuildOptions(_original.WithSystemChannelId(systemChannelId));
    }
    public IDiscordGuildOptions WithSystemChannelFlags(NetCord.Rest.SystemChannelFlags? systemChannelFlags) 
    {
        return new DiscordGuildOptions(_original.WithSystemChannelFlags(systemChannelFlags));
    }
    public IDiscordGuildOptions WithRulesChannelId(ulong? rulesChannelId) 
    {
        return new DiscordGuildOptions(_original.WithRulesChannelId(rulesChannelId));
    }
    public IDiscordGuildOptions WithPublicUpdatesChannelId(ulong? publicUpdatesChannelId) 
    {
        return new DiscordGuildOptions(_original.WithPublicUpdatesChannelId(publicUpdatesChannelId));
    }
    public IDiscordGuildOptions WithPreferredLocale(string preferredLocale) 
    {
        return new DiscordGuildOptions(_original.WithPreferredLocale(preferredLocale));
    }
    public IDiscordGuildOptions WithFeatures(IEnumerable<string>? features) 
    {
        return new DiscordGuildOptions(_original.WithFeatures(features));
    }
    public IDiscordGuildOptions AddFeatures(IEnumerable<string> features) 
    {
        return new DiscordGuildOptions(_original.AddFeatures(features));
    }
    public IDiscordGuildOptions AddFeatures(string[] features) 
    {
        return new DiscordGuildOptions(_original.AddFeatures(features));
    }
    public IDiscordGuildOptions WithDescription(string description) 
    {
        return new DiscordGuildOptions(_original.WithDescription(description));
    }
    public IDiscordGuildOptions WithPremiumProgressBarEnabled(bool? premiumProgressBarEnabled = true) 
    {
        return new DiscordGuildOptions(_original.WithPremiumProgressBarEnabled(premiumProgressBarEnabled));
    }
    public IDiscordGuildOptions WithSafetyAlertsChannelId(ulong? safetyAlertsChannelId) 
    {
        return new DiscordGuildOptions(_original.WithSafetyAlertsChannelId(safetyAlertsChannelId));
    }
}


public class DiscordGuildChannelProperties : IDiscordGuildChannelProperties 
{
    private readonly NetCord.Rest.GuildChannelProperties _original;
    public DiscordGuildChannelProperties(NetCord.Rest.GuildChannelProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildChannelProperties Original => _original;
    public string Name { get { return _original.Name; } set { _original.Name = value; } }
    public NetCord.ChannelType Type { get { return _original.Type; } set { _original.Type = value; } }
    public string? Topic { get { return _original.Topic; } set { _original.Topic = value; } }
    public int? Bitrate { get { return _original.Bitrate; } set { _original.Bitrate = value; } }
    public int? UserLimit { get { return _original.UserLimit; } set { _original.UserLimit = value; } }
    public int? Slowmode { get { return _original.Slowmode; } set { _original.Slowmode = value; } }
    public int? Position { get { return _original.Position; } set { _original.Position = value; } }
    public IEnumerable<IDiscordPermissionOverwriteProperties>? PermissionOverwrites { get { return _original.PermissionOverwrites is null ? null : _original.PermissionOverwrites.Select(x => new DiscordPermissionOverwriteProperties(x)); } set { _original.PermissionOverwrites = value?.Select(x => x.Original); } }
    public ulong? ParentId { get { return _original.ParentId; } set { _original.ParentId = value; } }
    public bool? Nsfw { get { return _original.Nsfw; } set { _original.Nsfw = value; } }
    public string? RtcRegion { get { return _original.RtcRegion; } set { _original.RtcRegion = value; } }
    public NetCord.VideoQualityMode? VideoQualityMode { get { return _original.VideoQualityMode; } set { _original.VideoQualityMode = value; } }
    public NetCord.ThreadArchiveDuration? DefaultAutoArchiveDuration { get { return _original.DefaultAutoArchiveDuration; } set { _original.DefaultAutoArchiveDuration = value; } }
    public NetCord.Rest.ForumGuildChannelDefaultReactionProperties? DefaultReactionEmoji { get { return _original.DefaultReactionEmoji; } set { _original.DefaultReactionEmoji = value; } }
    public IEnumerable<IDiscordForumTagProperties>? AvailableTags { get { return _original.AvailableTags is null ? null : _original.AvailableTags.Select(x => new DiscordForumTagProperties(x)); } set { _original.AvailableTags = value?.Select(x => x.Original); } }
    public NetCord.SortOrderType? DefaultSortOrder { get { return _original.DefaultSortOrder; } set { _original.DefaultSortOrder = value; } }
    public NetCord.ForumLayoutType? DefaultForumLayout { get { return _original.DefaultForumLayout; } set { _original.DefaultForumLayout = value; } }
    public int? DefaultThreadSlowmode { get { return _original.DefaultThreadSlowmode; } set { _original.DefaultThreadSlowmode = value; } }
    public IDiscordGuildChannelProperties WithName(string name) 
    {
        return new DiscordGuildChannelProperties(_original.WithName(name));
    }
    public IDiscordGuildChannelProperties WithType(NetCord.ChannelType type) 
    {
        return new DiscordGuildChannelProperties(_original.WithType(type));
    }
    public IDiscordGuildChannelProperties WithTopic(string topic) 
    {
        return new DiscordGuildChannelProperties(_original.WithTopic(topic));
    }
    public IDiscordGuildChannelProperties WithBitrate(int? bitrate) 
    {
        return new DiscordGuildChannelProperties(_original.WithBitrate(bitrate));
    }
    public IDiscordGuildChannelProperties WithUserLimit(int? userLimit) 
    {
        return new DiscordGuildChannelProperties(_original.WithUserLimit(userLimit));
    }
    public IDiscordGuildChannelProperties WithSlowmode(int? slowmode) 
    {
        return new DiscordGuildChannelProperties(_original.WithSlowmode(slowmode));
    }
    public IDiscordGuildChannelProperties WithPosition(int? position) 
    {
        return new DiscordGuildChannelProperties(_original.WithPosition(position));
    }
    public IDiscordGuildChannelProperties WithPermissionOverwrites(IEnumerable<IDiscordPermissionOverwriteProperties>? permissionOverwrites) 
    {
        return new DiscordGuildChannelProperties(_original.WithPermissionOverwrites(permissionOverwrites?.Select(x => x.Original)));
    }
    public IDiscordGuildChannelProperties AddPermissionOverwrites(IEnumerable<IDiscordPermissionOverwriteProperties> permissionOverwrites) 
    {
        return new DiscordGuildChannelProperties(_original.AddPermissionOverwrites(permissionOverwrites.Select(x => x.Original)));
    }
    public IDiscordGuildChannelProperties AddPermissionOverwrites(IDiscordPermissionOverwriteProperties[] permissionOverwrites) 
    {
        return new DiscordGuildChannelProperties(_original.AddPermissionOverwrites(permissionOverwrites.Select(x => x.Original).ToArray()));
    }
    public IDiscordGuildChannelProperties WithParentId(ulong? parentId) 
    {
        return new DiscordGuildChannelProperties(_original.WithParentId(parentId));
    }
    public IDiscordGuildChannelProperties WithNsfw(bool? nsfw = true) 
    {
        return new DiscordGuildChannelProperties(_original.WithNsfw(nsfw));
    }
    public IDiscordGuildChannelProperties WithRtcRegion(string rtcRegion) 
    {
        return new DiscordGuildChannelProperties(_original.WithRtcRegion(rtcRegion));
    }
    public IDiscordGuildChannelProperties WithVideoQualityMode(NetCord.VideoQualityMode? videoQualityMode) 
    {
        return new DiscordGuildChannelProperties(_original.WithVideoQualityMode(videoQualityMode));
    }
    public IDiscordGuildChannelProperties WithDefaultAutoArchiveDuration(NetCord.ThreadArchiveDuration? defaultAutoArchiveDuration) 
    {
        return new DiscordGuildChannelProperties(_original.WithDefaultAutoArchiveDuration(defaultAutoArchiveDuration));
    }
    public IDiscordGuildChannelProperties WithDefaultReactionEmoji(NetCord.Rest.ForumGuildChannelDefaultReactionProperties? defaultReactionEmoji) 
    {
        return new DiscordGuildChannelProperties(_original.WithDefaultReactionEmoji(defaultReactionEmoji));
    }
    public IDiscordGuildChannelProperties WithAvailableTags(IEnumerable<IDiscordForumTagProperties>? availableTags) 
    {
        return new DiscordGuildChannelProperties(_original.WithAvailableTags(availableTags?.Select(x => x.Original)));
    }
    public IDiscordGuildChannelProperties AddAvailableTags(IEnumerable<IDiscordForumTagProperties> availableTags) 
    {
        return new DiscordGuildChannelProperties(_original.AddAvailableTags(availableTags.Select(x => x.Original)));
    }
    public IDiscordGuildChannelProperties AddAvailableTags(IDiscordForumTagProperties[] availableTags) 
    {
        return new DiscordGuildChannelProperties(_original.AddAvailableTags(availableTags.Select(x => x.Original).ToArray()));
    }
    public IDiscordGuildChannelProperties WithDefaultSortOrder(NetCord.SortOrderType? defaultSortOrder) 
    {
        return new DiscordGuildChannelProperties(_original.WithDefaultSortOrder(defaultSortOrder));
    }
    public IDiscordGuildChannelProperties WithDefaultForumLayout(NetCord.ForumLayoutType? defaultForumLayout) 
    {
        return new DiscordGuildChannelProperties(_original.WithDefaultForumLayout(defaultForumLayout));
    }
    public IDiscordGuildChannelProperties WithDefaultThreadSlowmode(int? defaultThreadSlowmode) 
    {
        return new DiscordGuildChannelProperties(_original.WithDefaultThreadSlowmode(defaultThreadSlowmode));
    }
}


public class DiscordGuildChannelPositionProperties : IDiscordGuildChannelPositionProperties 
{
    private readonly NetCord.Rest.GuildChannelPositionProperties _original;
    public DiscordGuildChannelPositionProperties(NetCord.Rest.GuildChannelPositionProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildChannelPositionProperties Original => _original;
    public ulong Id { get { return _original.Id; } set { _original.Id = value; } }
    public int? Position { get { return _original.Position; } set { _original.Position = value; } }
    public bool? LockPermissions { get { return _original.LockPermissions; } set { _original.LockPermissions = value; } }
    public ulong? ParentId { get { return _original.ParentId; } set { _original.ParentId = value; } }
    public IDiscordGuildChannelPositionProperties WithId(ulong id) 
    {
        return new DiscordGuildChannelPositionProperties(_original.WithId(id));
    }
    public IDiscordGuildChannelPositionProperties WithPosition(int? position) 
    {
        return new DiscordGuildChannelPositionProperties(_original.WithPosition(position));
    }
    public IDiscordGuildChannelPositionProperties WithLockPermissions(bool? lockPermissions = true) 
    {
        return new DiscordGuildChannelPositionProperties(_original.WithLockPermissions(lockPermissions));
    }
    public IDiscordGuildChannelPositionProperties WithParentId(ulong? parentId) 
    {
        return new DiscordGuildChannelPositionProperties(_original.WithParentId(parentId));
    }
}


public class DiscordPaginationProperties<T> : IDiscordPaginationProperties<T> where T : struct
{
    private readonly NetCord.Rest.PaginationProperties<T> _original;
    public DiscordPaginationProperties(NetCord.Rest.PaginationProperties<T> original)
    {
        _original = original;
    }
    public NetCord.Rest.PaginationProperties<T> Original => _original;
    public T? From { get { return _original.From; } set { _original.From = value; } }
    public NetCord.Rest.PaginationDirection? Direction { get { return _original.Direction; } set { _original.Direction = value; } }
    public int? BatchSize { get { return _original.BatchSize; } set { _original.BatchSize = value; } }
    public IDiscordPaginationProperties<T> WithFrom(T? from) 
    {
        return new DiscordPaginationProperties<T>(_original.WithFrom(from));
    }
    public IDiscordPaginationProperties<T> WithDirection(NetCord.Rest.PaginationDirection? direction) 
    {
        return new DiscordPaginationProperties<T>(_original.WithDirection(direction));
    }
    public IDiscordPaginationProperties<T> WithBatchSize(int? batchSize) 
    {
        return new DiscordPaginationProperties<T>(_original.WithBatchSize(batchSize));
    }
}


public class DiscordGuildUserProperties : IDiscordGuildUserProperties 
{
    private readonly NetCord.Rest.GuildUserProperties _original;
    public DiscordGuildUserProperties(NetCord.Rest.GuildUserProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildUserProperties Original => _original;
    public string AccessToken { get { return _original.AccessToken; } set { _original.AccessToken = value; } }
    public string? Nickname { get { return _original.Nickname; } set { _original.Nickname = value; } }
    public IEnumerable<ulong>? RolesIds { get { return _original.RolesIds; } set { _original.RolesIds = value; } }
    public bool? Muted { get { return _original.Muted; } set { _original.Muted = value; } }
    public bool? Deafened { get { return _original.Deafened; } set { _original.Deafened = value; } }
    public IDiscordGuildUserProperties WithAccessToken(string accessToken) 
    {
        return new DiscordGuildUserProperties(_original.WithAccessToken(accessToken));
    }
    public IDiscordGuildUserProperties WithNickname(string nickname) 
    {
        return new DiscordGuildUserProperties(_original.WithNickname(nickname));
    }
    public IDiscordGuildUserProperties WithRolesIds(IEnumerable<ulong>? rolesIds) 
    {
        return new DiscordGuildUserProperties(_original.WithRolesIds(rolesIds));
    }
    public IDiscordGuildUserProperties AddRolesIds(IEnumerable<ulong> rolesIds) 
    {
        return new DiscordGuildUserProperties(_original.AddRolesIds(rolesIds));
    }
    public IDiscordGuildUserProperties AddRolesIds(ulong[] rolesIds) 
    {
        return new DiscordGuildUserProperties(_original.AddRolesIds(rolesIds));
    }
    public IDiscordGuildUserProperties WithMuted(bool? muted = true) 
    {
        return new DiscordGuildUserProperties(_original.WithMuted(muted));
    }
    public IDiscordGuildUserProperties WithDeafened(bool? deafened = true) 
    {
        return new DiscordGuildUserProperties(_original.WithDeafened(deafened));
    }
}


public class DiscordGuildUserOptions : IDiscordGuildUserOptions 
{
    private readonly NetCord.Rest.GuildUserOptions _original;
    public DiscordGuildUserOptions(NetCord.Rest.GuildUserOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildUserOptions Original => _original;
    public IEnumerable<ulong>? RoleIds { get { return _original.RoleIds; } set { _original.RoleIds = value; } }
    public bool? Muted { get { return _original.Muted; } set { _original.Muted = value; } }
    public bool? Deafened { get { return _original.Deafened; } set { _original.Deafened = value; } }
    public ulong? ChannelId { get { return _original.ChannelId; } set { _original.ChannelId = value; } }
    public System.DateTimeOffset? TimeOutUntil { get { return _original.TimeOutUntil; } set { _original.TimeOutUntil = value; } }
    public NetCord.GuildUserFlags? GuildFlags { get { return _original.GuildFlags; } set { _original.GuildFlags = value; } }
    public string? Nickname { get { return _original.Nickname; } set { _original.Nickname = value; } }
    public IDiscordGuildUserOptions WithRoleIds(IEnumerable<ulong>? roleIds) 
    {
        return new DiscordGuildUserOptions(_original.WithRoleIds(roleIds));
    }
    public IDiscordGuildUserOptions AddRoleIds(IEnumerable<ulong> roleIds) 
    {
        return new DiscordGuildUserOptions(_original.AddRoleIds(roleIds));
    }
    public IDiscordGuildUserOptions AddRoleIds(ulong[] roleIds) 
    {
        return new DiscordGuildUserOptions(_original.AddRoleIds(roleIds));
    }
    public IDiscordGuildUserOptions WithMuted(bool? muted = true) 
    {
        return new DiscordGuildUserOptions(_original.WithMuted(muted));
    }
    public IDiscordGuildUserOptions WithDeafened(bool? deafened = true) 
    {
        return new DiscordGuildUserOptions(_original.WithDeafened(deafened));
    }
    public IDiscordGuildUserOptions WithChannelId(ulong? channelId) 
    {
        return new DiscordGuildUserOptions(_original.WithChannelId(channelId));
    }
    public IDiscordGuildUserOptions WithTimeOutUntil(System.DateTimeOffset? timeOutUntil) 
    {
        return new DiscordGuildUserOptions(_original.WithTimeOutUntil(timeOutUntil));
    }
    public IDiscordGuildUserOptions WithGuildFlags(NetCord.GuildUserFlags? guildFlags) 
    {
        return new DiscordGuildUserOptions(_original.WithGuildFlags(guildFlags));
    }
    public IDiscordGuildUserOptions WithNickname(string nickname) 
    {
        return new DiscordGuildUserOptions(_original.WithNickname(nickname));
    }
}


public class DiscordCurrentGuildUserOptions : IDiscordCurrentGuildUserOptions 
{
    private readonly NetCord.Rest.CurrentGuildUserOptions _original;
    public DiscordCurrentGuildUserOptions(NetCord.Rest.CurrentGuildUserOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.CurrentGuildUserOptions Original => _original;
    public string? Nickname { get { return _original.Nickname; } set { _original.Nickname = value; } }
    public IDiscordCurrentGuildUserOptions WithNickname(string nickname) 
    {
        return new DiscordCurrentGuildUserOptions(_original.WithNickname(nickname));
    }
}


public class DiscordGuildBan : IDiscordGuildBan 
{
    private readonly NetCord.Rest.GuildBan _original;
    public DiscordGuildBan(NetCord.Rest.GuildBan original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildBan Original => _original;
    public string? Reason => _original.Reason;
    public IDiscordUser User => new DiscordUser(_original.User);
    public ulong GuildId => _original.GuildId;
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAsync(properties?.Original, cancellationToken);
    }
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
    public string? Name { get { return _original.Name; } set { _original.Name = value; } }
    public NetCord.Permissions? Permissions { get { return _original.Permissions; } set { _original.Permissions = value; } }
    public NetCord.Color? Color { get { return _original.Color; } set { _original.Color = value; } }
    public bool? Hoist { get { return _original.Hoist; } set { _original.Hoist = value; } }
    public NetCord.Rest.ImageProperties? Icon { get { return _original.Icon; } set { _original.Icon = value; } }
    public string? UnicodeIcon { get { return _original.UnicodeIcon; } set { _original.UnicodeIcon = value; } }
    public bool? Mentionable { get { return _original.Mentionable; } set { _original.Mentionable = value; } }
    public IDiscordRoleProperties WithName(string name) 
    {
        return new DiscordRoleProperties(_original.WithName(name));
    }
    public IDiscordRoleProperties WithPermissions(NetCord.Permissions? permissions) 
    {
        return new DiscordRoleProperties(_original.WithPermissions(permissions));
    }
    public IDiscordRoleProperties WithColor(NetCord.Color? color) 
    {
        return new DiscordRoleProperties(_original.WithColor(color));
    }
    public IDiscordRoleProperties WithHoist(bool? hoist = true) 
    {
        return new DiscordRoleProperties(_original.WithHoist(hoist));
    }
    public IDiscordRoleProperties WithIcon(NetCord.Rest.ImageProperties? icon) 
    {
        return new DiscordRoleProperties(_original.WithIcon(icon));
    }
    public IDiscordRoleProperties WithUnicodeIcon(string unicodeIcon) 
    {
        return new DiscordRoleProperties(_original.WithUnicodeIcon(unicodeIcon));
    }
    public IDiscordRoleProperties WithMentionable(bool? mentionable = true) 
    {
        return new DiscordRoleProperties(_original.WithMentionable(mentionable));
    }
}


public class DiscordRolePositionProperties : IDiscordRolePositionProperties 
{
    private readonly NetCord.Rest.RolePositionProperties _original;
    public DiscordRolePositionProperties(NetCord.Rest.RolePositionProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.RolePositionProperties Original => _original;
    public ulong Id { get { return _original.Id; } set { _original.Id = value; } }
    public int? Position { get { return _original.Position; } set { _original.Position = value; } }
    public IDiscordRolePositionProperties WithId(ulong id) 
    {
        return new DiscordRolePositionProperties(_original.WithId(id));
    }
    public IDiscordRolePositionProperties WithPosition(int? position) 
    {
        return new DiscordRolePositionProperties(_original.WithPosition(position));
    }
}


public class DiscordRoleOptions : IDiscordRoleOptions 
{
    private readonly NetCord.Rest.RoleOptions _original;
    public DiscordRoleOptions(NetCord.Rest.RoleOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.RoleOptions Original => _original;
    public string? Name { get { return _original.Name; } set { _original.Name = value; } }
    public NetCord.Permissions? Permissions { get { return _original.Permissions; } set { _original.Permissions = value; } }
    public NetCord.Color? Color { get { return _original.Color; } set { _original.Color = value; } }
    public bool? Hoist { get { return _original.Hoist; } set { _original.Hoist = value; } }
    public NetCord.Rest.ImageProperties? Icon { get { return _original.Icon; } set { _original.Icon = value; } }
    public string? UnicodeIcon { get { return _original.UnicodeIcon; } set { _original.UnicodeIcon = value; } }
    public bool? Mentionable { get { return _original.Mentionable; } set { _original.Mentionable = value; } }
    public IDiscordRoleOptions WithName(string name) 
    {
        return new DiscordRoleOptions(_original.WithName(name));
    }
    public IDiscordRoleOptions WithPermissions(NetCord.Permissions? permissions) 
    {
        return new DiscordRoleOptions(_original.WithPermissions(permissions));
    }
    public IDiscordRoleOptions WithColor(NetCord.Color? color) 
    {
        return new DiscordRoleOptions(_original.WithColor(color));
    }
    public IDiscordRoleOptions WithHoist(bool? hoist = true) 
    {
        return new DiscordRoleOptions(_original.WithHoist(hoist));
    }
    public IDiscordRoleOptions WithIcon(NetCord.Rest.ImageProperties? icon) 
    {
        return new DiscordRoleOptions(_original.WithIcon(icon));
    }
    public IDiscordRoleOptions WithUnicodeIcon(string unicodeIcon) 
    {
        return new DiscordRoleOptions(_original.WithUnicodeIcon(unicodeIcon));
    }
    public IDiscordRoleOptions WithMentionable(bool? mentionable = true) 
    {
        return new DiscordRoleOptions(_original.WithMentionable(mentionable));
    }
}


public class DiscordGuildPruneProperties : IDiscordGuildPruneProperties 
{
    private readonly NetCord.Rest.GuildPruneProperties _original;
    public DiscordGuildPruneProperties(NetCord.Rest.GuildPruneProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildPruneProperties Original => _original;
    public int Days { get { return _original.Days; } set { _original.Days = value; } }
    public bool ComputePruneCount { get { return _original.ComputePruneCount; } set { _original.ComputePruneCount = value; } }
    public IEnumerable<ulong>? Roles { get { return _original.Roles; } set { _original.Roles = value; } }
    public IDiscordGuildPruneProperties WithDays(int days) 
    {
        return new DiscordGuildPruneProperties(_original.WithDays(days));
    }
    public IDiscordGuildPruneProperties WithComputePruneCount(bool computePruneCount = true) 
    {
        return new DiscordGuildPruneProperties(_original.WithComputePruneCount(computePruneCount));
    }
    public IDiscordGuildPruneProperties WithRoles(IEnumerable<ulong>? roles) 
    {
        return new DiscordGuildPruneProperties(_original.WithRoles(roles));
    }
    public IDiscordGuildPruneProperties AddRoles(IEnumerable<ulong> roles) 
    {
        return new DiscordGuildPruneProperties(_original.AddRoles(roles));
    }
    public IDiscordGuildPruneProperties AddRoles(ulong[] roles) 
    {
        return new DiscordGuildPruneProperties(_original.AddRoles(roles));
    }
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
    public IDiscordRestGuild? Guild => _original.Guild is null ? null : new DiscordRestGuild(_original.Guild);
    public IDiscordChannel? Channel => _original.Channel is null ? null : new DiscordChannel(_original.Channel);
    public IDiscordUser? Inviter => _original.Inviter is null ? null : new DiscordUser(_original.Inviter);
    public NetCord.InviteTargetType? TargetType => _original.TargetType;
    public IDiscordUser? TargetUser => _original.TargetUser is null ? null : new DiscordUser(_original.TargetUser);
    public IDiscordApplication? TargetApplication => _original.TargetApplication is null ? null : new DiscordApplication(_original.TargetApplication);
    public int? ApproximatePresenceCount => _original.ApproximatePresenceCount;
    public int? ApproximateUserCount => _original.ApproximateUserCount;
    public System.DateTimeOffset? ExpiresAt => _original.ExpiresAt;
    public IDiscordStageInstance? StageInstance => _original.StageInstance is null ? null : new DiscordStageInstance(_original.StageInstance);
    public IDiscordGuildScheduledEvent? GuildScheduledEvent => _original.GuildScheduledEvent is null ? null : new DiscordGuildScheduledEvent(_original.GuildScheduledEvent);
    public int? Uses => _original.Uses;
    public int? MaxUses => _original.MaxUses;
    public int? MaxAge => _original.MaxAge;
    public bool? Temporary => _original.Temporary;
    public System.DateTimeOffset? CreatedAt => _original.CreatedAt;
    public async Task<IDiscordRestInvite> GetGuildAsync(bool withCounts = false, bool withExpiration = false, ulong? guildScheduledEventId = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestInvite(await _original.GetGuildAsync(withCounts, withExpiration, guildScheduledEventId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestInvite> DeleteGuildAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestInvite(await _original.DeleteGuildAsync(properties?.Original, cancellationToken));
    }
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
    public IDiscordUser? User => _original.User is null ? null : new DiscordUser(_original.User);
    public IDiscordAccount Account => new DiscordAccount(_original.Account);
    public System.DateTimeOffset? SyncedAt => _original.SyncedAt;
    public int? SubscriberCount => _original.SubscriberCount;
    public bool? Revoked => _original.Revoked;
    public IDiscordIntegrationApplication? Application => _original.Application is null ? null : new DiscordIntegrationApplication(_original.Application);
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
    public bool Enabled { get { return _original.Enabled; } set { _original.Enabled = value; } }
    public ulong? ChannelId { get { return _original.ChannelId; } set { _original.ChannelId = value; } }
    public IDiscordGuildWidgetSettingsOptions WithEnabled(bool enabled = true) 
    {
        return new DiscordGuildWidgetSettingsOptions(_original.WithEnabled(enabled));
    }
    public IDiscordGuildWidgetSettingsOptions WithChannelId(ulong? channelId) 
    {
        return new DiscordGuildWidgetSettingsOptions(_original.WithChannelId(channelId));
    }
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
    public string? InstantInvite => _original.InstantInvite;
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
    public bool? Enabled { get { return _original.Enabled; } set { _original.Enabled = value; } }
    public IEnumerable<IDiscordGuildWelcomeScreenChannelProperties>? WelcomeChannels { get { return _original.WelcomeChannels is null ? null : _original.WelcomeChannels.Select(x => new DiscordGuildWelcomeScreenChannelProperties(x)); } set { _original.WelcomeChannels = value?.Select(x => x.Original); } }
    public string? Description { get { return _original.Description; } set { _original.Description = value; } }
    public IDiscordGuildWelcomeScreenOptions WithEnabled(bool? enabled = true) 
    {
        return new DiscordGuildWelcomeScreenOptions(_original.WithEnabled(enabled));
    }
    public IDiscordGuildWelcomeScreenOptions WithWelcomeChannels(IEnumerable<IDiscordGuildWelcomeScreenChannelProperties>? welcomeChannels) 
    {
        return new DiscordGuildWelcomeScreenOptions(_original.WithWelcomeChannels(welcomeChannels?.Select(x => x.Original)));
    }
    public IDiscordGuildWelcomeScreenOptions AddWelcomeChannels(IEnumerable<IDiscordGuildWelcomeScreenChannelProperties> welcomeChannels) 
    {
        return new DiscordGuildWelcomeScreenOptions(_original.AddWelcomeChannels(welcomeChannels.Select(x => x.Original)));
    }
    public IDiscordGuildWelcomeScreenOptions AddWelcomeChannels(IDiscordGuildWelcomeScreenChannelProperties[] welcomeChannels) 
    {
        return new DiscordGuildWelcomeScreenOptions(_original.AddWelcomeChannels(welcomeChannels.Select(x => x.Original).ToArray()));
    }
    public IDiscordGuildWelcomeScreenOptions WithDescription(string description) 
    {
        return new DiscordGuildWelcomeScreenOptions(_original.WithDescription(description));
    }
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
    public IEnumerable<IDiscordGuildOnboardingPromptProperties>? Prompts { get { return _original.Prompts is null ? null : _original.Prompts.Select(x => new DiscordGuildOnboardingPromptProperties(x)); } set { _original.Prompts = value?.Select(x => x.Original); } }
    public IEnumerable<ulong>? DefaultChannelIds { get { return _original.DefaultChannelIds; } set { _original.DefaultChannelIds = value; } }
    public bool? Enabled { get { return _original.Enabled; } set { _original.Enabled = value; } }
    public NetCord.Rest.GuildOnboardingMode? Mode { get { return _original.Mode; } set { _original.Mode = value; } }
    public IDiscordGuildOnboardingOptions WithPrompts(IEnumerable<IDiscordGuildOnboardingPromptProperties>? prompts) 
    {
        return new DiscordGuildOnboardingOptions(_original.WithPrompts(prompts?.Select(x => x.Original)));
    }
    public IDiscordGuildOnboardingOptions AddPrompts(IEnumerable<IDiscordGuildOnboardingPromptProperties> prompts) 
    {
        return new DiscordGuildOnboardingOptions(_original.AddPrompts(prompts.Select(x => x.Original)));
    }
    public IDiscordGuildOnboardingOptions AddPrompts(IDiscordGuildOnboardingPromptProperties[] prompts) 
    {
        return new DiscordGuildOnboardingOptions(_original.AddPrompts(prompts.Select(x => x.Original).ToArray()));
    }
    public IDiscordGuildOnboardingOptions WithDefaultChannelIds(IEnumerable<ulong>? defaultChannelIds) 
    {
        return new DiscordGuildOnboardingOptions(_original.WithDefaultChannelIds(defaultChannelIds));
    }
    public IDiscordGuildOnboardingOptions AddDefaultChannelIds(IEnumerable<ulong> defaultChannelIds) 
    {
        return new DiscordGuildOnboardingOptions(_original.AddDefaultChannelIds(defaultChannelIds));
    }
    public IDiscordGuildOnboardingOptions AddDefaultChannelIds(ulong[] defaultChannelIds) 
    {
        return new DiscordGuildOnboardingOptions(_original.AddDefaultChannelIds(defaultChannelIds));
    }
    public IDiscordGuildOnboardingOptions WithEnabled(bool? enabled = true) 
    {
        return new DiscordGuildOnboardingOptions(_original.WithEnabled(enabled));
    }
    public IDiscordGuildOnboardingOptions WithMode(NetCord.Rest.GuildOnboardingMode? mode) 
    {
        return new DiscordGuildOnboardingOptions(_original.WithMode(mode));
    }
}


public class DiscordGuildScheduledEventProperties : IDiscordGuildScheduledEventProperties 
{
    private readonly NetCord.Rest.GuildScheduledEventProperties _original;
    public DiscordGuildScheduledEventProperties(NetCord.Rest.GuildScheduledEventProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildScheduledEventProperties Original => _original;
    public ulong? ChannelId { get { return _original.ChannelId; } set { _original.ChannelId = value; } }
    public IDiscordGuildScheduledEventMetadataProperties? Metadata { get { return _original.Metadata is null ? null : new DiscordGuildScheduledEventMetadataProperties(_original.Metadata); } set { _original.Metadata = value?.Original; } }
    public string Name { get { return _original.Name; } set { _original.Name = value; } }
    public NetCord.GuildScheduledEventPrivacyLevel PrivacyLevel { get { return _original.PrivacyLevel; } set { _original.PrivacyLevel = value; } }
    public System.DateTimeOffset ScheduledStartTime { get { return _original.ScheduledStartTime; } set { _original.ScheduledStartTime = value; } }
    public System.DateTimeOffset? ScheduledEndTime { get { return _original.ScheduledEndTime; } set { _original.ScheduledEndTime = value; } }
    public string? Description { get { return _original.Description; } set { _original.Description = value; } }
    public NetCord.GuildScheduledEventEntityType EntityType { get { return _original.EntityType; } set { _original.EntityType = value; } }
    public NetCord.Rest.ImageProperties? Image { get { return _original.Image; } set { _original.Image = value; } }
    public IDiscordGuildScheduledEventProperties WithChannelId(ulong? channelId) 
    {
        return new DiscordGuildScheduledEventProperties(_original.WithChannelId(channelId));
    }
    public IDiscordGuildScheduledEventProperties WithMetadata(IDiscordGuildScheduledEventMetadataProperties? metadata) 
    {
        return new DiscordGuildScheduledEventProperties(_original.WithMetadata(metadata?.Original));
    }
    public IDiscordGuildScheduledEventProperties WithName(string name) 
    {
        return new DiscordGuildScheduledEventProperties(_original.WithName(name));
    }
    public IDiscordGuildScheduledEventProperties WithPrivacyLevel(NetCord.GuildScheduledEventPrivacyLevel privacyLevel) 
    {
        return new DiscordGuildScheduledEventProperties(_original.WithPrivacyLevel(privacyLevel));
    }
    public IDiscordGuildScheduledEventProperties WithScheduledStartTime(System.DateTimeOffset scheduledStartTime) 
    {
        return new DiscordGuildScheduledEventProperties(_original.WithScheduledStartTime(scheduledStartTime));
    }
    public IDiscordGuildScheduledEventProperties WithScheduledEndTime(System.DateTimeOffset? scheduledEndTime) 
    {
        return new DiscordGuildScheduledEventProperties(_original.WithScheduledEndTime(scheduledEndTime));
    }
    public IDiscordGuildScheduledEventProperties WithDescription(string description) 
    {
        return new DiscordGuildScheduledEventProperties(_original.WithDescription(description));
    }
    public IDiscordGuildScheduledEventProperties WithEntityType(NetCord.GuildScheduledEventEntityType entityType) 
    {
        return new DiscordGuildScheduledEventProperties(_original.WithEntityType(entityType));
    }
    public IDiscordGuildScheduledEventProperties WithImage(NetCord.Rest.ImageProperties? image) 
    {
        return new DiscordGuildScheduledEventProperties(_original.WithImage(image));
    }
}


public class DiscordGuildScheduledEventOptions : IDiscordGuildScheduledEventOptions 
{
    private readonly NetCord.Rest.GuildScheduledEventOptions _original;
    public DiscordGuildScheduledEventOptions(NetCord.Rest.GuildScheduledEventOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildScheduledEventOptions Original => _original;
    public ulong? ChannelId { get { return _original.ChannelId; } set { _original.ChannelId = value; } }
    public IDiscordGuildScheduledEventMetadataProperties? Metadata { get { return _original.Metadata is null ? null : new DiscordGuildScheduledEventMetadataProperties(_original.Metadata); } set { _original.Metadata = value?.Original; } }
    public string? Name { get { return _original.Name; } set { _original.Name = value; } }
    public NetCord.GuildScheduledEventPrivacyLevel? PrivacyLevel { get { return _original.PrivacyLevel; } set { _original.PrivacyLevel = value; } }
    public System.DateTimeOffset? ScheduledStartTime { get { return _original.ScheduledStartTime; } set { _original.ScheduledStartTime = value; } }
    public System.DateTimeOffset? ScheduledEndTime { get { return _original.ScheduledEndTime; } set { _original.ScheduledEndTime = value; } }
    public string? Description { get { return _original.Description; } set { _original.Description = value; } }
    public NetCord.GuildScheduledEventEntityType? EntityType { get { return _original.EntityType; } set { _original.EntityType = value; } }
    public NetCord.GuildScheduledEventStatus? Status { get { return _original.Status; } set { _original.Status = value; } }
    public NetCord.Rest.ImageProperties? Image { get { return _original.Image; } set { _original.Image = value; } }
    public IDiscordGuildScheduledEventOptions WithChannelId(ulong? channelId) 
    {
        return new DiscordGuildScheduledEventOptions(_original.WithChannelId(channelId));
    }
    public IDiscordGuildScheduledEventOptions WithMetadata(IDiscordGuildScheduledEventMetadataProperties? metadata) 
    {
        return new DiscordGuildScheduledEventOptions(_original.WithMetadata(metadata?.Original));
    }
    public IDiscordGuildScheduledEventOptions WithName(string name) 
    {
        return new DiscordGuildScheduledEventOptions(_original.WithName(name));
    }
    public IDiscordGuildScheduledEventOptions WithPrivacyLevel(NetCord.GuildScheduledEventPrivacyLevel? privacyLevel) 
    {
        return new DiscordGuildScheduledEventOptions(_original.WithPrivacyLevel(privacyLevel));
    }
    public IDiscordGuildScheduledEventOptions WithScheduledStartTime(System.DateTimeOffset? scheduledStartTime) 
    {
        return new DiscordGuildScheduledEventOptions(_original.WithScheduledStartTime(scheduledStartTime));
    }
    public IDiscordGuildScheduledEventOptions WithScheduledEndTime(System.DateTimeOffset? scheduledEndTime) 
    {
        return new DiscordGuildScheduledEventOptions(_original.WithScheduledEndTime(scheduledEndTime));
    }
    public IDiscordGuildScheduledEventOptions WithDescription(string description) 
    {
        return new DiscordGuildScheduledEventOptions(_original.WithDescription(description));
    }
    public IDiscordGuildScheduledEventOptions WithEntityType(NetCord.GuildScheduledEventEntityType? entityType) 
    {
        return new DiscordGuildScheduledEventOptions(_original.WithEntityType(entityType));
    }
    public IDiscordGuildScheduledEventOptions WithStatus(NetCord.GuildScheduledEventStatus? status) 
    {
        return new DiscordGuildScheduledEventOptions(_original.WithStatus(status));
    }
    public IDiscordGuildScheduledEventOptions WithImage(NetCord.Rest.ImageProperties? image) 
    {
        return new DiscordGuildScheduledEventOptions(_original.WithImage(image));
    }
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
    public bool WithGuildUsers { get { return _original.WithGuildUsers; } set { _original.WithGuildUsers = value; } }
    public ulong? From { get { return _original.From; } set { _original.From = value; } }
    public NetCord.Rest.PaginationDirection? Direction { get { return _original.Direction; } set { _original.Direction = value; } }
    public int? BatchSize { get { return _original.BatchSize; } set { _original.BatchSize = value; } }
    public IDiscordOptionalGuildUsersPaginationProperties WithWithGuildUsers(bool withGuildUsers = true) 
    {
        return new DiscordOptionalGuildUsersPaginationProperties(_original.WithWithGuildUsers(withGuildUsers));
    }
    public IDiscordOptionalGuildUsersPaginationProperties WithFrom(ulong? from) 
    {
        return new DiscordOptionalGuildUsersPaginationProperties(_original.WithFrom(from));
    }
    public IDiscordOptionalGuildUsersPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction) 
    {
        return new DiscordOptionalGuildUsersPaginationProperties(_original.WithDirection(direction));
    }
    public IDiscordOptionalGuildUsersPaginationProperties WithBatchSize(int? batchSize) 
    {
        return new DiscordOptionalGuildUsersPaginationProperties(_original.WithBatchSize(batchSize));
    }
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
    public async Task<IDiscordGuildTemplate> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildTemplate(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestGuild> CreateGuildAsync(IDiscordGuildFromGuildTemplateProperties guildProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestGuild(await _original.CreateGuildAsync(guildProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildTemplate> SyncAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildTemplate(await _original.SyncAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildTemplate> ModifyAsync(Action<IDiscordGuildTemplateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildTemplate(await _original.ModifyAsync(x => action(new DiscordGuildTemplateOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildTemplate> DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildTemplate(await _original.DeleteAsync(properties?.Original, cancellationToken));
    }
}


public class DiscordGuildTemplateProperties : IDiscordGuildTemplateProperties 
{
    private readonly NetCord.Rest.GuildTemplateProperties _original;
    public DiscordGuildTemplateProperties(NetCord.Rest.GuildTemplateProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildTemplateProperties Original => _original;
    public string Name { get { return _original.Name; } set { _original.Name = value; } }
    public string? Description { get { return _original.Description; } set { _original.Description = value; } }
    public IDiscordGuildTemplateProperties WithName(string name) 
    {
        return new DiscordGuildTemplateProperties(_original.WithName(name));
    }
    public IDiscordGuildTemplateProperties WithDescription(string description) 
    {
        return new DiscordGuildTemplateProperties(_original.WithDescription(description));
    }
}


public class DiscordGuildTemplateOptions : IDiscordGuildTemplateOptions 
{
    private readonly NetCord.Rest.GuildTemplateOptions _original;
    public DiscordGuildTemplateOptions(NetCord.Rest.GuildTemplateOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildTemplateOptions Original => _original;
    public string? Name { get { return _original.Name; } set { _original.Name = value; } }
    public string? Description { get { return _original.Description; } set { _original.Description = value; } }
    public IDiscordGuildTemplateOptions WithName(string name) 
    {
        return new DiscordGuildTemplateOptions(_original.WithName(name));
    }
    public IDiscordGuildTemplateOptions WithDescription(string description) 
    {
        return new DiscordGuildTemplateOptions(_original.WithDescription(description));
    }
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
    public IReadOnlyDictionary<string, string>? NameLocalizations => _original.NameLocalizations;
    public string Description => _original.Description;
    public IReadOnlyDictionary<string, string>? DescriptionLocalizations => _original.DescriptionLocalizations;
    public NetCord.Permissions? DefaultGuildUserPermissions => _original.DefaultGuildUserPermissions;
    public IReadOnlyList<IDiscordApplicationCommandOption> Options => _original.Options.Select(x => new DiscordApplicationCommandOption(x)).ToList();
    public bool Nsfw => _original.Nsfw;
    public IReadOnlyList<NetCord.ApplicationIntegrationType>? IntegrationTypes => _original.IntegrationTypes;
    public IReadOnlyList<NetCord.InteractionContextType>? Contexts => _original.Contexts;
    public ulong Version => _original.Version;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordApplicationCommand> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationCommand(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordApplicationCommand> ModifyAsync(Action<IDiscordApplicationCommandOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationCommand(await _original.ModifyAsync(x => action(new DiscordApplicationCommandOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAsync(properties?.Original, cancellationToken);
    }
    public async Task<IDiscordApplicationCommandGuildPermissions> GetPermissionsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationCommandGuildPermissions(await _original.GetPermissionsAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordApplicationCommandGuildPermissions> OverwritePermissionsAsync(IEnumerable<IDiscordApplicationCommandGuildPermissionProperties> newPermissions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationCommandGuildPermissions(await _original.OverwritePermissionsAsync(newPermissions.Select(x => x.Original), properties?.Original, cancellationToken));
    }
    public async Task<IDiscordApplicationCommandGuildPermissions> GetGuildPermissionsAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationCommandGuildPermissions(await _original.GetGuildPermissionsAsync(guildId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordApplicationCommandGuildPermissions> OverwriteGuildPermissionsAsync(ulong guildId, IEnumerable<IDiscordApplicationCommandGuildPermissionProperties> newPermissions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationCommandGuildPermissions(await _original.OverwriteGuildPermissionsAsync(guildId, newPermissions.Select(x => x.Original), properties?.Original, cancellationToken));
    }
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
    public string Name { get { return _original.Name; } set { _original.Name = value; } }
    public IReadOnlyDictionary<string, string>? NameLocalizations { get { return _original.NameLocalizations; } set { _original.NameLocalizations = value; } }
    public NetCord.Permissions? DefaultGuildUserPermissions { get { return _original.DefaultGuildUserPermissions; } set { _original.DefaultGuildUserPermissions = value; } }
    public IEnumerable<NetCord.ApplicationIntegrationType>? IntegrationTypes { get { return _original.IntegrationTypes; } set { _original.IntegrationTypes = value; } }
    public IEnumerable<NetCord.InteractionContextType>? Contexts { get { return _original.Contexts; } set { _original.Contexts = value; } }
    public bool Nsfw { get { return _original.Nsfw; } set { _original.Nsfw = value; } }
    public IDiscordApplicationCommandProperties WithName(string name) 
    {
        return new DiscordApplicationCommandProperties(_original.WithName(name));
    }
    public IDiscordApplicationCommandProperties WithNameLocalizations(IReadOnlyDictionary<string, string>? nameLocalizations) 
    {
        return new DiscordApplicationCommandProperties(_original.WithNameLocalizations(nameLocalizations));
    }
    public IDiscordApplicationCommandProperties WithDefaultGuildUserPermissions(NetCord.Permissions? defaultGuildUserPermissions) 
    {
        return new DiscordApplicationCommandProperties(_original.WithDefaultGuildUserPermissions(defaultGuildUserPermissions));
    }
    public IDiscordApplicationCommandProperties WithIntegrationTypes(IEnumerable<NetCord.ApplicationIntegrationType>? integrationTypes) 
    {
        return new DiscordApplicationCommandProperties(_original.WithIntegrationTypes(integrationTypes));
    }
    public IDiscordApplicationCommandProperties AddIntegrationTypes(IEnumerable<NetCord.ApplicationIntegrationType> integrationTypes) 
    {
        return new DiscordApplicationCommandProperties(_original.AddIntegrationTypes(integrationTypes));
    }
    public IDiscordApplicationCommandProperties AddIntegrationTypes(NetCord.ApplicationIntegrationType[] integrationTypes) 
    {
        return new DiscordApplicationCommandProperties(_original.AddIntegrationTypes(integrationTypes));
    }
    public IDiscordApplicationCommandProperties WithContexts(IEnumerable<NetCord.InteractionContextType>? contexts) 
    {
        return new DiscordApplicationCommandProperties(_original.WithContexts(contexts));
    }
    public IDiscordApplicationCommandProperties AddContexts(IEnumerable<NetCord.InteractionContextType> contexts) 
    {
        return new DiscordApplicationCommandProperties(_original.AddContexts(contexts));
    }
    public IDiscordApplicationCommandProperties AddContexts(NetCord.InteractionContextType[] contexts) 
    {
        return new DiscordApplicationCommandProperties(_original.AddContexts(contexts));
    }
    public IDiscordApplicationCommandProperties WithNsfw(bool nsfw = true) 
    {
        return new DiscordApplicationCommandProperties(_original.WithNsfw(nsfw));
    }
}


public class DiscordApplicationCommandOptions : IDiscordApplicationCommandOptions 
{
    private readonly NetCord.Rest.ApplicationCommandOptions _original;
    public DiscordApplicationCommandOptions(NetCord.Rest.ApplicationCommandOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationCommandOptions Original => _original;
    public string? Name { get { return _original.Name; } set { _original.Name = value; } }
    public IReadOnlyDictionary<string, string>? NameLocalizations { get { return _original.NameLocalizations; } set { _original.NameLocalizations = value; } }
    public string? Description { get { return _original.Description; } set { _original.Description = value; } }
    public IReadOnlyDictionary<string, string>? DescriptionLocalizations { get { return _original.DescriptionLocalizations; } set { _original.DescriptionLocalizations = value; } }
    public IEnumerable<IDiscordApplicationCommandOptionProperties>? Options { get { return _original.Options is null ? null : _original.Options.Select(x => new DiscordApplicationCommandOptionProperties(x)); } set { _original.Options = value?.Select(x => x.Original); } }
    public NetCord.Permissions? DefaultGuildUserPermissions { get { return _original.DefaultGuildUserPermissions; } set { _original.DefaultGuildUserPermissions = value; } }
    public IEnumerable<NetCord.ApplicationIntegrationType>? IntegrationTypes { get { return _original.IntegrationTypes; } set { _original.IntegrationTypes = value; } }
    public IEnumerable<NetCord.InteractionContextType>? Contexts { get { return _original.Contexts; } set { _original.Contexts = value; } }
    public bool? Nsfw { get { return _original.Nsfw; } set { _original.Nsfw = value; } }
    public IDiscordApplicationCommandOptions WithName(string name) 
    {
        return new DiscordApplicationCommandOptions(_original.WithName(name));
    }
    public IDiscordApplicationCommandOptions WithNameLocalizations(IReadOnlyDictionary<string, string>? nameLocalizations) 
    {
        return new DiscordApplicationCommandOptions(_original.WithNameLocalizations(nameLocalizations));
    }
    public IDiscordApplicationCommandOptions WithDescription(string description) 
    {
        return new DiscordApplicationCommandOptions(_original.WithDescription(description));
    }
    public IDiscordApplicationCommandOptions WithDescriptionLocalizations(IReadOnlyDictionary<string, string>? descriptionLocalizations) 
    {
        return new DiscordApplicationCommandOptions(_original.WithDescriptionLocalizations(descriptionLocalizations));
    }
    public IDiscordApplicationCommandOptions WithOptions(IEnumerable<IDiscordApplicationCommandOptionProperties>? options) 
    {
        return new DiscordApplicationCommandOptions(_original.WithOptions(options?.Select(x => x.Original)));
    }
    public IDiscordApplicationCommandOptions AddOptions(IEnumerable<IDiscordApplicationCommandOptionProperties> options) 
    {
        return new DiscordApplicationCommandOptions(_original.AddOptions(options.Select(x => x.Original)));
    }
    public IDiscordApplicationCommandOptions AddOptions(IDiscordApplicationCommandOptionProperties[] options) 
    {
        return new DiscordApplicationCommandOptions(_original.AddOptions(options.Select(x => x.Original).ToArray()));
    }
    public IDiscordApplicationCommandOptions WithDefaultGuildUserPermissions(NetCord.Permissions? defaultGuildUserPermissions) 
    {
        return new DiscordApplicationCommandOptions(_original.WithDefaultGuildUserPermissions(defaultGuildUserPermissions));
    }
    public IDiscordApplicationCommandOptions WithIntegrationTypes(IEnumerable<NetCord.ApplicationIntegrationType>? integrationTypes) 
    {
        return new DiscordApplicationCommandOptions(_original.WithIntegrationTypes(integrationTypes));
    }
    public IDiscordApplicationCommandOptions AddIntegrationTypes(IEnumerable<NetCord.ApplicationIntegrationType> integrationTypes) 
    {
        return new DiscordApplicationCommandOptions(_original.AddIntegrationTypes(integrationTypes));
    }
    public IDiscordApplicationCommandOptions AddIntegrationTypes(NetCord.ApplicationIntegrationType[] integrationTypes) 
    {
        return new DiscordApplicationCommandOptions(_original.AddIntegrationTypes(integrationTypes));
    }
    public IDiscordApplicationCommandOptions WithContexts(IEnumerable<NetCord.InteractionContextType>? contexts) 
    {
        return new DiscordApplicationCommandOptions(_original.WithContexts(contexts));
    }
    public IDiscordApplicationCommandOptions AddContexts(IEnumerable<NetCord.InteractionContextType> contexts) 
    {
        return new DiscordApplicationCommandOptions(_original.AddContexts(contexts));
    }
    public IDiscordApplicationCommandOptions AddContexts(NetCord.InteractionContextType[] contexts) 
    {
        return new DiscordApplicationCommandOptions(_original.AddContexts(contexts));
    }
    public IDiscordApplicationCommandOptions WithNsfw(bool? nsfw = true) 
    {
        return new DiscordApplicationCommandOptions(_original.WithNsfw(nsfw));
    }
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
    public ulong Id { get { return _original.Id; } set { _original.Id = value; } }
    public NetCord.ApplicationCommandGuildPermissionType Type { get { return _original.Type; } set { _original.Type = value; } }
    public bool Permission { get { return _original.Permission; } set { _original.Permission = value; } }
    public IDiscordApplicationCommandGuildPermissionProperties WithId(ulong id) 
    {
        return new DiscordApplicationCommandGuildPermissionProperties(_original.WithId(id));
    }
    public IDiscordApplicationCommandGuildPermissionProperties WithType(NetCord.ApplicationCommandGuildPermissionType type) 
    {
        return new DiscordApplicationCommandGuildPermissionProperties(_original.WithType(type));
    }
    public IDiscordApplicationCommandGuildPermissionProperties WithPermission(bool permission = true) 
    {
        return new DiscordApplicationCommandGuildPermissionProperties(_original.WithPermission(permission));
    }
}


public class DiscordGuildStickerProperties : IDiscordGuildStickerProperties 
{
    private readonly NetCord.Rest.GuildStickerProperties _original;
    public DiscordGuildStickerProperties(NetCord.Rest.GuildStickerProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildStickerProperties Original => _original;
    public IDiscordAttachmentProperties Attachment { get { return new DiscordAttachmentProperties(_original.Attachment); } set { _original.Attachment = value.Original; } }
    public NetCord.StickerFormat Format { get { return _original.Format; } set { _original.Format = value; } }
    public IEnumerable<string> Tags { get { return _original.Tags; } set { _original.Tags = value; } }
    public HttpContent Serialize() 
    {
        return _original.Serialize();
    }
    public IDiscordGuildStickerProperties WithAttachment(IDiscordAttachmentProperties attachment) 
    {
        return new DiscordGuildStickerProperties(_original.WithAttachment(attachment.Original));
    }
    public IDiscordGuildStickerProperties WithFormat(NetCord.StickerFormat format) 
    {
        return new DiscordGuildStickerProperties(_original.WithFormat(format));
    }
    public IDiscordGuildStickerProperties WithTags(IEnumerable<string> tags) 
    {
        return new DiscordGuildStickerProperties(_original.WithTags(tags));
    }
    public IDiscordGuildStickerProperties AddTags(IEnumerable<string> tags) 
    {
        return new DiscordGuildStickerProperties(_original.AddTags(tags));
    }
    public IDiscordGuildStickerProperties AddTags(string[] tags) 
    {
        return new DiscordGuildStickerProperties(_original.AddTags(tags));
    }
}


public class DiscordGuildStickerOptions : IDiscordGuildStickerOptions 
{
    private readonly NetCord.Rest.GuildStickerOptions _original;
    public DiscordGuildStickerOptions(NetCord.Rest.GuildStickerOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildStickerOptions Original => _original;
    public string? Name { get { return _original.Name; } set { _original.Name = value; } }
    public string? Description { get { return _original.Description; } set { _original.Description = value; } }
    public string? Tags { get { return _original.Tags; } set { _original.Tags = value; } }
    public IDiscordGuildStickerOptions WithName(string name) 
    {
        return new DiscordGuildStickerOptions(_original.WithName(name));
    }
    public IDiscordGuildStickerOptions WithDescription(string description) 
    {
        return new DiscordGuildStickerOptions(_original.WithDescription(description));
    }
    public IDiscordGuildStickerOptions WithTags(string tags) 
    {
        return new DiscordGuildStickerOptions(_original.WithTags(tags));
    }
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
    public string? SourceInviteCode => _original.SourceInviteCode;
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
    public IEnumerable<IDiscordGuildUsersSearchQuery>? OrQuery { get { return _original.OrQuery is null ? null : _original.OrQuery.Select(x => new DiscordGuildUsersSearchQuery(x)); } set { _original.OrQuery = value?.Select(x => x.Original); } }
    public IEnumerable<IDiscordGuildUsersSearchQuery>? AndQuery { get { return _original.AndQuery is null ? null : _original.AndQuery.Select(x => new DiscordGuildUsersSearchQuery(x)); } set { _original.AndQuery = value?.Select(x => x.Original); } }
    public NetCord.Rest.GuildUsersSearchTimestamp? From { get { return _original.From; } set { _original.From = value; } }
    public NetCord.Rest.PaginationDirection? Direction { get { return _original.Direction; } set { _original.Direction = value; } }
    public int? BatchSize { get { return _original.BatchSize; } set { _original.BatchSize = value; } }
    public IDiscordGuildUsersSearchPaginationProperties WithOrQuery(IEnumerable<IDiscordGuildUsersSearchQuery>? orQuery) 
    {
        return new DiscordGuildUsersSearchPaginationProperties(_original.WithOrQuery(orQuery?.Select(x => x.Original)));
    }
    public IDiscordGuildUsersSearchPaginationProperties AddOrQuery(IEnumerable<IDiscordGuildUsersSearchQuery> orQuery) 
    {
        return new DiscordGuildUsersSearchPaginationProperties(_original.AddOrQuery(orQuery.Select(x => x.Original)));
    }
    public IDiscordGuildUsersSearchPaginationProperties AddOrQuery(IDiscordGuildUsersSearchQuery[] orQuery) 
    {
        return new DiscordGuildUsersSearchPaginationProperties(_original.AddOrQuery(orQuery.Select(x => x.Original).ToArray()));
    }
    public IDiscordGuildUsersSearchPaginationProperties WithAndQuery(IEnumerable<IDiscordGuildUsersSearchQuery>? andQuery) 
    {
        return new DiscordGuildUsersSearchPaginationProperties(_original.WithAndQuery(andQuery?.Select(x => x.Original)));
    }
    public IDiscordGuildUsersSearchPaginationProperties AddAndQuery(IEnumerable<IDiscordGuildUsersSearchQuery> andQuery) 
    {
        return new DiscordGuildUsersSearchPaginationProperties(_original.AddAndQuery(andQuery.Select(x => x.Original)));
    }
    public IDiscordGuildUsersSearchPaginationProperties AddAndQuery(IDiscordGuildUsersSearchQuery[] andQuery) 
    {
        return new DiscordGuildUsersSearchPaginationProperties(_original.AddAndQuery(andQuery.Select(x => x.Original).ToArray()));
    }
    public IDiscordGuildUsersSearchPaginationProperties WithFrom(NetCord.Rest.GuildUsersSearchTimestamp? from) 
    {
        return new DiscordGuildUsersSearchPaginationProperties(_original.WithFrom(from));
    }
    public IDiscordGuildUsersSearchPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction) 
    {
        return new DiscordGuildUsersSearchPaginationProperties(_original.WithDirection(direction));
    }
    public IDiscordGuildUsersSearchPaginationProperties WithBatchSize(int? batchSize) 
    {
        return new DiscordGuildUsersSearchPaginationProperties(_original.WithBatchSize(batchSize));
    }
}


public class DiscordCurrentUserVoiceStateOptions : IDiscordCurrentUserVoiceStateOptions 
{
    private readonly NetCord.Rest.CurrentUserVoiceStateOptions _original;
    public DiscordCurrentUserVoiceStateOptions(NetCord.Rest.CurrentUserVoiceStateOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.CurrentUserVoiceStateOptions Original => _original;
    public ulong? ChannelId { get { return _original.ChannelId; } set { _original.ChannelId = value; } }
    public bool? Suppress { get { return _original.Suppress; } set { _original.Suppress = value; } }
    public System.DateTimeOffset? RequestToSpeakTimestamp { get { return _original.RequestToSpeakTimestamp; } set { _original.RequestToSpeakTimestamp = value; } }
    public IDiscordCurrentUserVoiceStateOptions WithChannelId(ulong? channelId) 
    {
        return new DiscordCurrentUserVoiceStateOptions(_original.WithChannelId(channelId));
    }
    public IDiscordCurrentUserVoiceStateOptions WithSuppress(bool? suppress = true) 
    {
        return new DiscordCurrentUserVoiceStateOptions(_original.WithSuppress(suppress));
    }
    public IDiscordCurrentUserVoiceStateOptions WithRequestToSpeakTimestamp(System.DateTimeOffset? requestToSpeakTimestamp) 
    {
        return new DiscordCurrentUserVoiceStateOptions(_original.WithRequestToSpeakTimestamp(requestToSpeakTimestamp));
    }
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
    public bool? Suppress { get { return _original.Suppress; } set { _original.Suppress = value; } }
    public IDiscordVoiceStateOptions WithSuppress(bool? suppress = true) 
    {
        return new DiscordVoiceStateOptions(_original.WithSuppress(suppress));
    }
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
    public IDiscordUser? Creator => _original.Creator is null ? null : new DiscordUser(_original.Creator);
    public string? Name => _original.Name;
    public string? AvatarHash => _original.AvatarHash;
    public ulong? ApplicationId => _original.ApplicationId;
    public IDiscordRestGuild? Guild => _original.Guild is null ? null : new DiscordRestGuild(_original.Guild);
    public IDiscordChannel? Channel => _original.Channel is null ? null : new DiscordChannel(_original.Channel);
    public string? Url => _original.Url;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordWebhook> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordWebhook(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordWebhook> ModifyAsync(Action<IDiscordWebhookOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordWebhook(await _original.ModifyAsync(x => action(new DiscordWebhookOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAsync(properties?.Original, cancellationToken);
    }
}


public class DiscordMessageProperties : IDiscordMessageProperties 
{
    private readonly NetCord.Rest.MessageProperties _original;
    public DiscordMessageProperties(NetCord.Rest.MessageProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.MessageProperties Original => _original;
    public string? Content { get { return _original.Content; } set { _original.Content = value; } }
    public IDiscordNonceProperties? Nonce { get { return _original.Nonce is null ? null : new DiscordNonceProperties(_original.Nonce); } set { _original.Nonce = value?.Original; } }
    public bool Tts { get { return _original.Tts; } set { _original.Tts = value; } }
    public IEnumerable<IDiscordAttachmentProperties>? Attachments { get { return _original.Attachments is null ? null : _original.Attachments.Select(x => new DiscordAttachmentProperties(x)); } set { _original.Attachments = value?.Select(x => x.Original); } }
    public IEnumerable<IDiscordEmbedProperties>? Embeds { get { return _original.Embeds is null ? null : _original.Embeds.Select(x => new DiscordEmbedProperties(x)); } set { _original.Embeds = value?.Select(x => x.Original); } }
    public IDiscordAllowedMentionsProperties? AllowedMentions { get { return _original.AllowedMentions is null ? null : new DiscordAllowedMentionsProperties(_original.AllowedMentions); } set { _original.AllowedMentions = value?.Original; } }
    public IDiscordMessageReferenceProperties? MessageReference { get { return _original.MessageReference is null ? null : new DiscordMessageReferenceProperties(_original.MessageReference); } set { _original.MessageReference = value?.Original; } }
    public IEnumerable<IDiscordComponentProperties>? Components { get { return _original.Components is null ? null : _original.Components.Select(x => new DiscordComponentProperties(x)); } set { _original.Components = value?.Select(x => x.Original); } }
    public IEnumerable<ulong>? StickerIds { get { return _original.StickerIds; } set { _original.StickerIds = value; } }
    public NetCord.MessageFlags? Flags { get { return _original.Flags; } set { _original.Flags = value; } }
    public IDiscordMessagePollProperties? Poll { get { return _original.Poll is null ? null : new DiscordMessagePollProperties(_original.Poll); } set { _original.Poll = value?.Original; } }
    public HttpContent Serialize() 
    {
        return _original.Serialize();
    }
    public IDiscordMessageProperties WithContent(string content) 
    {
        return new DiscordMessageProperties(_original.WithContent(content));
    }
    public IDiscordMessageProperties WithNonce(IDiscordNonceProperties? nonce) 
    {
        return new DiscordMessageProperties(_original.WithNonce(nonce?.Original));
    }
    public IDiscordMessageProperties WithTts(bool tts = true) 
    {
        return new DiscordMessageProperties(_original.WithTts(tts));
    }
    public IDiscordMessageProperties WithAttachments(IEnumerable<IDiscordAttachmentProperties>? attachments) 
    {
        return new DiscordMessageProperties(_original.WithAttachments(attachments?.Select(x => x.Original)));
    }
    public IDiscordMessageProperties AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments) 
    {
        return new DiscordMessageProperties(_original.AddAttachments(attachments.Select(x => x.Original)));
    }
    public IDiscordMessageProperties AddAttachments(IDiscordAttachmentProperties[] attachments) 
    {
        return new DiscordMessageProperties(_original.AddAttachments(attachments.Select(x => x.Original).ToArray()));
    }
    public IDiscordMessageProperties WithEmbeds(IEnumerable<IDiscordEmbedProperties>? embeds) 
    {
        return new DiscordMessageProperties(_original.WithEmbeds(embeds?.Select(x => x.Original)));
    }
    public IDiscordMessageProperties AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds) 
    {
        return new DiscordMessageProperties(_original.AddEmbeds(embeds.Select(x => x.Original)));
    }
    public IDiscordMessageProperties AddEmbeds(IDiscordEmbedProperties[] embeds) 
    {
        return new DiscordMessageProperties(_original.AddEmbeds(embeds.Select(x => x.Original).ToArray()));
    }
    public IDiscordMessageProperties WithAllowedMentions(IDiscordAllowedMentionsProperties? allowedMentions) 
    {
        return new DiscordMessageProperties(_original.WithAllowedMentions(allowedMentions?.Original));
    }
    public IDiscordMessageProperties WithMessageReference(IDiscordMessageReferenceProperties? messageReference) 
    {
        return new DiscordMessageProperties(_original.WithMessageReference(messageReference?.Original));
    }
    public IDiscordMessageProperties WithComponents(IEnumerable<IDiscordComponentProperties>? components) 
    {
        return new DiscordMessageProperties(_original.WithComponents(components?.Select(x => x.Original)));
    }
    public IDiscordMessageProperties AddComponents(IEnumerable<IDiscordComponentProperties> components) 
    {
        return new DiscordMessageProperties(_original.AddComponents(components.Select(x => x.Original)));
    }
    public IDiscordMessageProperties AddComponents(IDiscordComponentProperties[] components) 
    {
        return new DiscordMessageProperties(_original.AddComponents(components.Select(x => x.Original).ToArray()));
    }
    public IDiscordMessageProperties WithStickerIds(IEnumerable<ulong>? stickerIds) 
    {
        return new DiscordMessageProperties(_original.WithStickerIds(stickerIds));
    }
    public IDiscordMessageProperties AddStickerIds(IEnumerable<ulong> stickerIds) 
    {
        return new DiscordMessageProperties(_original.AddStickerIds(stickerIds));
    }
    public IDiscordMessageProperties AddStickerIds(ulong[] stickerIds) 
    {
        return new DiscordMessageProperties(_original.AddStickerIds(stickerIds));
    }
    public IDiscordMessageProperties WithFlags(NetCord.MessageFlags? flags) 
    {
        return new DiscordMessageProperties(_original.WithFlags(flags));
    }
    public IDiscordMessageProperties WithPoll(IDiscordMessagePollProperties? poll) 
    {
        return new DiscordMessageProperties(_original.WithPoll(poll?.Original));
    }
}


public class DiscordReactionEmojiProperties : IDiscordReactionEmojiProperties 
{
    private readonly NetCord.Rest.ReactionEmojiProperties _original;
    public DiscordReactionEmojiProperties(NetCord.Rest.ReactionEmojiProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ReactionEmojiProperties Original => _original;
    public string Name { get { return _original.Name; } set { _original.Name = value; } }
    public ulong? Id { get { return _original.Id; } set { _original.Id = value; } }
    public IDiscordReactionEmojiProperties WithName(string name) 
    {
        return new DiscordReactionEmojiProperties(_original.WithName(name));
    }
    public IDiscordReactionEmojiProperties WithId(ulong? id) 
    {
        return new DiscordReactionEmojiProperties(_original.WithId(id));
    }
}


public class DiscordMessageReactionsPaginationProperties : IDiscordMessageReactionsPaginationProperties 
{
    private readonly NetCord.Rest.MessageReactionsPaginationProperties _original;
    public DiscordMessageReactionsPaginationProperties(NetCord.Rest.MessageReactionsPaginationProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.MessageReactionsPaginationProperties Original => _original;
    public NetCord.ReactionType? Type { get { return _original.Type; } set { _original.Type = value; } }
    public ulong? From { get { return _original.From; } set { _original.From = value; } }
    public NetCord.Rest.PaginationDirection? Direction { get { return _original.Direction; } set { _original.Direction = value; } }
    public int? BatchSize { get { return _original.BatchSize; } set { _original.BatchSize = value; } }
    public IDiscordMessageReactionsPaginationProperties WithType(NetCord.ReactionType? type) 
    {
        return new DiscordMessageReactionsPaginationProperties(_original.WithType(type));
    }
    public IDiscordMessageReactionsPaginationProperties WithFrom(ulong? from) 
    {
        return new DiscordMessageReactionsPaginationProperties(_original.WithFrom(from));
    }
    public IDiscordMessageReactionsPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction) 
    {
        return new DiscordMessageReactionsPaginationProperties(_original.WithDirection(direction));
    }
    public IDiscordMessageReactionsPaginationProperties WithBatchSize(int? batchSize) 
    {
        return new DiscordMessageReactionsPaginationProperties(_original.WithBatchSize(batchSize));
    }
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
    public string FileName { get { return _original.FileName; } set { _original.FileName = value; } }
    public long FileSize { get { return _original.FileSize; } set { _original.FileSize = value; } }
    public long? Id { get { return _original.Id; } set { _original.Id = value; } }
    public IDiscordGoogleCloudPlatformStorageBucketProperties WithFileName(string fileName) 
    {
        return new DiscordGoogleCloudPlatformStorageBucketProperties(_original.WithFileName(fileName));
    }
    public IDiscordGoogleCloudPlatformStorageBucketProperties WithFileSize(long fileSize) 
    {
        return new DiscordGoogleCloudPlatformStorageBucketProperties(_original.WithFileSize(fileSize));
    }
    public IDiscordGoogleCloudPlatformStorageBucketProperties WithId(long? id) 
    {
        return new DiscordGoogleCloudPlatformStorageBucketProperties(_original.WithId(id));
    }
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
    public async Task<IDiscordDMChannel> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordDMChannel(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordDMChannel> DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordDMChannel(await _original.DeleteAsync(properties?.Original, cancellationToken));
    }
    public async IAsyncEnumerable<IDiscordRestMessage> GetMessagesAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetMessagesAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordRestMessage(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetMessagesAroundAsync(ulong messageId, int? limit = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetMessagesAroundAsync(messageId, limit, properties?.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    }
    public async Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.GetMessageAsync(messageId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestMessage> SendMessageAsync(IDiscordMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.SendMessageAsync(message.Original, properties?.Original, cancellationToken));
    }
    public Task AddMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.AddMessageReactionAsync(messageId, emoji.Original, properties?.Original, cancellationToken);
    }
    public Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessageReactionAsync(messageId, emoji.Original, properties?.Original, cancellationToken);
    }
    public Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessageReactionAsync(messageId, emoji.Original, userId, properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordUser> GetMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordMessageReactionsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetMessageReactionsAsync(messageId, emoji.Original, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAllMessageReactionsAsync(messageId, properties?.Original, cancellationToken);
    }
    public Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAllMessageReactionsAsync(messageId, emoji.Original, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.ModifyMessageAsync(messageId, x => action(new DiscordMessageOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessageAsync(messageId, properties?.Original, cancellationToken);
    }
    public Task DeleteMessagesAsync(IEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessagesAsync(messageIds, properties?.Original, cancellationToken);
    }
    public Task DeleteMessagesAsync(IAsyncEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessagesAsync(messageIds, properties?.Original, cancellationToken);
    }
    public Task TriggerTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.TriggerTypingStateAsync(properties?.Original, cancellationToken);
    }
    public Task<IDisposable> EnterTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.EnterTypingStateAsync(properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetPinnedMessagesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetPinnedMessagesAsync(properties?.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    }
    public Task PinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.PinMessageAsync(messageId, properties?.Original, cancellationToken);
    }
    public Task UnpinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.UnpinMessageAsync(messageId, properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordUser> GetMessagePollAnswerVotersAsync(ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetMessagePollAnswerVotersAsync(messageId, answerId, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public async Task<IDiscordRestMessage> EndMessagePollAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.EndMessagePollAsync(messageId, properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.CreateGoogleCloudPlatformStorageBucketsAsync(buckets.Select(x => x.Original), properties?.Original, cancellationToken)).Select(x => new DiscordGoogleCloudPlatformStorageBucket(x)).ToList();
    }
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
    public string? Title => _original.Title;
    public string? Description => _original.Description;
    public string? ContentType => _original.ContentType;
    public int Size => _original.Size;
    public string Url => _original.Url;
    public string ProxyUrl => _original.ProxyUrl;
    public bool Ephemeral => _original.Ephemeral;
    public NetCord.AttachmentFlags Flags => _original.Flags;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordAttachmentExpirationInfo GetExpirationInfo() 
    {
        return new DiscordAttachmentExpirationInfo(_original.GetExpirationInfo());
    }
}


public class DiscordEmbed : IDiscordEmbed 
{
    private readonly NetCord.Embed _original;
    public DiscordEmbed(NetCord.Embed original)
    {
        _original = original;
    }
    public NetCord.Embed Original => _original;
    public string? Title => _original.Title;
    public NetCord.EmbedType? Type => _original.Type;
    public string? Description => _original.Description;
    public string? Url => _original.Url;
    public System.DateTimeOffset? Timestamp => _original.Timestamp;
    public NetCord.Color? Color => _original.Color;
    public IDiscordEmbedFooter? Footer => _original.Footer is null ? null : new DiscordEmbedFooter(_original.Footer);
    public IDiscordEmbedImage? Image => _original.Image is null ? null : new DiscordEmbedImage(_original.Image);
    public IDiscordEmbedThumbnail? Thumbnail => _original.Thumbnail is null ? null : new DiscordEmbedThumbnail(_original.Thumbnail);
    public IDiscordEmbedVideo? Video => _original.Video is null ? null : new DiscordEmbedVideo(_original.Video);
    public IDiscordEmbedProvider? Provider => _original.Provider is null ? null : new DiscordEmbedProvider(_original.Provider);
    public IDiscordEmbedAuthor? Author => _original.Author is null ? null : new DiscordEmbedAuthor(_original.Author);
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
    public string? PartyId => _original.PartyId;
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
    public string? IconHash => _original.IconHash;
    public string Description => _original.Description;
    public IReadOnlyList<string> RpcOrigins => _original.RpcOrigins;
    public bool? BotPublic => _original.BotPublic;
    public bool? BotRequireCodeGrant => _original.BotRequireCodeGrant;
    public IDiscordUser? Bot => _original.Bot is null ? null : new DiscordUser(_original.Bot);
    public string? TermsOfServiceUrl => _original.TermsOfServiceUrl;
    public string? PrivacyPolicyUrl => _original.PrivacyPolicyUrl;
    public IDiscordUser? Owner => _original.Owner is null ? null : new DiscordUser(_original.Owner);
    public string VerifyKey => _original.VerifyKey;
    public IDiscordTeam? Team => _original.Team is null ? null : new DiscordTeam(_original.Team);
    public ulong? GuildId => _original.GuildId;
    public IDiscordRestGuild? Guild => _original.Guild is null ? null : new DiscordRestGuild(_original.Guild);
    public ulong? PrimarySkuId => _original.PrimarySkuId;
    public string? Slug => _original.Slug;
    public string? CoverImageHash => _original.CoverImageHash;
    public NetCord.ApplicationFlags? Flags => _original.Flags;
    public int? ApproximateGuildCount => _original.ApproximateGuildCount;
    public int? ApproximateUserInstallCount => _original.ApproximateUserInstallCount;
    public IReadOnlyList<string>? RedirectUris => _original.RedirectUris;
    public string? InteractionsEndpointUrl => _original.InteractionsEndpointUrl;
    public string? RoleConnectionsVerificationUrl => _original.RoleConnectionsVerificationUrl;
    public IReadOnlyList<string>? Tags => _original.Tags;
    public IDiscordApplicationInstallParams? InstallParams => _original.InstallParams is null ? null : new DiscordApplicationInstallParams(_original.InstallParams);
    public IReadOnlyDictionary<NetCord.ApplicationIntegrationType, IDiscordApplicationIntegrationTypeConfiguration>? IntegrationTypesConfiguration => _original.IntegrationTypesConfiguration is null ? null : _original.IntegrationTypesConfiguration.ToDictionary(kv => kv.Key, kv => (IDiscordApplicationIntegrationTypeConfiguration)new DiscordApplicationIntegrationTypeConfiguration(kv.Value));
    public string? CustomInstallUrl => _original.CustomInstallUrl;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordImageUrl? GetIconUrl(NetCord.ImageFormat format) 
    {
        var temp = _original.GetIconUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetCoverUrl(NetCord.ImageFormat format) 
    {
        var temp = _original.GetCoverUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetAssetUrl(ulong assetId, NetCord.ImageFormat format) 
    {
        var temp = _original.GetAssetUrl(assetId, format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetAchievementIconUrl(ulong achievementId, string iconHash, NetCord.ImageFormat format) 
    {
        var temp = _original.GetAchievementIconUrl(achievementId, iconHash, format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetStorePageAssetUrl(ulong assetId, NetCord.ImageFormat format) 
    {
        var temp = _original.GetStorePageAssetUrl(assetId, format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public async Task<IReadOnlyList<IDiscordApplicationEmoji>> GetEmojisAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetEmojisAsync(properties?.Original, cancellationToken)).Select(x => new DiscordApplicationEmoji(x)).ToList();
    }
    public async Task<IDiscordApplicationEmoji> GetEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationEmoji(await _original.GetEmojiAsync(emojiId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordApplicationEmoji> CreateEmojiAsync(IDiscordApplicationEmojiProperties applicationEmojiProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationEmoji(await _original.CreateEmojiAsync(applicationEmojiProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordApplicationEmoji> ModifyEmojiAsync(ulong emojiId, Action<IDiscordApplicationEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationEmoji(await _original.ModifyEmojiAsync(emojiId, x => action(new DiscordApplicationEmojiOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteEmojiAsync(emojiId, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordApplication> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplication(await _original.GetAsync(properties?.Original, cancellationToken));
    }
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
    public IDiscordMessageInteractionMetadata? TriggeringInteractionMetadata => _original.TriggeringInteractionMetadata is null ? null : new DiscordMessageInteractionMetadata(_original.TriggeringInteractionMetadata);
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
    public IReadOnlyDictionary<ulong, IDiscordUser>? Users => _original.Users is null ? null : _original.Users.ToDictionary(kv => kv.Key, kv => (IDiscordUser)new DiscordUser(kv.Value));
    public IReadOnlyDictionary<ulong, IDiscordRole>? Roles => _original.Roles is null ? null : _original.Roles.ToDictionary(kv => kv.Key, kv => (IDiscordRole)new DiscordRole(kv.Value));
    public IReadOnlyDictionary<ulong, IDiscordChannel>? Channels => _original.Channels is null ? null : _original.Channels.ToDictionary(kv => kv.Key, kv => (IDiscordChannel)new DiscordChannel(kv.Value));
    public IReadOnlyDictionary<ulong, IDiscordAttachment>? Attachments => _original.Attachments is null ? null : _original.Attachments.ToDictionary(kv => kv.Key, kv => (IDiscordAttachment)new DiscordAttachment(kv.Value));
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
    public IDiscordMessagePollResults? Results => _original.Results is null ? null : new DiscordMessagePollResults(_original.Results);
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
    public string? Content { get { return _original.Content; } set { _original.Content = value; } }
    public IDiscordNonceProperties? Nonce { get { return _original.Nonce is null ? null : new DiscordNonceProperties(_original.Nonce); } set { _original.Nonce = value?.Original; } }
    public bool Tts { get { return _original.Tts; } set { _original.Tts = value; } }
    public IEnumerable<IDiscordAttachmentProperties>? Attachments { get { return _original.Attachments is null ? null : _original.Attachments.Select(x => new DiscordAttachmentProperties(x)); } set { _original.Attachments = value?.Select(x => x.Original); } }
    public IEnumerable<IDiscordEmbedProperties>? Embeds { get { return _original.Embeds is null ? null : _original.Embeds.Select(x => new DiscordEmbedProperties(x)); } set { _original.Embeds = value?.Select(x => x.Original); } }
    public IDiscordAllowedMentionsProperties? AllowedMentions { get { return _original.AllowedMentions is null ? null : new DiscordAllowedMentionsProperties(_original.AllowedMentions); } set { _original.AllowedMentions = value?.Original; } }
    public bool? FailIfNotExists { get { return _original.FailIfNotExists; } set { _original.FailIfNotExists = value; } }
    public IEnumerable<IDiscordComponentProperties>? Components { get { return _original.Components is null ? null : _original.Components.Select(x => new DiscordComponentProperties(x)); } set { _original.Components = value?.Select(x => x.Original); } }
    public IEnumerable<ulong>? StickerIds { get { return _original.StickerIds; } set { _original.StickerIds = value; } }
    public NetCord.MessageFlags? Flags { get { return _original.Flags; } set { _original.Flags = value; } }
    public IDiscordMessagePollProperties? Poll { get { return _original.Poll is null ? null : new DiscordMessagePollProperties(_original.Poll); } set { _original.Poll = value?.Original; } }
    public IDiscordMessageProperties ToMessageProperties(ulong messageReferenceId) 
    {
        return new DiscordMessageProperties(_original.ToMessageProperties(messageReferenceId));
    }
    public IDiscordReplyMessageProperties WithContent(string content) 
    {
        return new DiscordReplyMessageProperties(_original.WithContent(content));
    }
    public IDiscordReplyMessageProperties WithNonce(IDiscordNonceProperties? nonce) 
    {
        return new DiscordReplyMessageProperties(_original.WithNonce(nonce?.Original));
    }
    public IDiscordReplyMessageProperties WithTts(bool tts = true) 
    {
        return new DiscordReplyMessageProperties(_original.WithTts(tts));
    }
    public IDiscordReplyMessageProperties WithAttachments(IEnumerable<IDiscordAttachmentProperties>? attachments) 
    {
        return new DiscordReplyMessageProperties(_original.WithAttachments(attachments?.Select(x => x.Original)));
    }
    public IDiscordReplyMessageProperties AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments) 
    {
        return new DiscordReplyMessageProperties(_original.AddAttachments(attachments.Select(x => x.Original)));
    }
    public IDiscordReplyMessageProperties AddAttachments(IDiscordAttachmentProperties[] attachments) 
    {
        return new DiscordReplyMessageProperties(_original.AddAttachments(attachments.Select(x => x.Original).ToArray()));
    }
    public IDiscordReplyMessageProperties WithEmbeds(IEnumerable<IDiscordEmbedProperties>? embeds) 
    {
        return new DiscordReplyMessageProperties(_original.WithEmbeds(embeds?.Select(x => x.Original)));
    }
    public IDiscordReplyMessageProperties AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds) 
    {
        return new DiscordReplyMessageProperties(_original.AddEmbeds(embeds.Select(x => x.Original)));
    }
    public IDiscordReplyMessageProperties AddEmbeds(IDiscordEmbedProperties[] embeds) 
    {
        return new DiscordReplyMessageProperties(_original.AddEmbeds(embeds.Select(x => x.Original).ToArray()));
    }
    public IDiscordReplyMessageProperties WithAllowedMentions(IDiscordAllowedMentionsProperties? allowedMentions) 
    {
        return new DiscordReplyMessageProperties(_original.WithAllowedMentions(allowedMentions?.Original));
    }
    public IDiscordReplyMessageProperties WithFailIfNotExists(bool? failIfNotExists = true) 
    {
        return new DiscordReplyMessageProperties(_original.WithFailIfNotExists(failIfNotExists));
    }
    public IDiscordReplyMessageProperties WithComponents(IEnumerable<IDiscordComponentProperties>? components) 
    {
        return new DiscordReplyMessageProperties(_original.WithComponents(components?.Select(x => x.Original)));
    }
    public IDiscordReplyMessageProperties AddComponents(IEnumerable<IDiscordComponentProperties> components) 
    {
        return new DiscordReplyMessageProperties(_original.AddComponents(components.Select(x => x.Original)));
    }
    public IDiscordReplyMessageProperties AddComponents(IDiscordComponentProperties[] components) 
    {
        return new DiscordReplyMessageProperties(_original.AddComponents(components.Select(x => x.Original).ToArray()));
    }
    public IDiscordReplyMessageProperties WithStickerIds(IEnumerable<ulong>? stickerIds) 
    {
        return new DiscordReplyMessageProperties(_original.WithStickerIds(stickerIds));
    }
    public IDiscordReplyMessageProperties AddStickerIds(IEnumerable<ulong> stickerIds) 
    {
        return new DiscordReplyMessageProperties(_original.AddStickerIds(stickerIds));
    }
    public IDiscordReplyMessageProperties AddStickerIds(ulong[] stickerIds) 
    {
        return new DiscordReplyMessageProperties(_original.AddStickerIds(stickerIds));
    }
    public IDiscordReplyMessageProperties WithFlags(NetCord.MessageFlags? flags) 
    {
        return new DiscordReplyMessageProperties(_original.WithFlags(flags));
    }
    public IDiscordReplyMessageProperties WithPoll(IDiscordMessagePollProperties? poll) 
    {
        return new DiscordReplyMessageProperties(_original.WithPoll(poll?.Original));
    }
}


public class DiscordGuildThreadFromMessageProperties : IDiscordGuildThreadFromMessageProperties 
{
    private readonly NetCord.Rest.GuildThreadFromMessageProperties _original;
    public DiscordGuildThreadFromMessageProperties(NetCord.Rest.GuildThreadFromMessageProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildThreadFromMessageProperties Original => _original;
    public string Name { get { return _original.Name; } set { _original.Name = value; } }
    public NetCord.ThreadArchiveDuration? AutoArchiveDuration { get { return _original.AutoArchiveDuration; } set { _original.AutoArchiveDuration = value; } }
    public int? Slowmode { get { return _original.Slowmode; } set { _original.Slowmode = value; } }
    public IDiscordGuildThreadFromMessageProperties WithName(string name) 
    {
        return new DiscordGuildThreadFromMessageProperties(_original.WithName(name));
    }
    public IDiscordGuildThreadFromMessageProperties WithAutoArchiveDuration(NetCord.ThreadArchiveDuration? autoArchiveDuration) 
    {
        return new DiscordGuildThreadFromMessageProperties(_original.WithAutoArchiveDuration(autoArchiveDuration));
    }
    public IDiscordGuildThreadFromMessageProperties WithSlowmode(int? slowmode) 
    {
        return new DiscordGuildThreadFromMessageProperties(_original.WithSlowmode(slowmode));
    }
}


public class DiscordEmbedProperties : IDiscordEmbedProperties 
{
    private readonly NetCord.Rest.EmbedProperties _original;
    public DiscordEmbedProperties(NetCord.Rest.EmbedProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.EmbedProperties Original => _original;
    public string? Title { get { return _original.Title; } set { _original.Title = value; } }
    public string? Description { get { return _original.Description; } set { _original.Description = value; } }
    public string? Url { get { return _original.Url; } set { _original.Url = value; } }
    public System.DateTimeOffset? Timestamp { get { return _original.Timestamp; } set { _original.Timestamp = value; } }
    public NetCord.Color Color { get { return _original.Color; } set { _original.Color = value; } }
    public IDiscordEmbedFooterProperties? Footer { get { return _original.Footer is null ? null : new DiscordEmbedFooterProperties(_original.Footer); } set { _original.Footer = value?.Original; } }
    public IDiscordEmbedImageProperties? Image { get { return _original.Image is null ? null : new DiscordEmbedImageProperties(_original.Image); } set { _original.Image = value?.Original; } }
    public IDiscordEmbedThumbnailProperties? Thumbnail { get { return _original.Thumbnail is null ? null : new DiscordEmbedThumbnailProperties(_original.Thumbnail); } set { _original.Thumbnail = value?.Original; } }
    public IDiscordEmbedAuthorProperties? Author { get { return _original.Author is null ? null : new DiscordEmbedAuthorProperties(_original.Author); } set { _original.Author = value?.Original; } }
    public IEnumerable<IDiscordEmbedFieldProperties>? Fields { get { return _original.Fields is null ? null : _original.Fields.Select(x => new DiscordEmbedFieldProperties(x)); } set { _original.Fields = value?.Select(x => x.Original); } }
    public IDiscordEmbedProperties WithTitle(string title) 
    {
        return new DiscordEmbedProperties(_original.WithTitle(title));
    }
    public IDiscordEmbedProperties WithDescription(string description) 
    {
        return new DiscordEmbedProperties(_original.WithDescription(description));
    }
    public IDiscordEmbedProperties WithUrl(string url) 
    {
        return new DiscordEmbedProperties(_original.WithUrl(url));
    }
    public IDiscordEmbedProperties WithTimestamp(System.DateTimeOffset? timestamp) 
    {
        return new DiscordEmbedProperties(_original.WithTimestamp(timestamp));
    }
    public IDiscordEmbedProperties WithColor(NetCord.Color color) 
    {
        return new DiscordEmbedProperties(_original.WithColor(color));
    }
    public IDiscordEmbedProperties WithFooter(IDiscordEmbedFooterProperties? footer) 
    {
        return new DiscordEmbedProperties(_original.WithFooter(footer?.Original));
    }
    public IDiscordEmbedProperties WithImage(IDiscordEmbedImageProperties? image) 
    {
        return new DiscordEmbedProperties(_original.WithImage(image?.Original));
    }
    public IDiscordEmbedProperties WithThumbnail(IDiscordEmbedThumbnailProperties? thumbnail) 
    {
        return new DiscordEmbedProperties(_original.WithThumbnail(thumbnail?.Original));
    }
    public IDiscordEmbedProperties WithAuthor(IDiscordEmbedAuthorProperties? author) 
    {
        return new DiscordEmbedProperties(_original.WithAuthor(author?.Original));
    }
    public IDiscordEmbedProperties WithFields(IEnumerable<IDiscordEmbedFieldProperties>? fields) 
    {
        return new DiscordEmbedProperties(_original.WithFields(fields?.Select(x => x.Original)));
    }
    public IDiscordEmbedProperties AddFields(IEnumerable<IDiscordEmbedFieldProperties> fields) 
    {
        return new DiscordEmbedProperties(_original.AddFields(fields.Select(x => x.Original)));
    }
    public IDiscordEmbedProperties AddFields(IDiscordEmbedFieldProperties[] fields) 
    {
        return new DiscordEmbedProperties(_original.AddFields(fields.Select(x => x.Original).ToArray()));
    }
}


public class DiscordAllowedMentionsProperties : IDiscordAllowedMentionsProperties 
{
    private readonly NetCord.Rest.AllowedMentionsProperties _original;
    public DiscordAllowedMentionsProperties(NetCord.Rest.AllowedMentionsProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.AllowedMentionsProperties Original => _original;
    public bool Everyone { get { return _original.Everyone; } set { _original.Everyone = value; } }
    public IEnumerable<ulong>? AllowedRoles { get { return _original.AllowedRoles; } set { _original.AllowedRoles = value; } }
    public IEnumerable<ulong>? AllowedUsers { get { return _original.AllowedUsers; } set { _original.AllowedUsers = value; } }
    public bool ReplyMention { get { return _original.ReplyMention; } set { _original.ReplyMention = value; } }
    public static IDiscordAllowedMentionsProperties All => new DiscordAllowedMentionsProperties(NetCord.Rest.AllowedMentionsProperties.All);
    public static IDiscordAllowedMentionsProperties None => new DiscordAllowedMentionsProperties(NetCord.Rest.AllowedMentionsProperties.None);
    public IDiscordAllowedMentionsProperties WithEveryone(bool everyone = true) 
    {
        return new DiscordAllowedMentionsProperties(_original.WithEveryone(everyone));
    }
    public IDiscordAllowedMentionsProperties WithAllowedRoles(IEnumerable<ulong>? allowedRoles) 
    {
        return new DiscordAllowedMentionsProperties(_original.WithAllowedRoles(allowedRoles));
    }
    public IDiscordAllowedMentionsProperties AddAllowedRoles(IEnumerable<ulong> allowedRoles) 
    {
        return new DiscordAllowedMentionsProperties(_original.AddAllowedRoles(allowedRoles));
    }
    public IDiscordAllowedMentionsProperties AddAllowedRoles(ulong[] allowedRoles) 
    {
        return new DiscordAllowedMentionsProperties(_original.AddAllowedRoles(allowedRoles));
    }
    public IDiscordAllowedMentionsProperties WithAllowedUsers(IEnumerable<ulong>? allowedUsers) 
    {
        return new DiscordAllowedMentionsProperties(_original.WithAllowedUsers(allowedUsers));
    }
    public IDiscordAllowedMentionsProperties AddAllowedUsers(IEnumerable<ulong> allowedUsers) 
    {
        return new DiscordAllowedMentionsProperties(_original.AddAllowedUsers(allowedUsers));
    }
    public IDiscordAllowedMentionsProperties AddAllowedUsers(ulong[] allowedUsers) 
    {
        return new DiscordAllowedMentionsProperties(_original.AddAllowedUsers(allowedUsers));
    }
    public IDiscordAllowedMentionsProperties WithReplyMention(bool replyMention = true) 
    {
        return new DiscordAllowedMentionsProperties(_original.WithReplyMention(replyMention));
    }
}


public class DiscordComponentProperties : IDiscordComponentProperties 
{
    private readonly NetCord.Rest.IComponentProperties _original;
    public DiscordComponentProperties(NetCord.Rest.IComponentProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.IComponentProperties Original => _original;
    public int? Id { get { return _original.Id; } set { _original.Id = value; } }
    public NetCord.ComponentType ComponentType => _original.ComponentType;
    public IDiscordComponentProperties WithId(int? id) 
    {
        return new DiscordComponentProperties(_original.WithId(id));
    }
}


public class DiscordAttachmentProperties : IDiscordAttachmentProperties 
{
    private readonly NetCord.Rest.AttachmentProperties _original;
    public DiscordAttachmentProperties(NetCord.Rest.AttachmentProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.AttachmentProperties Original => _original;
    public string FileName { get { return _original.FileName; } set { _original.FileName = value; } }
    public string? Title { get { return _original.Title; } set { _original.Title = value; } }
    public string? Description { get { return _original.Description; } set { _original.Description = value; } }
    public bool SupportsHttpSerialization => _original.SupportsHttpSerialization;
    public HttpContent Serialize() 
    {
        return _original.Serialize();
    }
    public IDiscordAttachmentProperties WithFileName(string fileName) 
    {
        return new DiscordAttachmentProperties(_original.WithFileName(fileName));
    }
    public IDiscordAttachmentProperties WithTitle(string title) 
    {
        return new DiscordAttachmentProperties(_original.WithTitle(title));
    }
    public IDiscordAttachmentProperties WithDescription(string description) 
    {
        return new DiscordAttachmentProperties(_original.WithDescription(description));
    }
}


public class DiscordMessagePollProperties : IDiscordMessagePollProperties 
{
    private readonly NetCord.MessagePollProperties _original;
    public DiscordMessagePollProperties(NetCord.MessagePollProperties original)
    {
        _original = original;
    }
    public NetCord.MessagePollProperties Original => _original;
    public IDiscordMessagePollMediaProperties Question { get { return new DiscordMessagePollMediaProperties(_original.Question); } set { _original.Question = value.Original; } }
    public IEnumerable<IDiscordMessagePollAnswerProperties> Answers { get { return _original.Answers.Select(x => new DiscordMessagePollAnswerProperties(x)); } set { _original.Answers = value.Select(x => x.Original); } }
    public int? DurationInHours { get { return _original.DurationInHours; } set { _original.DurationInHours = value; } }
    public bool AllowMultiselect { get { return _original.AllowMultiselect; } set { _original.AllowMultiselect = value; } }
    public NetCord.MessagePollLayoutType? LayoutType { get { return _original.LayoutType; } set { _original.LayoutType = value; } }
    public IDiscordMessagePollProperties WithQuestion(IDiscordMessagePollMediaProperties question) 
    {
        return new DiscordMessagePollProperties(_original.WithQuestion(question.Original));
    }
    public IDiscordMessagePollProperties WithAnswers(IEnumerable<IDiscordMessagePollAnswerProperties> answers) 
    {
        return new DiscordMessagePollProperties(_original.WithAnswers(answers.Select(x => x.Original)));
    }
    public IDiscordMessagePollProperties AddAnswers(IEnumerable<IDiscordMessagePollAnswerProperties> answers) 
    {
        return new DiscordMessagePollProperties(_original.AddAnswers(answers.Select(x => x.Original)));
    }
    public IDiscordMessagePollProperties AddAnswers(IDiscordMessagePollAnswerProperties[] answers) 
    {
        return new DiscordMessagePollProperties(_original.AddAnswers(answers.Select(x => x.Original).ToArray()));
    }
    public IDiscordMessagePollProperties WithDurationInHours(int? durationInHours) 
    {
        return new DiscordMessagePollProperties(_original.WithDurationInHours(durationInHours));
    }
    public IDiscordMessagePollProperties WithAllowMultiselect(bool allowMultiselect = true) 
    {
        return new DiscordMessagePollProperties(_original.WithAllowMultiselect(allowMultiselect));
    }
    public IDiscordMessagePollProperties WithLayoutType(NetCord.MessagePollLayoutType? layoutType) 
    {
        return new DiscordMessagePollProperties(_original.WithLayoutType(layoutType));
    }
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
    public string? Name { get { return _original.Name; } set { _original.Name = value; } }
    public NetCord.ChannelType? ChannelType { get { return _original.ChannelType; } set { _original.ChannelType = value; } }
    public int? Position { get { return _original.Position; } set { _original.Position = value; } }
    public string? Topic { get { return _original.Topic; } set { _original.Topic = value; } }
    public bool? Nsfw { get { return _original.Nsfw; } set { _original.Nsfw = value; } }
    public int? Slowmode { get { return _original.Slowmode; } set { _original.Slowmode = value; } }
    public int? Bitrate { get { return _original.Bitrate; } set { _original.Bitrate = value; } }
    public int? UserLimit { get { return _original.UserLimit; } set { _original.UserLimit = value; } }
    public IEnumerable<IDiscordPermissionOverwriteProperties>? PermissionOverwrites { get { return _original.PermissionOverwrites is null ? null : _original.PermissionOverwrites.Select(x => new DiscordPermissionOverwriteProperties(x)); } set { _original.PermissionOverwrites = value?.Select(x => x.Original); } }
    public ulong? ParentId { get { return _original.ParentId; } set { _original.ParentId = value; } }
    public string? RtcRegion { get { return _original.RtcRegion; } set { _original.RtcRegion = value; } }
    public NetCord.VideoQualityMode? VideoQualityMode { get { return _original.VideoQualityMode; } set { _original.VideoQualityMode = value; } }
    public NetCord.ThreadArchiveDuration? DefaultAutoArchiveDuration { get { return _original.DefaultAutoArchiveDuration; } set { _original.DefaultAutoArchiveDuration = value; } }
    public IEnumerable<IDiscordForumTagProperties>? AvailableTags { get { return _original.AvailableTags is null ? null : _original.AvailableTags.Select(x => new DiscordForumTagProperties(x)); } set { _original.AvailableTags = value?.Select(x => x.Original); } }
    public NetCord.Rest.ForumGuildChannelDefaultReactionProperties? DefaultReactionEmoji { get { return _original.DefaultReactionEmoji; } set { _original.DefaultReactionEmoji = value; } }
    public int? DefaultThreadSlowmode { get { return _original.DefaultThreadSlowmode; } set { _original.DefaultThreadSlowmode = value; } }
    public NetCord.ChannelFlags? Flags { get { return _original.Flags; } set { _original.Flags = value; } }
    public NetCord.SortOrderType? DefaultSortOrder { get { return _original.DefaultSortOrder; } set { _original.DefaultSortOrder = value; } }
    public NetCord.ForumLayoutType? DefaultForumLayout { get { return _original.DefaultForumLayout; } set { _original.DefaultForumLayout = value; } }
    public bool? Archived { get { return _original.Archived; } set { _original.Archived = value; } }
    public NetCord.ThreadArchiveDuration? AutoArchiveDuration { get { return _original.AutoArchiveDuration; } set { _original.AutoArchiveDuration = value; } }
    public bool? Locked { get { return _original.Locked; } set { _original.Locked = value; } }
    public bool? Invitable { get { return _original.Invitable; } set { _original.Invitable = value; } }
    public IEnumerable<ulong>? AppliedTags { get { return _original.AppliedTags; } set { _original.AppliedTags = value; } }
    public IDiscordGuildChannelOptions WithName(string name) 
    {
        return new DiscordGuildChannelOptions(_original.WithName(name));
    }
    public IDiscordGuildChannelOptions WithChannelType(NetCord.ChannelType? channelType) 
    {
        return new DiscordGuildChannelOptions(_original.WithChannelType(channelType));
    }
    public IDiscordGuildChannelOptions WithPosition(int? position) 
    {
        return new DiscordGuildChannelOptions(_original.WithPosition(position));
    }
    public IDiscordGuildChannelOptions WithTopic(string topic) 
    {
        return new DiscordGuildChannelOptions(_original.WithTopic(topic));
    }
    public IDiscordGuildChannelOptions WithNsfw(bool? nsfw = true) 
    {
        return new DiscordGuildChannelOptions(_original.WithNsfw(nsfw));
    }
    public IDiscordGuildChannelOptions WithSlowmode(int? slowmode) 
    {
        return new DiscordGuildChannelOptions(_original.WithSlowmode(slowmode));
    }
    public IDiscordGuildChannelOptions WithBitrate(int? bitrate) 
    {
        return new DiscordGuildChannelOptions(_original.WithBitrate(bitrate));
    }
    public IDiscordGuildChannelOptions WithUserLimit(int? userLimit) 
    {
        return new DiscordGuildChannelOptions(_original.WithUserLimit(userLimit));
    }
    public IDiscordGuildChannelOptions WithPermissionOverwrites(IEnumerable<IDiscordPermissionOverwriteProperties>? permissionOverwrites) 
    {
        return new DiscordGuildChannelOptions(_original.WithPermissionOverwrites(permissionOverwrites?.Select(x => x.Original)));
    }
    public IDiscordGuildChannelOptions AddPermissionOverwrites(IEnumerable<IDiscordPermissionOverwriteProperties> permissionOverwrites) 
    {
        return new DiscordGuildChannelOptions(_original.AddPermissionOverwrites(permissionOverwrites.Select(x => x.Original)));
    }
    public IDiscordGuildChannelOptions AddPermissionOverwrites(IDiscordPermissionOverwriteProperties[] permissionOverwrites) 
    {
        return new DiscordGuildChannelOptions(_original.AddPermissionOverwrites(permissionOverwrites.Select(x => x.Original).ToArray()));
    }
    public IDiscordGuildChannelOptions WithParentId(ulong? parentId) 
    {
        return new DiscordGuildChannelOptions(_original.WithParentId(parentId));
    }
    public IDiscordGuildChannelOptions WithRtcRegion(string rtcRegion) 
    {
        return new DiscordGuildChannelOptions(_original.WithRtcRegion(rtcRegion));
    }
    public IDiscordGuildChannelOptions WithVideoQualityMode(NetCord.VideoQualityMode? videoQualityMode) 
    {
        return new DiscordGuildChannelOptions(_original.WithVideoQualityMode(videoQualityMode));
    }
    public IDiscordGuildChannelOptions WithDefaultAutoArchiveDuration(NetCord.ThreadArchiveDuration? defaultAutoArchiveDuration) 
    {
        return new DiscordGuildChannelOptions(_original.WithDefaultAutoArchiveDuration(defaultAutoArchiveDuration));
    }
    public IDiscordGuildChannelOptions WithAvailableTags(IEnumerable<IDiscordForumTagProperties>? availableTags) 
    {
        return new DiscordGuildChannelOptions(_original.WithAvailableTags(availableTags?.Select(x => x.Original)));
    }
    public IDiscordGuildChannelOptions AddAvailableTags(IEnumerable<IDiscordForumTagProperties> availableTags) 
    {
        return new DiscordGuildChannelOptions(_original.AddAvailableTags(availableTags.Select(x => x.Original)));
    }
    public IDiscordGuildChannelOptions AddAvailableTags(IDiscordForumTagProperties[] availableTags) 
    {
        return new DiscordGuildChannelOptions(_original.AddAvailableTags(availableTags.Select(x => x.Original).ToArray()));
    }
    public IDiscordGuildChannelOptions WithDefaultReactionEmoji(NetCord.Rest.ForumGuildChannelDefaultReactionProperties? defaultReactionEmoji) 
    {
        return new DiscordGuildChannelOptions(_original.WithDefaultReactionEmoji(defaultReactionEmoji));
    }
    public IDiscordGuildChannelOptions WithDefaultThreadSlowmode(int? defaultThreadSlowmode) 
    {
        return new DiscordGuildChannelOptions(_original.WithDefaultThreadSlowmode(defaultThreadSlowmode));
    }
    public IDiscordGuildChannelOptions WithFlags(NetCord.ChannelFlags? flags) 
    {
        return new DiscordGuildChannelOptions(_original.WithFlags(flags));
    }
    public IDiscordGuildChannelOptions WithDefaultSortOrder(NetCord.SortOrderType? defaultSortOrder) 
    {
        return new DiscordGuildChannelOptions(_original.WithDefaultSortOrder(defaultSortOrder));
    }
    public IDiscordGuildChannelOptions WithDefaultForumLayout(NetCord.ForumLayoutType? defaultForumLayout) 
    {
        return new DiscordGuildChannelOptions(_original.WithDefaultForumLayout(defaultForumLayout));
    }
    public IDiscordGuildChannelOptions WithArchived(bool? archived = true) 
    {
        return new DiscordGuildChannelOptions(_original.WithArchived(archived));
    }
    public IDiscordGuildChannelOptions WithAutoArchiveDuration(NetCord.ThreadArchiveDuration? autoArchiveDuration) 
    {
        return new DiscordGuildChannelOptions(_original.WithAutoArchiveDuration(autoArchiveDuration));
    }
    public IDiscordGuildChannelOptions WithLocked(bool? locked = true) 
    {
        return new DiscordGuildChannelOptions(_original.WithLocked(locked));
    }
    public IDiscordGuildChannelOptions WithInvitable(bool? invitable = true) 
    {
        return new DiscordGuildChannelOptions(_original.WithInvitable(invitable));
    }
    public IDiscordGuildChannelOptions WithAppliedTags(IEnumerable<ulong>? appliedTags) 
    {
        return new DiscordGuildChannelOptions(_original.WithAppliedTags(appliedTags));
    }
    public IDiscordGuildChannelOptions AddAppliedTags(IEnumerable<ulong> appliedTags) 
    {
        return new DiscordGuildChannelOptions(_original.AddAppliedTags(appliedTags));
    }
    public IDiscordGuildChannelOptions AddAppliedTags(ulong[] appliedTags) 
    {
        return new DiscordGuildChannelOptions(_original.AddAppliedTags(appliedTags));
    }
}


public class DiscordPermissionOverwriteProperties : IDiscordPermissionOverwriteProperties 
{
    private readonly NetCord.Rest.PermissionOverwriteProperties _original;
    public DiscordPermissionOverwriteProperties(NetCord.Rest.PermissionOverwriteProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.PermissionOverwriteProperties Original => _original;
    public ulong Id { get { return _original.Id; } set { _original.Id = value; } }
    public NetCord.PermissionOverwriteType Type { get { return _original.Type; } set { _original.Type = value; } }
    public NetCord.Permissions? Allowed { get { return _original.Allowed; } set { _original.Allowed = value; } }
    public NetCord.Permissions? Denied { get { return _original.Denied; } set { _original.Denied = value; } }
    public IDiscordPermissionOverwriteProperties WithId(ulong id) 
    {
        return new DiscordPermissionOverwriteProperties(_original.WithId(id));
    }
    public IDiscordPermissionOverwriteProperties WithType(NetCord.PermissionOverwriteType type) 
    {
        return new DiscordPermissionOverwriteProperties(_original.WithType(type));
    }
    public IDiscordPermissionOverwriteProperties WithAllowed(NetCord.Permissions? allowed) 
    {
        return new DiscordPermissionOverwriteProperties(_original.WithAllowed(allowed));
    }
    public IDiscordPermissionOverwriteProperties WithDenied(NetCord.Permissions? denied) 
    {
        return new DiscordPermissionOverwriteProperties(_original.WithDenied(denied));
    }
}


public class DiscordInviteProperties : IDiscordInviteProperties 
{
    private readonly NetCord.Rest.InviteProperties _original;
    public DiscordInviteProperties(NetCord.Rest.InviteProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.InviteProperties Original => _original;
    public int? MaxAge { get { return _original.MaxAge; } set { _original.MaxAge = value; } }
    public int? MaxUses { get { return _original.MaxUses; } set { _original.MaxUses = value; } }
    public bool? Temporary { get { return _original.Temporary; } set { _original.Temporary = value; } }
    public bool? Unique { get { return _original.Unique; } set { _original.Unique = value; } }
    public NetCord.InviteTargetType? TargetType { get { return _original.TargetType; } set { _original.TargetType = value; } }
    public ulong? TargetUserId { get { return _original.TargetUserId; } set { _original.TargetUserId = value; } }
    public ulong? TargetApplicationId { get { return _original.TargetApplicationId; } set { _original.TargetApplicationId = value; } }
    public IDiscordInviteProperties WithMaxAge(int? maxAge) 
    {
        return new DiscordInviteProperties(_original.WithMaxAge(maxAge));
    }
    public IDiscordInviteProperties WithMaxUses(int? maxUses) 
    {
        return new DiscordInviteProperties(_original.WithMaxUses(maxUses));
    }
    public IDiscordInviteProperties WithTemporary(bool? temporary = true) 
    {
        return new DiscordInviteProperties(_original.WithTemporary(temporary));
    }
    public IDiscordInviteProperties WithUnique(bool? unique = true) 
    {
        return new DiscordInviteProperties(_original.WithUnique(unique));
    }
    public IDiscordInviteProperties WithTargetType(NetCord.InviteTargetType? targetType) 
    {
        return new DiscordInviteProperties(_original.WithTargetType(targetType));
    }
    public IDiscordInviteProperties WithTargetUserId(ulong? targetUserId) 
    {
        return new DiscordInviteProperties(_original.WithTargetUserId(targetUserId));
    }
    public IDiscordInviteProperties WithTargetApplicationId(ulong? targetApplicationId) 
    {
        return new DiscordInviteProperties(_original.WithTargetApplicationId(targetApplicationId));
    }
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
    public NetCord.ChannelType? ChannelType { get { return _original.ChannelType; } set { _original.ChannelType = value; } }
    public bool? Invitable { get { return _original.Invitable; } set { _original.Invitable = value; } }
    public string Name { get { return _original.Name; } set { _original.Name = value; } }
    public NetCord.ThreadArchiveDuration? AutoArchiveDuration { get { return _original.AutoArchiveDuration; } set { _original.AutoArchiveDuration = value; } }
    public int? Slowmode { get { return _original.Slowmode; } set { _original.Slowmode = value; } }
    public IDiscordGuildThreadProperties WithChannelType(NetCord.ChannelType? channelType) 
    {
        return new DiscordGuildThreadProperties(_original.WithChannelType(channelType));
    }
    public IDiscordGuildThreadProperties WithInvitable(bool? invitable = true) 
    {
        return new DiscordGuildThreadProperties(_original.WithInvitable(invitable));
    }
    public IDiscordGuildThreadProperties WithName(string name) 
    {
        return new DiscordGuildThreadProperties(_original.WithName(name));
    }
    public IDiscordGuildThreadProperties WithAutoArchiveDuration(NetCord.ThreadArchiveDuration? autoArchiveDuration) 
    {
        return new DiscordGuildThreadProperties(_original.WithAutoArchiveDuration(autoArchiveDuration));
    }
    public IDiscordGuildThreadProperties WithSlowmode(int? slowmode) 
    {
        return new DiscordGuildThreadProperties(_original.WithSlowmode(slowmode));
    }
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
    public IDiscordUser? Creator => _original.Creator is null ? null : new DiscordUser(_original.Creator);
    public string? Name => _original.Name;
    public string? AvatarHash => _original.AvatarHash;
    public ulong? ApplicationId => _original.ApplicationId;
    public IDiscordRestGuild? Guild => _original.Guild is null ? null : new DiscordRestGuild(_original.Guild);
    public IDiscordChannel? Channel => _original.Channel is null ? null : new DiscordChannel(_original.Channel);
    public string? Url => _original.Url;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordWebhookClient ToClient(IDiscordWebhookClientConfiguration? configuration = null) 
    {
        return new DiscordWebhookClient(_original.ToClient(configuration?.Original));
    }
    public async Task<IDiscordIncomingWebhook> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordIncomingWebhook(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordIncomingWebhook> GetWithTokenAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordIncomingWebhook(await _original.GetWithTokenAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordIncomingWebhook> ModifyAsync(Action<IDiscordWebhookOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordIncomingWebhook(await _original.ModifyAsync(x => action(new DiscordWebhookOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IDiscordIncomingWebhook> ModifyWithTokenAsync(Action<IDiscordWebhookOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordIncomingWebhook(await _original.ModifyWithTokenAsync(x => action(new DiscordWebhookOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteWithTokenAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteWithTokenAsync(properties?.Original, cancellationToken);
    }
    public async Task<IDiscordRestMessage?> ExecuteAsync(IDiscordWebhookMessageProperties message, bool wait = false, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        var temp = await _original.ExecuteAsync(message.Original, wait, threadId, withComponents, properties?.Original, cancellationToken);
        return temp is null ? null : new DiscordRestMessage(temp);
    }
    public async Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.GetMessageAsync(messageId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.ModifyMessageAsync(messageId, x => action(new DiscordMessageOptions(x)), threadId, withComponents, properties?.Original, cancellationToken));
    }
    public Task DeleteMessageAsync(ulong messageId, ulong? threadId = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessageAsync(messageId, threadId, properties?.Original, cancellationToken);
    }
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAsync(properties?.Original, cancellationToken);
    }
}


public class DiscordWebhookProperties : IDiscordWebhookProperties 
{
    private readonly NetCord.Rest.WebhookProperties _original;
    public DiscordWebhookProperties(NetCord.Rest.WebhookProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.WebhookProperties Original => _original;
    public string Name { get { return _original.Name; } set { _original.Name = value; } }
    public NetCord.Rest.ImageProperties? Avatar { get { return _original.Avatar; } set { _original.Avatar = value; } }
    public IDiscordWebhookProperties WithName(string name) 
    {
        return new DiscordWebhookProperties(_original.WithName(name));
    }
    public IDiscordWebhookProperties WithAvatar(NetCord.Rest.ImageProperties? avatar) 
    {
        return new DiscordWebhookProperties(_original.WithAvatar(avatar));
    }
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
    public string? Url => _original.Url;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordUserActivityTimestamps? Timestamps => _original.Timestamps is null ? null : new DiscordUserActivityTimestamps(_original.Timestamps);
    public ulong? ApplicationId => _original.ApplicationId;
    public string? Details => _original.Details;
    public string? State => _original.State;
    public IDiscordEmoji? Emoji => _original.Emoji is null ? null : new DiscordEmoji(_original.Emoji);
    public IDiscordParty? Party => _original.Party is null ? null : new DiscordParty(_original.Party);
    public IDiscordUserActivityAssets? Assets => _original.Assets is null ? null : new DiscordUserActivityAssets(_original.Assets);
    public IDiscordUserActivitySecrets? Secrets => _original.Secrets is null ? null : new DiscordUserActivitySecrets(_original.Secrets);
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
    public string? Topic { get { return _original.Topic; } set { _original.Topic = value; } }
    public NetCord.StageInstancePrivacyLevel? PrivacyLevel { get { return _original.PrivacyLevel; } set { _original.PrivacyLevel = value; } }
    public IDiscordStageInstanceOptions WithTopic(string topic) 
    {
        return new DiscordStageInstanceOptions(_original.WithTopic(topic));
    }
    public IDiscordStageInstanceOptions WithPrivacyLevel(NetCord.StageInstancePrivacyLevel? privacyLevel) 
    {
        return new DiscordStageInstanceOptions(_original.WithPrivacyLevel(privacyLevel));
    }
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
    public IReadOnlyList<NetCord.GuildScheduledEventRecurrenceRuleWeekday>? ByWeekday => _original.ByWeekday;
    public IReadOnlyList<IDiscordGuildScheduledEventRecurrenceRuleNWeekday>? ByNWeekday => _original.ByNWeekday is null ? null : _original.ByNWeekday.Select(x => new DiscordGuildScheduledEventRecurrenceRuleNWeekday(x)).ToList();
    public IReadOnlyList<NetCord.GuildScheduledEventRecurrenceRuleMonth>? ByMonth => _original.ByMonth;
    public IReadOnlyList<int>? ByMonthDay => _original.ByMonthDay;
    public IReadOnlyList<int>? ByYearDay => _original.ByYearDay;
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
    public string? EmojiName => _original.EmojiName;
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
    public IDiscordAuditLogChange<TValueParam> WithValues<TValueParam>(JsonTypeInfo<TValueParam> jsonTypeInfo) 
    {
        return new DiscordAuditLogChange<TValueParam>(_original.WithValues<TValueParam>(jsonTypeInfo));
    }
    public IDiscordAuditLogChange<TValueParam> WithValues<TValueParam>() 
    {
        return new DiscordAuditLogChange<TValueParam>(_original.WithValues<TValueParam>());
    }
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
    public string? AutoModerationRuleName => _original.AutoModerationRuleName;
    public NetCord.AutoModerationRuleTriggerType? AutoModerationRuleTriggerType => _original.AutoModerationRuleTriggerType;
    public ulong? ChannelId => _original.ChannelId;
    public int? Count => _original.Count;
    public int? DeleteGuildUserDays => _original.DeleteGuildUserDays;
    public ulong? Id => _original.Id;
    public int? GuildUsersRemoved => _original.GuildUsersRemoved;
    public ulong? MessageId => _original.MessageId;
    public string? RoleName => _original.RoleName;
    public NetCord.PermissionOverwriteType? Type => _original.Type;
    public NetCord.IntegrationType? IntegrationType => _original.IntegrationType;
}


public class DiscordAuditLogChange<TValue> : IDiscordAuditLogChange<TValue> 
{
    private readonly NetCord.AuditLogChange<TValue> _original;
    public DiscordAuditLogChange(NetCord.AuditLogChange<TValue> original)
    {
        _original = original;
    }
    public NetCord.AuditLogChange<TValue> Original => _original;
    public TValue? NewValue => _original.NewValue;
    public TValue? OldValue => _original.OldValue;
    public string Key => _original.Key;
    public bool HasNewValue => _original.HasNewValue;
    public bool HasOldValue => _original.HasOldValue;
    public IDiscordAuditLogChange<TValueParam> WithValues<TValueParam>(JsonTypeInfo<TValueParam> jsonTypeInfo) 
    {
        return new DiscordAuditLogChange<TValueParam>(_original.WithValues<TValueParam>(jsonTypeInfo));
    }
    public IDiscordAuditLogChange<TValueParam> WithValues<TValueParam>() 
    {
        return new DiscordAuditLogChange<TValueParam>(_original.WithValues<TValueParam>());
    }
}


public class DiscordAutoModerationRuleTriggerMetadata : IDiscordAutoModerationRuleTriggerMetadata 
{
    private readonly NetCord.AutoModerationRuleTriggerMetadata _original;
    public DiscordAutoModerationRuleTriggerMetadata(NetCord.AutoModerationRuleTriggerMetadata original)
    {
        _original = original;
    }
    public NetCord.AutoModerationRuleTriggerMetadata Original => _original;
    public IReadOnlyList<string>? KeywordFilter => _original.KeywordFilter;
    public IReadOnlyList<string>? RegexPatterns => _original.RegexPatterns;
    public IReadOnlyList<NetCord.AutoModerationRuleKeywordPresetType>? Presets => _original.Presets;
    public IReadOnlyList<string>? AllowList => _original.AllowList;
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
    public IDiscordAutoModerationActionMetadata? Metadata => _original.Metadata is null ? null : new DiscordAutoModerationActionMetadata(_original.Metadata);
}


public class DiscordAutoModerationRuleTriggerMetadataProperties : IDiscordAutoModerationRuleTriggerMetadataProperties 
{
    private readonly NetCord.AutoModerationRuleTriggerMetadataProperties _original;
    public DiscordAutoModerationRuleTriggerMetadataProperties(NetCord.AutoModerationRuleTriggerMetadataProperties original)
    {
        _original = original;
    }
    public NetCord.AutoModerationRuleTriggerMetadataProperties Original => _original;
    public IEnumerable<string>? KeywordFilter { get { return _original.KeywordFilter; } set { _original.KeywordFilter = value; } }
    public IEnumerable<string>? RegexPatterns { get { return _original.RegexPatterns; } set { _original.RegexPatterns = value; } }
    public IEnumerable<NetCord.AutoModerationRuleKeywordPresetType>? Presets { get { return _original.Presets; } set { _original.Presets = value; } }
    public IEnumerable<string>? AllowList { get { return _original.AllowList; } set { _original.AllowList = value; } }
    public int? MentionTotalLimit { get { return _original.MentionTotalLimit; } set { _original.MentionTotalLimit = value; } }
    public bool MentionRaidProtectionEnabled { get { return _original.MentionRaidProtectionEnabled; } set { _original.MentionRaidProtectionEnabled = value; } }
    public IDiscordAutoModerationRuleTriggerMetadataProperties WithKeywordFilter(IEnumerable<string>? keywordFilter) 
    {
        return new DiscordAutoModerationRuleTriggerMetadataProperties(_original.WithKeywordFilter(keywordFilter));
    }
    public IDiscordAutoModerationRuleTriggerMetadataProperties AddKeywordFilter(IEnumerable<string> keywordFilter) 
    {
        return new DiscordAutoModerationRuleTriggerMetadataProperties(_original.AddKeywordFilter(keywordFilter));
    }
    public IDiscordAutoModerationRuleTriggerMetadataProperties AddKeywordFilter(string[] keywordFilter) 
    {
        return new DiscordAutoModerationRuleTriggerMetadataProperties(_original.AddKeywordFilter(keywordFilter));
    }
    public IDiscordAutoModerationRuleTriggerMetadataProperties WithRegexPatterns(IEnumerable<string>? regexPatterns) 
    {
        return new DiscordAutoModerationRuleTriggerMetadataProperties(_original.WithRegexPatterns(regexPatterns));
    }
    public IDiscordAutoModerationRuleTriggerMetadataProperties AddRegexPatterns(IEnumerable<string> regexPatterns) 
    {
        return new DiscordAutoModerationRuleTriggerMetadataProperties(_original.AddRegexPatterns(regexPatterns));
    }
    public IDiscordAutoModerationRuleTriggerMetadataProperties AddRegexPatterns(string[] regexPatterns) 
    {
        return new DiscordAutoModerationRuleTriggerMetadataProperties(_original.AddRegexPatterns(regexPatterns));
    }
    public IDiscordAutoModerationRuleTriggerMetadataProperties WithPresets(IEnumerable<NetCord.AutoModerationRuleKeywordPresetType>? presets) 
    {
        return new DiscordAutoModerationRuleTriggerMetadataProperties(_original.WithPresets(presets));
    }
    public IDiscordAutoModerationRuleTriggerMetadataProperties AddPresets(IEnumerable<NetCord.AutoModerationRuleKeywordPresetType> presets) 
    {
        return new DiscordAutoModerationRuleTriggerMetadataProperties(_original.AddPresets(presets));
    }
    public IDiscordAutoModerationRuleTriggerMetadataProperties AddPresets(NetCord.AutoModerationRuleKeywordPresetType[] presets) 
    {
        return new DiscordAutoModerationRuleTriggerMetadataProperties(_original.AddPresets(presets));
    }
    public IDiscordAutoModerationRuleTriggerMetadataProperties WithAllowList(IEnumerable<string>? allowList) 
    {
        return new DiscordAutoModerationRuleTriggerMetadataProperties(_original.WithAllowList(allowList));
    }
    public IDiscordAutoModerationRuleTriggerMetadataProperties AddAllowList(IEnumerable<string> allowList) 
    {
        return new DiscordAutoModerationRuleTriggerMetadataProperties(_original.AddAllowList(allowList));
    }
    public IDiscordAutoModerationRuleTriggerMetadataProperties AddAllowList(string[] allowList) 
    {
        return new DiscordAutoModerationRuleTriggerMetadataProperties(_original.AddAllowList(allowList));
    }
    public IDiscordAutoModerationRuleTriggerMetadataProperties WithMentionTotalLimit(int? mentionTotalLimit) 
    {
        return new DiscordAutoModerationRuleTriggerMetadataProperties(_original.WithMentionTotalLimit(mentionTotalLimit));
    }
    public IDiscordAutoModerationRuleTriggerMetadataProperties WithMentionRaidProtectionEnabled(bool mentionRaidProtectionEnabled = true) 
    {
        return new DiscordAutoModerationRuleTriggerMetadataProperties(_original.WithMentionRaidProtectionEnabled(mentionRaidProtectionEnabled));
    }
}


public class DiscordAutoModerationActionProperties : IDiscordAutoModerationActionProperties 
{
    private readonly NetCord.AutoModerationActionProperties _original;
    public DiscordAutoModerationActionProperties(NetCord.AutoModerationActionProperties original)
    {
        _original = original;
    }
    public NetCord.AutoModerationActionProperties Original => _original;
    public NetCord.AutoModerationActionType Type { get { return _original.Type; } set { _original.Type = value; } }
    public IDiscordAutoModerationActionMetadataProperties? Metadata { get { return _original.Metadata is null ? null : new DiscordAutoModerationActionMetadataProperties(_original.Metadata); } set { _original.Metadata = value?.Original; } }
    public IDiscordAutoModerationActionProperties WithType(NetCord.AutoModerationActionType type) 
    {
        return new DiscordAutoModerationActionProperties(_original.WithType(type));
    }
    public IDiscordAutoModerationActionProperties WithMetadata(IDiscordAutoModerationActionMetadataProperties? metadata) 
    {
        return new DiscordAutoModerationActionProperties(_original.WithMetadata(metadata?.Original));
    }
}


public class DiscordForumTagProperties : IDiscordForumTagProperties 
{
    private readonly NetCord.Rest.ForumTagProperties _original;
    public DiscordForumTagProperties(NetCord.Rest.ForumTagProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ForumTagProperties Original => _original;
    public ulong? Id { get { return _original.Id; } set { _original.Id = value; } }
    public string Name { get { return _original.Name; } set { _original.Name = value; } }
    public bool? Moderated { get { return _original.Moderated; } set { _original.Moderated = value; } }
    public ulong? EmojiId { get { return _original.EmojiId; } set { _original.EmojiId = value; } }
    public string? EmojiName { get { return _original.EmojiName; } set { _original.EmojiName = value; } }
    public IDiscordForumTagProperties WithId(ulong? id) 
    {
        return new DiscordForumTagProperties(_original.WithId(id));
    }
    public IDiscordForumTagProperties WithName(string name) 
    {
        return new DiscordForumTagProperties(_original.WithName(name));
    }
    public IDiscordForumTagProperties WithModerated(bool? moderated = true) 
    {
        return new DiscordForumTagProperties(_original.WithModerated(moderated));
    }
    public IDiscordForumTagProperties WithEmojiId(ulong? emojiId) 
    {
        return new DiscordForumTagProperties(_original.WithEmojiId(emojiId));
    }
    public IDiscordForumTagProperties WithEmojiName(string emojiName) 
    {
        return new DiscordForumTagProperties(_original.WithEmojiName(emojiName));
    }
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
    public async Task<IDiscordChannel> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordChannel(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordChannel> DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordChannel(await _original.DeleteAsync(properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.CreateGoogleCloudPlatformStorageBucketsAsync(buckets.Select(x => x.Original), properties?.Original, cancellationToken)).Select(x => new DiscordGoogleCloudPlatformStorageBucket(x)).ToList();
    }
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
    public string? IconHash => _original.IconHash;
    public string Description => _original.Description;
    public string Summary => _original.Summary;
    public IDiscordUser? Bot => _original.Bot is null ? null : new DiscordUser(_original.Bot);
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
    public ulong ChannelId { get { return _original.ChannelId; } set { _original.ChannelId = value; } }
    public string Description { get { return _original.Description; } set { _original.Description = value; } }
    public IDiscordEmojiProperties? Emoji { get { return _original.Emoji is null ? null : new DiscordEmojiProperties(_original.Emoji); } set { _original.Emoji = value?.Original; } }
    public IDiscordGuildWelcomeScreenChannelProperties WithChannelId(ulong channelId) 
    {
        return new DiscordGuildWelcomeScreenChannelProperties(_original.WithChannelId(channelId));
    }
    public IDiscordGuildWelcomeScreenChannelProperties WithDescription(string description) 
    {
        return new DiscordGuildWelcomeScreenChannelProperties(_original.WithDescription(description));
    }
    public IDiscordGuildWelcomeScreenChannelProperties WithEmoji(IDiscordEmojiProperties? emoji) 
    {
        return new DiscordGuildWelcomeScreenChannelProperties(_original.WithEmoji(emoji?.Original));
    }
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
    public ulong? Id { get { return _original.Id; } set { _original.Id = value; } }
    public NetCord.Rest.GuildOnboardingPromptType Type { get { return _original.Type; } set { _original.Type = value; } }
    public IEnumerable<IDiscordGuildOnboardingPromptOptionProperties> Options { get { return _original.Options.Select(x => new DiscordGuildOnboardingPromptOptionProperties(x)); } set { _original.Options = value.Select(x => x.Original); } }
    public string Title { get { return _original.Title; } set { _original.Title = value; } }
    public bool SingleSelect { get { return _original.SingleSelect; } set { _original.SingleSelect = value; } }
    public bool Required { get { return _original.Required; } set { _original.Required = value; } }
    public bool InOnboarding { get { return _original.InOnboarding; } set { _original.InOnboarding = value; } }
    public IDiscordGuildOnboardingPromptProperties WithId(ulong? id) 
    {
        return new DiscordGuildOnboardingPromptProperties(_original.WithId(id));
    }
    public IDiscordGuildOnboardingPromptProperties WithType(NetCord.Rest.GuildOnboardingPromptType type) 
    {
        return new DiscordGuildOnboardingPromptProperties(_original.WithType(type));
    }
    public IDiscordGuildOnboardingPromptProperties WithOptions(IEnumerable<IDiscordGuildOnboardingPromptOptionProperties> options) 
    {
        return new DiscordGuildOnboardingPromptProperties(_original.WithOptions(options.Select(x => x.Original)));
    }
    public IDiscordGuildOnboardingPromptProperties AddOptions(IEnumerable<IDiscordGuildOnboardingPromptOptionProperties> options) 
    {
        return new DiscordGuildOnboardingPromptProperties(_original.AddOptions(options.Select(x => x.Original)));
    }
    public IDiscordGuildOnboardingPromptProperties AddOptions(IDiscordGuildOnboardingPromptOptionProperties[] options) 
    {
        return new DiscordGuildOnboardingPromptProperties(_original.AddOptions(options.Select(x => x.Original).ToArray()));
    }
    public IDiscordGuildOnboardingPromptProperties WithTitle(string title) 
    {
        return new DiscordGuildOnboardingPromptProperties(_original.WithTitle(title));
    }
    public IDiscordGuildOnboardingPromptProperties WithSingleSelect(bool singleSelect = true) 
    {
        return new DiscordGuildOnboardingPromptProperties(_original.WithSingleSelect(singleSelect));
    }
    public IDiscordGuildOnboardingPromptProperties WithRequired(bool required = true) 
    {
        return new DiscordGuildOnboardingPromptProperties(_original.WithRequired(required));
    }
    public IDiscordGuildOnboardingPromptProperties WithInOnboarding(bool inOnboarding = true) 
    {
        return new DiscordGuildOnboardingPromptProperties(_original.WithInOnboarding(inOnboarding));
    }
}


public class DiscordGuildScheduledEventMetadataProperties : IDiscordGuildScheduledEventMetadataProperties 
{
    private readonly NetCord.Rest.GuildScheduledEventMetadataProperties _original;
    public DiscordGuildScheduledEventMetadataProperties(NetCord.Rest.GuildScheduledEventMetadataProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildScheduledEventMetadataProperties Original => _original;
    public string Location { get { return _original.Location; } set { _original.Location = value; } }
    public IDiscordGuildScheduledEventMetadataProperties WithLocation(string location) 
    {
        return new DiscordGuildScheduledEventMetadataProperties(_original.WithLocation(location));
    }
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
    public string? IconHash => _original.IconHash;
    public string? Description => _original.Description;
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
    public string Name { get { return _original.Name; } set { _original.Name = value; } }
    public NetCord.Rest.ImageProperties? Icon { get { return _original.Icon; } set { _original.Icon = value; } }
    public IDiscordGuildFromGuildTemplateProperties WithName(string name) 
    {
        return new DiscordGuildFromGuildTemplateProperties(_original.WithName(name));
    }
    public IDiscordGuildFromGuildTemplateProperties WithIcon(NetCord.Rest.ImageProperties? icon) 
    {
        return new DiscordGuildFromGuildTemplateProperties(_original.WithIcon(icon));
    }
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
    public IReadOnlyDictionary<string, string>? NameLocalizations => _original.NameLocalizations;
    public string Description => _original.Description;
    public IReadOnlyDictionary<string, string>? DescriptionLocalizations => _original.DescriptionLocalizations;
    public bool Required => _original.Required;
    public IReadOnlyList<IDiscordApplicationCommandOptionChoice>? Choices => _original.Choices is null ? null : _original.Choices.Select(x => new DiscordApplicationCommandOptionChoice(x)).ToList();
    public IReadOnlyList<IDiscordApplicationCommandOption>? Options => _original.Options is null ? null : _original.Options.Select(x => new DiscordApplicationCommandOption(x)).ToList();
    public IReadOnlyList<NetCord.ChannelType>? ChannelTypes => _original.ChannelTypes;
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
    public IReadOnlyDictionary<string, string>? NameLocalizations => _original.NameLocalizations;
    public string Description => _original.Description;
    public IReadOnlyDictionary<string, string>? DescriptionLocalizations => _original.DescriptionLocalizations;
    public NetCord.Permissions? DefaultGuildUserPermissions => _original.DefaultGuildUserPermissions;
    public IReadOnlyList<IDiscordApplicationCommandOption> Options => _original.Options.Select(x => new DiscordApplicationCommandOption(x)).ToList();
    public bool Nsfw => _original.Nsfw;
    public IReadOnlyList<NetCord.ApplicationIntegrationType>? IntegrationTypes => _original.IntegrationTypes;
    public IReadOnlyList<NetCord.InteractionContextType>? Contexts => _original.Contexts;
    public ulong Version => _original.Version;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordApplicationCommand> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationCommand(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordApplicationCommand> ModifyAsync(Action<IDiscordApplicationCommandOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationCommand(await _original.ModifyAsync(x => action(new DiscordApplicationCommandOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAsync(properties?.Original, cancellationToken);
    }
    public async Task<IDiscordApplicationCommandGuildPermissions> GetGuildPermissionsAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationCommandGuildPermissions(await _original.GetGuildPermissionsAsync(guildId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordApplicationCommandGuildPermissions> OverwriteGuildPermissionsAsync(ulong guildId, IEnumerable<IDiscordApplicationCommandGuildPermissionProperties> newPermissions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationCommandGuildPermissions(await _original.OverwriteGuildPermissionsAsync(guildId, newPermissions.Select(x => x.Original), properties?.Original, cancellationToken));
    }
}


public class DiscordApplicationCommandOptionProperties : IDiscordApplicationCommandOptionProperties 
{
    private readonly NetCord.Rest.ApplicationCommandOptionProperties _original;
    public DiscordApplicationCommandOptionProperties(NetCord.Rest.ApplicationCommandOptionProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationCommandOptionProperties Original => _original;
    public NetCord.ApplicationCommandOptionType Type { get { return _original.Type; } set { _original.Type = value; } }
    public string Name { get { return _original.Name; } set { _original.Name = value; } }
    public IReadOnlyDictionary<string, string>? NameLocalizations { get { return _original.NameLocalizations; } set { _original.NameLocalizations = value; } }
    public string Description { get { return _original.Description; } set { _original.Description = value; } }
    public IReadOnlyDictionary<string, string>? DescriptionLocalizations { get { return _original.DescriptionLocalizations; } set { _original.DescriptionLocalizations = value; } }
    public bool? Required { get { return _original.Required; } set { _original.Required = value; } }
    public IEnumerable<IDiscordApplicationCommandOptionChoiceProperties>? Choices { get { return _original.Choices is null ? null : _original.Choices.Select(x => new DiscordApplicationCommandOptionChoiceProperties(x)); } set { _original.Choices = value?.Select(x => x.Original); } }
    public IEnumerable<IDiscordApplicationCommandOptionProperties>? Options { get { return _original.Options is null ? null : _original.Options.Select(x => new DiscordApplicationCommandOptionProperties(x)); } set { _original.Options = value?.Select(x => x.Original); } }
    public IEnumerable<NetCord.ChannelType>? ChannelTypes { get { return _original.ChannelTypes; } set { _original.ChannelTypes = value; } }
    public double? MinValue { get { return _original.MinValue; } set { _original.MinValue = value; } }
    public double? MaxValue { get { return _original.MaxValue; } set { _original.MaxValue = value; } }
    public int? MinLength { get { return _original.MinLength; } set { _original.MinLength = value; } }
    public int? MaxLength { get { return _original.MaxLength; } set { _original.MaxLength = value; } }
    public bool? Autocomplete { get { return _original.Autocomplete; } set { _original.Autocomplete = value; } }
    public IDiscordApplicationCommandOptionProperties WithType(NetCord.ApplicationCommandOptionType type) 
    {
        return new DiscordApplicationCommandOptionProperties(_original.WithType(type));
    }
    public IDiscordApplicationCommandOptionProperties WithName(string name) 
    {
        return new DiscordApplicationCommandOptionProperties(_original.WithName(name));
    }
    public IDiscordApplicationCommandOptionProperties WithNameLocalizations(IReadOnlyDictionary<string, string>? nameLocalizations) 
    {
        return new DiscordApplicationCommandOptionProperties(_original.WithNameLocalizations(nameLocalizations));
    }
    public IDiscordApplicationCommandOptionProperties WithDescription(string description) 
    {
        return new DiscordApplicationCommandOptionProperties(_original.WithDescription(description));
    }
    public IDiscordApplicationCommandOptionProperties WithDescriptionLocalizations(IReadOnlyDictionary<string, string>? descriptionLocalizations) 
    {
        return new DiscordApplicationCommandOptionProperties(_original.WithDescriptionLocalizations(descriptionLocalizations));
    }
    public IDiscordApplicationCommandOptionProperties WithRequired(bool? required = true) 
    {
        return new DiscordApplicationCommandOptionProperties(_original.WithRequired(required));
    }
    public IDiscordApplicationCommandOptionProperties WithChoices(IEnumerable<IDiscordApplicationCommandOptionChoiceProperties>? choices) 
    {
        return new DiscordApplicationCommandOptionProperties(_original.WithChoices(choices?.Select(x => x.Original)));
    }
    public IDiscordApplicationCommandOptionProperties AddChoices(IEnumerable<IDiscordApplicationCommandOptionChoiceProperties> choices) 
    {
        return new DiscordApplicationCommandOptionProperties(_original.AddChoices(choices.Select(x => x.Original)));
    }
    public IDiscordApplicationCommandOptionProperties AddChoices(IDiscordApplicationCommandOptionChoiceProperties[] choices) 
    {
        return new DiscordApplicationCommandOptionProperties(_original.AddChoices(choices.Select(x => x.Original).ToArray()));
    }
    public IDiscordApplicationCommandOptionProperties WithOptions(IEnumerable<IDiscordApplicationCommandOptionProperties>? options) 
    {
        return new DiscordApplicationCommandOptionProperties(_original.WithOptions(options?.Select(x => x.Original)));
    }
    public IDiscordApplicationCommandOptionProperties AddOptions(IEnumerable<IDiscordApplicationCommandOptionProperties> options) 
    {
        return new DiscordApplicationCommandOptionProperties(_original.AddOptions(options.Select(x => x.Original)));
    }
    public IDiscordApplicationCommandOptionProperties AddOptions(IDiscordApplicationCommandOptionProperties[] options) 
    {
        return new DiscordApplicationCommandOptionProperties(_original.AddOptions(options.Select(x => x.Original).ToArray()));
    }
    public IDiscordApplicationCommandOptionProperties WithChannelTypes(IEnumerable<NetCord.ChannelType>? channelTypes) 
    {
        return new DiscordApplicationCommandOptionProperties(_original.WithChannelTypes(channelTypes));
    }
    public IDiscordApplicationCommandOptionProperties AddChannelTypes(IEnumerable<NetCord.ChannelType> channelTypes) 
    {
        return new DiscordApplicationCommandOptionProperties(_original.AddChannelTypes(channelTypes));
    }
    public IDiscordApplicationCommandOptionProperties AddChannelTypes(NetCord.ChannelType[] channelTypes) 
    {
        return new DiscordApplicationCommandOptionProperties(_original.AddChannelTypes(channelTypes));
    }
    public IDiscordApplicationCommandOptionProperties WithMinValue(double? minValue) 
    {
        return new DiscordApplicationCommandOptionProperties(_original.WithMinValue(minValue));
    }
    public IDiscordApplicationCommandOptionProperties WithMaxValue(double? maxValue) 
    {
        return new DiscordApplicationCommandOptionProperties(_original.WithMaxValue(maxValue));
    }
    public IDiscordApplicationCommandOptionProperties WithMinLength(int? minLength) 
    {
        return new DiscordApplicationCommandOptionProperties(_original.WithMinLength(minLength));
    }
    public IDiscordApplicationCommandOptionProperties WithMaxLength(int? maxLength) 
    {
        return new DiscordApplicationCommandOptionProperties(_original.WithMaxLength(maxLength));
    }
    public IDiscordApplicationCommandOptionProperties WithAutocomplete(bool? autocomplete = true) 
    {
        return new DiscordApplicationCommandOptionProperties(_original.WithAutocomplete(autocomplete));
    }
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
    public void Serialize(Utf8JsonWriter writer) 
    {
        _original.Serialize(writer);
    }
}


public class DiscordWebhookOptions : IDiscordWebhookOptions 
{
    private readonly NetCord.Rest.WebhookOptions _original;
    public DiscordWebhookOptions(NetCord.Rest.WebhookOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.WebhookOptions Original => _original;
    public string? Name { get { return _original.Name; } set { _original.Name = value; } }
    public NetCord.Rest.ImageProperties? Avatar { get { return _original.Avatar; } set { _original.Avatar = value; } }
    public ulong? ChannelId { get { return _original.ChannelId; } set { _original.ChannelId = value; } }
    public IDiscordWebhookOptions WithName(string name) 
    {
        return new DiscordWebhookOptions(_original.WithName(name));
    }
    public IDiscordWebhookOptions WithAvatar(NetCord.Rest.ImageProperties? avatar) 
    {
        return new DiscordWebhookOptions(_original.WithAvatar(avatar));
    }
    public IDiscordWebhookOptions WithChannelId(ulong? channelId) 
    {
        return new DiscordWebhookOptions(_original.WithChannelId(channelId));
    }
}


public class DiscordNonceProperties : IDiscordNonceProperties 
{
    private readonly NetCord.Rest.NonceProperties _original;
    public DiscordNonceProperties(NetCord.Rest.NonceProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.NonceProperties Original => _original;
    public bool Unique { get { return _original.Unique; } set { _original.Unique = value; } }
    public IDiscordNonceProperties WithUnique(bool unique = true) 
    {
        return new DiscordNonceProperties(_original.WithUnique(unique));
    }
}


public class DiscordMessageReferenceProperties : IDiscordMessageReferenceProperties 
{
    private readonly NetCord.Rest.MessageReferenceProperties _original;
    public DiscordMessageReferenceProperties(NetCord.Rest.MessageReferenceProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.MessageReferenceProperties Original => _original;
    public NetCord.MessageReferenceType Type { get { return _original.Type; } set { _original.Type = value; } }
    public ulong? ChannelId { get { return _original.ChannelId; } set { _original.ChannelId = value; } }
    public ulong MessageId { get { return _original.MessageId; } set { _original.MessageId = value; } }
    public bool FailIfNotExists { get { return _original.FailIfNotExists; } set { _original.FailIfNotExists = value; } }
    public IDiscordMessageReferenceProperties WithType(NetCord.MessageReferenceType type) 
    {
        return new DiscordMessageReferenceProperties(_original.WithType(type));
    }
    public IDiscordMessageReferenceProperties WithChannelId(ulong? channelId) 
    {
        return new DiscordMessageReferenceProperties(_original.WithChannelId(channelId));
    }
    public IDiscordMessageReferenceProperties WithMessageId(ulong messageId) 
    {
        return new DiscordMessageReferenceProperties(_original.WithMessageId(messageId));
    }
    public IDiscordMessageReferenceProperties WithFailIfNotExists(bool failIfNotExists = true) 
    {
        return new DiscordMessageReferenceProperties(_original.WithFailIfNotExists(failIfNotExists));
    }
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
    public string? IconUrl => _original.IconUrl;
    public string? ProxyIconUrl => _original.ProxyIconUrl;
}


public class DiscordEmbedImage : IDiscordEmbedImage 
{
    private readonly NetCord.EmbedImage _original;
    public DiscordEmbedImage(NetCord.EmbedImage original)
    {
        _original = original;
    }
    public NetCord.EmbedImage Original => _original;
    public string? Url => _original.Url;
    public string? ProxyUrl => _original.ProxyUrl;
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
    public string? Url => _original.Url;
    public string? ProxyUrl => _original.ProxyUrl;
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
    public string? Url => _original.Url;
    public string? ProxyUrl => _original.ProxyUrl;
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
    public string? Name => _original.Name;
    public string? Url => _original.Url;
}


public class DiscordEmbedAuthor : IDiscordEmbedAuthor 
{
    private readonly NetCord.EmbedAuthor _original;
    public DiscordEmbedAuthor(NetCord.EmbedAuthor original)
    {
        _original = original;
    }
    public NetCord.EmbedAuthor Original => _original;
    public string? Name => _original.Name;
    public string? Url => _original.Url;
    public string? IconUrl => _original.IconUrl;
    public string? ProxyIconUrl => _original.ProxyIconUrl;
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
    public string? Name => _original.Name;
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
    public string? IconHash => _original.IconHash;
    public IReadOnlyList<IDiscordTeamUser> Users => _original.Users.Select(x => new DiscordTeamUser(x)).ToList();
    public string Name => _original.Name;
    public ulong OwnerId => _original.OwnerId;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordImageUrl? GetIconUrl(NetCord.ImageFormat format) 
    {
        var temp = _original.GetIconUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
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
    public IDiscordApplicationInstallParams? OAuth2InstallParams => _original.OAuth2InstallParams is null ? null : new DiscordApplicationInstallParams(_original.OAuth2InstallParams);
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
    public IDiscordUser? Creator => _original.Creator is null ? null : new DiscordUser(_original.Creator);
    public bool? RequireColons => _original.RequireColons;
    public bool? Managed => _original.Managed;
    public bool? Available => _original.Available;
    public string Name => _original.Name;
    public bool Animated => _original.Animated;
    public async Task<IDiscordApplicationEmoji> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationEmoji(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordApplicationEmoji> ModifyAsync(Action<IDiscordApplicationEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationEmoji(await _original.ModifyAsync(x => action(new DiscordApplicationEmojiOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAsync(properties?.Original, cancellationToken);
    }
    public IDiscordImageUrl GetImageUrl(NetCord.ImageFormat format) 
    {
        return new DiscordImageUrl(_original.GetImageUrl(format));
    }
}


public class DiscordApplicationEmojiProperties : IDiscordApplicationEmojiProperties 
{
    private readonly NetCord.Rest.ApplicationEmojiProperties _original;
    public DiscordApplicationEmojiProperties(NetCord.Rest.ApplicationEmojiProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationEmojiProperties Original => _original;
    public string Name { get { return _original.Name; } set { _original.Name = value; } }
    public NetCord.Rest.ImageProperties Image { get { return _original.Image; } set { _original.Image = value; } }
    public IDiscordApplicationEmojiProperties WithName(string name) 
    {
        return new DiscordApplicationEmojiProperties(_original.WithName(name));
    }
    public IDiscordApplicationEmojiProperties WithImage(NetCord.Rest.ImageProperties image) 
    {
        return new DiscordApplicationEmojiProperties(_original.WithImage(image));
    }
}


public class DiscordApplicationEmojiOptions : IDiscordApplicationEmojiOptions 
{
    private readonly NetCord.Rest.ApplicationEmojiOptions _original;
    public DiscordApplicationEmojiOptions(NetCord.Rest.ApplicationEmojiOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationEmojiOptions Original => _original;
    public string? Name { get { return _original.Name; } set { _original.Name = value; } }
    public IDiscordApplicationEmojiOptions WithName(string name) 
    {
        return new DiscordApplicationEmojiOptions(_original.WithName(name));
    }
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
    public string? Text => _original.Text;
    public IDiscordEmojiReference? Emoji => _original.Emoji is null ? null : new DiscordEmojiReference(_original.Emoji);
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
    public string? Text { get { return _original.Text; } set { _original.Text = value; } }
    public string? IconUrl { get { return _original.IconUrl; } set { _original.IconUrl = value; } }
    public IDiscordEmbedFooterProperties WithText(string text) 
    {
        return new DiscordEmbedFooterProperties(_original.WithText(text));
    }
    public IDiscordEmbedFooterProperties WithIconUrl(string iconUrl) 
    {
        return new DiscordEmbedFooterProperties(_original.WithIconUrl(iconUrl));
    }
}


public class DiscordEmbedImageProperties : IDiscordEmbedImageProperties 
{
    private readonly NetCord.Rest.EmbedImageProperties _original;
    public DiscordEmbedImageProperties(NetCord.Rest.EmbedImageProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.EmbedImageProperties Original => _original;
    public string? Url { get { return _original.Url; } set { _original.Url = value; } }
    public IDiscordEmbedImageProperties WithUrl(string url) 
    {
        return new DiscordEmbedImageProperties(_original.WithUrl(url));
    }
}


public class DiscordEmbedThumbnailProperties : IDiscordEmbedThumbnailProperties 
{
    private readonly NetCord.Rest.EmbedThumbnailProperties _original;
    public DiscordEmbedThumbnailProperties(NetCord.Rest.EmbedThumbnailProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.EmbedThumbnailProperties Original => _original;
    public string? Url { get { return _original.Url; } set { _original.Url = value; } }
    public IDiscordEmbedThumbnailProperties WithUrl(string url) 
    {
        return new DiscordEmbedThumbnailProperties(_original.WithUrl(url));
    }
}


public class DiscordEmbedAuthorProperties : IDiscordEmbedAuthorProperties 
{
    private readonly NetCord.Rest.EmbedAuthorProperties _original;
    public DiscordEmbedAuthorProperties(NetCord.Rest.EmbedAuthorProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.EmbedAuthorProperties Original => _original;
    public string? Name { get { return _original.Name; } set { _original.Name = value; } }
    public string? Url { get { return _original.Url; } set { _original.Url = value; } }
    public string? IconUrl { get { return _original.IconUrl; } set { _original.IconUrl = value; } }
    public IDiscordEmbedAuthorProperties WithName(string name) 
    {
        return new DiscordEmbedAuthorProperties(_original.WithName(name));
    }
    public IDiscordEmbedAuthorProperties WithUrl(string url) 
    {
        return new DiscordEmbedAuthorProperties(_original.WithUrl(url));
    }
    public IDiscordEmbedAuthorProperties WithIconUrl(string iconUrl) 
    {
        return new DiscordEmbedAuthorProperties(_original.WithIconUrl(iconUrl));
    }
}


public class DiscordEmbedFieldProperties : IDiscordEmbedFieldProperties 
{
    private readonly NetCord.Rest.EmbedFieldProperties _original;
    public DiscordEmbedFieldProperties(NetCord.Rest.EmbedFieldProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.EmbedFieldProperties Original => _original;
    public string? Name { get { return _original.Name; } set { _original.Name = value; } }
    public string? Value { get { return _original.Value; } set { _original.Value = value; } }
    public bool Inline { get { return _original.Inline; } set { _original.Inline = value; } }
    public IDiscordEmbedFieldProperties WithName(string name) 
    {
        return new DiscordEmbedFieldProperties(_original.WithName(name));
    }
    public IDiscordEmbedFieldProperties WithValue(string value) 
    {
        return new DiscordEmbedFieldProperties(_original.WithValue(value));
    }
    public IDiscordEmbedFieldProperties WithInline(bool inline = true) 
    {
        return new DiscordEmbedFieldProperties(_original.WithInline(inline));
    }
}


public class DiscordMessagePollMediaProperties : IDiscordMessagePollMediaProperties 
{
    private readonly NetCord.MessagePollMediaProperties _original;
    public DiscordMessagePollMediaProperties(NetCord.MessagePollMediaProperties original)
    {
        _original = original;
    }
    public NetCord.MessagePollMediaProperties Original => _original;
    public string? Text { get { return _original.Text; } set { _original.Text = value; } }
    public IDiscordEmojiProperties? Emoji { get { return _original.Emoji is null ? null : new DiscordEmojiProperties(_original.Emoji); } set { _original.Emoji = value?.Original; } }
    public IDiscordMessagePollMediaProperties WithText(string text) 
    {
        return new DiscordMessagePollMediaProperties(_original.WithText(text));
    }
    public IDiscordMessagePollMediaProperties WithEmoji(IDiscordEmojiProperties? emoji) 
    {
        return new DiscordMessagePollMediaProperties(_original.WithEmoji(emoji?.Original));
    }
}


public class DiscordMessagePollAnswerProperties : IDiscordMessagePollAnswerProperties 
{
    private readonly NetCord.MessagePollAnswerProperties _original;
    public DiscordMessagePollAnswerProperties(NetCord.MessagePollAnswerProperties original)
    {
        _original = original;
    }
    public NetCord.MessagePollAnswerProperties Original => _original;
    public IDiscordMessagePollMediaProperties PollMedia { get { return new DiscordMessagePollMediaProperties(_original.PollMedia); } set { _original.PollMedia = value.Original; } }
    public IDiscordMessagePollAnswerProperties WithPollMedia(IDiscordMessagePollMediaProperties pollMedia) 
    {
        return new DiscordMessagePollAnswerProperties(_original.WithPollMedia(pollMedia.Original));
    }
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
    public void Dispose() 
    {
        _original.Dispose();
    }
    public async Task<IDiscordWebhook> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordWebhook(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordWebhook> ModifyAsync(Action<IDiscordWebhookOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordWebhook(await _original.ModifyAsync(x => action(new DiscordWebhookOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAsync(properties?.Original, cancellationToken);
    }
    public async Task<IDiscordRestMessage?> ExecuteAsync(IDiscordWebhookMessageProperties message, bool wait = false, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        var temp = await _original.ExecuteAsync(message.Original, wait, threadId, withComponents, properties?.Original, cancellationToken);
        return temp is null ? null : new DiscordRestMessage(temp);
    }
    public async Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.GetMessageAsync(messageId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.ModifyMessageAsync(messageId, x => action(new DiscordMessageOptions(x)), threadId, withComponents, properties?.Original, cancellationToken));
    }
    public Task DeleteMessageAsync(ulong messageId, ulong? threadId = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessageAsync(messageId, threadId, properties?.Original, cancellationToken);
    }
}


public class DiscordWebhookClientConfiguration : IDiscordWebhookClientConfiguration 
{
    private readonly NetCord.Rest.WebhookClientConfiguration _original;
    public DiscordWebhookClientConfiguration(NetCord.Rest.WebhookClientConfiguration original)
    {
        _original = original;
    }
    public NetCord.Rest.WebhookClientConfiguration Original => _original;
    public IDiscordRestClient? Client { get { return _original.Client is null ? null : new DiscordRestClient(_original.Client); } set { _original.Client = value?.Original; } }
}


public class DiscordWebhookMessageProperties : IDiscordWebhookMessageProperties 
{
    private readonly NetCord.Rest.WebhookMessageProperties _original;
    public DiscordWebhookMessageProperties(NetCord.Rest.WebhookMessageProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.WebhookMessageProperties Original => _original;
    public string? Content { get { return _original.Content; } set { _original.Content = value; } }
    public string? Username { get { return _original.Username; } set { _original.Username = value; } }
    public string? AvatarUrl { get { return _original.AvatarUrl; } set { _original.AvatarUrl = value; } }
    public bool Tts { get { return _original.Tts; } set { _original.Tts = value; } }
    public IEnumerable<IDiscordEmbedProperties>? Embeds { get { return _original.Embeds is null ? null : _original.Embeds.Select(x => new DiscordEmbedProperties(x)); } set { _original.Embeds = value?.Select(x => x.Original); } }
    public IDiscordAllowedMentionsProperties? AllowedMentions { get { return _original.AllowedMentions is null ? null : new DiscordAllowedMentionsProperties(_original.AllowedMentions); } set { _original.AllowedMentions = value?.Original; } }
    public IEnumerable<IDiscordComponentProperties>? Components { get { return _original.Components is null ? null : _original.Components.Select(x => new DiscordComponentProperties(x)); } set { _original.Components = value?.Select(x => x.Original); } }
    public IEnumerable<IDiscordAttachmentProperties>? Attachments { get { return _original.Attachments is null ? null : _original.Attachments.Select(x => new DiscordAttachmentProperties(x)); } set { _original.Attachments = value?.Select(x => x.Original); } }
    public NetCord.MessageFlags? Flags { get { return _original.Flags; } set { _original.Flags = value; } }
    public string? ThreadName { get { return _original.ThreadName; } set { _original.ThreadName = value; } }
    public IEnumerable<ulong>? AppliedTags { get { return _original.AppliedTags; } set { _original.AppliedTags = value; } }
    public IDiscordMessagePollProperties? Poll { get { return _original.Poll is null ? null : new DiscordMessagePollProperties(_original.Poll); } set { _original.Poll = value?.Original; } }
    public HttpContent Serialize() 
    {
        return _original.Serialize();
    }
    public IDiscordWebhookMessageProperties WithContent(string content) 
    {
        return new DiscordWebhookMessageProperties(_original.WithContent(content));
    }
    public IDiscordWebhookMessageProperties WithUsername(string username) 
    {
        return new DiscordWebhookMessageProperties(_original.WithUsername(username));
    }
    public IDiscordWebhookMessageProperties WithAvatarUrl(string avatarUrl) 
    {
        return new DiscordWebhookMessageProperties(_original.WithAvatarUrl(avatarUrl));
    }
    public IDiscordWebhookMessageProperties WithTts(bool tts = true) 
    {
        return new DiscordWebhookMessageProperties(_original.WithTts(tts));
    }
    public IDiscordWebhookMessageProperties WithEmbeds(IEnumerable<IDiscordEmbedProperties>? embeds) 
    {
        return new DiscordWebhookMessageProperties(_original.WithEmbeds(embeds?.Select(x => x.Original)));
    }
    public IDiscordWebhookMessageProperties AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds) 
    {
        return new DiscordWebhookMessageProperties(_original.AddEmbeds(embeds.Select(x => x.Original)));
    }
    public IDiscordWebhookMessageProperties AddEmbeds(IDiscordEmbedProperties[] embeds) 
    {
        return new DiscordWebhookMessageProperties(_original.AddEmbeds(embeds.Select(x => x.Original).ToArray()));
    }
    public IDiscordWebhookMessageProperties WithAllowedMentions(IDiscordAllowedMentionsProperties? allowedMentions) 
    {
        return new DiscordWebhookMessageProperties(_original.WithAllowedMentions(allowedMentions?.Original));
    }
    public IDiscordWebhookMessageProperties WithComponents(IEnumerable<IDiscordComponentProperties>? components) 
    {
        return new DiscordWebhookMessageProperties(_original.WithComponents(components?.Select(x => x.Original)));
    }
    public IDiscordWebhookMessageProperties AddComponents(IEnumerable<IDiscordComponentProperties> components) 
    {
        return new DiscordWebhookMessageProperties(_original.AddComponents(components.Select(x => x.Original)));
    }
    public IDiscordWebhookMessageProperties AddComponents(IDiscordComponentProperties[] components) 
    {
        return new DiscordWebhookMessageProperties(_original.AddComponents(components.Select(x => x.Original).ToArray()));
    }
    public IDiscordWebhookMessageProperties WithAttachments(IEnumerable<IDiscordAttachmentProperties>? attachments) 
    {
        return new DiscordWebhookMessageProperties(_original.WithAttachments(attachments?.Select(x => x.Original)));
    }
    public IDiscordWebhookMessageProperties AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments) 
    {
        return new DiscordWebhookMessageProperties(_original.AddAttachments(attachments.Select(x => x.Original)));
    }
    public IDiscordWebhookMessageProperties AddAttachments(IDiscordAttachmentProperties[] attachments) 
    {
        return new DiscordWebhookMessageProperties(_original.AddAttachments(attachments.Select(x => x.Original).ToArray()));
    }
    public IDiscordWebhookMessageProperties WithFlags(NetCord.MessageFlags? flags) 
    {
        return new DiscordWebhookMessageProperties(_original.WithFlags(flags));
    }
    public IDiscordWebhookMessageProperties WithThreadName(string threadName) 
    {
        return new DiscordWebhookMessageProperties(_original.WithThreadName(threadName));
    }
    public IDiscordWebhookMessageProperties WithAppliedTags(IEnumerable<ulong>? appliedTags) 
    {
        return new DiscordWebhookMessageProperties(_original.WithAppliedTags(appliedTags));
    }
    public IDiscordWebhookMessageProperties AddAppliedTags(IEnumerable<ulong> appliedTags) 
    {
        return new DiscordWebhookMessageProperties(_original.AddAppliedTags(appliedTags));
    }
    public IDiscordWebhookMessageProperties AddAppliedTags(ulong[] appliedTags) 
    {
        return new DiscordWebhookMessageProperties(_original.AddAppliedTags(appliedTags));
    }
    public IDiscordWebhookMessageProperties WithPoll(IDiscordMessagePollProperties? poll) 
    {
        return new DiscordWebhookMessageProperties(_original.WithPoll(poll?.Original));
    }
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
    public string? Id => _original.Id;
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
    public string? LargeImageId => _original.LargeImageId;
    public string? LargeText => _original.LargeText;
    public string? SmallImageId => _original.SmallImageId;
    public string? SmallText => _original.SmallText;
}


public class DiscordUserActivitySecrets : IDiscordUserActivitySecrets 
{
    private readonly NetCord.Gateway.UserActivitySecrets _original;
    public DiscordUserActivitySecrets(NetCord.Gateway.UserActivitySecrets original)
    {
        _original = original;
    }
    public NetCord.Gateway.UserActivitySecrets Original => _original;
    public string? Join => _original.Join;
    public string? Spectate => _original.Spectate;
    public string? Match => _original.Match;
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
    public string? CustomMessage => _original.CustomMessage;
}


public class DiscordAutoModerationActionMetadataProperties : IDiscordAutoModerationActionMetadataProperties 
{
    private readonly NetCord.AutoModerationActionMetadataProperties _original;
    public DiscordAutoModerationActionMetadataProperties(NetCord.AutoModerationActionMetadataProperties original)
    {
        _original = original;
    }
    public NetCord.AutoModerationActionMetadataProperties Original => _original;
    public ulong? ChannelId { get { return _original.ChannelId; } set { _original.ChannelId = value; } }
    public int? DurationSeconds { get { return _original.DurationSeconds; } set { _original.DurationSeconds = value; } }
    public string? CustomMessage { get { return _original.CustomMessage; } set { _original.CustomMessage = value; } }
    public IDiscordAutoModerationActionMetadataProperties WithChannelId(ulong? channelId) 
    {
        return new DiscordAutoModerationActionMetadataProperties(_original.WithChannelId(channelId));
    }
    public IDiscordAutoModerationActionMetadataProperties WithDurationSeconds(int? durationSeconds) 
    {
        return new DiscordAutoModerationActionMetadataProperties(_original.WithDurationSeconds(durationSeconds));
    }
    public IDiscordAutoModerationActionMetadataProperties WithCustomMessage(string customMessage) 
    {
        return new DiscordAutoModerationActionMetadataProperties(_original.WithCustomMessage(customMessage));
    }
}


public class DiscordEmojiProperties : IDiscordEmojiProperties 
{
    private readonly NetCord.EmojiProperties _original;
    public DiscordEmojiProperties(NetCord.EmojiProperties original)
    {
        _original = original;
    }
    public NetCord.EmojiProperties Original => _original;
    public ulong? Id { get { return _original.Id; } set { _original.Id = value; } }
    public string? Unicode { get { return _original.Unicode; } set { _original.Unicode = value; } }
    public IDiscordEmojiProperties WithId(ulong? id) 
    {
        return new DiscordEmojiProperties(_original.WithId(id));
    }
    public IDiscordEmojiProperties WithUnicode(string unicode) 
    {
        return new DiscordEmojiProperties(_original.WithUnicode(unicode));
    }
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
    public IDiscordEmoji? Emoji => _original.Emoji is null ? null : new DiscordEmoji(_original.Emoji);
    public string Title => _original.Title;
    public string? Description => _original.Description;
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
    public ulong? Id { get { return _original.Id; } set { _original.Id = value; } }
    public IEnumerable<ulong>? ChannelIds { get { return _original.ChannelIds; } set { _original.ChannelIds = value; } }
    public IEnumerable<ulong>? RoleIds { get { return _original.RoleIds; } set { _original.RoleIds = value; } }
    public ulong? EmojiId { get { return _original.EmojiId; } set { _original.EmojiId = value; } }
    public string? EmojiName { get { return _original.EmojiName; } set { _original.EmojiName = value; } }
    public bool? EmojiAnimated { get { return _original.EmojiAnimated; } set { _original.EmojiAnimated = value; } }
    public string Title { get { return _original.Title; } set { _original.Title = value; } }
    public string? Description { get { return _original.Description; } set { _original.Description = value; } }
    public IDiscordGuildOnboardingPromptOptionProperties WithId(ulong? id) 
    {
        return new DiscordGuildOnboardingPromptOptionProperties(_original.WithId(id));
    }
    public IDiscordGuildOnboardingPromptOptionProperties WithChannelIds(IEnumerable<ulong>? channelIds) 
    {
        return new DiscordGuildOnboardingPromptOptionProperties(_original.WithChannelIds(channelIds));
    }
    public IDiscordGuildOnboardingPromptOptionProperties AddChannelIds(IEnumerable<ulong> channelIds) 
    {
        return new DiscordGuildOnboardingPromptOptionProperties(_original.AddChannelIds(channelIds));
    }
    public IDiscordGuildOnboardingPromptOptionProperties AddChannelIds(ulong[] channelIds) 
    {
        return new DiscordGuildOnboardingPromptOptionProperties(_original.AddChannelIds(channelIds));
    }
    public IDiscordGuildOnboardingPromptOptionProperties WithRoleIds(IEnumerable<ulong>? roleIds) 
    {
        return new DiscordGuildOnboardingPromptOptionProperties(_original.WithRoleIds(roleIds));
    }
    public IDiscordGuildOnboardingPromptOptionProperties AddRoleIds(IEnumerable<ulong> roleIds) 
    {
        return new DiscordGuildOnboardingPromptOptionProperties(_original.AddRoleIds(roleIds));
    }
    public IDiscordGuildOnboardingPromptOptionProperties AddRoleIds(ulong[] roleIds) 
    {
        return new DiscordGuildOnboardingPromptOptionProperties(_original.AddRoleIds(roleIds));
    }
    public IDiscordGuildOnboardingPromptOptionProperties WithEmojiId(ulong? emojiId) 
    {
        return new DiscordGuildOnboardingPromptOptionProperties(_original.WithEmojiId(emojiId));
    }
    public IDiscordGuildOnboardingPromptOptionProperties WithEmojiName(string emojiName) 
    {
        return new DiscordGuildOnboardingPromptOptionProperties(_original.WithEmojiName(emojiName));
    }
    public IDiscordGuildOnboardingPromptOptionProperties WithEmojiAnimated(bool? emojiAnimated = true) 
    {
        return new DiscordGuildOnboardingPromptOptionProperties(_original.WithEmojiAnimated(emojiAnimated));
    }
    public IDiscordGuildOnboardingPromptOptionProperties WithTitle(string title) 
    {
        return new DiscordGuildOnboardingPromptOptionProperties(_original.WithTitle(title));
    }
    public IDiscordGuildOnboardingPromptOptionProperties WithDescription(string description) 
    {
        return new DiscordGuildOnboardingPromptOptionProperties(_original.WithDescription(description));
    }
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
    public IReadOnlyDictionary<string, string>? NameLocalizations => _original.NameLocalizations;
    public string? ValueString => _original.ValueString;
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
    public string Name { get { return _original.Name; } set { _original.Name = value; } }
    public IReadOnlyDictionary<string, string>? NameLocalizations { get { return _original.NameLocalizations; } set { _original.NameLocalizations = value; } }
    public string? StringValue { get { return _original.StringValue; } set { _original.StringValue = value; } }
    public double? NumericValue { get { return _original.NumericValue; } set { _original.NumericValue = value; } }
    public NetCord.Rest.ApplicationCommandOptionChoiceValueType ValueType { get { return _original.ValueType; } set { _original.ValueType = value; } }
    public IDiscordApplicationCommandOptionChoiceProperties WithName(string name) 
    {
        return new DiscordApplicationCommandOptionChoiceProperties(_original.WithName(name));
    }
    public IDiscordApplicationCommandOptionChoiceProperties WithNameLocalizations(IReadOnlyDictionary<string, string>? nameLocalizations) 
    {
        return new DiscordApplicationCommandOptionChoiceProperties(_original.WithNameLocalizations(nameLocalizations));
    }
    public IDiscordApplicationCommandOptionChoiceProperties WithStringValue(string stringValue) 
    {
        return new DiscordApplicationCommandOptionChoiceProperties(_original.WithStringValue(stringValue));
    }
    public IDiscordApplicationCommandOptionChoiceProperties WithNumericValue(double? numericValue) 
    {
        return new DiscordApplicationCommandOptionChoiceProperties(_original.WithNumericValue(numericValue));
    }
    public IDiscordApplicationCommandOptionChoiceProperties WithValueType(NetCord.Rest.ApplicationCommandOptionChoiceValueType valueType) 
    {
        return new DiscordApplicationCommandOptionChoiceProperties(_original.WithValueType(valueType));
    }
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
    public string? GlobalName => _original.GlobalName;
    public string? AvatarHash => _original.AvatarHash;
    public bool IsBot => _original.IsBot;
    public bool? IsSystemUser => _original.IsSystemUser;
    public bool? MfaEnabled => _original.MfaEnabled;
    public string? BannerHash => _original.BannerHash;
    public NetCord.Color? AccentColor => _original.AccentColor;
    public string? Locale => _original.Locale;
    public bool? Verified => _original.Verified;
    public string? Email => _original.Email;
    public NetCord.UserFlags? Flags => _original.Flags;
    public NetCord.PremiumType? PremiumType => _original.PremiumType;
    public NetCord.UserFlags? PublicFlags => _original.PublicFlags;
    public IDiscordAvatarDecorationData? AvatarDecorationData => _original.AvatarDecorationData is null ? null : new DiscordAvatarDecorationData(_original.AvatarDecorationData);
    public bool HasAvatar => _original.HasAvatar;
    public bool HasBanner => _original.HasBanner;
    public bool HasAvatarDecoration => _original.HasAvatarDecoration;
    public IDiscordImageUrl DefaultAvatarUrl => new DiscordImageUrl(_original.DefaultAvatarUrl);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public IDiscordImageUrl? GetAvatarUrl(NetCord.ImageFormat? format = default) 
    {
        var temp = _original.GetAvatarUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetBannerUrl(NetCord.ImageFormat? format = default) 
    {
        var temp = _original.GetBannerUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetAvatarDecorationUrl() 
    {
        var temp = _original.GetAvatarDecorationUrl();
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public async Task<IDiscordUser> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordUser(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordDMChannel> GetDMChannelAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordDMChannel(await _original.GetDMChannelAsync(properties?.Original, cancellationToken));
    }
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
    public IDiscordToken? Token => _original.Token is null ? null : new DiscordToken(_original.Token);
    public async Task<IDiscordCurrentApplication> GetCurrentApplicationAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordCurrentApplication(await _original.GetCurrentApplicationAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordCurrentApplication> ModifyCurrentApplicationAsync(Action<IDiscordCurrentApplicationOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordCurrentApplication(await _original.ModifyCurrentApplicationAsync(x => action(new DiscordCurrentApplicationOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordApplicationRoleConnectionMetadata>> GetApplicationRoleConnectionMetadataRecordsAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetApplicationRoleConnectionMetadataRecordsAsync(applicationId, properties?.Original, cancellationToken)).Select(x => new DiscordApplicationRoleConnectionMetadata(x)).ToList();
    }
    public async Task<IReadOnlyList<IDiscordApplicationRoleConnectionMetadata>> UpdateApplicationRoleConnectionMetadataRecordsAsync(ulong applicationId, IEnumerable<IDiscordApplicationRoleConnectionMetadataProperties> applicationRoleConnectionMetadataProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.UpdateApplicationRoleConnectionMetadataRecordsAsync(applicationId, applicationRoleConnectionMetadataProperties.Select(x => x.Original), properties?.Original, cancellationToken)).Select(x => new DiscordApplicationRoleConnectionMetadata(x)).ToList();
    }
    public async IAsyncEnumerable<IDiscordRestAuditLogEntry> GetGuildAuditLogAsync(ulong guildId, IDiscordGuildAuditLogPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetGuildAuditLogAsync(guildId, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordRestAuditLogEntry(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordAutoModerationRule>> GetAutoModerationRulesAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetAutoModerationRulesAsync(guildId, properties?.Original, cancellationToken)).Select(x => new DiscordAutoModerationRule(x)).ToList();
    }
    public async Task<IDiscordAutoModerationRule> GetAutoModerationRuleAsync(ulong guildId, ulong autoModerationRuleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordAutoModerationRule(await _original.GetAutoModerationRuleAsync(guildId, autoModerationRuleId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordAutoModerationRule> CreateAutoModerationRuleAsync(ulong guildId, IDiscordAutoModerationRuleProperties autoModerationRuleProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordAutoModerationRule(await _original.CreateAutoModerationRuleAsync(guildId, autoModerationRuleProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordAutoModerationRule> ModifyAutoModerationRuleAsync(ulong guildId, ulong autoModerationRuleId, Action<IDiscordAutoModerationRuleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordAutoModerationRule(await _original.ModifyAutoModerationRuleAsync(guildId, autoModerationRuleId, x => action(new DiscordAutoModerationRuleOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteAutoModerationRuleAsync(ulong guildId, ulong autoModerationRuleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAutoModerationRuleAsync(guildId, autoModerationRuleId, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordChannel> GetChannelAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordChannel(await _original.GetChannelAsync(channelId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordChannel> ModifyGroupDMChannelAsync(ulong channelId, Action<IDiscordGroupDMChannelOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordChannel(await _original.ModifyGroupDMChannelAsync(channelId, x => action(new DiscordGroupDMChannelOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IDiscordChannel> ModifyGuildChannelAsync(ulong channelId, Action<IDiscordGuildChannelOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordChannel(await _original.ModifyGuildChannelAsync(channelId, x => action(new DiscordGuildChannelOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IDiscordChannel> DeleteChannelAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordChannel(await _original.DeleteChannelAsync(channelId, properties?.Original, cancellationToken));
    }
    public async IAsyncEnumerable<IDiscordRestMessage> GetMessagesAsync(ulong channelId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetMessagesAsync(channelId, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordRestMessage(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetMessagesAroundAsync(ulong channelId, ulong messageId, int? limit = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetMessagesAroundAsync(channelId, messageId, limit, properties?.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    }
    public async Task<IDiscordRestMessage> GetMessageAsync(ulong channelId, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.GetMessageAsync(channelId, messageId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestMessage> SendMessageAsync(ulong channelId, IDiscordMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.SendMessageAsync(channelId, message.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestMessage> CrosspostMessageAsync(ulong channelId, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.CrosspostMessageAsync(channelId, messageId, properties?.Original, cancellationToken));
    }
    public Task AddMessageReactionAsync(ulong channelId, ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.AddMessageReactionAsync(channelId, messageId, emoji.Original, properties?.Original, cancellationToken);
    }
    public Task DeleteMessageReactionAsync(ulong channelId, ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessageReactionAsync(channelId, messageId, emoji.Original, properties?.Original, cancellationToken);
    }
    public Task DeleteMessageReactionAsync(ulong channelId, ulong messageId, IDiscordReactionEmojiProperties emoji, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessageReactionAsync(channelId, messageId, emoji.Original, userId, properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordUser> GetMessageReactionsAsync(ulong channelId, ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordMessageReactionsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetMessageReactionsAsync(channelId, messageId, emoji.Original, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public Task DeleteAllMessageReactionsAsync(ulong channelId, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAllMessageReactionsAsync(channelId, messageId, properties?.Original, cancellationToken);
    }
    public Task DeleteAllMessageReactionsAsync(ulong channelId, ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAllMessageReactionsAsync(channelId, messageId, emoji.Original, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordRestMessage> ModifyMessageAsync(ulong channelId, ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.ModifyMessageAsync(channelId, messageId, x => action(new DiscordMessageOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteMessageAsync(ulong channelId, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessageAsync(channelId, messageId, properties?.Original, cancellationToken);
    }
    public Task DeleteMessagesAsync(ulong channelId, IEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessagesAsync(channelId, messageIds, properties?.Original, cancellationToken);
    }
    public Task DeleteMessagesAsync(ulong channelId, IAsyncEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessagesAsync(channelId, messageIds, properties?.Original, cancellationToken);
    }
    public Task ModifyGuildChannelPermissionsAsync(ulong channelId, IDiscordPermissionOverwriteProperties permissionOverwrite, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.ModifyGuildChannelPermissionsAsync(channelId, permissionOverwrite.Original, properties?.Original, cancellationToken);
    }
    public async Task<IEnumerable<IDiscordRestInvite>> GetGuildChannelInvitesAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetGuildChannelInvitesAsync(channelId, properties?.Original, cancellationToken)).Select(x => new DiscordRestInvite(x));
    }
    public async Task<IDiscordRestInvite> CreateGuildChannelInviteAsync(ulong channelId, IDiscordInviteProperties? inviteProperties = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestInvite(await _original.CreateGuildChannelInviteAsync(channelId, inviteProperties?.Original, properties?.Original, cancellationToken));
    }
    public Task DeleteGuildChannelPermissionAsync(ulong channelId, ulong overwriteId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteGuildChannelPermissionAsync(channelId, overwriteId, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordFollowedChannel> FollowAnnouncementGuildChannelAsync(ulong channelId, ulong webhookChannelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordFollowedChannel(await _original.FollowAnnouncementGuildChannelAsync(channelId, webhookChannelId, properties?.Original, cancellationToken));
    }
    public Task TriggerTypingStateAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.TriggerTypingStateAsync(channelId, properties?.Original, cancellationToken);
    }
    public Task<IDisposable> EnterTypingStateAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.EnterTypingStateAsync(channelId, properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetPinnedMessagesAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetPinnedMessagesAsync(channelId, properties?.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    }
    public Task PinMessageAsync(ulong channelId, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.PinMessageAsync(channelId, messageId, properties?.Original, cancellationToken);
    }
    public Task UnpinMessageAsync(ulong channelId, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.UnpinMessageAsync(channelId, messageId, properties?.Original, cancellationToken);
    }
    public Task GroupDMChannelAddUserAsync(ulong channelId, ulong userId, IDiscordGroupDMChannelUserAddProperties groupDMChannelUserAddProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.GroupDMChannelAddUserAsync(channelId, userId, groupDMChannelUserAddProperties.Original, properties?.Original, cancellationToken);
    }
    public Task GroupDMChannelDeleteUserAsync(ulong channelId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.GroupDMChannelDeleteUserAsync(channelId, userId, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordGuildThread> CreateGuildThreadAsync(ulong channelId, ulong messageId, IDiscordGuildThreadFromMessageProperties threadFromMessageProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildThread(await _original.CreateGuildThreadAsync(channelId, messageId, threadFromMessageProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildThread> CreateGuildThreadAsync(ulong channelId, IDiscordGuildThreadProperties threadProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildThread(await _original.CreateGuildThreadAsync(channelId, threadProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordForumGuildThread> CreateForumGuildThreadAsync(ulong channelId, IDiscordForumGuildThreadProperties threadProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordForumGuildThread(await _original.CreateForumGuildThreadAsync(channelId, threadProperties.Original, properties?.Original, cancellationToken));
    }
    public Task JoinGuildThreadAsync(ulong threadId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.JoinGuildThreadAsync(threadId, properties?.Original, cancellationToken);
    }
    public Task AddGuildThreadUserAsync(ulong threadId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.AddGuildThreadUserAsync(threadId, userId, properties?.Original, cancellationToken);
    }
    public Task LeaveGuildThreadAsync(ulong threadId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.LeaveGuildThreadAsync(threadId, properties?.Original, cancellationToken);
    }
    public Task DeleteGuildThreadUserAsync(ulong threadId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteGuildThreadUserAsync(threadId, userId, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordThreadUser> GetGuildThreadUserAsync(ulong threadId, ulong userId, bool withGuildUser = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordThreadUser(await _original.GetGuildThreadUserAsync(threadId, userId, withGuildUser, properties?.Original, cancellationToken));
    }
    public async IAsyncEnumerable<IDiscordThreadUser> GetGuildThreadUsersAsync(ulong threadId, IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetGuildThreadUsersAsync(threadId, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordThreadUser(original);
        }
    }
    public async IAsyncEnumerable<IDiscordGuildThread> GetPublicArchivedGuildThreadsAsync(ulong channelId, IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetPublicArchivedGuildThreadsAsync(channelId, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordGuildThread(original);
        }
    }
    public async IAsyncEnumerable<IDiscordGuildThread> GetPrivateArchivedGuildThreadsAsync(ulong channelId, IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetPrivateArchivedGuildThreadsAsync(channelId, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordGuildThread(original);
        }
    }
    public async IAsyncEnumerable<IDiscordGuildThread> GetJoinedPrivateArchivedGuildThreadsAsync(ulong channelId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetJoinedPrivateArchivedGuildThreadsAsync(channelId, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordGuildThread(original);
        }
    }
    public Task<Stream> SendRequestAsync(HttpMethod method, FormattableString route, string? query = null, NetCord.Rest.TopLevelResourceInfo? resourceInfo = default, IDiscordRestRequestProperties? properties = null, bool global = true, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.SendRequestAsync(method, route, query, resourceInfo, properties?.Original, global, cancellationToken);
    }
    public Task<Stream> SendRequestAsync(HttpMethod method, HttpContent content, FormattableString route, string? query = null, NetCord.Rest.TopLevelResourceInfo? resourceInfo = default, IDiscordRestRequestProperties? properties = null, bool global = true, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.SendRequestAsync(method, content, route, query, resourceInfo, properties?.Original, global, cancellationToken);
    }
    public void Dispose() 
    {
        _original.Dispose();
    }
    public async Task<IReadOnlyList<IDiscordGuildEmoji>> GetGuildEmojisAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetGuildEmojisAsync(guildId, properties?.Original, cancellationToken)).Select(x => new DiscordGuildEmoji(x)).ToList();
    }
    public async Task<IDiscordGuildEmoji> GetGuildEmojiAsync(ulong guildId, ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildEmoji(await _original.GetGuildEmojiAsync(guildId, emojiId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildEmoji> CreateGuildEmojiAsync(ulong guildId, IDiscordGuildEmojiProperties guildEmojiProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildEmoji(await _original.CreateGuildEmojiAsync(guildId, guildEmojiProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildEmoji> ModifyGuildEmojiAsync(ulong guildId, ulong emojiId, Action<IDiscordGuildEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildEmoji(await _original.ModifyGuildEmojiAsync(guildId, emojiId, x => action(new DiscordGuildEmojiOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteGuildEmojiAsync(ulong guildId, ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteGuildEmojiAsync(guildId, emojiId, properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordApplicationEmoji>> GetApplicationEmojisAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetApplicationEmojisAsync(applicationId, properties?.Original, cancellationToken)).Select(x => new DiscordApplicationEmoji(x)).ToList();
    }
    public async Task<IDiscordApplicationEmoji> GetApplicationEmojiAsync(ulong applicationId, ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationEmoji(await _original.GetApplicationEmojiAsync(applicationId, emojiId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordApplicationEmoji> CreateApplicationEmojiAsync(ulong applicationId, IDiscordApplicationEmojiProperties applicationEmojiProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationEmoji(await _original.CreateApplicationEmojiAsync(applicationId, applicationEmojiProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordApplicationEmoji> ModifyApplicationEmojiAsync(ulong applicationId, ulong emojiId, Action<IDiscordApplicationEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationEmoji(await _original.ModifyApplicationEmojiAsync(applicationId, emojiId, x => action(new DiscordApplicationEmojiOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteApplicationEmojiAsync(ulong applicationId, ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteApplicationEmojiAsync(applicationId, emojiId, properties?.Original, cancellationToken);
    }
    public Task<string> GetGatewayAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.GetGatewayAsync(properties?.Original, cancellationToken);
    }
    public async Task<IDiscordGatewayBot> GetGatewayBotAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGatewayBot(await _original.GetGatewayBotAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestGuild> CreateGuildAsync(IDiscordGuildProperties guildProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestGuild(await _original.CreateGuildAsync(guildProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestGuild> GetGuildAsync(ulong guildId, bool withCounts = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestGuild(await _original.GetGuildAsync(guildId, withCounts, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildPreview> GetGuildPreviewAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildPreview(await _original.GetGuildPreviewAsync(guildId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestGuild> ModifyGuildAsync(ulong guildId, Action<IDiscordGuildOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestGuild(await _original.ModifyGuildAsync(guildId, x => action(new DiscordGuildOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteGuildAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteGuildAsync(guildId, properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordGuildChannel>> GetGuildChannelsAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetGuildChannelsAsync(guildId, properties?.Original, cancellationToken)).Select(x => new DiscordGuildChannel(x)).ToList();
    }
    public async Task<IDiscordGuildChannel> CreateGuildChannelAsync(ulong guildId, IDiscordGuildChannelProperties channelProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildChannel(await _original.CreateGuildChannelAsync(guildId, channelProperties.Original, properties?.Original, cancellationToken));
    }
    public Task ModifyGuildChannelPositionsAsync(ulong guildId, IEnumerable<IDiscordGuildChannelPositionProperties> positions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.ModifyGuildChannelPositionsAsync(guildId, positions.Select(x => x.Original), properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordGuildThread>> GetActiveGuildThreadsAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetActiveGuildThreadsAsync(guildId, properties?.Original, cancellationToken)).Select(x => new DiscordGuildThread(x)).ToList();
    }
    public async Task<IDiscordGuildUser> GetGuildUserAsync(ulong guildId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildUser(await _original.GetGuildUserAsync(guildId, userId, properties?.Original, cancellationToken));
    }
    public async IAsyncEnumerable<IDiscordGuildUser> GetGuildUsersAsync(ulong guildId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetGuildUsersAsync(guildId, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordGuildUser(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordGuildUser>> FindGuildUserAsync(ulong guildId, string name, int limit, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.FindGuildUserAsync(guildId, name, limit, properties?.Original, cancellationToken)).Select(x => new DiscordGuildUser(x)).ToList();
    }
    public async Task<IDiscordGuildUser?> AddGuildUserAsync(ulong guildId, ulong userId, IDiscordGuildUserProperties userProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        var temp = await _original.AddGuildUserAsync(guildId, userId, userProperties.Original, properties?.Original, cancellationToken);
        return temp is null ? null : new DiscordGuildUser(temp);
    }
    public async Task<IDiscordGuildUser> ModifyGuildUserAsync(ulong guildId, ulong userId, Action<IDiscordGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildUser(await _original.ModifyGuildUserAsync(guildId, userId, x => action(new DiscordGuildUserOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildUser> ModifyCurrentGuildUserAsync(ulong guildId, Action<IDiscordCurrentGuildUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildUser(await _original.ModifyCurrentGuildUserAsync(guildId, x => action(new DiscordCurrentGuildUserOptions(x)), properties?.Original, cancellationToken));
    }
    public Task AddGuildUserRoleAsync(ulong guildId, ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.AddGuildUserRoleAsync(guildId, userId, roleId, properties?.Original, cancellationToken);
    }
    public Task RemoveGuildUserRoleAsync(ulong guildId, ulong userId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.RemoveGuildUserRoleAsync(guildId, userId, roleId, properties?.Original, cancellationToken);
    }
    public Task KickGuildUserAsync(ulong guildId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.KickGuildUserAsync(guildId, userId, properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordGuildBan> GetGuildBansAsync(ulong guildId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetGuildBansAsync(guildId, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordGuildBan(original);
        }
    }
    public async Task<IDiscordGuildBan> GetGuildBanAsync(ulong guildId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildBan(await _original.GetGuildBanAsync(guildId, userId, properties?.Original, cancellationToken));
    }
    public Task BanGuildUserAsync(ulong guildId, ulong userId, int deleteMessageSeconds = 0, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.BanGuildUserAsync(guildId, userId, deleteMessageSeconds, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordGuildBulkBan> BanGuildUsersAsync(ulong guildId, IEnumerable<ulong> userIds, int deleteMessageSeconds = 0, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildBulkBan(await _original.BanGuildUsersAsync(guildId, userIds, deleteMessageSeconds, properties?.Original, cancellationToken));
    }
    public Task UnbanGuildUserAsync(ulong guildId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.UnbanGuildUserAsync(guildId, userId, properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordRole>> GetGuildRolesAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetGuildRolesAsync(guildId, properties?.Original, cancellationToken)).Select(x => new DiscordRole(x)).ToList();
    }
    public async Task<IDiscordRole> GetGuildRoleAsync(ulong guildId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRole(await _original.GetGuildRoleAsync(guildId, roleId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRole> CreateGuildRoleAsync(ulong guildId, IDiscordRoleProperties guildRoleProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRole(await _original.CreateGuildRoleAsync(guildId, guildRoleProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordRole>> ModifyGuildRolePositionsAsync(ulong guildId, IEnumerable<IDiscordRolePositionProperties> positions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.ModifyGuildRolePositionsAsync(guildId, positions.Select(x => x.Original), properties?.Original, cancellationToken)).Select(x => new DiscordRole(x)).ToList();
    }
    public async Task<IDiscordRole> ModifyGuildRoleAsync(ulong guildId, ulong roleId, Action<IDiscordRoleOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRole(await _original.ModifyGuildRoleAsync(guildId, roleId, x => action(new DiscordRoleOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteGuildRoleAsync(ulong guildId, ulong roleId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteGuildRoleAsync(guildId, roleId, properties?.Original, cancellationToken);
    }
    public Task<NetCord.MfaLevel> ModifyGuildMfaLevelAsync(ulong guildId, NetCord.MfaLevel mfaLevel, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.ModifyGuildMfaLevelAsync(guildId, mfaLevel, properties?.Original, cancellationToken);
    }
    public Task<int> GetGuildPruneCountAsync(ulong guildId, int days, IEnumerable<ulong>? roles = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.GetGuildPruneCountAsync(guildId, days, roles, properties?.Original, cancellationToken);
    }
    public Task<int?> GuildPruneAsync(ulong guildId, IDiscordGuildPruneProperties pruneProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.GuildPruneAsync(guildId, pruneProperties.Original, properties?.Original, cancellationToken);
    }
    public async Task<IEnumerable<IDiscordVoiceRegion>> GetGuildVoiceRegionsAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetGuildVoiceRegionsAsync(guildId, properties?.Original, cancellationToken)).Select(x => new DiscordVoiceRegion(x));
    }
    public async Task<IEnumerable<IDiscordRestInvite>> GetGuildInvitesAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetGuildInvitesAsync(guildId, properties?.Original, cancellationToken)).Select(x => new DiscordRestInvite(x));
    }
    public async Task<IReadOnlyList<IDiscordIntegration>> GetGuildIntegrationsAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetGuildIntegrationsAsync(guildId, properties?.Original, cancellationToken)).Select(x => new DiscordIntegration(x)).ToList();
    }
    public Task DeleteGuildIntegrationAsync(ulong guildId, ulong integrationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteGuildIntegrationAsync(guildId, integrationId, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordGuildWidgetSettings> GetGuildWidgetSettingsAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildWidgetSettings(await _original.GetGuildWidgetSettingsAsync(guildId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildWidgetSettings> ModifyGuildWidgetSettingsAsync(ulong guildId, Action<IDiscordGuildWidgetSettingsOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildWidgetSettings(await _original.ModifyGuildWidgetSettingsAsync(guildId, x => action(new DiscordGuildWidgetSettingsOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildWidget> GetGuildWidgetAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildWidget(await _original.GetGuildWidgetAsync(guildId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildVanityInvite> GetGuildVanityInviteAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildVanityInvite(await _original.GetGuildVanityInviteAsync(guildId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildWelcomeScreen> GetGuildWelcomeScreenAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildWelcomeScreen(await _original.GetGuildWelcomeScreenAsync(guildId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildWelcomeScreen> ModifyGuildWelcomeScreenAsync(ulong guildId, Action<IDiscordGuildWelcomeScreenOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildWelcomeScreen(await _original.ModifyGuildWelcomeScreenAsync(guildId, x => action(new DiscordGuildWelcomeScreenOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildOnboarding> GetGuildOnboardingAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildOnboarding(await _original.GetGuildOnboardingAsync(guildId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildOnboarding> ModifyGuildOnboardingAsync(ulong guildId, Action<IDiscordGuildOnboardingOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildOnboarding(await _original.ModifyGuildOnboardingAsync(guildId, x => action(new DiscordGuildOnboardingOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordGuildScheduledEvent>> GetGuildScheduledEventsAsync(ulong guildId, bool withUserCount = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetGuildScheduledEventsAsync(guildId, withUserCount, properties?.Original, cancellationToken)).Select(x => new DiscordGuildScheduledEvent(x)).ToList();
    }
    public async Task<IDiscordGuildScheduledEvent> CreateGuildScheduledEventAsync(ulong guildId, IDiscordGuildScheduledEventProperties guildScheduledEventProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildScheduledEvent(await _original.CreateGuildScheduledEventAsync(guildId, guildScheduledEventProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildScheduledEvent> GetGuildScheduledEventAsync(ulong guildId, ulong scheduledEventId, bool withUserCount = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildScheduledEvent(await _original.GetGuildScheduledEventAsync(guildId, scheduledEventId, withUserCount, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildScheduledEvent> ModifyGuildScheduledEventAsync(ulong guildId, ulong scheduledEventId, Action<IDiscordGuildScheduledEventOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildScheduledEvent(await _original.ModifyGuildScheduledEventAsync(guildId, scheduledEventId, x => action(new DiscordGuildScheduledEventOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteGuildScheduledEventAsync(ulong guildId, ulong scheduledEventId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteGuildScheduledEventAsync(guildId, scheduledEventId, properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordGuildScheduledEventUser> GetGuildScheduledEventUsersAsync(ulong guildId, ulong scheduledEventId, IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetGuildScheduledEventUsersAsync(guildId, scheduledEventId, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordGuildScheduledEventUser(original);
        }
    }
    public async Task<IDiscordGuildTemplate> GetGuildTemplateAsync(string templateCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildTemplate(await _original.GetGuildTemplateAsync(templateCode, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestGuild> CreateGuildFromGuildTemplateAsync(string templateCode, IDiscordGuildFromGuildTemplateProperties guildProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestGuild(await _original.CreateGuildFromGuildTemplateAsync(templateCode, guildProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IEnumerable<IDiscordGuildTemplate>> GetGuildTemplatesAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetGuildTemplatesAsync(guildId, properties?.Original, cancellationToken)).Select(x => new DiscordGuildTemplate(x));
    }
    public async Task<IDiscordGuildTemplate> CreateGuildTemplateAsync(ulong guildId, IDiscordGuildTemplateProperties guildTemplateProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildTemplate(await _original.CreateGuildTemplateAsync(guildId, guildTemplateProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildTemplate> SyncGuildTemplateAsync(ulong guildId, string templateCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildTemplate(await _original.SyncGuildTemplateAsync(guildId, templateCode, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildTemplate> ModifyGuildTemplateAsync(ulong guildId, string templateCode, Action<IDiscordGuildTemplateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildTemplate(await _original.ModifyGuildTemplateAsync(guildId, templateCode, x => action(new DiscordGuildTemplateOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildTemplate> DeleteGuildTemplateAsync(ulong guildId, string templateCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildTemplate(await _original.DeleteGuildTemplateAsync(guildId, templateCode, properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordApplicationCommand>> GetGlobalApplicationCommandsAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetGlobalApplicationCommandsAsync(applicationId, properties?.Original, cancellationToken)).Select(x => new DiscordApplicationCommand(x)).ToList();
    }
    public async Task<IDiscordApplicationCommand> CreateGlobalApplicationCommandAsync(ulong applicationId, IDiscordApplicationCommandProperties applicationCommandProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationCommand(await _original.CreateGlobalApplicationCommandAsync(applicationId, applicationCommandProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordApplicationCommand> GetGlobalApplicationCommandAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationCommand(await _original.GetGlobalApplicationCommandAsync(applicationId, commandId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordApplicationCommand> ModifyGlobalApplicationCommandAsync(ulong applicationId, ulong commandId, Action<IDiscordApplicationCommandOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationCommand(await _original.ModifyGlobalApplicationCommandAsync(applicationId, commandId, x => action(new DiscordApplicationCommandOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteGlobalApplicationCommandAsync(ulong applicationId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteGlobalApplicationCommandAsync(applicationId, commandId, properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordApplicationCommand>> BulkOverwriteGlobalApplicationCommandsAsync(ulong applicationId, IEnumerable<IDiscordApplicationCommandProperties> commands, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.BulkOverwriteGlobalApplicationCommandsAsync(applicationId, commands.Select(x => x.Original), properties?.Original, cancellationToken)).Select(x => new DiscordApplicationCommand(x)).ToList();
    }
    public async Task<IReadOnlyList<IDiscordGuildApplicationCommand>> GetGuildApplicationCommandsAsync(ulong applicationId, ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetGuildApplicationCommandsAsync(applicationId, guildId, properties?.Original, cancellationToken)).Select(x => new DiscordGuildApplicationCommand(x)).ToList();
    }
    public async Task<IDiscordGuildApplicationCommand> CreateGuildApplicationCommandAsync(ulong applicationId, ulong guildId, IDiscordApplicationCommandProperties applicationCommandProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildApplicationCommand(await _original.CreateGuildApplicationCommandAsync(applicationId, guildId, applicationCommandProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildApplicationCommand> GetGuildApplicationCommandAsync(ulong applicationId, ulong guildId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildApplicationCommand(await _original.GetGuildApplicationCommandAsync(applicationId, guildId, commandId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildApplicationCommand> ModifyGuildApplicationCommandAsync(ulong applicationId, ulong guildId, ulong commandId, Action<IDiscordApplicationCommandOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildApplicationCommand(await _original.ModifyGuildApplicationCommandAsync(applicationId, guildId, commandId, x => action(new DiscordApplicationCommandOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteGuildApplicationCommandAsync(ulong applicationId, ulong guildId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteGuildApplicationCommandAsync(applicationId, guildId, commandId, properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordGuildApplicationCommand>> BulkOverwriteGuildApplicationCommandsAsync(ulong applicationId, ulong guildId, IEnumerable<IDiscordApplicationCommandProperties> commands, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.BulkOverwriteGuildApplicationCommandsAsync(applicationId, guildId, commands.Select(x => x.Original), properties?.Original, cancellationToken)).Select(x => new DiscordGuildApplicationCommand(x)).ToList();
    }
    public async Task<IReadOnlyList<IDiscordApplicationCommandGuildPermissions>> GetApplicationCommandsGuildPermissionsAsync(ulong applicationId, ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetApplicationCommandsGuildPermissionsAsync(applicationId, guildId, properties?.Original, cancellationToken)).Select(x => new DiscordApplicationCommandGuildPermissions(x)).ToList();
    }
    public async Task<IDiscordApplicationCommandGuildPermissions> GetApplicationCommandGuildPermissionsAsync(ulong applicationId, ulong guildId, ulong commandId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationCommandGuildPermissions(await _original.GetApplicationCommandGuildPermissionsAsync(applicationId, guildId, commandId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordApplicationCommandGuildPermissions> OverwriteApplicationCommandGuildPermissionsAsync(ulong applicationId, ulong guildId, ulong commandId, IEnumerable<IDiscordApplicationCommandGuildPermissionProperties> newPermissions, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationCommandGuildPermissions(await _original.OverwriteApplicationCommandGuildPermissionsAsync(applicationId, guildId, commandId, newPermissions.Select(x => x.Original), properties?.Original, cancellationToken));
    }
    public Task SendInteractionResponseAsync(ulong interactionId, string interactionToken, IDiscordInteractionCallback callback, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.SendInteractionResponseAsync(interactionId, interactionToken, callback.Original, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordRestMessage> GetInteractionResponseAsync(ulong applicationId, string interactionToken, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.GetInteractionResponseAsync(applicationId, interactionToken, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestMessage> ModifyInteractionResponseAsync(ulong applicationId, string interactionToken, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.ModifyInteractionResponseAsync(applicationId, interactionToken, x => action(new DiscordMessageOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteInteractionResponseAsync(ulong applicationId, string interactionToken, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteInteractionResponseAsync(applicationId, interactionToken, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordRestMessage> SendInteractionFollowupMessageAsync(ulong applicationId, string interactionToken, IDiscordInteractionMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.SendInteractionFollowupMessageAsync(applicationId, interactionToken, message.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestMessage> GetInteractionFollowupMessageAsync(ulong applicationId, string interactionToken, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.GetInteractionFollowupMessageAsync(applicationId, interactionToken, messageId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestMessage> ModifyInteractionFollowupMessageAsync(ulong applicationId, string interactionToken, ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.ModifyInteractionFollowupMessageAsync(applicationId, interactionToken, messageId, x => action(new DiscordMessageOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteInteractionFollowupMessageAsync(ulong applicationId, string interactionToken, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteInteractionFollowupMessageAsync(applicationId, interactionToken, messageId, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordRestInvite> GetGuildInviteAsync(string inviteCode, bool withCounts = false, bool withExpiration = false, ulong? guildScheduledEventId = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestInvite(await _original.GetGuildInviteAsync(inviteCode, withCounts, withExpiration, guildScheduledEventId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestInvite> DeleteGuildInviteAsync(string inviteCode, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestInvite(await _original.DeleteGuildInviteAsync(inviteCode, properties?.Original, cancellationToken));
    }
    public async IAsyncEnumerable<IDiscordEntitlement> GetEntitlementsAsync(ulong applicationId, IDiscordEntitlementsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetEntitlementsAsync(applicationId, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordEntitlement(original);
        }
    }
    public async Task<IDiscordEntitlement> GetEntitlementAsync(ulong applicationId, ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordEntitlement(await _original.GetEntitlementAsync(applicationId, entitlementId, properties?.Original, cancellationToken));
    }
    public Task ConsumeEntitlementAsync(ulong applicationId, ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.ConsumeEntitlementAsync(applicationId, entitlementId, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordEntitlement> CreateTestEntitlementAsync(ulong applicationId, IDiscordTestEntitlementProperties testEntitlementProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordEntitlement(await _original.CreateTestEntitlementAsync(applicationId, testEntitlementProperties.Original, properties?.Original, cancellationToken));
    }
    public Task DeleteTestEntitlementAsync(ulong applicationId, ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteTestEntitlementAsync(applicationId, entitlementId, properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordSku>> GetSkusAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetSkusAsync(applicationId, properties?.Original, cancellationToken)).Select(x => new DiscordSku(x)).ToList();
    }
    public async Task<IDiscordCurrentApplication> GetCurrentBotApplicationInformationAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordCurrentApplication(await _original.GetCurrentBotApplicationInformationAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordAuthorizationInformation> GetCurrentAuthorizationInformationAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordAuthorizationInformation(await _original.GetCurrentAuthorizationInformationAsync(properties?.Original, cancellationToken));
    }
    public async IAsyncEnumerable<IDiscordUser> GetMessagePollAnswerVotersAsync(ulong channelId, ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetMessagePollAnswerVotersAsync(channelId, messageId, answerId, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public async Task<IDiscordRestMessage> EndMessagePollAsync(ulong channelId, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.EndMessagePollAsync(channelId, messageId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordStageInstance> CreateStageInstanceAsync(IDiscordStageInstanceProperties stageInstanceProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordStageInstance(await _original.CreateStageInstanceAsync(stageInstanceProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordStageInstance> GetStageInstanceAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordStageInstance(await _original.GetStageInstanceAsync(channelId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordStageInstance> ModifyStageInstanceAsync(ulong channelId, Action<IDiscordStageInstanceOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordStageInstance(await _original.ModifyStageInstanceAsync(channelId, x => action(new DiscordStageInstanceOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteStageInstanceAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteStageInstanceAsync(channelId, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordStandardSticker> GetStickerAsync(ulong stickerId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordStandardSticker(await _original.GetStickerAsync(stickerId, properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordStickerPack>> GetStickerPacksAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetStickerPacksAsync(properties?.Original, cancellationToken)).Select(x => new DiscordStickerPack(x)).ToList();
    }
    public async Task<IDiscordStickerPack> GetStickerPackAsync(ulong stickerPackId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordStickerPack(await _original.GetStickerPackAsync(stickerPackId, properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordGuildSticker>> GetGuildStickersAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetGuildStickersAsync(guildId, properties?.Original, cancellationToken)).Select(x => new DiscordGuildSticker(x)).ToList();
    }
    public async Task<IDiscordGuildSticker> GetGuildStickerAsync(ulong guildId, ulong stickerId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildSticker(await _original.GetGuildStickerAsync(guildId, stickerId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildSticker> CreateGuildStickerAsync(ulong guildId, IDiscordGuildStickerProperties sticker, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildSticker(await _original.CreateGuildStickerAsync(guildId, sticker.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildSticker> ModifyGuildStickerAsync(ulong guildId, ulong stickerId, Action<IDiscordGuildStickerOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildSticker(await _original.ModifyGuildStickerAsync(guildId, stickerId, x => action(new DiscordGuildStickerOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteGuildStickerAsync(ulong guildId, ulong stickerId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteGuildStickerAsync(guildId, stickerId, properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordSubscription> GetSkuSubscriptionsAsync(ulong skuId, IDiscordSubscriptionPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetSkuSubscriptionsAsync(skuId, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordSubscription(original);
        }
    }
    public async Task<IDiscordSubscription> GetSkuSubscriptionAsync(ulong skuId, ulong subscriptionId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordSubscription(await _original.GetSkuSubscriptionAsync(skuId, subscriptionId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordApplication> GetApplicationAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplication(await _original.GetApplicationAsync(applicationId, properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(ulong channelId, IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.CreateGoogleCloudPlatformStorageBucketsAsync(channelId, buckets.Select(x => x.Original), properties?.Original, cancellationToken)).Select(x => new DiscordGoogleCloudPlatformStorageBucket(x)).ToList();
    }
    public async IAsyncEnumerable<IDiscordGuildUserInfo> SearchGuildUsersAsync(ulong guildId, IDiscordGuildUsersSearchPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.SearchGuildUsersAsync(guildId, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordGuildUserInfo(original);
        }
    }
    public async Task<IDiscordCurrentUser> GetCurrentUserAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordCurrentUser(await _original.GetCurrentUserAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordUser> GetUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordUser(await _original.GetUserAsync(userId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordCurrentUser> ModifyCurrentUserAsync(Action<IDiscordCurrentUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordCurrentUser(await _original.ModifyCurrentUserAsync(x => action(new DiscordCurrentUserOptions(x)), properties?.Original, cancellationToken));
    }
    public async IAsyncEnumerable<IDiscordRestGuild> GetCurrentUserGuildsAsync(IDiscordGuildsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetCurrentUserGuildsAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordRestGuild(original);
        }
    }
    public async Task<IDiscordGuildUser> GetCurrentUserGuildUserAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildUser(await _original.GetCurrentUserGuildUserAsync(guildId, properties?.Original, cancellationToken));
    }
    public Task LeaveGuildAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.LeaveGuildAsync(guildId, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordDMChannel> GetDMChannelAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordDMChannel(await _original.GetDMChannelAsync(userId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGroupDMChannel> CreateGroupDMChannelAsync(IDiscordGroupDMChannelProperties groupDMChannelProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGroupDMChannel(await _original.CreateGroupDMChannelAsync(groupDMChannelProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordConnection>> GetCurrentUserConnectionsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetCurrentUserConnectionsAsync(properties?.Original, cancellationToken)).Select(x => new DiscordConnection(x)).ToList();
    }
    public async Task<IDiscordApplicationRoleConnection> GetCurrentUserApplicationRoleConnectionAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationRoleConnection(await _original.GetCurrentUserApplicationRoleConnectionAsync(applicationId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordApplicationRoleConnection> UpdateCurrentUserApplicationRoleConnectionAsync(ulong applicationId, IDiscordApplicationRoleConnectionProperties applicationRoleConnectionProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationRoleConnection(await _original.UpdateCurrentUserApplicationRoleConnectionAsync(applicationId, applicationRoleConnectionProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IEnumerable<IDiscordVoiceRegion>> GetVoiceRegionsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetVoiceRegionsAsync(properties?.Original, cancellationToken)).Select(x => new DiscordVoiceRegion(x));
    }
    public async Task<IDiscordVoiceState> GetCurrentGuildUserVoiceStateAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordVoiceState(await _original.GetCurrentGuildUserVoiceStateAsync(guildId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordVoiceState> GetGuildUserVoiceStateAsync(ulong guildId, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordVoiceState(await _original.GetGuildUserVoiceStateAsync(guildId, userId, properties?.Original, cancellationToken));
    }
    public Task ModifyCurrentGuildUserVoiceStateAsync(ulong guildId, Action<IDiscordCurrentUserVoiceStateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.ModifyCurrentGuildUserVoiceStateAsync(guildId, x => action(new DiscordCurrentUserVoiceStateOptions(x)), properties?.Original, cancellationToken);
    }
    public Task ModifyGuildUserVoiceStateAsync(ulong guildId, ulong channelId, ulong userId, Action<IDiscordVoiceStateOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.ModifyGuildUserVoiceStateAsync(guildId, channelId, userId, x => action(new DiscordVoiceStateOptions(x)), properties?.Original, cancellationToken);
    }
    public async Task<IDiscordIncomingWebhook> CreateWebhookAsync(ulong channelId, IDiscordWebhookProperties webhookProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordIncomingWebhook(await _original.CreateWebhookAsync(channelId, webhookProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordWebhook>> GetChannelWebhooksAsync(ulong channelId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetChannelWebhooksAsync(channelId, properties?.Original, cancellationToken)).Select(x => new DiscordWebhook(x)).ToList();
    }
    public async Task<IReadOnlyList<IDiscordWebhook>> GetGuildWebhooksAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetGuildWebhooksAsync(guildId, properties?.Original, cancellationToken)).Select(x => new DiscordWebhook(x)).ToList();
    }
    public async Task<IDiscordWebhook> GetWebhookAsync(ulong webhookId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordWebhook(await _original.GetWebhookAsync(webhookId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordWebhook> GetWebhookWithTokenAsync(ulong webhookId, string webhookToken, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordWebhook(await _original.GetWebhookWithTokenAsync(webhookId, webhookToken, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordWebhook> ModifyWebhookAsync(ulong webhookId, Action<IDiscordWebhookOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordWebhook(await _original.ModifyWebhookAsync(webhookId, x => action(new DiscordWebhookOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IDiscordWebhook> ModifyWebhookWithTokenAsync(ulong webhookId, string webhookToken, Action<IDiscordWebhookOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordWebhook(await _original.ModifyWebhookWithTokenAsync(webhookId, webhookToken, x => action(new DiscordWebhookOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteWebhookAsync(ulong webhookId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteWebhookAsync(webhookId, properties?.Original, cancellationToken);
    }
    public Task DeleteWebhookWithTokenAsync(ulong webhookId, string webhookToken, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteWebhookWithTokenAsync(webhookId, webhookToken, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordRestMessage?> ExecuteWebhookAsync(ulong webhookId, string webhookToken, IDiscordWebhookMessageProperties message, bool wait = false, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        var temp = await _original.ExecuteWebhookAsync(webhookId, webhookToken, message.Original, wait, threadId, withComponents, properties?.Original, cancellationToken);
        return temp is null ? null : new DiscordRestMessage(temp);
    }
    public async Task<IDiscordRestMessage> GetWebhookMessageAsync(ulong webhookId, string webhookToken, ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.GetWebhookMessageAsync(webhookId, webhookToken, messageId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestMessage> ModifyWebhookMessageAsync(ulong webhookId, string webhookToken, ulong messageId, Action<IDiscordMessageOptions> action, ulong? threadId = default, bool withComponents = true, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.ModifyWebhookMessageAsync(webhookId, webhookToken, messageId, x => action(new DiscordMessageOptions(x)), threadId, withComponents, properties?.Original, cancellationToken));
    }
    public Task DeleteWebhookMessageAsync(ulong webhookId, string webhookToken, ulong messageId, ulong? threadId = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteWebhookMessageAsync(webhookId, webhookToken, messageId, threadId, properties?.Original, cancellationToken);
    }
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
    public string? IconHash => _original.IconHash;
    public string Description => _original.Description;
    public IReadOnlyList<string> RpcOrigins => _original.RpcOrigins;
    public bool? BotPublic => _original.BotPublic;
    public bool? BotRequireCodeGrant => _original.BotRequireCodeGrant;
    public IDiscordUser? Bot => _original.Bot is null ? null : new DiscordUser(_original.Bot);
    public string? TermsOfServiceUrl => _original.TermsOfServiceUrl;
    public string? PrivacyPolicyUrl => _original.PrivacyPolicyUrl;
    public IDiscordUser? Owner => _original.Owner is null ? null : new DiscordUser(_original.Owner);
    public string VerifyKey => _original.VerifyKey;
    public IDiscordTeam? Team => _original.Team is null ? null : new DiscordTeam(_original.Team);
    public ulong? GuildId => _original.GuildId;
    public IDiscordRestGuild? Guild => _original.Guild is null ? null : new DiscordRestGuild(_original.Guild);
    public ulong? PrimarySkuId => _original.PrimarySkuId;
    public string? Slug => _original.Slug;
    public string? CoverImageHash => _original.CoverImageHash;
    public NetCord.ApplicationFlags? Flags => _original.Flags;
    public int? ApproximateGuildCount => _original.ApproximateGuildCount;
    public int? ApproximateUserInstallCount => _original.ApproximateUserInstallCount;
    public IReadOnlyList<string>? RedirectUris => _original.RedirectUris;
    public string? InteractionsEndpointUrl => _original.InteractionsEndpointUrl;
    public string? RoleConnectionsVerificationUrl => _original.RoleConnectionsVerificationUrl;
    public IReadOnlyList<string>? Tags => _original.Tags;
    public IDiscordApplicationInstallParams? InstallParams => _original.InstallParams is null ? null : new DiscordApplicationInstallParams(_original.InstallParams);
    public IReadOnlyDictionary<NetCord.ApplicationIntegrationType, IDiscordApplicationIntegrationTypeConfiguration>? IntegrationTypesConfiguration => _original.IntegrationTypesConfiguration is null ? null : _original.IntegrationTypesConfiguration.ToDictionary(kv => kv.Key, kv => (IDiscordApplicationIntegrationTypeConfiguration)new DiscordApplicationIntegrationTypeConfiguration(kv.Value));
    public string? CustomInstallUrl => _original.CustomInstallUrl;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordApplication> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplication(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordCurrentApplication> ModifyAsync(Action<IDiscordCurrentApplicationOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordCurrentApplication(await _original.ModifyAsync(x => action(new DiscordCurrentApplicationOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordApplicationRoleConnectionMetadata>> GetRoleConnectionMetadataRecordsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetRoleConnectionMetadataRecordsAsync(properties?.Original, cancellationToken)).Select(x => new DiscordApplicationRoleConnectionMetadata(x)).ToList();
    }
    public async Task<IReadOnlyList<IDiscordApplicationRoleConnectionMetadata>> UpdateRoleConnectionMetadataRecordsAsync(IEnumerable<IDiscordApplicationRoleConnectionMetadataProperties> applicationRoleConnectionMetadataProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.UpdateRoleConnectionMetadataRecordsAsync(applicationRoleConnectionMetadataProperties.Select(x => x.Original), properties?.Original, cancellationToken)).Select(x => new DiscordApplicationRoleConnectionMetadata(x)).ToList();
    }
    public async IAsyncEnumerable<IDiscordEntitlement> GetEntitlementsAsync(IDiscordEntitlementsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetEntitlementsAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordEntitlement(original);
        }
    }
    public async Task<IDiscordEntitlement> GetEntitlementAsync(ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordEntitlement(await _original.GetEntitlementAsync(entitlementId, properties?.Original, cancellationToken));
    }
    public Task ConsumeEntitlementAsync(ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.ConsumeEntitlementAsync(entitlementId, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordEntitlement> CreateTestEntitlementAsync(IDiscordTestEntitlementProperties testEntitlementProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordEntitlement(await _original.CreateTestEntitlementAsync(testEntitlementProperties.Original, properties?.Original, cancellationToken));
    }
    public Task DeleteTestEntitlementAsync(ulong entitlementId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteTestEntitlementAsync(entitlementId, properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordSku>> GetSkusAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetSkusAsync(properties?.Original, cancellationToken)).Select(x => new DiscordSku(x)).ToList();
    }
    public IDiscordImageUrl? GetIconUrl(NetCord.ImageFormat format) 
    {
        var temp = _original.GetIconUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetCoverUrl(NetCord.ImageFormat format) 
    {
        var temp = _original.GetCoverUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetAssetUrl(ulong assetId, NetCord.ImageFormat format) 
    {
        var temp = _original.GetAssetUrl(assetId, format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetAchievementIconUrl(ulong achievementId, string iconHash, NetCord.ImageFormat format) 
    {
        var temp = _original.GetAchievementIconUrl(achievementId, iconHash, format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetStorePageAssetUrl(ulong assetId, NetCord.ImageFormat format) 
    {
        var temp = _original.GetStorePageAssetUrl(assetId, format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public async Task<IReadOnlyList<IDiscordApplicationEmoji>> GetEmojisAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetEmojisAsync(properties?.Original, cancellationToken)).Select(x => new DiscordApplicationEmoji(x)).ToList();
    }
    public async Task<IDiscordApplicationEmoji> GetEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationEmoji(await _original.GetEmojiAsync(emojiId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordApplicationEmoji> CreateEmojiAsync(IDiscordApplicationEmojiProperties applicationEmojiProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationEmoji(await _original.CreateEmojiAsync(applicationEmojiProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordApplicationEmoji> ModifyEmojiAsync(ulong emojiId, Action<IDiscordApplicationEmojiOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationEmoji(await _original.ModifyEmojiAsync(emojiId, x => action(new DiscordApplicationEmojiOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteEmojiAsync(ulong emojiId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteEmojiAsync(emojiId, properties?.Original, cancellationToken);
    }
}


public class DiscordCurrentApplicationOptions : IDiscordCurrentApplicationOptions 
{
    private readonly NetCord.Rest.CurrentApplicationOptions _original;
    public DiscordCurrentApplicationOptions(NetCord.Rest.CurrentApplicationOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.CurrentApplicationOptions Original => _original;
    public string? CustomInstallUrl { get { return _original.CustomInstallUrl; } set { _original.CustomInstallUrl = value; } }
    public string? Description { get { return _original.Description; } set { _original.Description = value; } }
    public string? RoleConnectionsVerificationUrl { get { return _original.RoleConnectionsVerificationUrl; } set { _original.RoleConnectionsVerificationUrl = value; } }
    public IDiscordApplicationInstallParamsProperties? InstallParams { get { return _original.InstallParams is null ? null : new DiscordApplicationInstallParamsProperties(_original.InstallParams); } set { _original.InstallParams = value?.Original; } }
    public IReadOnlyDictionary<NetCord.ApplicationIntegrationType, IDiscordApplicationIntegrationTypeConfigurationProperties>? IntegrationTypesConfiguration { get { return _original.IntegrationTypesConfiguration is null ? null : _original.IntegrationTypesConfiguration.ToDictionary(kv => kv.Key, kv => (IDiscordApplicationIntegrationTypeConfigurationProperties)new DiscordApplicationIntegrationTypeConfigurationProperties(kv.Value)); } set { _original.IntegrationTypesConfiguration = value?.ToDictionary(kv => kv.Key, kv => kv.Value.Original); } }
    public NetCord.ApplicationFlags? Flags { get { return _original.Flags; } set { _original.Flags = value; } }
    public NetCord.Rest.ImageProperties? Icon { get { return _original.Icon; } set { _original.Icon = value; } }
    public NetCord.Rest.ImageProperties? CoverImage { get { return _original.CoverImage; } set { _original.CoverImage = value; } }
    public string? InteractionsEndpointUrl { get { return _original.InteractionsEndpointUrl; } set { _original.InteractionsEndpointUrl = value; } }
    public IEnumerable<string>? Tags { get { return _original.Tags; } set { _original.Tags = value; } }
    public IDiscordCurrentApplicationOptions WithCustomInstallUrl(string customInstallUrl) 
    {
        return new DiscordCurrentApplicationOptions(_original.WithCustomInstallUrl(customInstallUrl));
    }
    public IDiscordCurrentApplicationOptions WithDescription(string description) 
    {
        return new DiscordCurrentApplicationOptions(_original.WithDescription(description));
    }
    public IDiscordCurrentApplicationOptions WithRoleConnectionsVerificationUrl(string roleConnectionsVerificationUrl) 
    {
        return new DiscordCurrentApplicationOptions(_original.WithRoleConnectionsVerificationUrl(roleConnectionsVerificationUrl));
    }
    public IDiscordCurrentApplicationOptions WithInstallParams(IDiscordApplicationInstallParamsProperties? installParams) 
    {
        return new DiscordCurrentApplicationOptions(_original.WithInstallParams(installParams?.Original));
    }
    public IDiscordCurrentApplicationOptions WithIntegrationTypesConfiguration(IReadOnlyDictionary<NetCord.ApplicationIntegrationType, IDiscordApplicationIntegrationTypeConfigurationProperties>? integrationTypesConfiguration) 
    {
        return new DiscordCurrentApplicationOptions(_original.WithIntegrationTypesConfiguration(integrationTypesConfiguration?.ToDictionary(kv => kv.Key, kv => kv.Value.Original)));
    }
    public IDiscordCurrentApplicationOptions WithFlags(NetCord.ApplicationFlags? flags) 
    {
        return new DiscordCurrentApplicationOptions(_original.WithFlags(flags));
    }
    public IDiscordCurrentApplicationOptions WithIcon(NetCord.Rest.ImageProperties? icon) 
    {
        return new DiscordCurrentApplicationOptions(_original.WithIcon(icon));
    }
    public IDiscordCurrentApplicationOptions WithCoverImage(NetCord.Rest.ImageProperties? coverImage) 
    {
        return new DiscordCurrentApplicationOptions(_original.WithCoverImage(coverImage));
    }
    public IDiscordCurrentApplicationOptions WithInteractionsEndpointUrl(string interactionsEndpointUrl) 
    {
        return new DiscordCurrentApplicationOptions(_original.WithInteractionsEndpointUrl(interactionsEndpointUrl));
    }
    public IDiscordCurrentApplicationOptions WithTags(IEnumerable<string>? tags) 
    {
        return new DiscordCurrentApplicationOptions(_original.WithTags(tags));
    }
    public IDiscordCurrentApplicationOptions AddTags(IEnumerable<string> tags) 
    {
        return new DiscordCurrentApplicationOptions(_original.AddTags(tags));
    }
    public IDiscordCurrentApplicationOptions AddTags(string[] tags) 
    {
        return new DiscordCurrentApplicationOptions(_original.AddTags(tags));
    }
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
    public IReadOnlyDictionary<string, string>? NameLocalizations => _original.NameLocalizations;
    public string Description => _original.Description;
    public IReadOnlyDictionary<string, string>? DescriptionLocalizations => _original.DescriptionLocalizations;
}


public class DiscordApplicationRoleConnectionMetadataProperties : IDiscordApplicationRoleConnectionMetadataProperties 
{
    private readonly NetCord.Rest.ApplicationRoleConnectionMetadataProperties _original;
    public DiscordApplicationRoleConnectionMetadataProperties(NetCord.Rest.ApplicationRoleConnectionMetadataProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationRoleConnectionMetadataProperties Original => _original;
    public NetCord.Rest.ApplicationRoleConnectionMetadataType Type { get { return _original.Type; } set { _original.Type = value; } }
    public string Key { get { return _original.Key; } set { _original.Key = value; } }
    public string Name { get { return _original.Name; } set { _original.Name = value; } }
    public IReadOnlyDictionary<string, string>? NameLocalizations { get { return _original.NameLocalizations; } set { _original.NameLocalizations = value; } }
    public string Description { get { return _original.Description; } set { _original.Description = value; } }
    public IReadOnlyDictionary<string, string>? DescriptionLocalizations { get { return _original.DescriptionLocalizations; } set { _original.DescriptionLocalizations = value; } }
    public IDiscordApplicationRoleConnectionMetadataProperties WithType(NetCord.Rest.ApplicationRoleConnectionMetadataType type) 
    {
        return new DiscordApplicationRoleConnectionMetadataProperties(_original.WithType(type));
    }
    public IDiscordApplicationRoleConnectionMetadataProperties WithKey(string key) 
    {
        return new DiscordApplicationRoleConnectionMetadataProperties(_original.WithKey(key));
    }
    public IDiscordApplicationRoleConnectionMetadataProperties WithName(string name) 
    {
        return new DiscordApplicationRoleConnectionMetadataProperties(_original.WithName(name));
    }
    public IDiscordApplicationRoleConnectionMetadataProperties WithNameLocalizations(IReadOnlyDictionary<string, string>? nameLocalizations) 
    {
        return new DiscordApplicationRoleConnectionMetadataProperties(_original.WithNameLocalizations(nameLocalizations));
    }
    public IDiscordApplicationRoleConnectionMetadataProperties WithDescription(string description) 
    {
        return new DiscordApplicationRoleConnectionMetadataProperties(_original.WithDescription(description));
    }
    public IDiscordApplicationRoleConnectionMetadataProperties WithDescriptionLocalizations(IReadOnlyDictionary<string, string>? descriptionLocalizations) 
    {
        return new DiscordApplicationRoleConnectionMetadataProperties(_original.WithDescriptionLocalizations(descriptionLocalizations));
    }
}


public class DiscordGroupDMChannelOptions : IDiscordGroupDMChannelOptions 
{
    private readonly NetCord.Rest.GroupDMChannelOptions _original;
    public DiscordGroupDMChannelOptions(NetCord.Rest.GroupDMChannelOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.GroupDMChannelOptions Original => _original;
    public string? Name { get { return _original.Name; } set { _original.Name = value; } }
    public NetCord.Rest.ImageProperties? Icon { get { return _original.Icon; } set { _original.Icon = value; } }
    public IDiscordGroupDMChannelOptions WithName(string name) 
    {
        return new DiscordGroupDMChannelOptions(_original.WithName(name));
    }
    public IDiscordGroupDMChannelOptions WithIcon(NetCord.Rest.ImageProperties? icon) 
    {
        return new DiscordGroupDMChannelOptions(_original.WithIcon(icon));
    }
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
    public string AccessToken { get { return _original.AccessToken; } set { _original.AccessToken = value; } }
    public string? Nickname { get { return _original.Nickname; } set { _original.Nickname = value; } }
    public IDiscordGroupDMChannelUserAddProperties WithAccessToken(string accessToken) 
    {
        return new DiscordGroupDMChannelUserAddProperties(_original.WithAccessToken(accessToken));
    }
    public IDiscordGroupDMChannelUserAddProperties WithNickname(string nickname) 
    {
        return new DiscordGroupDMChannelUserAddProperties(_original.WithNickname(nickname));
    }
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
    public IReadOnlyList<ulong>? AppliedTags => _original.AppliedTags;
    public ulong OwnerId => _original.OwnerId;
    public int MessageCount => _original.MessageCount;
    public int UserCount => _original.UserCount;
    public IDiscordGuildThreadMetadata Metadata => new DiscordGuildThreadMetadata(_original.Metadata);
    public IDiscordThreadCurrentUser? CurrentUser => _original.CurrentUser is null ? null : new DiscordThreadCurrentUser(_original.CurrentUser);
    public int TotalMessageSent => _original.TotalMessageSent;
    public ulong GuildId => _original.GuildId;
    public int? Position => _original.Position;
    public IReadOnlyDictionary<ulong, IDiscordPermissionOverwrite> PermissionOverwrites => _original.PermissionOverwrites.ToDictionary(kv => kv.Key, kv => (IDiscordPermissionOverwrite)new DiscordPermissionOverwrite(kv.Value));
    public string Name => _original.Name;
    public string? Topic => _original.Topic;
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
    public async Task<IDiscordForumGuildThread> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordForumGuildThread(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordForumGuildThread> ModifyAsync(Action<IDiscordGuildChannelOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordForumGuildThread(await _original.ModifyAsync(x => action(new DiscordGuildChannelOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IDiscordForumGuildThread> DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordForumGuildThread(await _original.DeleteAsync(properties?.Original, cancellationToken));
    }
    public Task JoinAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.JoinAsync(properties?.Original, cancellationToken);
    }
    public Task AddUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.AddUserAsync(userId, properties?.Original, cancellationToken);
    }
    public Task LeaveAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.LeaveAsync(properties?.Original, cancellationToken);
    }
    public Task DeleteUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteUserAsync(userId, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordThreadUser> GetUserAsync(ulong userId, bool withGuildUser = false, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordThreadUser(await _original.GetUserAsync(userId, withGuildUser, properties?.Original, cancellationToken));
    }
    public async IAsyncEnumerable<IDiscordThreadUser> GetUsersAsync(IDiscordOptionalGuildUsersPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetUsersAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordThreadUser(original);
        }
    }
    public Task ModifyPermissionsAsync(IDiscordPermissionOverwriteProperties permissionOverwrite, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.ModifyPermissionsAsync(permissionOverwrite.Original, properties?.Original, cancellationToken);
    }
    public async Task<IEnumerable<IDiscordRestInvite>> GetInvitesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetInvitesAsync(properties?.Original, cancellationToken)).Select(x => new DiscordRestInvite(x));
    }
    public async Task<IDiscordRestInvite> CreateInviteAsync(IDiscordInviteProperties? inviteProperties = null, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestInvite(await _original.CreateInviteAsync(inviteProperties?.Original, properties?.Original, cancellationToken));
    }
    public Task DeletePermissionAsync(ulong overwriteId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeletePermissionAsync(overwriteId, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordGuildThread> CreateGuildThreadAsync(ulong messageId, IDiscordGuildThreadFromMessageProperties threadFromMessageProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildThread(await _original.CreateGuildThreadAsync(messageId, threadFromMessageProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGuildThread> CreateGuildThreadAsync(IDiscordGuildThreadProperties threadProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildThread(await _original.CreateGuildThreadAsync(threadProperties.Original, properties?.Original, cancellationToken));
    }
    public async IAsyncEnumerable<IDiscordGuildThread> GetPublicArchivedGuildThreadsAsync(IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetPublicArchivedGuildThreadsAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordGuildThread(original);
        }
    }
    public async IAsyncEnumerable<IDiscordGuildThread> GetPrivateArchivedGuildThreadsAsync(IDiscordPaginationProperties<System.DateTimeOffset>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetPrivateArchivedGuildThreadsAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordGuildThread(original);
        }
    }
    public async IAsyncEnumerable<IDiscordGuildThread> GetJoinedPrivateArchivedGuildThreadsAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetJoinedPrivateArchivedGuildThreadsAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordGuildThread(original);
        }
    }
    public async Task<IDiscordIncomingWebhook> CreateWebhookAsync(IDiscordWebhookProperties webhookProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordIncomingWebhook(await _original.CreateWebhookAsync(webhookProperties.Original, properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordWebhook>> GetChannelWebhooksAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetChannelWebhooksAsync(properties?.Original, cancellationToken)).Select(x => new DiscordWebhook(x)).ToList();
    }
    public async IAsyncEnumerable<IDiscordRestMessage> GetMessagesAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetMessagesAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordRestMessage(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetMessagesAroundAsync(ulong messageId, int? limit = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetMessagesAroundAsync(messageId, limit, properties?.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    }
    public async Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.GetMessageAsync(messageId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestMessage> SendMessageAsync(IDiscordMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.SendMessageAsync(message.Original, properties?.Original, cancellationToken));
    }
    public Task AddMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.AddMessageReactionAsync(messageId, emoji.Original, properties?.Original, cancellationToken);
    }
    public Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessageReactionAsync(messageId, emoji.Original, properties?.Original, cancellationToken);
    }
    public Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessageReactionAsync(messageId, emoji.Original, userId, properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordUser> GetMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordMessageReactionsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetMessageReactionsAsync(messageId, emoji.Original, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAllMessageReactionsAsync(messageId, properties?.Original, cancellationToken);
    }
    public Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAllMessageReactionsAsync(messageId, emoji.Original, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.ModifyMessageAsync(messageId, x => action(new DiscordMessageOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessageAsync(messageId, properties?.Original, cancellationToken);
    }
    public Task DeleteMessagesAsync(IEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessagesAsync(messageIds, properties?.Original, cancellationToken);
    }
    public Task DeleteMessagesAsync(IAsyncEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessagesAsync(messageIds, properties?.Original, cancellationToken);
    }
    public Task TriggerTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.TriggerTypingStateAsync(properties?.Original, cancellationToken);
    }
    public Task<IDisposable> EnterTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.EnterTypingStateAsync(properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetPinnedMessagesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetPinnedMessagesAsync(properties?.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    }
    public Task PinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.PinMessageAsync(messageId, properties?.Original, cancellationToken);
    }
    public Task UnpinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.UnpinMessageAsync(messageId, properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordUser> GetMessagePollAnswerVotersAsync(ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetMessagePollAnswerVotersAsync(messageId, answerId, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public async Task<IDiscordRestMessage> EndMessagePollAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.EndMessagePollAsync(messageId, properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.CreateGoogleCloudPlatformStorageBucketsAsync(buckets.Select(x => x.Original), properties?.Original, cancellationToken)).Select(x => new DiscordGoogleCloudPlatformStorageBucket(x)).ToList();
    }
}


public class DiscordForumGuildThreadProperties : IDiscordForumGuildThreadProperties 
{
    private readonly NetCord.Rest.ForumGuildThreadProperties _original;
    public DiscordForumGuildThreadProperties(NetCord.Rest.ForumGuildThreadProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ForumGuildThreadProperties Original => _original;
    public IDiscordForumGuildThreadMessageProperties Message { get { return new DiscordForumGuildThreadMessageProperties(_original.Message); } set { _original.Message = value.Original; } }
    public IEnumerable<ulong>? AppliedTags { get { return _original.AppliedTags; } set { _original.AppliedTags = value; } }
    public string Name { get { return _original.Name; } set { _original.Name = value; } }
    public NetCord.ThreadArchiveDuration? AutoArchiveDuration { get { return _original.AutoArchiveDuration; } set { _original.AutoArchiveDuration = value; } }
    public int? Slowmode { get { return _original.Slowmode; } set { _original.Slowmode = value; } }
    public HttpContent Serialize() 
    {
        return _original.Serialize();
    }
    public IDiscordForumGuildThreadProperties WithMessage(IDiscordForumGuildThreadMessageProperties message) 
    {
        return new DiscordForumGuildThreadProperties(_original.WithMessage(message.Original));
    }
    public IDiscordForumGuildThreadProperties WithAppliedTags(IEnumerable<ulong>? appliedTags) 
    {
        return new DiscordForumGuildThreadProperties(_original.WithAppliedTags(appliedTags));
    }
    public IDiscordForumGuildThreadProperties AddAppliedTags(IEnumerable<ulong> appliedTags) 
    {
        return new DiscordForumGuildThreadProperties(_original.AddAppliedTags(appliedTags));
    }
    public IDiscordForumGuildThreadProperties AddAppliedTags(ulong[] appliedTags) 
    {
        return new DiscordForumGuildThreadProperties(_original.AddAppliedTags(appliedTags));
    }
    public IDiscordForumGuildThreadProperties WithName(string name) 
    {
        return new DiscordForumGuildThreadProperties(_original.WithName(name));
    }
    public IDiscordForumGuildThreadProperties WithAutoArchiveDuration(NetCord.ThreadArchiveDuration? autoArchiveDuration) 
    {
        return new DiscordForumGuildThreadProperties(_original.WithAutoArchiveDuration(autoArchiveDuration));
    }
    public IDiscordForumGuildThreadProperties WithSlowmode(int? slowmode) 
    {
        return new DiscordForumGuildThreadProperties(_original.WithSlowmode(slowmode));
    }
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
    public string Name { get { return _original.Name; } set { _original.Name = value; } }
    public NetCord.Rest.ImageProperties? Icon { get { return _original.Icon; } set { _original.Icon = value; } }
    public NetCord.VerificationLevel? VerificationLevel { get { return _original.VerificationLevel; } set { _original.VerificationLevel = value; } }
    public NetCord.DefaultMessageNotificationLevel? DefaultMessageNotificationLevel { get { return _original.DefaultMessageNotificationLevel; } set { _original.DefaultMessageNotificationLevel = value; } }
    public NetCord.ContentFilter? ContentFilter { get { return _original.ContentFilter; } set { _original.ContentFilter = value; } }
    public IEnumerable<IDiscordRoleProperties>? Roles { get { return _original.Roles is null ? null : _original.Roles.Select(x => new DiscordRoleProperties(x)); } set { _original.Roles = value?.Select(x => x.Original); } }
    public IEnumerable<IDiscordGuildChannelProperties>? Channels { get { return _original.Channels is null ? null : _original.Channels.Select(x => new DiscordGuildChannelProperties(x)); } set { _original.Channels = value?.Select(x => x.Original); } }
    public ulong? AfkChannelId { get { return _original.AfkChannelId; } set { _original.AfkChannelId = value; } }
    public int? AfkTimeout { get { return _original.AfkTimeout; } set { _original.AfkTimeout = value; } }
    public ulong? SystemChannelId { get { return _original.SystemChannelId; } set { _original.SystemChannelId = value; } }
    public NetCord.Rest.SystemChannelFlags? SystemChannelFlags { get { return _original.SystemChannelFlags; } set { _original.SystemChannelFlags = value; } }
    public IDiscordGuildProperties WithName(string name) 
    {
        return new DiscordGuildProperties(_original.WithName(name));
    }
    public IDiscordGuildProperties WithIcon(NetCord.Rest.ImageProperties? icon) 
    {
        return new DiscordGuildProperties(_original.WithIcon(icon));
    }
    public IDiscordGuildProperties WithVerificationLevel(NetCord.VerificationLevel? verificationLevel) 
    {
        return new DiscordGuildProperties(_original.WithVerificationLevel(verificationLevel));
    }
    public IDiscordGuildProperties WithDefaultMessageNotificationLevel(NetCord.DefaultMessageNotificationLevel? defaultMessageNotificationLevel) 
    {
        return new DiscordGuildProperties(_original.WithDefaultMessageNotificationLevel(defaultMessageNotificationLevel));
    }
    public IDiscordGuildProperties WithContentFilter(NetCord.ContentFilter? contentFilter) 
    {
        return new DiscordGuildProperties(_original.WithContentFilter(contentFilter));
    }
    public IDiscordGuildProperties WithRoles(IEnumerable<IDiscordRoleProperties>? roles) 
    {
        return new DiscordGuildProperties(_original.WithRoles(roles?.Select(x => x.Original)));
    }
    public IDiscordGuildProperties AddRoles(IEnumerable<IDiscordRoleProperties> roles) 
    {
        return new DiscordGuildProperties(_original.AddRoles(roles.Select(x => x.Original)));
    }
    public IDiscordGuildProperties AddRoles(IDiscordRoleProperties[] roles) 
    {
        return new DiscordGuildProperties(_original.AddRoles(roles.Select(x => x.Original).ToArray()));
    }
    public IDiscordGuildProperties WithChannels(IEnumerable<IDiscordGuildChannelProperties>? channels) 
    {
        return new DiscordGuildProperties(_original.WithChannels(channels?.Select(x => x.Original)));
    }
    public IDiscordGuildProperties AddChannels(IEnumerable<IDiscordGuildChannelProperties> channels) 
    {
        return new DiscordGuildProperties(_original.AddChannels(channels.Select(x => x.Original)));
    }
    public IDiscordGuildProperties AddChannels(IDiscordGuildChannelProperties[] channels) 
    {
        return new DiscordGuildProperties(_original.AddChannels(channels.Select(x => x.Original).ToArray()));
    }
    public IDiscordGuildProperties WithAfkChannelId(ulong? afkChannelId) 
    {
        return new DiscordGuildProperties(_original.WithAfkChannelId(afkChannelId));
    }
    public IDiscordGuildProperties WithAfkTimeout(int? afkTimeout) 
    {
        return new DiscordGuildProperties(_original.WithAfkTimeout(afkTimeout));
    }
    public IDiscordGuildProperties WithSystemChannelId(ulong? systemChannelId) 
    {
        return new DiscordGuildProperties(_original.WithSystemChannelId(systemChannelId));
    }
    public IDiscordGuildProperties WithSystemChannelFlags(NetCord.Rest.SystemChannelFlags? systemChannelFlags) 
    {
        return new DiscordGuildProperties(_original.WithSystemChannelFlags(systemChannelFlags));
    }
}


public class DiscordEntitlementsPaginationProperties : IDiscordEntitlementsPaginationProperties 
{
    private readonly NetCord.Rest.EntitlementsPaginationProperties _original;
    public DiscordEntitlementsPaginationProperties(NetCord.Rest.EntitlementsPaginationProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.EntitlementsPaginationProperties Original => _original;
    public ulong? UserId { get { return _original.UserId; } set { _original.UserId = value; } }
    public IEnumerable<ulong>? SkuIds { get { return _original.SkuIds; } set { _original.SkuIds = value; } }
    public ulong? GuildId { get { return _original.GuildId; } set { _original.GuildId = value; } }
    public bool? ExcludeEnded { get { return _original.ExcludeEnded; } set { _original.ExcludeEnded = value; } }
    public ulong? From { get { return _original.From; } set { _original.From = value; } }
    public NetCord.Rest.PaginationDirection? Direction { get { return _original.Direction; } set { _original.Direction = value; } }
    public int? BatchSize { get { return _original.BatchSize; } set { _original.BatchSize = value; } }
    public IDiscordEntitlementsPaginationProperties WithUserId(ulong? userId) 
    {
        return new DiscordEntitlementsPaginationProperties(_original.WithUserId(userId));
    }
    public IDiscordEntitlementsPaginationProperties WithSkuIds(IEnumerable<ulong>? skuIds) 
    {
        return new DiscordEntitlementsPaginationProperties(_original.WithSkuIds(skuIds));
    }
    public IDiscordEntitlementsPaginationProperties AddSkuIds(IEnumerable<ulong> skuIds) 
    {
        return new DiscordEntitlementsPaginationProperties(_original.AddSkuIds(skuIds));
    }
    public IDiscordEntitlementsPaginationProperties AddSkuIds(ulong[] skuIds) 
    {
        return new DiscordEntitlementsPaginationProperties(_original.AddSkuIds(skuIds));
    }
    public IDiscordEntitlementsPaginationProperties WithGuildId(ulong? guildId) 
    {
        return new DiscordEntitlementsPaginationProperties(_original.WithGuildId(guildId));
    }
    public IDiscordEntitlementsPaginationProperties WithExcludeEnded(bool? excludeEnded = true) 
    {
        return new DiscordEntitlementsPaginationProperties(_original.WithExcludeEnded(excludeEnded));
    }
    public IDiscordEntitlementsPaginationProperties WithFrom(ulong? from) 
    {
        return new DiscordEntitlementsPaginationProperties(_original.WithFrom(from));
    }
    public IDiscordEntitlementsPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction) 
    {
        return new DiscordEntitlementsPaginationProperties(_original.WithDirection(direction));
    }
    public IDiscordEntitlementsPaginationProperties WithBatchSize(int? batchSize) 
    {
        return new DiscordEntitlementsPaginationProperties(_original.WithBatchSize(batchSize));
    }
}


public class DiscordTestEntitlementProperties : IDiscordTestEntitlementProperties 
{
    private readonly NetCord.Rest.TestEntitlementProperties _original;
    public DiscordTestEntitlementProperties(NetCord.Rest.TestEntitlementProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.TestEntitlementProperties Original => _original;
    public ulong SkuId { get { return _original.SkuId; } set { _original.SkuId = value; } }
    public ulong OwnerId { get { return _original.OwnerId; } set { _original.OwnerId = value; } }
    public NetCord.Rest.TestEntitlementOwnerType OwnerType { get { return _original.OwnerType; } set { _original.OwnerType = value; } }
    public IDiscordTestEntitlementProperties WithSkuId(ulong skuId) 
    {
        return new DiscordTestEntitlementProperties(_original.WithSkuId(skuId));
    }
    public IDiscordTestEntitlementProperties WithOwnerId(ulong ownerId) 
    {
        return new DiscordTestEntitlementProperties(_original.WithOwnerId(ownerId));
    }
    public IDiscordTestEntitlementProperties WithOwnerType(NetCord.Rest.TestEntitlementOwnerType ownerType) 
    {
        return new DiscordTestEntitlementProperties(_original.WithOwnerType(ownerType));
    }
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
        await foreach(var original in _original.GetSubscriptionsAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordSubscription(original);
        }
    }
    public async Task<IDiscordSubscription> GetSubscriptionAsync(ulong subscriptionId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordSubscription(await _original.GetSubscriptionAsync(subscriptionId, properties?.Original, cancellationToken));
    }
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
    public IDiscordUser? User => _original.User is null ? null : new DiscordUser(_original.User);
}


public class DiscordStageInstanceProperties : IDiscordStageInstanceProperties 
{
    private readonly NetCord.Rest.StageInstanceProperties _original;
    public DiscordStageInstanceProperties(NetCord.Rest.StageInstanceProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.StageInstanceProperties Original => _original;
    public ulong ChannelId { get { return _original.ChannelId; } set { _original.ChannelId = value; } }
    public string Topic { get { return _original.Topic; } set { _original.Topic = value; } }
    public NetCord.StageInstancePrivacyLevel? PrivacyLevel { get { return _original.PrivacyLevel; } set { _original.PrivacyLevel = value; } }
    public bool? SendStartNotification { get { return _original.SendStartNotification; } set { _original.SendStartNotification = value; } }
    public ulong? GuildScheduledEventId { get { return _original.GuildScheduledEventId; } set { _original.GuildScheduledEventId = value; } }
    public IDiscordStageInstanceProperties WithChannelId(ulong channelId) 
    {
        return new DiscordStageInstanceProperties(_original.WithChannelId(channelId));
    }
    public IDiscordStageInstanceProperties WithTopic(string topic) 
    {
        return new DiscordStageInstanceProperties(_original.WithTopic(topic));
    }
    public IDiscordStageInstanceProperties WithPrivacyLevel(NetCord.StageInstancePrivacyLevel? privacyLevel) 
    {
        return new DiscordStageInstanceProperties(_original.WithPrivacyLevel(privacyLevel));
    }
    public IDiscordStageInstanceProperties WithSendStartNotification(bool? sendStartNotification = true) 
    {
        return new DiscordStageInstanceProperties(_original.WithSendStartNotification(sendStartNotification));
    }
    public IDiscordStageInstanceProperties WithGuildScheduledEventId(ulong? guildScheduledEventId) 
    {
        return new DiscordStageInstanceProperties(_original.WithGuildScheduledEventId(guildScheduledEventId));
    }
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
    public IDiscordImageUrl GetImageUrl(NetCord.ImageFormat format) 
    {
        return new DiscordImageUrl(_original.GetImageUrl(format));
    }
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
    public IReadOnlyList<ulong>? RenewalSkuIds => _original.RenewalSkuIds;
    public System.DateTimeOffset CurrentPeriodStart => _original.CurrentPeriodStart;
    public System.DateTimeOffset CurrentPeriodEnd => _original.CurrentPeriodEnd;
    public NetCord.SubscriptionStatus Status => _original.Status;
    public System.DateTimeOffset? CanceledAt => _original.CanceledAt;
    public string? Country => _original.Country;
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
    public ulong? UserId { get { return _original.UserId; } set { _original.UserId = value; } }
    public ulong? From { get { return _original.From; } set { _original.From = value; } }
    public NetCord.Rest.PaginationDirection? Direction { get { return _original.Direction; } set { _original.Direction = value; } }
    public int? BatchSize { get { return _original.BatchSize; } set { _original.BatchSize = value; } }
    public IDiscordSubscriptionPaginationProperties WithUserId(ulong? userId) 
    {
        return new DiscordSubscriptionPaginationProperties(_original.WithUserId(userId));
    }
    public IDiscordSubscriptionPaginationProperties WithFrom(ulong? from) 
    {
        return new DiscordSubscriptionPaginationProperties(_original.WithFrom(from));
    }
    public IDiscordSubscriptionPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction) 
    {
        return new DiscordSubscriptionPaginationProperties(_original.WithDirection(direction));
    }
    public IDiscordSubscriptionPaginationProperties WithBatchSize(int? batchSize) 
    {
        return new DiscordSubscriptionPaginationProperties(_original.WithBatchSize(batchSize));
    }
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
    public string? GlobalName => _original.GlobalName;
    public string? AvatarHash => _original.AvatarHash;
    public bool IsBot => _original.IsBot;
    public bool? IsSystemUser => _original.IsSystemUser;
    public bool? MfaEnabled => _original.MfaEnabled;
    public string? BannerHash => _original.BannerHash;
    public NetCord.Color? AccentColor => _original.AccentColor;
    public string? Locale => _original.Locale;
    public bool? Verified => _original.Verified;
    public string? Email => _original.Email;
    public NetCord.UserFlags? Flags => _original.Flags;
    public NetCord.PremiumType? PremiumType => _original.PremiumType;
    public NetCord.UserFlags? PublicFlags => _original.PublicFlags;
    public IDiscordAvatarDecorationData? AvatarDecorationData => _original.AvatarDecorationData is null ? null : new DiscordAvatarDecorationData(_original.AvatarDecorationData);
    public bool HasAvatar => _original.HasAvatar;
    public bool HasBanner => _original.HasBanner;
    public bool HasAvatarDecoration => _original.HasAvatarDecoration;
    public IDiscordImageUrl DefaultAvatarUrl => new DiscordImageUrl(_original.DefaultAvatarUrl);
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordCurrentUser> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordCurrentUser(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordCurrentUser> ModifyAsync(Action<IDiscordCurrentUserOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordCurrentUser(await _original.ModifyAsync(x => action(new DiscordCurrentUserOptions(x)), properties?.Original, cancellationToken));
    }
    public async IAsyncEnumerable<IDiscordRestGuild> GetGuildsAsync(IDiscordGuildsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetGuildsAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordRestGuild(original);
        }
    }
    public async Task<IDiscordGuildUser> GetGuildUserAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGuildUser(await _original.GetGuildUserAsync(guildId, properties?.Original, cancellationToken));
    }
    public Task LeaveGuildAsync(ulong guildId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.LeaveGuildAsync(guildId, properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordConnection>> GetConnectionsAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetConnectionsAsync(properties?.Original, cancellationToken)).Select(x => new DiscordConnection(x)).ToList();
    }
    public async Task<IDiscordApplicationRoleConnection> GetApplicationRoleConnectionAsync(ulong applicationId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationRoleConnection(await _original.GetApplicationRoleConnectionAsync(applicationId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordApplicationRoleConnection> UpdateApplicationRoleConnectionAsync(ulong applicationId, IDiscordApplicationRoleConnectionProperties applicationRoleConnectionProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordApplicationRoleConnection(await _original.UpdateApplicationRoleConnectionAsync(applicationId, applicationRoleConnectionProperties.Original, properties?.Original, cancellationToken));
    }
    public IDiscordImageUrl? GetAvatarUrl(NetCord.ImageFormat? format = default) 
    {
        var temp = _original.GetAvatarUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetBannerUrl(NetCord.ImageFormat? format = default) 
    {
        var temp = _original.GetBannerUrl(format);
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public IDiscordImageUrl? GetAvatarDecorationUrl() 
    {
        var temp = _original.GetAvatarDecorationUrl();
        return temp is null ? null : new DiscordImageUrl(temp);
    }
    public async Task<IDiscordDMChannel> GetDMChannelAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordDMChannel(await _original.GetDMChannelAsync(properties?.Original, cancellationToken));
    }
}


public class DiscordCurrentUserOptions : IDiscordCurrentUserOptions 
{
    private readonly NetCord.Rest.CurrentUserOptions _original;
    public DiscordCurrentUserOptions(NetCord.Rest.CurrentUserOptions original)
    {
        _original = original;
    }
    public NetCord.Rest.CurrentUserOptions Original => _original;
    public string? Username { get { return _original.Username; } set { _original.Username = value; } }
    public NetCord.Rest.ImageProperties? Avatar { get { return _original.Avatar; } set { _original.Avatar = value; } }
    public NetCord.Rest.ImageProperties? Banner { get { return _original.Banner; } set { _original.Banner = value; } }
    public IDiscordCurrentUserOptions WithUsername(string username) 
    {
        return new DiscordCurrentUserOptions(_original.WithUsername(username));
    }
    public IDiscordCurrentUserOptions WithAvatar(NetCord.Rest.ImageProperties? avatar) 
    {
        return new DiscordCurrentUserOptions(_original.WithAvatar(avatar));
    }
    public IDiscordCurrentUserOptions WithBanner(NetCord.Rest.ImageProperties? banner) 
    {
        return new DiscordCurrentUserOptions(_original.WithBanner(banner));
    }
}


public class DiscordGuildsPaginationProperties : IDiscordGuildsPaginationProperties 
{
    private readonly NetCord.Rest.GuildsPaginationProperties _original;
    public DiscordGuildsPaginationProperties(NetCord.Rest.GuildsPaginationProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GuildsPaginationProperties Original => _original;
    public bool WithCounts { get { return _original.WithCounts; } set { _original.WithCounts = value; } }
    public ulong? From { get { return _original.From; } set { _original.From = value; } }
    public NetCord.Rest.PaginationDirection? Direction { get { return _original.Direction; } set { _original.Direction = value; } }
    public int? BatchSize { get { return _original.BatchSize; } set { _original.BatchSize = value; } }
    public IDiscordGuildsPaginationProperties WithWithCounts(bool withCounts = true) 
    {
        return new DiscordGuildsPaginationProperties(_original.WithWithCounts(withCounts));
    }
    public IDiscordGuildsPaginationProperties WithFrom(ulong? from) 
    {
        return new DiscordGuildsPaginationProperties(_original.WithFrom(from));
    }
    public IDiscordGuildsPaginationProperties WithDirection(NetCord.Rest.PaginationDirection? direction) 
    {
        return new DiscordGuildsPaginationProperties(_original.WithDirection(direction));
    }
    public IDiscordGuildsPaginationProperties WithBatchSize(int? batchSize) 
    {
        return new DiscordGuildsPaginationProperties(_original.WithBatchSize(batchSize));
    }
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
    public string? IconHash => _original.IconHash;
    public ulong OwnerId => _original.OwnerId;
    public ulong? ApplicationId => _original.ApplicationId;
    public bool Managed => _original.Managed;
    public IReadOnlyDictionary<ulong, IDiscordUser> Users => _original.Users.ToDictionary(kv => kv.Key, kv => (IDiscordUser)new DiscordUser(kv.Value));
    public ulong? LastMessageId => _original.LastMessageId;
    public System.DateTimeOffset? LastPin => _original.LastPin;
    public ulong Id => _original.Id;
    public NetCord.ChannelFlags Flags => _original.Flags;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
    public async Task<IDiscordGroupDMChannel> GetAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGroupDMChannel(await _original.GetAsync(properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGroupDMChannel> ModifyAsync(Action<IDiscordGroupDMChannelOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGroupDMChannel(await _original.ModifyAsync(x => action(new DiscordGroupDMChannelOptions(x)), properties?.Original, cancellationToken));
    }
    public async Task<IDiscordGroupDMChannel> DeleteAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordGroupDMChannel(await _original.DeleteAsync(properties?.Original, cancellationToken));
    }
    public Task AddUserAsync(ulong userId, IDiscordGroupDMChannelUserAddProperties groupDMChannelUserAddProperties, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.AddUserAsync(userId, groupDMChannelUserAddProperties.Original, properties?.Original, cancellationToken);
    }
    public Task DeleteUserAsync(ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteUserAsync(userId, properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordRestMessage> GetMessagesAsync(IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetMessagesAsync(paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordRestMessage(original);
        }
    }
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetMessagesAroundAsync(ulong messageId, int? limit = default, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetMessagesAroundAsync(messageId, limit, properties?.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    }
    public async Task<IDiscordRestMessage> GetMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.GetMessageAsync(messageId, properties?.Original, cancellationToken));
    }
    public async Task<IDiscordRestMessage> SendMessageAsync(IDiscordMessageProperties message, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.SendMessageAsync(message.Original, properties?.Original, cancellationToken));
    }
    public Task AddMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.AddMessageReactionAsync(messageId, emoji.Original, properties?.Original, cancellationToken);
    }
    public Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessageReactionAsync(messageId, emoji.Original, properties?.Original, cancellationToken);
    }
    public Task DeleteMessageReactionAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, ulong userId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessageReactionAsync(messageId, emoji.Original, userId, properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordUser> GetMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordMessageReactionsPaginationProperties? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetMessageReactionsAsync(messageId, emoji.Original, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAllMessageReactionsAsync(messageId, properties?.Original, cancellationToken);
    }
    public Task DeleteAllMessageReactionsAsync(ulong messageId, IDiscordReactionEmojiProperties emoji, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteAllMessageReactionsAsync(messageId, emoji.Original, properties?.Original, cancellationToken);
    }
    public async Task<IDiscordRestMessage> ModifyMessageAsync(ulong messageId, Action<IDiscordMessageOptions> action, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.ModifyMessageAsync(messageId, x => action(new DiscordMessageOptions(x)), properties?.Original, cancellationToken));
    }
    public Task DeleteMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessageAsync(messageId, properties?.Original, cancellationToken);
    }
    public Task DeleteMessagesAsync(IEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessagesAsync(messageIds, properties?.Original, cancellationToken);
    }
    public Task DeleteMessagesAsync(IAsyncEnumerable<ulong> messageIds, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.DeleteMessagesAsync(messageIds, properties?.Original, cancellationToken);
    }
    public Task TriggerTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.TriggerTypingStateAsync(properties?.Original, cancellationToken);
    }
    public Task<IDisposable> EnterTypingStateAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.EnterTypingStateAsync(properties?.Original, cancellationToken);
    }
    public async Task<IReadOnlyList<IDiscordRestMessage>> GetPinnedMessagesAsync(IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.GetPinnedMessagesAsync(properties?.Original, cancellationToken)).Select(x => new DiscordRestMessage(x)).ToList();
    }
    public Task PinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.PinMessageAsync(messageId, properties?.Original, cancellationToken);
    }
    public Task UnpinMessageAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return _original.UnpinMessageAsync(messageId, properties?.Original, cancellationToken);
    }
    public async IAsyncEnumerable<IDiscordUser> GetMessagePollAnswerVotersAsync(ulong messageId, int answerId, IDiscordPaginationProperties<ulong>? paginationProperties = null, IDiscordRestRequestProperties? properties = null) 
    {
        await foreach(var original in _original.GetMessagePollAnswerVotersAsync(messageId, answerId, paginationProperties?.Original, properties?.Original))
        {
            yield return new DiscordUser(original);
        }
    }
    public async Task<IDiscordRestMessage> EndMessagePollAsync(ulong messageId, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return new DiscordRestMessage(await _original.EndMessagePollAsync(messageId, properties?.Original, cancellationToken));
    }
    public async Task<IReadOnlyList<IDiscordGoogleCloudPlatformStorageBucket>> CreateGoogleCloudPlatformStorageBucketsAsync(IEnumerable<IDiscordGoogleCloudPlatformStorageBucketProperties> buckets, IDiscordRestRequestProperties? properties = null, System.Threading.CancellationToken cancellationToken = default) 
    {
        return (await _original.CreateGoogleCloudPlatformStorageBucketsAsync(buckets.Select(x => x.Original), properties?.Original, cancellationToken)).Select(x => new DiscordGoogleCloudPlatformStorageBucket(x)).ToList();
    }
}


public class DiscordGroupDMChannelProperties : IDiscordGroupDMChannelProperties 
{
    private readonly NetCord.Rest.GroupDMChannelProperties _original;
    public DiscordGroupDMChannelProperties(NetCord.Rest.GroupDMChannelProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.GroupDMChannelProperties Original => _original;
    public IEnumerable<string> AccessTokens { get { return _original.AccessTokens; } set { _original.AccessTokens = value; } }
    public IReadOnlyDictionary<ulong, string>? Nicknames { get { return _original.Nicknames; } set { _original.Nicknames = value; } }
    public IDiscordGroupDMChannelProperties WithAccessTokens(IEnumerable<string> accessTokens) 
    {
        return new DiscordGroupDMChannelProperties(_original.WithAccessTokens(accessTokens));
    }
    public IDiscordGroupDMChannelProperties AddAccessTokens(IEnumerable<string> accessTokens) 
    {
        return new DiscordGroupDMChannelProperties(_original.AddAccessTokens(accessTokens));
    }
    public IDiscordGroupDMChannelProperties AddAccessTokens(string[] accessTokens) 
    {
        return new DiscordGroupDMChannelProperties(_original.AddAccessTokens(accessTokens));
    }
    public IDiscordGroupDMChannelProperties WithNicknames(IReadOnlyDictionary<ulong, string>? nicknames) 
    {
        return new DiscordGroupDMChannelProperties(_original.WithNicknames(nicknames));
    }
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
    public IReadOnlyList<IDiscordIntegration>? Integrations => _original.Integrations is null ? null : _original.Integrations.Select(x => new DiscordIntegration(x)).ToList();
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
    public string? PlatformName => _original.PlatformName;
    public string? PlatformUsername => _original.PlatformUsername;
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
    public string? PlatformName { get { return _original.PlatformName; } set { _original.PlatformName = value; } }
    public string? PlatformUsername { get { return _original.PlatformUsername; } set { _original.PlatformUsername = value; } }
    public IReadOnlyDictionary<string, string>? Metadata { get { return _original.Metadata; } set { _original.Metadata = value; } }
    public IDiscordApplicationRoleConnectionProperties WithPlatformName(string platformName) 
    {
        return new DiscordApplicationRoleConnectionProperties(_original.WithPlatformName(platformName));
    }
    public IDiscordApplicationRoleConnectionProperties WithPlatformUsername(string platformUsername) 
    {
        return new DiscordApplicationRoleConnectionProperties(_original.WithPlatformUsername(platformUsername));
    }
    public IDiscordApplicationRoleConnectionProperties WithMetadata(IReadOnlyDictionary<string, string>? metadata) 
    {
        return new DiscordApplicationRoleConnectionProperties(_original.WithMetadata(metadata));
    }
}


public class DiscordApplicationInstallParamsProperties : IDiscordApplicationInstallParamsProperties 
{
    private readonly NetCord.Rest.ApplicationInstallParamsProperties _original;
    public DiscordApplicationInstallParamsProperties(NetCord.Rest.ApplicationInstallParamsProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationInstallParamsProperties Original => _original;
    public IEnumerable<string>? Scopes { get { return _original.Scopes; } set { _original.Scopes = value; } }
    public NetCord.Permissions? Permissions { get { return _original.Permissions; } set { _original.Permissions = value; } }
    public IDiscordApplicationInstallParamsProperties WithScopes(IEnumerable<string>? scopes) 
    {
        return new DiscordApplicationInstallParamsProperties(_original.WithScopes(scopes));
    }
    public IDiscordApplicationInstallParamsProperties AddScopes(IEnumerable<string> scopes) 
    {
        return new DiscordApplicationInstallParamsProperties(_original.AddScopes(scopes));
    }
    public IDiscordApplicationInstallParamsProperties AddScopes(string[] scopes) 
    {
        return new DiscordApplicationInstallParamsProperties(_original.AddScopes(scopes));
    }
    public IDiscordApplicationInstallParamsProperties WithPermissions(NetCord.Permissions? permissions) 
    {
        return new DiscordApplicationInstallParamsProperties(_original.WithPermissions(permissions));
    }
}


public class DiscordApplicationIntegrationTypeConfigurationProperties : IDiscordApplicationIntegrationTypeConfigurationProperties 
{
    private readonly NetCord.Rest.ApplicationIntegrationTypeConfigurationProperties _original;
    public DiscordApplicationIntegrationTypeConfigurationProperties(NetCord.Rest.ApplicationIntegrationTypeConfigurationProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ApplicationIntegrationTypeConfigurationProperties Original => _original;
    public IDiscordApplicationInstallParamsProperties? OAuth2InstallParams { get { return _original.OAuth2InstallParams is null ? null : new DiscordApplicationInstallParamsProperties(_original.OAuth2InstallParams); } set { _original.OAuth2InstallParams = value?.Original; } }
    public IDiscordApplicationIntegrationTypeConfigurationProperties WithOAuth2InstallParams(IDiscordApplicationInstallParamsProperties? oAuth2InstallParams) 
    {
        return new DiscordApplicationIntegrationTypeConfigurationProperties(_original.WithOAuth2InstallParams(oAuth2InstallParams?.Original));
    }
}


public class DiscordForumGuildThreadMessageProperties : IDiscordForumGuildThreadMessageProperties 
{
    private readonly NetCord.Rest.ForumGuildThreadMessageProperties _original;
    public DiscordForumGuildThreadMessageProperties(NetCord.Rest.ForumGuildThreadMessageProperties original)
    {
        _original = original;
    }
    public NetCord.Rest.ForumGuildThreadMessageProperties Original => _original;
    public string? Content { get { return _original.Content; } set { _original.Content = value; } }
    public IEnumerable<IDiscordEmbedProperties>? Embeds { get { return _original.Embeds is null ? null : _original.Embeds.Select(x => new DiscordEmbedProperties(x)); } set { _original.Embeds = value?.Select(x => x.Original); } }
    public IDiscordAllowedMentionsProperties? AllowedMentions { get { return _original.AllowedMentions is null ? null : new DiscordAllowedMentionsProperties(_original.AllowedMentions); } set { _original.AllowedMentions = value?.Original; } }
    public IEnumerable<IDiscordComponentProperties>? Components { get { return _original.Components is null ? null : _original.Components.Select(x => new DiscordComponentProperties(x)); } set { _original.Components = value?.Select(x => x.Original); } }
    public IEnumerable<ulong>? StickerIds { get { return _original.StickerIds; } set { _original.StickerIds = value; } }
    public IEnumerable<IDiscordAttachmentProperties>? Attachments { get { return _original.Attachments is null ? null : _original.Attachments.Select(x => new DiscordAttachmentProperties(x)); } set { _original.Attachments = value?.Select(x => x.Original); } }
    public NetCord.MessageFlags? Flags { get { return _original.Flags; } set { _original.Flags = value; } }
    public IDiscordForumGuildThreadMessageProperties WithContent(string content) 
    {
        return new DiscordForumGuildThreadMessageProperties(_original.WithContent(content));
    }
    public IDiscordForumGuildThreadMessageProperties WithEmbeds(IEnumerable<IDiscordEmbedProperties>? embeds) 
    {
        return new DiscordForumGuildThreadMessageProperties(_original.WithEmbeds(embeds?.Select(x => x.Original)));
    }
    public IDiscordForumGuildThreadMessageProperties AddEmbeds(IEnumerable<IDiscordEmbedProperties> embeds) 
    {
        return new DiscordForumGuildThreadMessageProperties(_original.AddEmbeds(embeds.Select(x => x.Original)));
    }
    public IDiscordForumGuildThreadMessageProperties AddEmbeds(IDiscordEmbedProperties[] embeds) 
    {
        return new DiscordForumGuildThreadMessageProperties(_original.AddEmbeds(embeds.Select(x => x.Original).ToArray()));
    }
    public IDiscordForumGuildThreadMessageProperties WithAllowedMentions(IDiscordAllowedMentionsProperties? allowedMentions) 
    {
        return new DiscordForumGuildThreadMessageProperties(_original.WithAllowedMentions(allowedMentions?.Original));
    }
    public IDiscordForumGuildThreadMessageProperties WithComponents(IEnumerable<IDiscordComponentProperties>? components) 
    {
        return new DiscordForumGuildThreadMessageProperties(_original.WithComponents(components?.Select(x => x.Original)));
    }
    public IDiscordForumGuildThreadMessageProperties AddComponents(IEnumerable<IDiscordComponentProperties> components) 
    {
        return new DiscordForumGuildThreadMessageProperties(_original.AddComponents(components.Select(x => x.Original)));
    }
    public IDiscordForumGuildThreadMessageProperties AddComponents(IDiscordComponentProperties[] components) 
    {
        return new DiscordForumGuildThreadMessageProperties(_original.AddComponents(components.Select(x => x.Original).ToArray()));
    }
    public IDiscordForumGuildThreadMessageProperties WithStickerIds(IEnumerable<ulong>? stickerIds) 
    {
        return new DiscordForumGuildThreadMessageProperties(_original.WithStickerIds(stickerIds));
    }
    public IDiscordForumGuildThreadMessageProperties AddStickerIds(IEnumerable<ulong> stickerIds) 
    {
        return new DiscordForumGuildThreadMessageProperties(_original.AddStickerIds(stickerIds));
    }
    public IDiscordForumGuildThreadMessageProperties AddStickerIds(ulong[] stickerIds) 
    {
        return new DiscordForumGuildThreadMessageProperties(_original.AddStickerIds(stickerIds));
    }
    public IDiscordForumGuildThreadMessageProperties WithAttachments(IEnumerable<IDiscordAttachmentProperties>? attachments) 
    {
        return new DiscordForumGuildThreadMessageProperties(_original.WithAttachments(attachments?.Select(x => x.Original)));
    }
    public IDiscordForumGuildThreadMessageProperties AddAttachments(IEnumerable<IDiscordAttachmentProperties> attachments) 
    {
        return new DiscordForumGuildThreadMessageProperties(_original.AddAttachments(attachments.Select(x => x.Original)));
    }
    public IDiscordForumGuildThreadMessageProperties AddAttachments(IDiscordAttachmentProperties[] attachments) 
    {
        return new DiscordForumGuildThreadMessageProperties(_original.AddAttachments(attachments.Select(x => x.Original).ToArray()));
    }
    public IDiscordForumGuildThreadMessageProperties WithFlags(NetCord.MessageFlags? flags) 
    {
        return new DiscordForumGuildThreadMessageProperties(_original.WithFlags(flags));
    }
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
    public IDiscordImageUrl GetImageUrl(NetCord.ImageFormat format) 
    {
        return new DiscordImageUrl(_original.GetImageUrl(format));
    }
}


