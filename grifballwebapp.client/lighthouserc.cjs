// Lighthouse CI (https://github.com/GoogleChrome/lighthouse-ci), run by the lighthouse job in
// .github/workflows/frontend-tests.yml, once per preset and session:
// LIGHTHOUSE_PRESET=mobile|desktop, LIGHTHOUSE_SESSION=signed-out|signed-in.
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

// The public pages, for both; signed in, also the user's own profile (in place of someone else's)
// and the pages only commissioners and admins have. Not the login page, which they have no use for.
const pages = signedIn
  ? [
      '/',
      '/season/12',
      '/season/12?tab=schedule',
      '/season/12?tab=bracket',
      '/seasonmatch/31',
      '/profile/999',
      '/season/13/signupForm',
      '/season/13/teams',
      '/seasonManager',
      '/commissioner-dashboard',
      '/usermanagement',
    ]
  : [
      '/',
      '/season/12',
      '/season/12?tab=schedule',
      '/season/12?tab=bracket',
      '/seasonmatch/31',
      '/profile/117',
      '/season/13/signupForm',
      '/login',
    ];

module.exports = {
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
      },
    },
    assert: {
      // Of several runs of a page, the median one counts
      assertions: {
        'categories:accessibility': ['error', { minScore: 0.9, aggregationMethod: 'median-run' }],
        'categories:best-practices': ['error', { minScore: 0.9, aggregationMethod: 'median-run' }],
        'categories:seo': ['error', { minScore: 0.9, aggregationMethod: 'median-run' }],
        'categories:performance': ['error', { minScore: performanceFloor, aggregationMethod: 'median-run' }],
      },
    },
    upload: {
      target: process.env.LIGHTHOUSE_UPLOAD_TARGET ?? 'temporary-public-storage',
    },
  },
};
