// Environment for the smoke suite. Everything a run needs comes from here, never from the code.

/** Hosts the suite may write to: the PR previews, or a local development server. */
const ALLOWED_HOST = /^(grif-preview\d+\.grifball\.xyz|localhost|127\.0\.0\.1)$/;

/**
 * The preview to test, from SMOKE_BASE_URL. The suite creates seasons, users and signups, so it
 * refuses to run against anything that isn't a preview (or localhost): never test, staging or prod.
 */
export function smokeBaseUrl(): string {
  const raw = process.env['SMOKE_BASE_URL'];
  if (!raw) {
    throw new Error('SMOKE_BASE_URL is not set (e.g. https://grif-preview1.grifball.xyz)');
  }
  let url: URL;
  try {
    url = new URL(raw);
  } catch {
    throw new Error(`SMOKE_BASE_URL is not a URL: ${raw}`);
  }
  if (!ALLOWED_HOST.test(url.hostname)) {
    throw new Error(`Refusing to run the smoke suite against ${url.hostname}: only grif-preview<N>.grifball.xyz or localhost`);
  }
  return url.origin;
}

export interface Credentials {
  username: string;
  password: string;
}

/** The preview's smoke-test accounts (Preview:SmokeUserPassword on the server; Sysadmin + Commissioner). */
export function smokeUsers(): [Credentials, Credentials] {
  const password = process.env['SMOKE_PASSWORD'];
  if (!password) {
    throw new Error('SMOKE_PASSWORD is not set (the preview\'s Preview:SmokeUserPassword)');
  }
  return [
    { username: process.env['SMOKE_USER'] || 'smoke-test', password },
    { username: process.env['SMOKE_USER_2'] || 'smoke-test-2', password: process.env['SMOKE_PASSWORD_2'] || password },
  ];
}

/**
 * Names everything this run creates (`smoke-<runid>-...`), so leftovers are easy to recognise.
 * SMOKE_RUN_ID if set (CI passes the workflow run), otherwise made up once and shared with the
 * workers through the environment.
 */
export function smokeRunId(): string {
  let id = process.env['SMOKE_RUN_ID'];
  if (!id) {
    id = Date.now().toString(36) + Math.random().toString(36).slice(2, 6);
    process.env['SMOKE_RUN_ID'] = id;
  }
  return id;
}
