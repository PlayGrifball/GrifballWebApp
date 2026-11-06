/** The matchmaking queue as the signed-in user (if any) sees it (GET /api/Queue/Status). */
export interface QueueStatusDto {
  /** How many players are in the queue. */
  count: number;
  /** Late League runs here, so joining is allowed (not in a preview without Discord). */
  open: boolean;
  signedIn: boolean;
  /** The user has a gamertag linked, which joining needs. */
  hasGamertag: boolean;
  inQueue: boolean;
  inMatch: boolean;
}
