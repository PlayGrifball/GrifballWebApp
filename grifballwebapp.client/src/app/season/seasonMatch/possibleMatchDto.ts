export interface PossibleMatchDto {
  matchID: string,
  homeTeam: PossibleTeamDto,
  awayTeam: PossibleTeamDto,
}

export interface PossibleTeamDto {
  teamID: number,
  score: number,
  outcome: 'Tie' | 'Won' | 'Lost' | 'DidNotFinish', // Outcomes enum, sent as its name
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
