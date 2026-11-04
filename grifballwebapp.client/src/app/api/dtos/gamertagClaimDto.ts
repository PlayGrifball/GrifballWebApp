import type { SeasonRefDto } from './seasonRefDto';

/**
 * How a claim came about: a historical roster name matched a user's Xbox account, a user set their gamertag to
 * it, or a background lookup found who currently holds a placeholder's gamertag on Xbox (informational: it is
 * never approved or rejected).
 */
export type GamertagClaimKind = 'RosterMatch' | 'SetGamertag' | 'XuidLookup' | 'UserClaim' | 'DiscordMatch';
export type GamertagClaimStatus = 'Pending' | 'Approved' | 'Rejected';
export const GAMERTAG_CLAIM_STATUSES: readonly GamertagClaimStatus[] = ['Pending', 'Approved', 'Rejected'];

/**
 * A claim that a historical player (a placeholder user from the archive) is a real user's Xbox account, or
 * (kind "XuidLookup") a note on who currently holds that gamertag on Xbox (GET /api/GamertagClaims/GetClaims).
 */
export interface GamertagClaimDto {
  gamertagClaimID: number;
  kind: GamertagClaimKind;
  status: GamertagClaimStatus;
  /** Null for a user's or Discord claim whose user has no Xbox account linked. */
  xboxUserID: number | null;
  xboxGamertag: string | null;
  /** The user the placeholder is merged into on approval: the claimant, or the Xbox account's user. */
  userID: number | null;
  /** Kinds "UserClaim" and "DiscordMatch": the user the claim is about. */
  claimantUserID?: number | null;
  claimantName?: string | null;
  /** Kind "UserClaim": when the user says they had the gamertag. */
  firstSeen?: string | null;
  lastSeen?: string | null;
  /** Why a sysadmin rejected it, shown to the claimant. */
  rejectionReason?: string | null;
  /** The gamertag being claimed, as the archive has it. */
  gamertag: string;
  placeholderUserID: number | null;
  seasonID: number | null;
  seasonName: string | null;
  teamID: number | null;
  teamName: string | null;
  createdAt: string;
  /** kind "XuidLookup" only: who Xbox says holds xboxUserID now (null until looked up, or if Xbox has no answer). */
  currentGamertag: string | null;
  /** kind "XuidLookup" only: when the lookup ran; null until it has. */
  lookedUpAt: string | null;
  /** The archive seasons (with year) this placeholder's gamertag played in, e.g. "Winter League 2019 (2019)". */
  placeholderSeasons: SeasonRefDto[];
}

/** A gamertag an Xbox account went by (POST /api/GamertagClaims/AddGamertagHistory). */
export interface AddGamertagHistoryDto {
  xboxUserID: number;
  gamertag: string;
  firstSeen: string | null;
  lastSeen: string | null;
  note: string | null;
}

/** How the background lookup of placeholders' current Xbox gamertags is going (GET /api/GamertagClaims/XuidLookupStatus, and the body of the 202 from POST LookUpXuids). */
export interface XuidLookupStatusDto {
  placeholders: number;
  due: number;
  found: number;
  notFound: number;
  failed: number;
  lastLookedUpAt: string | null;
}

/** An Xbox account's current gamertag and the gamertags it is known to have gone by (GET /api/GamertagClaims/PastGamertags). */
export interface PastGamertagsDto {
  xboxUserID: number;
  currentGamertag: string;
  userID: number | null;
  pastGamertags: PastGamertagDto[];
}

/** One gamertag an Xbox account is known to have gone by. */
export interface PastGamertagDto {
  xboxGamertagHistoryID: number;
  xboxUserID: number;
  gamertag: string;
  firstSeen: string | null;
  lastSeen: string | null;
  source: string;
  note: string | null;
  /** Only a manually-added row can be removed. */
  canRemove: boolean;
}

/** What claiming a past gamertag would merge (GET /api/MyProfile/PreviewClaim): its placeholder and seasons, or why it can't be claimed. */
export interface ClaimPreviewDto {
  gamertag: string;
  placeholderUserID: number | null;
  placeholderName: string | null;
  /** "Name (year)", oldest first. */
  seasons: SeasonRefDto[];
  /** Null when it can be claimed. */
  refusal: string | null;
}

/** A user's claim of a past gamertag (POST /api/MyProfile/ClaimGamertag); the dates are optional ISO strings. */
export interface SubmitGamertagClaimDto {
  gamertag: string;
  firstSeen: string | null;
  lastSeen: string | null;
}

/** Why a pair was suggested: the same loose gamertag, a few edits apart, the same name inside symmetric decoration
 * ("xX Name Xx"), or one placeholder's whole name as a word of the other placeholder's gamertag. */
export type DuplicateMatch = 'Exact' | 'Edits' | 'Framing' | 'Token';

/** A placeholder that may be the same player as another user (GET /api/GamertagClaims/DuplicateSuggestions). */
export interface DuplicateSuggestionDto {
  /** The placeholder, merged on "Merge". */
  sourceUserID: number;
  sourceGamertag: string;
  targetUserID: number;
  targetGamertag: string;
  targetIsPlaceholder: boolean;
  match: DuplicateMatch;
  /** Edits between the two gamertags, compared loosely (0: the same apart from case, spaces, punctuation or a "#1234" suffix). */
  distance: number;
  sourceSeasons: SeasonRefDto[];
  targetSeasons: SeasonRefDto[];
}
