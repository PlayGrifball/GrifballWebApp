import { expect, test } from '@playwright/test';
import { operationRoutes, pathMatches, type Operation } from './apiOperations.mts';
import { mockResponse, mockRoutes, type Role } from './mockApi.mts';

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

test("gives every player and account a picture, so no page it serves logs a missing one, and answers an unknown path not at all", () => {
  for (const path of ['/api/users/101/profile-picture', '/api/xbox-accounts/2535400000000010/profile-picture']) {
    const picture = mockResponse('anon', path);
    expect(picture?.status).toBe(200);
    expect(picture?.contentType).toBe('image/svg+xml');
  }
  expect(mockResponse('anon', '/api/users/101/halo-emblem')).toBeUndefined();
});
