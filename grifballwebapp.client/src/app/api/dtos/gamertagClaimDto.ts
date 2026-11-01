/**
 * How a claim came about: a historical roster name matched a user's Xbox account, a user set their gamertag to
 * it, or a background lookup found who currently holds a placeholder's gamertag on Xbox (informational: it is
 * never approved or rejected).
 */
export type GamertagClaimKind = 'RosterMatch' | 'SetGamertag' | 'XuidLookup';
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
  xboxUserID: number;
  xboxGamertag: string | null;
  userID: number | null;
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
  placeholderSeasons: string[];
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
