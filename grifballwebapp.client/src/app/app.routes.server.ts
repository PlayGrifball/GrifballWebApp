import { RenderMode, ServerRoute } from '@angular/ssr';

/**
 * How each page is served. Rendered on the server: the public pages, as a visitor who is not
 * signed in (the access token lives in the browser), so they show their content before the app's
 * scripts have loaded. A signed-in user's parts (their name, the commissioner tools) are added in
 * the browser. Rendered in the browser only: pages for signed-in users or with a form of their
 * own, which have nothing to show a visitor.
 */
export const serverRoutes: ServerRoute[] = [
  ...[
    'theme',
    'season/:seasonID/signupForm',
    'season/:seasonID/teams',
    'seasonManager',
    'seasonEdit/:seasonID',
    'seasonAvailability/:seasonID',
    'infiniteclient',
    'usermanagement',
    'usermanagement/**',
    'excel',
    'excel/**',
    'commissioner-dashboard',
    'reschedule-request/:seasonMatchID',
  ].map((path): ServerRoute => ({ path, renderMode: RenderMode.Client })),
  { path: '', renderMode: RenderMode.Server },
  { path: 'season/:seasonID', renderMode: RenderMode.Server },
  { path: 'season/:seasonID/signups', renderMode: RenderMode.Server },
  { path: 'season/:seasonID/team/:teamID', renderMode: RenderMode.Server },
  { path: 'season/:seasonID/playergrades', renderMode: RenderMode.Server },
  { path: 'seasonmatch/:seasonMatchID', renderMode: RenderMode.Server },
  { path: 'topstats', renderMode: RenderMode.Server },
  { path: 'lateLeague', renderMode: RenderMode.Server },
  { path: 'profile/:userID', renderMode: RenderMode.Server },
  { path: 'login', renderMode: RenderMode.Server },
  { path: 'register', renderMode: RenderMode.Server },
  { path: 'reset-password', renderMode: RenderMode.Server },
  // Anything else is the not-found page
  { path: '**', renderMode: RenderMode.Server, status: 404 },
];
