/** One user merged into another, from the merges' audit trail (GET /api/UserMerges/List). */
export interface UserMergeDto {
  userMergeID: number;
  mergedAt: string;
  /** Manual, ClaimApproval, SetGamertag, XuidLookup, UserClaim, XboxPlaceholder, DiscordMatch, Import, DuplicateSuggestion. */
  trigger: string;
  reason: string | null;
  triggeredByUserID: number | null;
  triggeredByUserName: string | null;
  sourceUserID: number;
  sourceUserName: string | null;
  targetUserID: number;
  targetUserName: string | null;
  rowsMoved: number;
  rowsRemoved: number;
  rowsAdded: number;
  undoneAt: string | null;
  undoneByUserID: number | null;
  undoNote: string | null;
}
