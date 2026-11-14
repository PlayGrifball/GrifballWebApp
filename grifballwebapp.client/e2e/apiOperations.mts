// The API's calls as the generated client has them (src/generated/api, from openapi.json), for the tests that answer
// them (the e2e mock, mockApi.mts) and the ones that make them (the smoke tests' Api, smoke/api.ts): their types, and
// each one's path and method, so a test's URL can be checked against the call it names.
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import type { Observable } from 'rxjs';
import type * as ApiFunctions from '../src/generated/api/functions';
import type { StrictHttpResponse } from '../src/generated/api/strict-http-response';

/** Every call of the API, by the name the generated client gives it (src/generated/api/functions.ts). Type-only. */
type Api = typeof ApiFunctions;
export type Operation = { [K in keyof Api]: Api[K] extends (...args: never[]) => Observable<StrictHttpResponse<unknown>> ? K : never }[keyof Api];
/** What an operation answers, as the generated client reads it. */
export type AnswerOf<K extends Operation> = Api[K] extends (...args: never[]) => Observable<StrictHttpResponse<infer T>> ? T : never;
/** What an operation takes as its request body (never: none). */
export type BodyOf<K extends Operation> = Api[K] extends (http: never, rootUrl: never, params: infer P, ...rest: never[]) => unknown
  ? NonNullable<P> extends { body?: infer B } ? B : never
  : never;

/** A generated call's path ("/seasons/{seasonID}/summary", without /api) and method ("GET"). */
export interface OperationRoute {
  path: string;
  method: string;
}

let routes: Map<string, OperationRoute> | undefined;

/**
 * Each generated call's path and method, read from its file (src/generated/api/fn/<tag>/<name>.ts: "<name>.PATH = '...'"
 * and its RequestBuilder's method). The tests run from the client's directory (npm scripts, Playwright), so that is
 * where src/generated is.
 */
export function operationRoutes(): Map<string, OperationRoute> {
  if (routes)
    return routes;
  const root = join(process.cwd(), 'src/generated/api/fn');
  routes = new Map();
  for (const tag of readdirSync(root))
    for (const file of readdirSync(join(root, tag))) {
      const text = readFileSync(join(root, tag, file), 'utf8');
      const declaration = text.split('\n').find(line => line.includes(".PATH = '"));
      const method = between(text, ".PATH, '", "'");
      if (declaration && method) {
        const [name, rest] = declaration.split(".PATH = '");
        routes.set(name, { path: rest.slice(0, rest.indexOf("'")), method: method.toUpperCase() });
      }
    }
  return routes;
}

/** The text after `start` up to `end`, if `start` is there. */
function between(text: string, start: string, end: string): string | undefined {
  const from = text.indexOf(start);
  return from < 0 ? undefined : text.slice(from + start.length, text.indexOf(end, from + start.length));
}

/** Whether a URL path ("/api/seasons/12/summary") is one a path template ("/seasons/{seasonID}/summary") names. */
export function pathMatches(template: string, urlPath: string): boolean {
  const expected = ('/api' + template).split('/');
  const actual = urlPath.split('/');
  return expected.length === actual.length && expected.every((part, i) => part.startsWith('{') ? actual[i] !== '' : part === actual[i]);
}

/**
 * Why a request isn't the call it says it is (`operation` at another path, or by another method), or null when it is.
 * `url` is its path from /api, with or without a query string.
 */
export function wrongCall(operation: Operation, method: string, url: string): string | null {
  const route = operationRoutes().get(operation);
  const [path] = url.split('?');
  if (!route)
    return `${operation} is not in the generated client`;
  if (route.method !== method || !pathMatches(route.path, path))
    return `${method} ${url} is not ${operation}, which is ${route.method} /api${route.path}`;
  return null;
}
