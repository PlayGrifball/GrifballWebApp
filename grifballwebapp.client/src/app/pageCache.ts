/**
 * The session cookie (BrowserAuth.CookieName on the API). A page rendered for a request that has it
 * may show that user's own things: their name, the commissioner tools, the API answers the server
 * fetched with it (in the page's transfer cache).
 */
export const SESSION_COOKIE = '__Host-Grifball';

/** Whether this Cookie header carries the session cookie. */
export function hasSession(cookieHeader: string | null | undefined): boolean {
  return (cookieHeader ?? '').split(';').some(cookie => cookie.trim().startsWith(SESSION_COOKIE + '='));
}

/**
 * The caching headers of a page the server rendered. Everyone's page is checked on every visit
 * (it changes with each build and its data). A page rendered with a session is that user's alone:
 * no browser or cache in between (Cloudflare) may keep it or hand it to anyone else, so it is
 * private and no-store, and varies by Cookie for any cache that ignores those. A visitor's page varies
 * by Cookie too: a cache that kept it must not hand it to a signed-in user, whose page differs.
 */
export function pageCacheHeaders(cookieHeader: string | null | undefined): Record<string, string> {
  return hasSession(cookieHeader)
    ? { 'Cache-Control': 'private, no-store', 'Vary': 'Cookie' }
    : { 'Cache-Control': 'no-cache', 'Vary': 'Cookie' };
}
