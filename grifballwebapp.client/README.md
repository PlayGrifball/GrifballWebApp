# grifballwebapp.client

The Angular frontend of GrifballWebApp.

- Setting up and running it: [docs/development-setup.md](../docs/development-setup.md)
- How it is organised (features, `core`, `shared`) and the import rules lint enforces: [docs/frontend.md](../docs/frontend.md)
- Tests and coverage: [docs/frontend-testing.md](../docs/frontend-testing.md), [COVERAGE_README.md](COVERAGE_README.md)

The common scripts: `npm start` (dev server on https://localhost:4200), `npm test`, `npm run test:ci`, `npm run lint`,
`npm run generate:api` (the API client in `src/generated/api`, from `openapi.json`) and `npm run smoke` (the
Playwright smoke tests the PR previews run).
