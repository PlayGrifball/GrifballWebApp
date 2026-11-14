import { request, type APIRequestContext, type APIResponse } from '@playwright/test';
import { wrongCall, type AnswerOf, type BodyOf, type Operation } from '../e2e/apiOperations.mts';
import type { Credentials } from './env';

/**
 * How long one API call may take. Signing in and registering hash a password, which on a small
 * preview busy with both workers' setup has taken over 15 seconds (the suite's action timeout).
 */
const API_TIMEOUT = 60_000;

/**
 * The app's API (/api/...) as one signed-in user, with a bearer token: the browser signs in with
 * a cookie, but the API still gives scripts a token (Login?useCookies=false), and a request with
 * one needs no antiforgery token.
 */
export class Api {
  private constructor(
    private readonly http: APIRequestContext,
    readonly credentials: Credentials,
    private token: string,
    readonly userID: number,
  ) {}

  static async login(baseURL: string, credentials: Credentials): Promise<Api> {
    const http = await request.newContext({ baseURL, timeout: API_TIMEOUT });
    const token = await Api.token(http, credentials);
    const meta = await http.get('/api/identity/meta-info', { headers: { Authorization: `Bearer ${token}` } });
    await ok(meta, `MetaInfo for ${credentials.username}`);
    const { userID } = await meta.json() as { userID: number };
    return new Api(http, credentials, token, userID);
  }

  private static async token(http: APIRequestContext, credentials: Credentials): Promise<string> {
    const res = await http.post('/api/identity/login?useCookies=false', { data: credentials });
    await ok(res, `login as ${credentials.username}`);
    return (await res.json() as { accessToken: string }).accessToken;
  }

  /** A new token, e.g. after a role change (roles are in the token). */
  async relogin(): Promise<void> {
    this.token = await Api.token(this.http, this.credentials);
  }

  /**
   * GET /api/`path`, the API's `operation` (its name in the generated client): answered as that operation answers, and
   * refused before it is sent when `path` isn't that operation's (the call the test means has moved or never was).
   */
  async get<K extends Operation>(operation: K, path: string): Promise<AnswerOf<K>> {
    checkCall(operation, 'GET', path);
    const res = await this.http.get(`/api/${path}`, { headers: this.headers() });
    await ok(res, `GET ${path}`);
    return body<AnswerOf<K>>(res);
  }

  /** POST /api/`path`, the API's `operation`, with the request body it takes: as get(). */
  async post<K extends Operation>(operation: K, path: string, data?: BodyOf<K>): Promise<AnswerOf<K>> {
    checkCall(operation, 'POST', path);
    const res = await this.http.post(`/api/${path}`, { headers: this.headers(), data });
    await ok(res, `POST ${path}`);
    return body<AnswerOf<K>>(res);
  }

  async dispose(): Promise<void> {
    await this.http.dispose();
  }

  private headers(): Record<string, string> {
    return { Authorization: `Bearer ${this.token}` };
  }
}

/** Anyone may register; used for the throwaway players. */
export async function register(baseURL: string, credentials: Credentials): Promise<void> {
  const http = await request.newContext({ baseURL, timeout: API_TIMEOUT });
  try {
    await ok(await http.post('/api/identity/register', { data: credentials }), `register ${credentials.username}`);
  } finally {
    await http.dispose();
  }
}

/** A call to the operation the test names, or an error saying what the operation is instead. */
function checkCall(operation: Operation, method: string, path: string): void {
  const wrong = wrongCall(operation, method, '/api/' + path);
  if (wrong)
    throw new Error(wrong);
}

async function ok(res: APIResponse, what: string): Promise<void> {
  if (!res.ok()) {
    throw new Error(`${what}: HTTP ${res.status()} ${(await res.text()).slice(0, 500)}`);
  }
}

async function body<T>(res: APIResponse): Promise<T> {
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}
