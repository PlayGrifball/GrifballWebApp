export interface AccessTokenResponse {
  tokenType: string,
  accessToken: string,
  expiresIn: number,
  refreshToken: string
}

export interface MetaInfoResponse {
  isSysAdmin: boolean,
  isCommissioner: boolean,
  isPlayer: boolean,
  displayName: string
}

