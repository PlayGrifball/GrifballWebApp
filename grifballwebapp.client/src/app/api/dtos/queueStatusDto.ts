/** The matchmaking queue as the signed-in user (if any) sees it (GET /api/Queue/Status). */
export interface QueueStatusDto {
  /** How many players are in the queue. */
  count: number;
  signedIn: boolean;
  /** The user has a gamertag linked, which joining needs. */
  hasGamertag: boolean;
  inQueue: boolean;
  inMatch: boolean;
}
