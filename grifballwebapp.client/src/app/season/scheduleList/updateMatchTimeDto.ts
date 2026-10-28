import { DateTime } from "luxon";

export interface UpdateMatchTimeDto {
    seasonMatchID: number,
    time: DateTime | null // DateTime? on the server
}