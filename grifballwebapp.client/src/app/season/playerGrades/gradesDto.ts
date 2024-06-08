export interface TotalDto {
  xboxUserID: number
  gamertag: string
  totalGoals: number,
  totalKDSpread: number,
  totalPunches: number,
  totalSprees: number,
  totalDoubleKills: number,
  totalTripleKills: number,
  totalMultiKills: number,
  totalXFactor: number,
  totalKills: number,
  totalGameTime: number,
}

export interface PerMinuteDto {
  xboxUserID: number,
  gamertag: string,
  goalsPM: number,
  kdSpreadPM: number,
  punchesPM: number,
  spreesPM: number,
  doubleKillsPM: number,
  tripleKillsPM: number,
  multiKillsPM: number,
  xFactorPM: number,
  killsPM: number,
}

export interface LetterDto {
  xboxUserID: number,
  gamertag: string,
  goals: Letter,
  kdSpread: Letter,
  punches: Letter,
  sprees: Letter,
  doubleKills: Letter,
  tripleKills: Letter,
  multiKills: Letter,
  xFactor: Letter,
  kills: Letter,
  gradeAvgMath: number,
  gradeAvg: Letter,
}

export interface GradesDto {
  totals: TotalDto[],
  perMinutes: PerMinuteDto[],
  letters: LetterDto[],
}

export type Letter = 'S+' | 'S' | 'S-' | 'A+' | 'A' | 'A-' | 'B+' | 'B' | 'B-' | 'C+' | 'C' | 'C-' | 'D+' | 'D' | 'D-' | 'E+' | 'E' | 'E-' | 'F+' | 'F' | 'F-';
