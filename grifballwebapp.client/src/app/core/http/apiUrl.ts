/**
 * The backend's address when API_URL doesn't name one: its Service (or compose service) inside the
 * cluster, where it serves plain HTTP. Never a public address: TLS ends at the ingress, in front of
 * the app's server.
 */
// eslint-disable-next-line sonarjs/no-clear-text-protocols -- the backend's in-cluster address: plain HTTP behind the ingress's TLS
export const DEFAULT_API_URL = 'http://grifballwebapp-server:5295'; // NOSONAR(S5332): the backend's in-cluster address, plain HTTP behind the ingress's TLS

/**
 * The backend the app's server calls (its proxy in server.ts, and the API calls of the pages it
 * renders): API_URL, or DEFAULT_API_URL, without trailing slashes.
 */
export function apiUrl(env: string | undefined): string {
  return withoutTrailingSlashes(env ?? DEFAULT_API_URL);
}

/** `url` without the slashes it ends with: "http://a/" -> "http://a". A loop, not a regex: linear. */
export function withoutTrailingSlashes(url: string): string {
  let end = url.length;
  while (end > 0 && url[end - 1] === '/')
    end--;
  return url.slice(0, end);
}
