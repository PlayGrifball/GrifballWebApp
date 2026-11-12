// ESLint: SonarCloud's JavaScript and TypeScript rules (eslint-plugin-sonarjs, its recommended set), so `npm run lint`
// shows what SonarCloud would before a push. Errors: the Lint job (frontend-tests.yml) fails a PR on any. A deliberate
// case is disabled where it is, with the reason: // eslint-disable-next-line sonarjs/<rule> -- <why>.
import sonarjs from 'eslint-plugin-sonarjs';
import tseslint from 'typescript-eslint';

const recommended = sonarjs.configs.recommended;
// Each rule the set turns on, as an error (its options kept); the rest stay off
const level = (setting) => (Array.isArray(setting) ? setting[0] : setting);
const asErrors = Object.fromEntries(
  Object.entries(recommended.rules)
    .filter(([, setting]) => level(setting) !== 'off' && level(setting) !== 0)
    .map(([rule, setting]) => [rule, Array.isArray(setting) ? ['error', ...setting.slice(1)] : 'error']),
);

export default [
  { ignores: ['dist/', 'coverage/', 'test-results/', 'playwright-report/', '.angular/', 'out-tsc/'] },
  {
    files: ['**/*.{ts,mts,cts,js,mjs,cjs}'],
    plugins: recommended.plugins,
    languageOptions: { parser: tseslint.parser },
    linterOptions: { reportUnusedDisableDirectives: 'error' },
    rules: asErrors,
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
