namespace DiscordInterfaceSourceGen;

using System.Collections.Immutable;
using System.Linq;
using System.Collections.Generic;

public interface IDiscordInteractionContext
{
    IDiscordInteraction Interaction { get; }
}


public interface IDiscordInteraction
{
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
}


public interface IDiscordInteractionGuildReference
{
    ulong Id { get; }
    IReadOnlyList<string> Features { get; }
    string Locale { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordGuild
{
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
}


public interface IDiscordTextChannel
{
    ulong? LastMessageId { get; }
    System.DateTimeOffset? LastPin { get; }
    ulong Id { get; }
    NetCord.ChannelFlags Flags { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordUser
{
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
}


public interface IDiscordEntitlement
{
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
}


public interface IDiscordInteractionData
{
}


public interface IDiscordVoiceState
{
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
}


public interface IDiscordGuildChannel
{
    ulong GuildId { get; }
    int? Position { get; }
    IReadOnlyDictionary<ulong, IDiscordPermissionOverwrite> PermissionOverwrites { get; }
}


public interface IDiscordGuildThread
{
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
}


public interface IDiscordPresence
{
    IDiscordUser User { get; }
    ulong GuildId { get; }
    NetCord.UserStatusType Status { get; }
    IReadOnlyList<IDiscordUserActivity> Activities { get; }
    IReadOnlyDictionary<NetCord.Gateway.Platform, NetCord.UserStatusType> Platform { get; }
}


public interface IDiscordStageInstance
{
    ulong Id { get; }
    ulong GuildId { get; }
    ulong ChannelId { get; }
    string Topic { get; }
    NetCord.StageInstancePrivacyLevel PrivacyLevel { get; }
    bool DiscoverableDisabled { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordGuildScheduledEvent
{
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
}


public interface IDiscordRole
{
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
}


public interface IDiscordGuildEmoji
{
    IReadOnlyList<ulong> AllowedRoles { get; }
    ulong GuildId { get; }
    ulong Id { get; }
    IDiscordUser Creator { get; }
    bool? RequireColons { get; }
    bool? Managed { get; }
    bool? Available { get; }
    string Name { get; }
    bool Animated { get; }
}


public interface IDiscordGuildWelcomeScreen
{
    string Description { get; }
    ImmutableDictionary<ulong, IDiscordGuildWelcomeScreenChannel> WelcomeChannels { get; }
}


public interface IDiscordGuildSticker
{
    bool? Available { get; }
    ulong GuildId { get; }
    IDiscordUser Creator { get; }
    ulong Id { get; }
    string Name { get; }
    string Description { get; }
    IReadOnlyList<string> Tags { get; }
    NetCord.StickerFormat Format { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordAvatarDecorationData
{
    string Hash { get; }
    ulong SkuId { get; }
}


public interface IDiscordImageUrl
{
}


public interface IDiscordPermissionOverwrite
{
    ulong Id { get; }
    NetCord.PermissionOverwriteType Type { get; }
    NetCord.Permissions Allowed { get; }
    NetCord.Permissions Denied { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordGuildThreadMetadata
{
    bool Archived { get; }
    NetCord.ThreadArchiveDuration AutoArchiveDuration { get; }
    System.DateTimeOffset ArchiveTimestamp { get; }
    bool Locked { get; }
    bool? Invitable { get; }
}


public interface IDiscordThreadCurrentUser
{
    System.DateTimeOffset JoinTimestamp { get; }
    int Flags { get; }
}


public interface IDiscordUserActivity
{
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


public interface IDiscordGuildScheduledEventRecurrenceRule
{
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
    ulong? BotId { get; }
    ulong? IntegrationId { get; }
    bool IsPremiumSubscriber { get; }
    ulong? SubscriptionListingId { get; }
    bool IsAvailableForPurchase { get; }
    bool GuildConnections { get; }
}


public interface IDiscordGuildWelcomeScreenChannel
{
    ulong Id { get; }
    string Description { get; }
    ulong? EmojiId { get; }
    string EmojiName { get; }
    System.DateTimeOffset CreatedAt { get; }
}


public interface IDiscordUserActivityTimestamps
{
    System.DateTimeOffset? StartTime { get; }
    System.DateTimeOffset? EndTime { get; }
}


public interface IDiscordEmoji
{
    string Name { get; }
    bool Animated { get; }
}


public interface IDiscordParty
{
    string Id { get; }
    int? CurrentSize { get; }
    int? MaxSize { get; }
}


public interface IDiscordUserActivityAssets
{
    string LargeImageId { get; }
    string LargeText { get; }
    string SmallImageId { get; }
    string SmallText { get; }
}


public interface IDiscordUserActivitySecrets
{
    string Join { get; }
    string Spectate { get; }
    string Match { get; }
}


public interface IDiscordUserActivityButton
{
    string Label { get; }
}


public interface IDiscordGuildScheduledEventRecurrenceRuleNWeekday
{
    int N { get; }
    NetCord.GuildScheduledEventRecurrenceRuleWeekday Day { get; }
}


public class DiscordInteractionContext : IDiscordInteractionContext
{
    private readonly NetCord.Services.IInteractionContext _original;
    public DiscordInteractionContext(NetCord.Services.IInteractionContext original)
    {
        _original = original;
    }
    public IDiscordInteraction Interaction => new DiscordInteraction(_original.Interaction);
}


public class DiscordInteraction : IDiscordInteraction
{
    private readonly NetCord.Interaction _original;
    public DiscordInteraction(NetCord.Interaction original)
    {
        _original = original;
    }
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
}


public class DiscordInteractionGuildReference : IDiscordInteractionGuildReference
{
    private readonly NetCord.InteractionGuildReference _original;
    public DiscordInteractionGuildReference(NetCord.InteractionGuildReference original)
    {
        _original = original;
    }
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
}


public class DiscordTextChannel : IDiscordTextChannel
{
    private readonly NetCord.TextChannel _original;
    public DiscordTextChannel(NetCord.TextChannel original)
    {
        _original = original;
    }
    public ulong? LastMessageId => _original.LastMessageId;
    public System.DateTimeOffset? LastPin => _original.LastPin;
    public ulong Id => _original.Id;
    public NetCord.ChannelFlags Flags => _original.Flags;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordUser : IDiscordUser
{
    private readonly NetCord.User _original;
    public DiscordUser(NetCord.User original)
    {
        _original = original;
    }
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
}


public class DiscordEntitlement : IDiscordEntitlement
{
    private readonly NetCord.Entitlement _original;
    public DiscordEntitlement(NetCord.Entitlement original)
    {
        _original = original;
    }
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
}


public class DiscordInteractionData : IDiscordInteractionData
{
    private readonly NetCord.InteractionData _original;
    public DiscordInteractionData(NetCord.InteractionData original)
    {
        _original = original;
    }
}


public class DiscordVoiceState : IDiscordVoiceState
{
    private readonly NetCord.Gateway.VoiceState _original;
    public DiscordVoiceState(NetCord.Gateway.VoiceState original)
    {
        _original = original;
    }
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
}


public class DiscordGuildChannel : IDiscordGuildChannel
{
    private readonly NetCord.IGuildChannel _original;
    public DiscordGuildChannel(NetCord.IGuildChannel original)
    {
        _original = original;
    }
    public ulong GuildId => _original.GuildId;
    public int? Position => _original.Position;
    public IReadOnlyDictionary<ulong, IDiscordPermissionOverwrite> PermissionOverwrites => _original.PermissionOverwrites.ToDictionary(kv => kv.Key, kv => (IDiscordPermissionOverwrite)new DiscordPermissionOverwrite(kv.Value));
}


public class DiscordGuildThread : IDiscordGuildThread
{
    private readonly NetCord.GuildThread _original;
    public DiscordGuildThread(NetCord.GuildThread original)
    {
        _original = original;
    }
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
}


public class DiscordPresence : IDiscordPresence
{
    private readonly NetCord.Gateway.Presence _original;
    public DiscordPresence(NetCord.Gateway.Presence original)
    {
        _original = original;
    }
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
    public ulong Id => _original.Id;
    public ulong GuildId => _original.GuildId;
    public ulong ChannelId => _original.ChannelId;
    public string Topic => _original.Topic;
    public NetCord.StageInstancePrivacyLevel PrivacyLevel => _original.PrivacyLevel;
    public bool DiscoverableDisabled => _original.DiscoverableDisabled;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordGuildScheduledEvent : IDiscordGuildScheduledEvent
{
    private readonly NetCord.GuildScheduledEvent _original;
    public DiscordGuildScheduledEvent(NetCord.GuildScheduledEvent original)
    {
        _original = original;
    }
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
}


public class DiscordRole : IDiscordRole
{
    private readonly NetCord.Role _original;
    public DiscordRole(NetCord.Role original)
    {
        _original = original;
    }
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
}


public class DiscordGuildEmoji : IDiscordGuildEmoji
{
    private readonly NetCord.GuildEmoji _original;
    public DiscordGuildEmoji(NetCord.GuildEmoji original)
    {
        _original = original;
    }
    public IReadOnlyList<ulong> AllowedRoles => _original.AllowedRoles;
    public ulong GuildId => _original.GuildId;
    public ulong Id => _original.Id;
    public IDiscordUser Creator => new DiscordUser(_original.Creator);
    public bool? RequireColons => _original.RequireColons;
    public bool? Managed => _original.Managed;
    public bool? Available => _original.Available;
    public string Name => _original.Name;
    public bool Animated => _original.Animated;
}


public class DiscordGuildWelcomeScreen : IDiscordGuildWelcomeScreen
{
    private readonly NetCord.GuildWelcomeScreen _original;
    public DiscordGuildWelcomeScreen(NetCord.GuildWelcomeScreen original)
    {
        _original = original;
    }
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
    public bool? Available => _original.Available;
    public ulong GuildId => _original.GuildId;
    public IDiscordUser Creator => new DiscordUser(_original.Creator);
    public ulong Id => _original.Id;
    public string Name => _original.Name;
    public string Description => _original.Description;
    public IReadOnlyList<string> Tags => _original.Tags;
    public NetCord.StickerFormat Format => _original.Format;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordAvatarDecorationData : IDiscordAvatarDecorationData
{
    private readonly NetCord.AvatarDecorationData _original;
    public DiscordAvatarDecorationData(NetCord.AvatarDecorationData original)
    {
        _original = original;
    }
    public string Hash => _original.Hash;
    public ulong SkuId => _original.SkuId;
}


public class DiscordImageUrl : IDiscordImageUrl
{
    private readonly NetCord.ImageUrl _original;
    public DiscordImageUrl(NetCord.ImageUrl original)
    {
        _original = original;
    }
}


public class DiscordPermissionOverwrite : IDiscordPermissionOverwrite
{
    private readonly NetCord.PermissionOverwrite _original;
    public DiscordPermissionOverwrite(NetCord.PermissionOverwrite original)
    {
        _original = original;
    }
    public ulong Id => _original.Id;
    public NetCord.PermissionOverwriteType Type => _original.Type;
    public NetCord.Permissions Allowed => _original.Allowed;
    public NetCord.Permissions Denied => _original.Denied;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordGuildThreadMetadata : IDiscordGuildThreadMetadata
{
    private readonly NetCord.GuildThreadMetadata _original;
    public DiscordGuildThreadMetadata(NetCord.GuildThreadMetadata original)
    {
        _original = original;
    }
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
    public System.DateTimeOffset JoinTimestamp => _original.JoinTimestamp;
    public int Flags => _original.Flags;
}


public class DiscordUserActivity : IDiscordUserActivity
{
    private readonly NetCord.Gateway.UserActivity _original;
    public DiscordUserActivity(NetCord.Gateway.UserActivity original)
    {
        _original = original;
    }
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


public class DiscordGuildScheduledEventRecurrenceRule : IDiscordGuildScheduledEventRecurrenceRule
{
    private readonly NetCord.GuildScheduledEventRecurrenceRule _original;
    public DiscordGuildScheduledEventRecurrenceRule(NetCord.GuildScheduledEventRecurrenceRule original)
    {
        _original = original;
    }
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
    public ulong Id => _original.Id;
    public string Description => _original.Description;
    public ulong? EmojiId => _original.EmojiId;
    public string EmojiName => _original.EmojiName;
    public System.DateTimeOffset CreatedAt => _original.CreatedAt;
}


public class DiscordUserActivityTimestamps : IDiscordUserActivityTimestamps
{
    private readonly NetCord.Gateway.UserActivityTimestamps _original;
    public DiscordUserActivityTimestamps(NetCord.Gateway.UserActivityTimestamps original)
    {
        _original = original;
    }
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
    public string Label => _original.Label;
}


public class DiscordGuildScheduledEventRecurrenceRuleNWeekday : IDiscordGuildScheduledEventRecurrenceRuleNWeekday
{
    private readonly NetCord.GuildScheduledEventRecurrenceRuleNWeekday _original;
    public DiscordGuildScheduledEventRecurrenceRuleNWeekday(NetCord.GuildScheduledEventRecurrenceRuleNWeekday original)
    {
        _original = original;
    }
    public int N => _original.N;
    public NetCord.GuildScheduledEventRecurrenceRuleWeekday Day => _original.Day;
}


