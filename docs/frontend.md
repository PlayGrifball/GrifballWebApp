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

## Components and stores

A component that talks to the server does it through its store: an `@Injectable()` class next to it
(`<name>.store.ts`) that the component lists in its `providers`, so each instance of the component has its own, made
and destroyed with it. The store holds what came from the server and what is in flight (signals, read-only from
outside), and makes the calls: loads (`rxResource`, or a `Subject` with `switchMap` when each request should cancel the
last), saves, polling, and the snackbar or error text when one fails. The component is the view: its inputs, outputs and
form fields, what the page shows computed from the store's signals, and methods that hand the user's actions to the
store. `profileCommendations` is the smallest example, `xboxAccount` one with paging and polling.

- **The component keeps the store's signals under its own names** (`readonly rows = this.store.rows;`), so its template
  reads `rows()` either way.
- **Inputs reach the store as a `StoreInput`** (`@shared/storeInput`). The store declares
  `readonly userID = new StoreInput<number>();` and reads `this.userID.value()` (undefined until the component hands
  its input over, so a resource keyed on it stays idle), and the component hands it over once, in its constructor:
  `this.store.userID.use(this.userID);`. The store then follows the input, with no effect copying it across.
- **What the component does after a call** (clear the form, emit an output) it passes as a callback:
  `this.store.create(name, days, () => this.name.set(''));`.
- **A form filled from the server** is a `linkedSignal` of the store's copy: `readonly model = linkedSignal(() =>
  this.store.season() ?? newSeason());`. The store keeps what the server sent; edits change only the form, which starts
  over whenever the store's copy is replaced. So a store never replaces that copy while the user may be editing it:
  not to refresh it after a save. What a save answers (a new ID, say) goes into the form through the save's callback.
- **Drawing with a library that doesn't read signals** (the playoff bracket's viewer) stays in the component: one
  effect on the store's data draws it, or clears it when there is none. The store doesn't touch the page.
- **Each store has its own spec** (`<name>.store.spec.ts`), which injects it with `HttpTestingController` and tests
  its calls, answers and failures without a template. The component's spec stays as it was: rendering and the user's
  actions, through the real store. A resource's value comes a microtask after the answer, so a spec waits for
  `ApplicationRef.whenStable()` (or the fixture's) before reading it; it waits only after answering, since a resource
  still loading keeps the app unstable.
