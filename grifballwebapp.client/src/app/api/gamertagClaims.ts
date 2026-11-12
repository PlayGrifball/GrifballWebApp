/**
 * How a claim came about: a historical roster name matched a user's Xbox account, a user set their gamertag to
 * it, or a background lookup found who currently holds a placeholder's gamertag on Xbox (informational: it is
 * never approved or rejected).
 */
export type GamertagClaimKind = 'RosterMatch' | 'SetGamertag' | 'XuidLookup' | 'UserClaim' | 'DiscordMatch';
