import { SESSION_COOKIE, hasSession, pageCacheHeaders } from './pageCache';

describe('pageCache', () => {
  it('finds the session cookie among the others', () => {
    expect(hasSession(`${SESSION_COOKIE}=abc`)).toBeTrue();
    expect(hasSession(`theme=dark; ${SESSION_COOKIE}=abc; other=1`)).toBeTrue();
    expect(hasSession(`theme=dark;${SESSION_COOKIE}=abc`)).toBeTrue();
  });

  it('is not fooled by other cookies', () => {
    expect(hasSession(undefined)).toBeFalse();
    expect(hasSession(null)).toBeFalse();
    expect(hasSession('')).toBeFalse();
    expect(hasSession('theme=dark')).toBeFalse();
    // The antiforgery cookie alone is not a session
    expect(hasSession(`${SESSION_COOKIE}.Antiforgery=xyz`)).toBeFalse();
    expect(hasSession(`x${SESSION_COOKIE}=abc`)).toBeFalse();
    expect(hasSession(`other=${SESSION_COOKIE}=abc`)).toBeFalse();
  });

  it('keeps a signed-in user\'s page private and out of every cache', () => {
    expect(pageCacheHeaders(`${SESSION_COOKIE}=abc`)).toEqual({ 'Cache-Control': 'private, no-store', 'Vary': 'Cookie' });
  });

  it('has a visitor\'s page checked on every visit, as before', () => {
    expect(pageCacheHeaders(undefined)).toEqual({ 'Cache-Control': 'no-cache' });
    expect(pageCacheHeaders('theme=dark')).toEqual({ 'Cache-Control': 'no-cache' });
  });
});
