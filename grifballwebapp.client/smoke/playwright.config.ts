import { defineConfig, devices } from '@playwright/test';
import { smokeBaseUrl, smokeRunId } from './env';

// Post-deploy smoke tests: the real app, against a PR's live preview environment
// (https://grif-preview<N>.grifball.xyz), nothing mocked. Separate from the mocked e2e suite
// (../playwright.config.ts, e2e/), which CI runs on every PR without a deployment.
//
//   SMOKE_BASE_URL=https://grif-preview1.grifball.xyz SMOKE_PASSWORD=... npm run smoke
//
// SMOKE_PASSWORD is the preview's Preview:SmokeUserPassword. Optional: SMOKE_USER / SMOKE_USER_2
// (default smoke-test / smoke-test-2), SMOKE_PASSWORD_2, SMOKE_RUN_ID (names the data: smoke-<id>-...).
// What a run creates and leaves behind: league.ts.
const baseURL = smokeBaseUrl(); // Refuses anything but a preview (or localhost) host.
smokeRunId(); // Fixed here, before workers start, so every worker names its data the same.

export default defineConfig({
  testDir: '.',
  testMatch: '*.spec.ts',
  outputDir: './results',
  // Each spec file builds its own season and drives it step by step: files may run side by side,
  // the tests inside one never do.
  fullyParallel: false,
  workers: process.env.CI ? 2 : 1,
  forbidOnly: !!process.env.CI,
  // A live environment over the internet: one retry for a network hiccup. A retry makes its
  // own season, so it never sees the first attempt's half-done data.
  retries: process.env.CI ? 1 : 0,
  timeout: 120_000,
  expect: { timeout: 15_000 },
  reporter: process.env.CI
    ? [['list'], ['html', { open: 'never', outputFolder: 'report' }], ['json', { outputFile: 'results/results.json' }]]
    : [['list'], ['html', { open: 'never', outputFolder: 'report' }]],
  use: {
    baseURL,
    // The availability table and date pickers show local times; pin them.
    timezoneId: 'UTC',
    locale: 'en-US',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
  },
  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        // Wide enough that the team builder's cards and pool sit side by side, as on a desktop.
        viewport: { width: 1600, height: 1000 },
        launchOptions: process.env.PW_CHROMIUM_PATH ? { executablePath: process.env.PW_CHROMIUM_PATH } : {},
      },
    },
  ],
});
