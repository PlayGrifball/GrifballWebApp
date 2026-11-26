/**
 * A GUID (a Waypoint match ID, or a Halo Infinite match ID): hex digits in the usual 8-4-4-4-12 groups, either
 * bare or wrapped in braces, `{...}`. The braces must balance: one alone (`{...` or `...}`) does not match.
 */
export const GUID_PATTERN = String.raw`^(?:\{[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}\}|[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12})$`;

export const GUID_ERROR_MESSAGE = 'Must be a valid GUID';
