export interface PossibleMatchDto {
  matchID: string,
  homeTeam: PossibleTeamDto,
  awayTeam: PossibleTeamDto,
}

export interface PossibleTeamDto {
  teamID: number,
  score: number,
  outcome: number, // Make this a string later
  players: PossiblePlayerDto[],
}

export interface PossiblePlayerDto {
  xboxUserID: number,
  gamertag: string,
  score: number,
  kills: number,
  deaths: number,
  isOnTeam: boolean,
}
