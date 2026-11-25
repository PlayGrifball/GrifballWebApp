# Frontend structure

The Angular client (`grifballwebapp.client/src`) is split by feature area, and lint enforces what may import what:
the client's version of the backend's architecture tests.

## Layout

| Folder | What's in it | May import |
| --- | --- | --- |
| `app/features/<area>` | One area's pages, components, state and API access (`account`, `admin`, `chat`, `draft`, `home`, `lateLeague`, `league`, `matches`, `players`) | its own files, `core`, `shared`, the generated clients, and another feature's `index.ts` |
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

- **A load is a `resourceView`** (`@shared/resourceView`):
  `private readonly claims = resourceView(rxResource({ … }), [] as GamertagClaimDto[]);`. It gives the load's signals
  (`value` with the fallback until there is one, `hasValue`, `isLoading`, `failed`, `error`, `resolved`, `status`) and
  `reload()`, so the store hands them out as they are (`readonly rows = this.claims.value;`) instead of repeating
  `hasValue() ? value() : []` and `status() === 'error'`. Its `value` never throws (a resource's own `value()` does
  after an error). A read where "no such thing" is an answer pipes `nullWhenNotFound()` and treats `null` as none.
- **The component keeps the store's signals under its own names** (`readonly rows = this.store.rows;`), so its template
  reads `rows()` either way.
- **Inputs reach the store as a `StoreInput`** (`@shared/storeInput`). The store declares
  `readonly userID = new StoreInput<number>();` and reads `this.userID.value()` (undefined until the component hands
  its input over, so a resource keyed on it stays idle), and the component hands it over once, in its constructor:
  `this.store.userID.use(this.userID);`. The store then follows the input, with no effect copying it across. A field
  the user edits that decides what is loaded (a tab, a filter, an ID typed in) is handed over the same way, as its
  signal or a `computed` of several.
- **What the component does after a call** (clear the form, emit an output) it passes as a callback:
  `this.store.create(name, days, () => this.name.set(''));`.
- **A form filled from the server** is a `linkedSignal` of the store's copy: `readonly model = linkedSignal(() =>
  this.store.season() ?? newSeason());`. The store keeps what the server sent; edits change only the form, which starts
  over whenever the store's copy is replaced. So a store never replaces that copy while the user may be editing it:
  not to refresh it after a save. What a save answers (a new ID, say) goes into the form through the save's callback.
- **Drawing with a library that doesn't read signals** (the playoff bracket's viewer) stays in the component: one
  effect on the store's data draws it, or clears it when there is none. The store doesn't touch the page.
- **A template that reads several states of one load** (its value, error and loading, as the Telemetry page's tabs do)
  can read the store's resource itself: the store gives it as a read-only `Resource<T>` (no `reload()`), and reloads
  it through its own methods. Its template narrows with `@if (x.hasValue())` before `x.value()`, which a
  `resourceView`'s separate signals can't do; every other store uses `resourceView`.
- **Each store has its own spec** (`<name>.store.spec.ts`), which injects it with `HttpTestingController` and tests
  its calls, answers and failures without a template. The component's spec stays as it was: rendering and the user's
  actions, through the real store. A resource's value comes a microtask after the answer, so a spec waits for
  `ApplicationRef.whenStable()` (or the fixture's) before reading it; it waits only after answering, since a resource
  still loading keeps the app unstable.

## Loading states

The server renders every page with its data (it waits for the API before it answers), so a first visit shows no
loading at all. A page opened from a link in the browser draws at once and loads its data after: what it shows
meanwhile is what this section is about. Nothing may appear from nothing and push the page down (layout shift), and
no page hides itself until it is ready.

- **Each load shows a skeleton until its first answer**: `app-skeleton` (`@shared/skeleton`), in the load's own
  `@if`/`@else`, in the place and about the size of what it stands for: `shape` `lines` (text), `table` (rows under a
  header; `[header]="false"` under a real table's), `cards` (a grid, `cardWidth` the grid's own minimum) or `block`
  (one area: a chart, a bracket, a form); `count`, `columns` and `height` to match.
  `<app-skeleton label="standings" shape="table" [header]="false" [count]="8" [columns]="4" />`.
  Screen readers hear "Loading standings…"; the shapes are hidden from them.
- **Only the first answer.** A reload (a live change, a retry) keeps the last answer shown. A `resourceView` says
  which with `firstLoad` (its first answer for these parameters is on its way); a store without one keeps a `loaded`
  signal that a failure sets too, so a failed load never leaves a skeleton up.
- **Not for something usually absent.** A part that shows only sometimes (the stat flags notice, a season's VODs, the
  chat's model picker) gets no skeleton: holding space for it would make the page jump when it doesn't come.
- **Text that isn't known yet is a non-breaking space, not a guess**: a heading waits with `&nbsp;` (its line kept),
  never "Unknown Spartan" for a name still loading.
- **`@defer` is for code, not data.** It delays loading a component's code (a heavy one below the fold); it doesn't
  wait for an API. On the server a `@defer` block without a hydrate trigger renders only its `@placeholder`, so one
  around a page's content would take that content out of the server's HTML (search engines, the first paint). A
  `@defer` therefore has a `hydrate on …` trigger whenever its content matters to the first render (the server then
  renders the content and the browser loads its code when the trigger fires), and a `@placeholder` of the content's
  size for navigations in the browser: an `app-skeleton`, or an empty box of the right height (who's online, in the
  footer).
- **Specs** check the skeleton is there before the answer and gone after it (`querySelector('app-skeleton')`).

## Logging

Log through `AppLog` (`@core/telemetry/appLog`), not `console`: `this.log.error('Failed to save the season', error)`.
Every line goes to the console as before (that's how the server render's logs reach the pod's logs). Lines at `info`,
`warn` and `error` also go to telemetry, in the trace of what the user is doing, with the cause's message and stack
redacted as the error reports redact them. Each line goes once a page, and at most `MAX_LOGS` of each level, so info lines can't crowd out an error. `debug` stays in the
console. A failed API call (an `HttpErrorResponse` cause) stays in the console too: `clientErrorsInterceptor` already
reports the ones worth reporting (no answer, or a 5xx) in the call's own trace, and a 4xx is the app working. The
message says what failed in words someone reading the logs understands; the cause goes second, not into the message.
Don't log and then throw: the thrown error is reported. Only `main.ts` uses `console` itself: a failed bootstrap has
no injector to ask. Lint (`no-console`) holds the rest of `src` to it. `shared` can't import `core`, so a shared component that has something to report hands it to its
caller.
