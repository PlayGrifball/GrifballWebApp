export class CaptainDto {
    personID: number = 0;
    name: string = "";
    order: number | null = null;
    /** Where the captain plays from, if known. */
    region?: string | null;
  }
