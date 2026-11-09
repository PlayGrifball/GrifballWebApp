// ESLint: SonarCloud's JavaScript and TypeScript rules (eslint-plugin-sonarjs, its recommended set), so `npm run lint`
// shows what SonarCloud would before a push. Warnings only, the existing issues included: paydown PRs fix them, and the
// SonarCloud workflow fails a PR that adds one (.github/scripts/sonar-pr-issues.sh).
import sonarjs from 'eslint-plugin-sonarjs';
import tseslint from 'typescript-eslint';

const recommended = sonarjs.configs.recommended;
// Each rule the set turns on, as a warning (its options kept); the rest stay off
const level = (setting) => (Array.isArray(setting) ? setting[0] : setting);
const asWarnings = Object.fromEntries(
  Object.entries(recommended.rules)
    .filter(([, setting]) => level(setting) !== 'off' && level(setting) !== 0)
    .map(([rule, setting]) => [rule, Array.isArray(setting) ? ['warn', ...setting.slice(1)] : 'warn']),
);

export default [
  { ignores: ['dist/', 'coverage/', 'test-results/', 'playwright-report/', '.angular/', 'out-tsc/'] },
  {
    files: ['**/*.{ts,mts,cts,js,mjs,cjs}'],
    plugins: recommended.plugins,
    languageOptions: { parser: tseslint.parser },
    linterOptions: { reportUnusedDisableDirectives: 'warn' },
    rules: asWarnings,
  },
  // The app's code with its types: the rules that need them run only here
  {
    files: ['src/**/*.ts'],
    languageOptions: {
      parserOptions: {
        // tsconfig.json last: it takes in a file neither of the others reaches (one nothing imports yet)
        project: ['./tsconfig.app.json', './tsconfig.spec.json', './tsconfig.json'],
        tsconfigRootDir: import.meta.dirname,
      },
    },
  },
];
