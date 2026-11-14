import { RenderMode, ServerRoute } from '@angular/ssr';

/**
 * How each page is served. Rendered on the server, as whoever the visitor is: the session cookie
 * goes with the server's API calls (ServerApiBackend), so a signed-in user gets their own page
 * (their name, the commissioner tools, the admin pages) before the app's scripts have loaded. Such a
 * page is never cached (server.ts). Every page: the theme page too, drawn with the site's colours
 * (the visitor's own are in their browser), which the browser draws again with theirs (ThemeComponent
 * skips hydration); drawn only in the browser, it showed nothing until all of the app's scripts had run.
 */
export const serverRoutes: ServerRoute[] = [
  ...[
    '',
    'theme',
    'season/:seasonID',
    'season/:seasonID/signups',
    'season/:seasonID/signupForm',
    'season/:seasonID/teams',
    'season/:seasonID/draft',
    'season/:seasonID/team/:teamID',
    'season/:seasonID/playergrades',
    'seasonmatch/:seasonMatchID',
    'match/:matchID',
    'profile/:userID',
    'xbox/:xuid',
    'history/team/:teamID',
    'seasons',
    'leaderboards',
    'late-league',
    'late-league/:matchmakingSeasonID',
    'login',
    'register',
    'reset-password',
    'seasonManager',
    'seasonEdit/:seasonID',
    'seasonAvailability/:seasonID',
    'infiniteclient',
    'databasebackup',
    'usermanagement',
    'usermanagement/**', // the user editor, merging and gamertag claims
    'excel',
    'excel/**',
    'commissioner-dashboard',
    'reschedule-request/:seasonMatchID',
  ].map((path): ServerRoute => ({ path, renderMode: RenderMode.Server })),
  // Anything else is the not-found page
  { path: '**', renderMode: RenderMode.Server, status: 404 },
];
