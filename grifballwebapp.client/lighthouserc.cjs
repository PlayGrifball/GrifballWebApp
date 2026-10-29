// Lighthouse CI (https://github.com/GoogleChrome/lighthouse-ci), run by the lighthouse job in
// .github/workflows/frontend-tests.yml, once per preset: LIGHTHOUSE_PRESET=mobile|desktop.
// (Not LHCI_*: lhci reads those environment variables as its own options.)
// It audits the production build served by e2e/serve.mjs with MOCK_API=1, which answers /api from
// e2e/mockApi.mts, as a visitor who is not logged in (Lighthouse cannot log in).
const port = Number(process.env.E2E_PORT ?? 4300);
const desktop = process.env.LIGHTHOUSE_PRESET === 'desktop';
const base = `http://127.0.0.1:${port}`;
// Performance must not drop below these on the CI runner: some margin below what it scores (in
// October 2026 about 0.80 on mobile, 0.98 on desktop). A single mobile run varies by some 15
// points there, so mobile takes the median of three. e2e/lighthouse-summary.mjs warns below 90.
const performanceFloor = desktop ? 0.9 : 0.65;

module.exports = {
  ci: {
    collect: {
      startServerCommand: 'node e2e/serve.mjs',
      startServerReadyPattern: 'Serving',
      url: [
        '/',
        '/season/12',
        '/season/12?tab=schedule',
        '/season/12?tab=bracket',
        '/seasonmatch/31',
        '/profile/117',
        '/season/13/signupForm',
        '/login',
      ].map(path => base + path),
      numberOfRuns: desktop ? 1 : 3,
      settings: {
        ...(desktop ? { preset: 'desktop' } : {}),
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
