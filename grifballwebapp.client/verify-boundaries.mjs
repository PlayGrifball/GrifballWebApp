#!/usr/bin/env node
// Checks that the boundaries in eslint.config.mjs (docs/frontend.md) still catch what they're there for, so a
// change to the config, the plugin or its resolver can't turn them off without a failure: `npm run lint` runs it after
// ESLint. Each case lints one import as a real file of src/app would make it, and expects an error or none.
import { ESLint } from 'eslint';

// Any of the plugin's rules: boundaries/dependencies, or no-unknown-dependencies for an import that resolves to nothing
const PLUGIN = 'boundaries/';

// One case a line: allow or deny, the file importing (under src/app, or ../ for src), what it imports, and "lazy" for a
// lazy import() instead of an import statement. # starts a comment.
const CASES = `
# A feature: its own files, core, shared, the generated clients, packages
allow  features/league/season/team/team.component.ts  ../seasonTabs/seasonTabs.component
allow  features/home/home.component.ts  @shared/pageMeta
allow  features/home/home.component.ts  ../../shared/pageMeta
allow  features/home/home.component.ts  @core/auth/account.service
allow  features/home/home.component.ts  @api/models
allow  features/home/home.component.ts  @angular/core
# ...another feature only through its index.ts
allow  features/home/home.component.ts  ../lateLeague
deny   features/home/home.component.ts  ../lateLeague/lateLeagueQueue/lateLeagueQueue.component
deny   features/home/home.component.ts  ../lateLeague/lateLeagueQueue/lateLeagueQueue.component  lazy
deny   features/draft/teamBuilder/teamBuilder.component.ts  ../../league/season/seasonTabs/seasonTabs.component
# ...and never the app's root files, nor a file that isn't there
deny   features/home/home.component.ts  ../../app.config
deny   features/home/home.component.ts  ../nothing/here
# Core: core, shared, the generated clients; no feature, not even its index.ts, nor the root
allow  core/auth/account.service.ts  @shared/notifier.service
allow  core/auth/account.service.ts  ../http/redactUrl
allow  core/auth/account.service.ts  @api/models
deny   core/auth/account.service.ts  ../../features/lateLeague
deny   core/auth/account.service.ts  ../../app.config
# Shared: shared and the generated clients; not core or a feature
allow  shared/pageMeta.ts  ./apiDates
allow  shared/pageMeta.ts  @api/models
deny   shared/pageMeta.ts  @core/auth/account.service
deny   shared/pageMeta.ts  ../features/league
deny   shared/pageMeta.ts  ../features/league/season/seasonTabs/seasonTabs.component
# The root (routes, config, main, server): anything
allow  app.routes.ts  ./features/lateLeague/lateLeagueQueue/lateLeagueQueue.component
allow  app.routes.ts  ./features/league/season/season.component  lazy
allow  app.config.ts  ./core/auth/auth.interceptor
allow  ../main.ts  ./app/app.config
allow  ../server.ts  ./app/core/server/afterFirstPaint
`;
const cases = CASES.split('\n')
  .map(line => line.trim())
  .filter(line => line && !line.startsWith('#'))
  .map(line => {
    const [verdict, file, from, lazy] = line.split(/\s+/);
    if (!['allow', 'deny'].includes(verdict) || !from || ![undefined, 'lazy'].includes(lazy))
      throw new Error(`not a case: ${line}`);
    const code = lazy === 'lazy' ? `const x = () => import('${from}');` : `import { X } from '${from}';`;
    return [`src/app/${file}`.replace('app/../', ''), code, verdict === 'allow'];
  });

// Only the plugin's rules, on the import alone: the rest (sonarjs's type-aware ones) aren't what's checked here, and can
// fail on a file's text swapped for one line
const eslint = new ESLint({
  cwd: import.meta.dirname,
  ruleFilter: ({ ruleId }) => ruleId.startsWith(PLUGIN),
  overrideConfig: { files: ['src/**/*.ts'], languageOptions: { parserOptions: { project: null } } },
});
let failures = 0;
for (const [file, code, allowed] of cases) {
  const [result] = await eslint.lintText(code, { filePath: file });
  const errors = result.messages.filter(m => m.ruleId?.startsWith(PLUGIN));
  const fatal = result.messages.filter(m => m.fatal);
  const ok = fatal.length === 0 && (allowed ? errors.length === 0 : errors.length > 0);
  if (!ok) {
    failures++;
    const expected = allowed ? 'no error' : 'an error from the boundaries plugin';
    console.error(`FAIL ${file}: ${code}\n  expected ${expected}, got:`);
    for (const m of [...fatal, ...errors])
      console.error(`  ${m.ruleId ?? 'fatal'}: ${m.message}`);
    if (fatal.length === 0 && errors.length === 0)
      console.error('  none');
  }
}
console.log(`${cases.length - failures}/${cases.length} boundary cases as expected`);
process.exitCode = failures ? 1 : 0;
