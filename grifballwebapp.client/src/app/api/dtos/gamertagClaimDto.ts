/** How a claim came about: a historical roster name matched a user's Xbox account, or a user set their gamertag to it. */
export type GamertagClaimKind = 'RosterMatch' | 'SetGamertag';
export type GamertagClaimStatus = 'Pending' | 'Approved' | 'Rejected';
export const GAMERTAG_CLAIM_STATUSES: readonly GamertagClaimStatus[] = ['Pending', 'Approved', 'Rejected'];

/**
 * A claim that a historical player (a placeholder user from the archive) is a real user's Xbox account
 * (GET /api/GamertagClaims/GetClaims).
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
}

/** A gamertag an Xbox account went by (POST /api/GamertagClaims/AddGamertagHistory). */
export interface AddGamertagHistoryDto {
  xboxUserID: number;
  gamertag: string;
  firstSeen: string | null;
  lastSeen: string | null;
}
