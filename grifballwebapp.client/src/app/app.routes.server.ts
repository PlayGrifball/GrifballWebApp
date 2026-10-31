import { RenderMode, ServerRoute } from '@angular/ssr';

/**
 * How each page is served. Rendered on the server, as whoever the visitor is: the session cookie
 * goes with the server's API calls (ServerApiBackend), so a signed-in user gets their own page
 * (their name, the commissioner tools, the admin pages) before the app's scripts have loaded. Such a
 * page is never cached (server.ts). Rendered in the browser only: the theme page, which reads and
 * sets the visitor's own colours (localStorage, the document's styles) and has nothing to draw
 * without them.
 */
export const serverRoutes: ServerRoute[] = [
  { path: 'theme', renderMode: RenderMode.Client },
  ...[
    '',
    'season/:seasonID',
    'season/:seasonID/signups',
    'season/:seasonID/signupForm',
    'season/:seasonID/teams',
    'season/:seasonID/team/:teamID',
    'season/:seasonID/playergrades',
    'seasonmatch/:seasonMatchID',
    'profile/:userID',
    'seasons',
    'leaderboards',
    'archive',
    'login',
    'register',
    'reset-password',
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
  ].map((path): ServerRoute => ({ path, renderMode: RenderMode.Server })),
  // Anything else is the not-found page
  { path: '**', renderMode: RenderMode.Server, status: 404 },
];
