# Frontend structure

The Angular client (`grifballwebapp.client/src`) is split by feature area, and lint enforces what may import what:
the client's version of the backend's architecture tests.

## Layout

| Folder | What's in it | May import |
| --- | --- | --- |
| `app/features/<area>` | One area's pages, components, state and API access (`account`, `admin`, `draft`, `home`, `lateLeague`, `league`, `matches`, `players`) | its own files, `core`, `shared`, the generated clients, and another feature's `index.ts` |
| `app/core` | What the app runs on, once: auth, HTTP, SignalR, telemetry, layout, server rendering | `core`, `shared`, the generated clients |
| `app/shared` | Building blocks any area uses: tables, date helpers, page metadata, test helpers | `shared`, the generated clients |
| `generated` | The API and SignalR clients, generated from the server (`@api/*`, `@signalr/*`) | itself |
| `app/*.ts`, `*.ts` | The root: routes, app config, `main.ts`, `server.ts` | anything |

Folder and file names are camelCase. `core` and `shared` are imported through `@core/*` and `@shared/*`
(`tsconfig.json`).

## Using another feature

A feature imports another only through that feature's `index.ts`, which lists what the others may use, for example
`features/lateLeague/index.ts`:

```ts
export { LateLeagueQueueComponent } from './lateLeagueQueue/lateLeagueQueue.component';
```

and the home page imports `from '../lateLeague'`. Everything else in a feature is its own. When something is wanted by
several areas and belongs to none, it goes in `shared` instead.

## The check

`eslint-plugin-boundaries` runs as part of `npm run lint` (`eslint.config.mjs`), in the Lint job of every PR that
changes the client. It fails on:

- an import a row above doesn't allow (`boundaries/dependencies`);
- a file under `src` outside the layout (`boundaries/no-unknown-files`);
- a relative import that resolves to no file (`boundaries/no-unknown-dependencies`).

After ESLint, `npm run lint` runs `verify-boundaries.mjs`, which lints a set of known good and bad imports and fails if
any comes out the other way, so a change to the config, the plugin or its resolver can't switch the check off quietly.
A new rule above gets a case there.
