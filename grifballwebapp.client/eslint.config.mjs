// ESLint: SonarCloud's JavaScript and TypeScript rules (eslint-plugin-sonarjs, its recommended set), so `npm run lint`
// shows what SonarCloud would before a push. Errors: the Lint job (frontend-tests.yml) fails a PR on any. A deliberate
// case is disabled where it is, with the reason: // eslint-disable-next-line sonarjs/<rule> -- <why>.
import boundaries from 'eslint-plugin-boundaries';
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
  { ignores: ['dist/', 'coverage/', 'test-results/', 'playwright-report/', '.angular/', 'out-tsc/', 'src/generated/'] },
  {
    files: ['**/*.{ts,mts,cts,js,mjs,cjs}'],
    plugins: recommended.plugins,
    languageOptions: { parser: tseslint.parser },
    linterOptions: { reportUnusedDisableDirectives: 'error' },
    rules: asErrors,
  },
  // No `any` in the app, its specs or its end-to-end tests (#387): the API's types are generated, and what isn't known
  // yet is `unknown`, narrowed where it is read
  {
    files: ['**/*.{ts,mts,cts}'],
    plugins: { '@typescript-eslint': tseslint.plugin },
    rules: { '@typescript-eslint/no-explicit-any': 'error' },
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
  // What may import what in src/app (docs/frontend.md): a feature uses its own files, core, shared and the generated
  // clients, and another feature only through that feature's index.ts; core uses core and shared; shared uses only
  // shared; the root files (routes, config) put it all together. verify-boundaries.mjs checks this still catches each.
  {
    files: ['src/**/*.ts'],
    plugins: { boundaries },
    settings: {
      'boundaries/elements': [
        { type: 'feature', pattern: 'src/app/features/*', capture: ['name'], partialMatch: false },
        { type: 'core', pattern: 'src/app/core', partialMatch: false },
        { type: 'shared', pattern: 'src/app/shared', partialMatch: false },
        { type: 'generated', pattern: 'src/generated', partialMatch: false },
        // The rest: the root files of src/app (routes, config) and src (main, server)
        { type: 'root', pattern: 'src', partialMatch: false },
      ],
      // @core, @shared, @api and @signalr resolve through tsconfig.json's paths
      'import/resolver': { typescript: { project: './tsconfig.json' } },
    },
    rules: {
      // Every file in an element and every import of one resolved (else a typo'd path or a broken resolver passes)
      'boundaries/no-unknown-files': 'error',
      'boundaries/no-unknown-dependencies': 'error',
      'boundaries/dependencies': ['error', {
        default: 'disallow',
        policies: [
          { allow: { to: { module: { origin: ['external', 'core'] } } } },
          { allow: { to: { element: { type: 'generated' } } } },
          { from: { element: { type: 'root' } }, allow: { to: { element: { type: '*' } } } },
          { from: { element: { type: 'feature' } }, allow: { to: { element: { types: ['core', 'shared'] } } } },
          {
            from: { element: { type: 'feature' } },
            allow: { to: { element: { type: 'feature', captured: { name: '{{ from.element.captured.name }}' } } } },
          },
          {
            from: { element: { type: 'feature' } },
            allow: { to: { element: { type: 'feature', fileInternalPath: 'index.ts' } } },
          },
          { from: { element: { type: 'core' } }, allow: { to: { element: { types: ['core', 'shared'] } } } },
          { from: { element: { type: 'shared' } }, allow: { to: { element: { type: 'shared' } } } },
        ],
      }],
    },
  },
];
