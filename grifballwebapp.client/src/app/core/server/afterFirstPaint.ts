// The app's scripts, fetched once a page rendered on the server has painted (used by src/server.ts).
//
// A page rendered on the server needs no script to show: its HTML and inline styles are the page. But
// the browser starts downloading every initial script (main and its chunks, some 300 kB compressed)
// as soon as it reads their tags, alongside the fonts the text waits for. On a slow phone connection
// they share the bandwidth, and the page's first paint (and its largest text, the LCP) waits on all
// of it. Here the module script tags and their modulepreload hints give way to one small inline
// script that imports them after the first frame is painted. Clicks before then are not lost: the
// server's event replay (withEventReplay) records them and replays them once the app has started.

const MODULE_SCRIPT = /<script src="([^"]+)" type="module"><\/script>/g;
const MODULE_PRELOAD = /<link rel="modulepreload" href="[^"]+">\s*/g;

/**
 * `text` as a string literal inside an inline <script>: JSON's quoting, with < and > and the two
 * line separators JavaScript reads as line ends escaped, so it can't close the script tag or the literal.
 */
export function scriptString(text: string): string {
  return JSON.stringify(text)
    .replaceAll('<', String.raw`\u003c`)
    .replaceAll('>', String.raw`\u003e`)
    .replaceAll('\u2028', String.raw`\u2028`)
    .replaceAll('\u2029', String.raw`\u2029`);
}

/**
 * The page with its module scripts loaded after its first paint: `html` without its modulepreload
 * hints, and its `<script src type="module">` tags replaced by one inline loader, in their order. A
 * page without any is returned as it is.
 */
export function loadScriptsAfterFirstPaint(html: string): string {
  const sources = [...html.matchAll(MODULE_SCRIPT)].map(match => match[1]);
  if (sources.length === 0)
    return html;
  // One after the other, as the tags ran: import(a).then(() => import(b))
  const [firstImport, ...rest] = sources.map(src => `import(new URL(${scriptString(src)}, document.baseURI).href)`);
  const imports = rest.reduce((chain, next) => `${chain}.then(() => ${next})`, firstImport);
  // After the next frame (requestAnimationFrame runs right before it is painted, the timeout once
  // it has been); a tab opened in the background, which paints no frames, starts after a second.
  const loader = `<script type="module">let started = false; const start = () => { if (!started) { started = true; ${imports}; } }; `
    + 'requestAnimationFrame(() => setTimeout(start)); setTimeout(start, 1000);</script>';
  let first = true;
  return html
    .replace(MODULE_PRELOAD, '')
    .replace(MODULE_SCRIPT, () => {
      const tag = first ? loader : '';
      first = false;
      return tag;
    });
}
