export class PlayerDto {
    personID: number = 0;
    name: string = "";
    round: number | null = null;
    pick: number | null = null;
    /** Where the player plays from, if known. */
    region?: string | null;
  }
