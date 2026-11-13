import type { UserResponseDto } from '@api/models';
import { parseUser } from './userResponseDto';

/** A user as GET /api/users/{id} sends it, with these fields. */
const apiUser = (changes: Partial<UserResponseDto>): UserResponseDto => ({
  userID: 1, userName: 'u', lockoutEnd: null, lockoutEnabled: false, isDummyUser: false, accessFailedCount: 0, region: null, regionID: null,
  displayName: null, gamertag: null, discord: null, externalAuthCount: 0, hasPassword: true, roles: [], ...changes,
});

// What the server's DateTimeJsonConverter writes: UTC with a Z. DateTimeOffset keeps its offset.
const UTC = '2026-01-02T03:04:00Z';
const OFFSET = '2026-01-02T03:04:00+00:00';

describe('parseUser', () => {
  it('parseUser converts lockoutEnd, and keeps null', () => {
    expect(parseUser(apiUser({ lockoutEnd: OFFSET }))!.lockoutEnd!.toMillis()).toBe(Date.parse(UTC));
    expect(parseUser(apiUser({ lockoutEnd: null }))!.lockoutEnd).toBeNull();
    expect(parseUser(apiUser({ userName: 'grif', lockoutEnd: null }))!.userName).toBe('grif');
  });

  it('keeps a null body (204: no such user) null', () => {
    expect(parseUser(null)).toBeNull();
    expect(parseUser(undefined)).toBeNull();
  });
});
