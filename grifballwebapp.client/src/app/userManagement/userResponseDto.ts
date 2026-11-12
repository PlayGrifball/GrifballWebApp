import { DateTime } from "luxon";
import { fromApiDate } from "../shared/apiDates";
import type { UserResponseDto as ApiUserResponseDto } from '@api/models';

/** A user as the edit page holds them: the API's UserResponseDto, its lockout end as a Luxon DateTime. */
export type UserResponseDto = Omit<ApiUserResponseDto, 'lockoutEnd'> & { lockoutEnd: DateTime | null };

export type { RoleDto } from '@api/models';

export function parseUser(raw: ApiUserResponseDto | null | undefined): UserResponseDto | null {
  if (raw == null)
    return null;
  return { ...raw, lockoutEnd: fromApiDate(raw.lockoutEnd) };
}
