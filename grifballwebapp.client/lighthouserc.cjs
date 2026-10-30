// Lighthouse CI (https://github.com/GoogleChrome/lighthouse-ci), run by the lighthouse job in
// .github/workflows/frontend-tests.yml, once per preset and session:
// LIGHTHOUSE_PRESET=mobile|desktop, LIGHTHOUSE_SESSION=signed-out|signed-in, and LIGHTHOUSE_SHARD for
// the pages (see below).
// (Not LHCI_*: lhci reads those environment variables as its own options.)
// It audits the production build as deployed, its server (src/server.ts) run by e2e/serve.mjs with
// the API answered from e2e/mockApi.mts. On localhost, which the server renders pages for, as it
// does for the site.
//
// Signed out, as a visitor. Signed in, as a commissioner and admin ("Commander Grif" in
// e2e/mockApi.mts): Lighthouse sends the session cookie with every request (extraHeaders), so the
// server renders each page as that user (the header with their name, the commissioner tools, the
// admin pages) and the page's own API calls are theirs too. A Cookie header rather than a cookie
// in the browser: the session cookie's __Host- name takes HTTPS, which this server does not have.
// e2e/lighthouse-summary.mjs checks that the server did render each page for them.
const port = Number(process.env.E2E_PORT ?? 4300);
const desktop = process.env.LIGHTHOUSE_PRESET === 'desktop';
const signedIn = process.env.LIGHTHOUSE_SESSION === 'signed-in';
const base = `http://localhost:${port}`;
// Performance must not drop below these on the CI runner: some margin below what it scores (in
// October 2026 0.77-0.86 on mobile, 0.98-1.00 on desktop). A single mobile run varies by some 15
// points there, so mobile takes the median of three. e2e/lighthouse-summary.mjs warns below 90.
const performanceFloor = desktop ? 0.9 : 0.7;

// The session cookie (SESSION_COOKIE in e2e/mockApi.mts), its value the role it signs in as there
const signedInCookie = '__Host-Grifball=admin';

// An address the site has no page for
const notFoundPage = '/no-such-page';

/** Escapes a string for use inside a RegExp (assert.assertMatrix's matchingUrlPattern). */
const escapeForRegExp = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

// The pages the browser draws (RenderMode.Client in src/app/app.routes.server.ts): the server sends
// them as it does to anyone, and the browser draws them as the user
const browserPages = ['/theme'];

// Every page of the site (src/app/app.routes.ts), with the mock data of e2e/mockApi.mts.
// Pages anyone can open, for both:
const publicPages = [
  '/',
  ...browserPages,
  '/season/12',
  '/season/12?tab=schedule',
  '/season/12?tab=bracket',
  '/season/12/signups',
  '/season/13/signupForm',
  '/season/13/teams',
  '/season/12/team/1',
  '/season/12/playergrades',
  '/seasonmatch/31',
  '/reschedule-request/31',
  // The page for any other address (answered with a 404)
  notFoundPage,
];
// Signed out, also someone's profile and the pages for signing in. Not the pages only commissioners
// and admins may open, which send a visitor home (the home page is audited already).
const signedOutPages = [
  '/profile/117',
  '/login',
  '/register',
  // Opened from the link in a reset email; without its token the page sends the visitor to /login
  '/reset-password?token=e2e-reset-token',
];
// Signed in, the user's own profile (in place of someone else's) and the commissioners' and admins'
// pages. Not login, register and reset-password, for visitors: a signed-in user has no use for them.
const signedInPages = [
  '/profile/999',
  '/seasonManager',
  // A new season (0), and an existing one
  '/seasonEdit/0',
  '/seasonEdit/12',
  '/seasonAvailability/12',
  '/commissioner-dashboard',
  '/excel',
  '/excel/1xGrifballAutumnBrawl2026/Week%203',
  '/infiniteclient',
  '/usermanagement',
  '/usermanagement/createuser',
  '/usermanagement/edituser/2',
  // Merging: nobody picked yet, the user to merge from, and both
  '/usermanagement/mergeuser',
  '/usermanagement/mergeuser/2',
  '/usermanagement/mergeuser/2/3',
];
const allPages = [...publicPages, ...(signedIn ? signedInPages : signedOutPages)];

// Too many pages for one job in reasonable time (each takes some 45 seconds: three mobile runs and a
// desktop one), so CI splits them between jobs: LIGHTHOUSE_SHARD=2/3 audits every third page from
// the second on. All of them without it.
const [shard, shards] = (process.env.LIGHTHOUSE_SHARD ?? '1/1').split('/').map(Number);
const pages = allPages.filter((_, i) => i % shards === shard - 1);

module.exports = {
  // For e2e/lighthouse-signed-in.mjs
  notFoundPage,
  browserPages,
  ci: {
    collect: {
      startServerCommand: 'node e2e/serve.mjs',
      startServerReadyPattern: 'Serving',
      url: pages.map(path => base + path),
      numberOfRuns: desktop ? 1 : 3,
      settings: {
        ...(desktop ? { preset: 'desktop' } : {}),
        ...(signedIn ? { extraHeaders: { Cookie: signedInCookie } } : {}),
        // e.g. --no-sandbox when Chromium runs as root in a container
        chromeFlags: process.env.LIGHTHOUSE_CHROME_FLAGS ?? '',
        // Slow, and only a picture for the report
        skipAudits: ['full-page-screenshot'],
        // The page for an unknown address is answered with a 404, which Lighthouse otherwise refuses
        // to audit. Any other page answering with an error still fails: its SEO audit
        // (http-status-code) does, and e2e/lighthouse-signed-in.mjs checks each page's status.
        ignoreStatusCode: true,
      },
    },
    assert: {
      // Of several runs of a page, the median one counts. assertMatrix: the 404 page
      // (notFoundPage) is asserted differently, see below; every other page keeps the one set of
      // assertions it always had.
      assertMatrix: [
        {
          matchingUrlPattern: escapeForRegExp(notFoundPage),
          assertions: {
            // The page correctly answers with a 404: Lighthouse's own best-practices and SEO
            // audits for that (errors-in-console, for the console message Angular's server logs
            // for an unknown route; http-status-code, for the 404 status itself) are expected to
            // fail here, and failing them drags those two categories' scores down too. That is
            // the page working as intended, not a regression, so both the audits and the
            // categories they belong to are off for this one URL; accessibility and performance
            // are asserted as normal (e2e/lighthouse-summary.mjs notes this row as expected).
            'errors-in-console': 'off',
            'http-status-code': 'off',
            'categories:best-practices': 'off',
            'categories:seo': 'off',
            'categories:accessibility': ['error', { minScore: 0.9, aggregationMethod: 'median-run' }],
            'categories:performance': ['error', { minScore: performanceFloor, aggregationMethod: 'median-run' }],
          },
        },
        {
          matchingUrlPattern: `^(?!.*${escapeForRegExp(notFoundPage)}).*$`,
          assertions: {
            'categories:accessibility': ['error', { minScore: 0.9, aggregationMethod: 'median-run' }],
            'categories:best-practices': ['error', { minScore: 0.9, aggregationMethod: 'median-run' }],
            'categories:seo': ['error', { minScore: 0.9, aggregationMethod: 'median-run' }],
            'categories:performance': ['error', { minScore: performanceFloor, aggregationMethod: 'median-run' }],
          },
        },
      ],
    },
    upload: {
      target: process.env.LIGHTHOUSE_UPLOAD_TARGET ?? 'temporary-public-storage',
    },
  },
};
