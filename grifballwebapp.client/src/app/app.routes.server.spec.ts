import { RenderMode } from '@angular/ssr';
import { APP_ROUTES } from './app.routes';
import { serverRoutes } from './app.routes.server';

describe('serverRoutes', () => {
  const modeOf = (path: string) => serverRoutes.find(route => route.path === path)?.renderMode;

  it('renders the public pages on the server', () => {
    for (const path of ['', 'season/:seasonID', 'seasonmatch/:seasonMatchID', 'profile/:userID', 'login', 'topstats'])
      expect(modeOf(path)).withContext(path).toBe(RenderMode.Server);
  });

  it('leaves the pages for signed-in users to the browser', () => {
    for (const path of ['seasonManager', 'usermanagement', 'usermanagement/**', 'commissioner-dashboard', 'season/:seasonID/signupForm', 'season/:seasonID/teams'])
      expect(modeOf(path)).withContext(path).toBe(RenderMode.Client);
  });

  it('renders the not-found page with status 404', () => {
    const notFound = serverRoutes.find(route => route.path === '**');
    expect(notFound).toEqual(jasmine.objectContaining({ renderMode: RenderMode.Server, status: 404 }));
  });

  it('says how every page of the app is served', () => {
    const covered = (path: string) => serverRoutes.some(route => route.path === path
      || (route.path!.endsWith('/**') && path.startsWith(route.path!.slice(0, -2))));
    for (const route of APP_ROUTES)
      expect(covered(route.path!)).withContext(route.path!).toBeTrue();
  });
});
