import { request, type APIRequestContext, type APIResponse } from '@playwright/test';
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
    const meta = await http.get('/api/Identity/MetaInfo', { headers: { Authorization: `Bearer ${token}` } });
    await ok(meta, `MetaInfo for ${credentials.username}`);
    const { userID } = await meta.json() as { userID: number };
    return new Api(http, credentials, token, userID);
  }

  private static async token(http: APIRequestContext, credentials: Credentials): Promise<string> {
    const res = await http.post('/api/Identity/Login?useCookies=false', { data: credentials });
    await ok(res, `login as ${credentials.username}`);
    return (await res.json() as { accessToken: string }).accessToken;
  }

  /** A new token, e.g. after a role change (roles are in the token). */
  async relogin(): Promise<void> {
    this.token = await Api.token(this.http, this.credentials);
  }

  async get<T>(path: string): Promise<T> {
    const res = await this.http.get(`/api/${path}`, { headers: this.headers() });
    await ok(res, `GET ${path}`);
    return body<T>(res);
  }

  async post<T = unknown>(path: string, data?: unknown): Promise<T> {
    const res = await this.http.post(`/api/${path}`, { headers: this.headers(), data });
    await ok(res, `POST ${path}`);
    return body<T>(res);
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
    await ok(await http.post('/api/Identity/Register', { data: credentials }), `register ${credentials.username}`);
  } finally {
    await http.dispose();
  }
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

// Shapes of the server's DTOs, only the fields the suite uses.

export interface SeasonDto {
  seasonID: number;
  seasonName: string;
  signupsOpen: string;
  signupsClose: string;
  draftStart: string;
  seasonStart: string;
  seasonEnd: string;
  /** The game the season is played on: the server requires it for a new season (6 is Halo Infinite). */
  gameVersionID?: number | null;
}

export interface TimeslotDto {
  optionID: number;
  dayOfWeek: string;
  time: string;
  isChecked: boolean;
}

export interface RoleDto { roleName: string; hasRole: boolean }

export interface UserResponseDto {
  userID: number;
  userName: string;
  displayName: string | null;
  lockoutEnd: string | null;
  lockoutEnabled: boolean;
  roles: RoleDto[];
  [key: string]: unknown;
}

export interface TeamResponseDto {
  teamID: number;
  teamName: string;
  captain: { name: string; personID: number; order: number };
  players: { name: string; personID: number; round: number | null }[];
}
