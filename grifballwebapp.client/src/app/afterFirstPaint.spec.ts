import { loadScriptsAfterFirstPaint, scriptString } from './afterFirstPaint';

describe('loadScriptsAfterFirstPaint', () => {
  const page = (head: string, body: string) => `<html><head>${head}</head><body><app-root></app-root>${body}</body></html>`;

  it('drops the modulepreload hints and loads the module scripts after the first paint, in order', () => {
    const html = page(
      '<link rel="modulepreload" href="chunk-A.js">\n<link rel="modulepreload" href="chunk-B.js">',
      '<script src="polyfills-P.js" type="module"></script><script src="main-M.js" type="module"></script>',
    );
    const out = loadScriptsAfterFirstPaint(html);
    expect(out).not.toContain('modulepreload');
    expect(out).not.toContain('<script src=');
    expect(out.match(/<script type="module">/g)!.length).toBe(1);
    const loader = out.slice(out.indexOf('<script type="module">'));
    expect(loader.indexOf('"polyfills-P.js"')).toBeLessThan(loader.indexOf('"main-M.js"'));
    expect(loader).toContain('requestAnimationFrame(() => setTimeout(start))');
    expect(loader).toContain('setTimeout(start, 1000)');
  });

  it('leaves a page without module scripts as it is', () => {
    const html = page('<link rel="modulepreload" href="chunk-A.js">', '<script>window.x = 1;</script>');
    expect(loadScriptsAfterFirstPaint(html)).toBe(html);
  });

  it('imports each script once, however many times the loader is started', async () => {
    const html = page('', '<script src="main-M.js" type="module"></script>');
    const loader = loadScriptsAfterFirstPaint(html).match(/<script type="module">(.*)<\/script>/)![1];
    const imported: string[] = [];
    const run = new Function('requestAnimationFrame', 'setTimeout', 'document', 'URL', 'importer',
      loader.replaceAll('import(', 'importer('));
    const timers: (() => void)[] = [];
    run((f: () => void) => timers.push(f), (f: () => void) => timers.push(f), { baseURI: 'https://grifball.xyz/' }, URL,
      (url: string) => { imported.push(url); return Promise.resolve(); });
    while (timers.length)
      timers.shift()!();
    expect(imported).toEqual(['https://grifball.xyz/main-M.js']);
  });
});

describe('scriptString', () => {
  it('is the JSON string of a script path', () => {
    expect(scriptString('chunk-AB12.js')).toBe('"chunk-AB12.js"');
  });

  it('cannot end the inline script or the string it is in', () => {
    const quoted = scriptString('a</script><script>alert(1)// "');
    expect(quoted).not.toContain('<');
    expect(quoted).not.toContain('>');
    expect(quoted).not.toContain(' ');
    // Still the same string to JavaScript
    expect(JSON.parse(quoted)).toBe('a</script><script>alert(1)// "');
  });
});
