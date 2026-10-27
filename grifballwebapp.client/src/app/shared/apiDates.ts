import { DateTime } from 'luxon';

/**
 * Dates from the API arrive as ISO 8601 strings: the server's DateTimeJsonConverter writes every
 * DateTime as UTC with a trailing Z, and DateTimeOffset values carry their offset. Each DTO
 * with date fields converts them where it is fetched (not in a global interceptor, which would
 * have to guess which strings are dates).
 *
 * Sending one back needs nothing: DateTime.toJSON() is ISO 8601 with an offset, which the
 * server's converter reads as the same instant.
 */
export function fromApiDate(value: string): DateTime;
export function fromApiDate(value: string | null | undefined): DateTime | null;
export function fromApiDate(value: string | null | undefined): DateTime | null {
  return value ? DateTime.fromISO(value) : null;
}
