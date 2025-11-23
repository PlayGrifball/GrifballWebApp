import { DateTime } from "luxon";

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
  roles: RoleDto[]
}

export interface RoleDto {
  roleName: string,
  hasRole: boolean
}
