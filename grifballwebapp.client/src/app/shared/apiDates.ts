import { DateTime } from 'luxon';

/*
 * Dates from the API arrive as ISO 8601 strings: the server's DateTimeJsonConverter writes every
 * DateTime as UTC with a trailing Z, and DateTimeOffset values carry their offset. Each DTO
 * with date fields converts them where it is fetched (not in a global interceptor, which would
 * have to guess which strings are dates).
 */

/** For a nullable server date (DateTime?, DateTimeOffset?): missing or empty is null. */
export function fromApiDate(value: string | null | undefined): DateTime | null {
  return value ? DateTime.fromISO(value) : null;
}

/** For a server date that is never null (DateTime). A malformed value gives an invalid DateTime. */
export function fromRequiredApiDate(value: string): DateTime {
  return DateTime.fromISO(value);
}

/**
 * A DateTime as ISO 8601 in UTC (never past year 9999 for a value the API sent), or null.
 *
 * Plain toJSON() writes the local offset, which the server reads as the same instant, except
 * near the end of time: DateTimeOffset.MaxValue in a zone ahead of UTC becomes year +010000,
 * which System.Text.Json rejects. Use this for values that can be that large, and send
 * untouched values back as the string the API sent.
 */
export function toApiDate(value: DateTime | null | undefined): string | null {
  return value ? value.toUTC().toISO() : null;
}
