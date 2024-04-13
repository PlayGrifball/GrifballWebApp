export class CaptainPlacementDto {
  seasonID: number = 0;
  personID: number = 0;
  orderNumber: number = 0;
}

export class CaptainAddedDto {
  seasonID: number = 0;
  personID: number = 0;
  teamName: string = '';
  captainName: string = '';
  orderNumber: number = 0;
}

export class RemoveCaptainDto {
  seasonID: number = 0;
  personID: number = 0;
}
