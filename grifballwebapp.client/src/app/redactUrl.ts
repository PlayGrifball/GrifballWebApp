/**
 * Query parameters whose values never go into a log line, a span or an error report: sign-in and reset
 * tokens, codes, keys, and who the request is for. Matched anywhere in the name, in any case, so
 * "resetToken", "access_token" and "code" all count. A few harmless names match too ("countryCode"); a
 * redacted value costs nothing.
 */
const SENSITIVE = /token|code|secret|passw|pwd|key|auth|session|sig|credential|cookie|email|user|state|nonce|otp|ticket|hash|jwt/i;

export const REDACTED = 'REDACTED';

/** Whether a query parameter's value is kept out of logs. */
export function isSensitiveParam(name: string): boolean {
  return SENSITIVE.test(name);
}

/**
 * A URL as it may be logged: its path and query, every sensitive parameter's value REDACTED. Absolute
 * URLs lose their scheme and host (`base` resolves relative ones). An unparseable URL gives ''.
 */
export function redactUrl(url: string | null | undefined, base = 'http://localhost'): string {
  if (!url)
    return '';
  let parsed: URL;
  try {
    parsed = new URL(url, base);
  } catch {
    return '';
  }
  const kept = [...parsed.searchParams].map(([name, value]) =>
    `${encodeURIComponent(name)}=${isSensitiveParam(name) ? REDACTED : encodeURIComponent(value)}`);
  return parsed.pathname + (kept.length ? '?' + kept.join('&') : '');
}

/** `text` with each occurrence of `url` replaced by its redacted form (HttpErrorResponse puts the URL in its message). */
export function redactUrlIn(text: string, url: string | null | undefined, base?: string): string {
  return url ? text.replaceAll(url, redactUrl(url, base)) : text;
}

/** A query string in text: what follows a "?" up to a space, quote, bracket or "#". */
const QUERY_IN_TEXT = /\?([^\s#'"<>()]+)/g;

/**
 * `text` (an error's message, a stack) with the sensitive values of every query string in it REDACTED, whatever the
 * URL around it: "see /reset?token=abc&page=2" -> "see /reset?token=REDACTED&page=2".
 */
export function redactUrlsIn(text: string): string {
  return text.replace(QUERY_IN_TEXT, (_, query: string) => '?' + query.split('&').map(pair => {
    const equals = pair.indexOf('=');
    if (equals < 0)
      return pair;
    let name = pair.slice(0, equals);
    try {
      name = decodeURIComponent(name);
    } catch {
      // Not URL-encoded after all: matched as it is
    }
    return isSensitiveParam(name) ? `${pair.slice(0, equals)}=${REDACTED}` : pair;
  }).join('&'));
}
