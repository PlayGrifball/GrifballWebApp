import { DateTime } from "luxon";
import { fromApiDate } from "../shared/apiDates";

export interface UserResponseDto {
  userID: number,
  userName: string,
  lockoutEnd: DateTime | null,
  lockoutEnabled: boolean,
  isDummyUser: boolean,
  accessFailedCount: number,
  region: string | null,
  displayName: string | null,
  gamertag: string | null,
  discord: string | null,
  externalAuthCount: number,
  hasPassword: boolean,
  roles: RoleDto[]
}

export interface RoleDto {
  roleName: string,
  hasRole: boolean
}

/** A user as the API sends it (lockoutEnd as an ISO string), with a real DateTime. */
export function parseUser(raw: any): UserResponseDto {
  return { ...raw, lockoutEnd: fromApiDate(raw.lockoutEnd) };
}
