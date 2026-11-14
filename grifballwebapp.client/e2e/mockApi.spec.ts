import { expect, test } from '@playwright/test';
import { operationRoutes, pathMatches, type Operation } from './apiOperations.mts';
import { mockRoutes, type Role } from './mockApi.mts';

// The mock API's routes against the generated client (src/generated/api, from openapi.json): each route answers the
// operation it names (its answer type-checked by tsconfig.e2e.json), at a URL that operation's path matches. A route
// left behind when the API's path changes fails here instead of answering a request the app no longer makes.

const operations = operationRoutes();

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
      else if (!pathMatches(generated.path, path))
        wrong.push(`${url}: ${operation} is ${generated.method} /api${generated.path}`);
    }
    expect(wrong).toEqual([]);
  });
}
