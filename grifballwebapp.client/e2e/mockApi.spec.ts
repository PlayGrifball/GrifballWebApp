import { expect, test } from '@playwright/test';
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { mockRoutes, type Operation, type Role } from './mockApi.mts';

// The mock API's routes against the generated client (src/generated/api, from openapi.json): each route answers the
// operation it names (its answer type-checked by tsconfig.e2e.json), at a URL that operation's path matches. A route
// left behind when the API's path changes fails here instead of answering a request the app no longer makes.

/** Each generated call's path and method (src/generated/api/fn/<tag>/<name>.ts: "<name>.PATH = '...'", its RequestBuilder). */
function generatedOperations(): Map<string, { path: string, method: string }> {
  // Playwright runs from the client's root (playwright.config.ts) and loads specs as CommonJS, without import.meta
  const root = join(process.cwd(), 'src/generated/api/fn');
  const operations = new Map<string, { path: string, method: string }>();
  for (const tag of readdirSync(root))
    for (const file of readdirSync(join(root, tag))) {
      const text = readFileSync(join(root, tag, file), 'utf8');
      const declaration = text.split('\n').find(line => line.includes(".PATH = '"));
      const method = between(text, ".PATH, '", "'");
      if (declaration && method) {
        const [name, rest] = declaration.split(".PATH = '");
        operations.set(name, { path: rest.slice(0, rest.indexOf("'")), method: method.toUpperCase() });
      }
    }
  return operations;
}

/** The text after `start` up to `end`, if `start` is there. */
function between(text: string, start: string, end: string): string | undefined {
  const from = text.indexOf(start);
  return from < 0 ? undefined : text.slice(from + start.length, text.indexOf(end, from + start.length));
}

/** Whether a URL path ("/api/seasons/12/summary") is one a path template ("/seasons/{seasonID}/summary") names. */
function matches(template: string, urlPath: string): boolean {
  const expected = ('/api' + template).split('/');
  const actual = urlPath.split('/');
  return expected.length === actual.length && expected.every((part, i) => part.startsWith('{') ? actual[i] !== '' : part === actual[i]);
}

const operations = generatedOperations();

test('the generated client has its operations where the mock reads them', () => {
  expect(operations.size).toBeGreaterThan(100);
  expect(operations.get('getMatchDetails')).toEqual({ path: '/matches/{matchID}', method: 'GET' });
});

for (const role of ['anon', 'player', 'admin'] as Role[]) {
  test(`every route the mock answers for ${role} is at its operation's path`, () => {
    const wrong: string[] = [];
    for (const [url, { operation }] of Object.entries(mockRoutes(role))) {
      const generated = operations.get(operation satisfies Operation);
      const [path] = url.split('?');
      if (!generated)
        wrong.push(`${url}: ${operation} is not in the generated client`);
      else if (!matches(generated.path, path))
        wrong.push(`${url}: ${operation} is ${generated.method} /api${generated.path}`);
    }
    expect(wrong).toEqual([]);
  });
}
