/** The hosts the server renders pages for whatever NG_ALLOWED_HOSTS says: the site's own, and localhost. */
export const DEFAULT_ALLOWED_HOSTS: readonly string[] = ['grifball.xyz', '*.grifball.xyz', 'localhost'];

/**
 * The hosts to render pages for: the defaults and NG_ALLOWED_HOSTS's comma-separated entries, in lower
 * case, as host names are compared (isAllowedHost lowercases the request's Host too).
 */
export function parseAllowedHosts(env: string | undefined): string[] {
  return [
    ...DEFAULT_ALLOWED_HOSTS,
    ...(env ?? '').split(',').map(host => host.trim().toLowerCase()).filter(host => host !== ''),
  ];
}

/** Whether pages for this Host header may be rendered (the same rule Angular's engine applies). */
export function isAllowedHost(host: string | undefined, allowed: readonly string[]): boolean {
  const hostname = (host ?? '').replace(/:\d+$/, '').replace(/^\[(.*)\]$/, '$1').toLowerCase();
  return hostname !== '' && allowed.some(entry => entry === hostname || (entry.startsWith('*.') && hostname.endsWith(entry.slice(1))));
}
