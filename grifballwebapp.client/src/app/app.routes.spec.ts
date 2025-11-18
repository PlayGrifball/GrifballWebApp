import { APP_ROUTES } from './app.routes';

describe('APP_ROUTES', () => {
  it('should have routes defined', () => {
    expect(APP_ROUTES).toBeDefined();
    expect(APP_ROUTES.length).toBeGreaterThan(0);
  });

  it('should have home route at root path', () => {
    const homeRoute = APP_ROUTES.find(r => r.path === '');
    expect(homeRoute).toBeDefined();
    expect(homeRoute?.title).toBe('Home');
  });

  it('should have login route', () => {
    const loginRoute = APP_ROUTES.find(r => r.path === 'login');
    expect(loginRoute).toBeDefined();
    expect(loginRoute?.title).toBe('Login');
  });

  it('should have register route', () => {
    const registerRoute = APP_ROUTES.find(r => r.path === 'register');
    expect(registerRoute).toBeDefined();
    expect(registerRoute?.title).toBe('Register');
  });

  it('should have theme route', () => {
    const themeRoute = APP_ROUTES.find(r => r.path === 'theme');
    expect(themeRoute).toBeDefined();
    expect(themeRoute?.title).toBe('Theme');
  });

  it('should have topstats route', () => {
    const topStatsRoute = APP_ROUTES.find(r => r.path === 'topstats');
    expect(topStatsRoute).toBeDefined();
    expect(topStatsRoute?.title).toBe('Top Stats');
  });

  it('should have wildcard route for 404', () => {
    const notFoundRoute = APP_ROUTES.find(r => r.path === '**');
    expect(notFoundRoute).toBeDefined();
    expect(notFoundRoute?.title).toBe('Page Not Found');
  });

  it('should have season route with parameter', () => {
    const seasonRoute = APP_ROUTES.find(r => r.path === 'season/:seasonID');
    expect(seasonRoute).toBeDefined();
    expect(seasonRoute?.title).toBe('Season');
  });

  it('should have protected seasonManager route', () => {
    const seasonManagerRoute = APP_ROUTES.find(r => r.path === 'seasonManager');
    expect(seasonManagerRoute).toBeDefined();
    expect(seasonManagerRoute?.title).toBe('Season Manager');
    expect(seasonManagerRoute?.canActivate).toBeDefined();
    expect(seasonManagerRoute?.canActivate?.length).toBe(1);
  });

  it('should have protected infiniteclient route', () => {
    const infiniteClientRoute = APP_ROUTES.find(r => r.path === 'infiniteclient');
    expect(infiniteClientRoute).toBeDefined();
    expect(infiniteClientRoute?.title).toBe('Infinite Client');
    expect(infiniteClientRoute?.canActivate).toBeDefined();
  });

  it('should have protected usermanagement route', () => {
    const userManagementRoute = APP_ROUTES.find(r => r.path === 'usermanagement');
    expect(userManagementRoute).toBeDefined();
    expect(userManagementRoute?.title).toBe('User Management');
    expect(userManagementRoute?.canActivate).toBeDefined();
  });

  it('should have commissioner-dashboard route with guard', () => {
    const commissionerRoute = APP_ROUTES.find(r => r.path === 'commissioner-dashboard');
    expect(commissionerRoute).toBeDefined();
    expect(commissionerRoute?.title).toBe('Commissioner Dashboard');
    expect(commissionerRoute?.canActivate).toBeDefined();
  });

  it('should have excel route with guard', () => {
    const excelRoute = APP_ROUTES.find(r => r.path === 'excel');
    expect(excelRoute).toBeDefined();
    expect(excelRoute?.title).toBe('Excel Exporter');
    expect(excelRoute?.canActivate).toBeDefined();
  });

  it('should have signups route', () => {
    const signupsRoute = APP_ROUTES.find(r => r.path === 'season/:seasonID/signups');
    expect(signupsRoute).toBeDefined();
    expect(signupsRoute?.title).toBe('Signups');
  });

  it('should have teams route', () => {
    const teamsRoute = APP_ROUTES.find(r => r.path === 'season/:seasonID/teams');
    expect(teamsRoute).toBeDefined();
    expect(teamsRoute?.title).toBe('Teams');
  });

  it('should have profile route with parameter', () => {
    const profileRoute = APP_ROUTES.find(r => r.path === 'profile/:userID');
    expect(profileRoute).toBeDefined();
    expect(profileRoute?.title).toBe('User Profile');
  });

  it('should have lateLeague route', () => {
    const lateLeagueRoute = APP_ROUTES.find(r => r.path === 'lateLeague');
    expect(lateLeagueRoute).toBeDefined();
    expect(lateLeagueRoute?.title).toBe('Late League');
  });

  it('should have all routes with lazy loading', () => {
    const routesWithComponents = APP_ROUTES.filter(r => r.path !== '**');
    routesWithComponents.forEach(route => {
      expect(route.loadComponent).toBeDefined();
    });
  });

  it('should have seasonEdit route with guard', () => {
    const seasonEditRoute = APP_ROUTES.find(r => r.path === 'seasonEdit/:seasonID');
    expect(seasonEditRoute).toBeDefined();
    expect(seasonEditRoute?.canActivate).toBeDefined();
  });

  it('should have reschedule-request route', () => {
    const rescheduleRoute = APP_ROUTES.find(r => r.path === 'reschedule-request/:seasonMatchID');
    expect(rescheduleRoute).toBeDefined();
    expect(rescheduleRoute?.title).toBe('Request Reschedule');
  });
});
