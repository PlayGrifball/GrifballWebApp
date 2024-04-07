export interface MovePlayerToTeamRequestDto {
  seasonID: number,
  previousCaptainID: number,
  newCaptainID: number,
  personID: number,
  roundNumber: number
}
