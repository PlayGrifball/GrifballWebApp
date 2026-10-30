/** Who is signed in (IdentityController.MetaInfo and Me). */
export interface MetaInfoResponse {
  isSysAdmin: boolean,
  isCommissioner: boolean,
  isPlayer: boolean,
  displayName: string,
  userID: number,
}
