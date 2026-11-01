import { RenderMode } from '@angular/ssr';
import { APP_ROUTES } from './app.routes';
import { serverRoutes } from './app.routes.server';

describe('serverRoutes', () => {
  const modeOf = (path: string) => serverRoutes.find(route => route.path === path)?.renderMode;

  it('renders the public pages on the server', () => {
    for (const path of ['', 'season/:seasonID', 'seasonmatch/:seasonMatchID', 'profile/:userID', 'login'])
      expect(modeOf(path)).withContext(path).toBe(RenderMode.Server);
  });

  it('renders the pages for signed-in users on the server too (as that user)', () => {
    for (const path of ['seasonManager', 'usermanagement', 'usermanagement/**', 'commissioner-dashboard', 'season/:seasonID/signupForm', 'season/:seasonID/teams', 'excel', 'infiniteclient'])
      expect(modeOf(path)).withContext(path).toBe(RenderMode.Server);
  });

  it('renders the gamertag claims page on the server, with the other user management pages', () => {
    const covering = serverRoutes.find(route => route.path === 'usermanagement/**');
    expect(covering?.renderMode).toBe(RenderMode.Server);
    expect(APP_ROUTES.some(route => route.path === 'usermanagement/claims')).toBeTrue();
  });

  it('leaves only the theme page to the browser', () => {
    expect(serverRoutes.filter(route => route.renderMode === RenderMode.Client).map(route => route.path)).toEqual(['theme']);
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
