#!/usr/bin/env node
// Checks that the boundaries in eslint.config.mjs (docs/frontend.md) still catch what they're there for, so a
// change to the config, the plugin or its resolver can't turn them off without a failure: `npm run lint` runs it after
// ESLint. Each case lints one import as a real file of src/app would make it, and expects an error or none.
import { ESLint } from 'eslint';

// Any of the plugin's rules: boundaries/dependencies, or no-unknown-dependencies for an import that resolves to nothing
const PLUGIN = 'boundaries/';

// [the file importing, the import, whether the boundaries allow it]
const cases = [
  // A feature: its own files, core, shared, the generated clients, packages
  ['src/app/features/league/season/team/team.component.ts', "import { X } from '../seasonTabs/seasonTabs.component';", true],
  ['src/app/features/home/home.component.ts', "import { X } from '@shared/pageMeta';", true],
  ['src/app/features/home/home.component.ts', "import { X } from '../../shared/pageMeta';", true],
  ['src/app/features/home/home.component.ts', "import { X } from '@core/auth/account.service';", true],
  ['src/app/features/home/home.component.ts', "import { X } from '@api/models';", true],
  ['src/app/features/home/home.component.ts', "import { X } from '@angular/core';", true],
  // ...another feature only through its index.ts
  ['src/app/features/home/home.component.ts', "import { X } from '../lateLeague';", true],
  ['src/app/features/home/home.component.ts', "import { X } from '../lateLeague/lateLeagueQueue/lateLeagueQueue.component';", false],
  ['src/app/features/home/home.component.ts', "const x = () => import('../lateLeague/lateLeagueQueue/lateLeagueQueue.component');", false],
  ['src/app/features/draft/teamBuilder/teamBuilder.component.ts', "import { X } from '../../league/season/seasonTabs/seasonTabs.component';", false],
  // ...and never the app's root files, nor a file that isn't there
  ['src/app/features/home/home.component.ts', "import { X } from '../../app.config';", false],
  ['src/app/features/home/home.component.ts', "import { X } from '../nothing/here';", false],
  // Core: core, shared, the generated clients; no feature, not even its index.ts, nor the root
  ['src/app/core/auth/account.service.ts', "import { X } from '@shared/notifier.service';", true],
  ['src/app/core/auth/account.service.ts', "import { X } from '../http/redactUrl';", true],
  ['src/app/core/auth/account.service.ts', "import { X } from '@api/models';", true],
  ['src/app/core/auth/account.service.ts', "import { X } from '../../features/lateLeague';", false],
  ['src/app/core/auth/account.service.ts', "import { X } from '../../app.config';", false],
  // Shared: shared and the generated clients; not core or a feature
  ['src/app/shared/pageMeta.ts', "import { X } from './apiDates';", true],
  ['src/app/shared/pageMeta.ts', "import { X } from '@api/models';", true],
  ['src/app/shared/pageMeta.ts', "import { X } from '@core/auth/account.service';", false],
  ['src/app/shared/pageMeta.ts', "import { X } from '../features/league';", false],
  ['src/app/shared/pageMeta.ts', "import { X } from '../features/league/season/seasonTabs/seasonTabs.component';", false],
  // The root (routes, config): anything
  ['src/app/app.routes.ts', "import { X } from './features/lateLeague/lateLeagueQueue/lateLeagueQueue.component';", true],
  ['src/app/app.routes.ts', "const x = () => import('./features/league/season/season.component');", true],
  ['src/app/app.config.ts', "import { X } from './core/auth/auth.interceptor';", true],
  ['src/main.ts', "import { X } from './app/app.config';", true],
  ['src/server.ts', "import { X } from './app/core/server/afterFirstPaint';", true],
];

const eslint = new ESLint({ cwd: import.meta.dirname });
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
