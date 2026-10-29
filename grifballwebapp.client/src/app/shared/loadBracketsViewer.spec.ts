import { loadBracketsViewer } from './loadBracketsViewer';

describe('loadBracketsViewer', () => {
  let original: unknown;
  const added = () => Array.from(document.head.querySelectorAll('script[src="brackets-viewer.js"], link[href="brackets-viewer.css"]'));

  beforeEach(() => {
    original = (window as any).bracketsViewer;
    delete (window as any).bracketsViewer;
    added().forEach(el => el.remove());
  });

  afterEach(() => {
    (window as any).bracketsViewer = original;
    added().forEach(el => el.remove());
  });

  it('adds nothing when the viewer is already loaded', async () => {
    (window as any).bracketsViewer = {};
    await loadBracketsViewer();
    expect(added()).toEqual([]);
  });

  it('adds the script and stylesheet once, and resolves when the script has loaded', async () => {
    const first = loadBracketsViewer();
    const second = loadBracketsViewer();
    expect(second).toBe(first);
    const script = document.head.querySelector<HTMLScriptElement>('script[src="brackets-viewer.js"]')!;
    expect(document.head.querySelectorAll('link[href="brackets-viewer.css"]').length).toBe(1);

    script.onload!(new Event('load'));
    await expectAsync(first).toBeResolved();
  });

  it('rejects when the script cannot be loaded, and tries again next time', async () => {
    const failed = loadBracketsViewer();
    document.head.querySelector<HTMLScriptElement>('script[src="brackets-viewer.js"]')!.onerror!(new Event('error'));
    await expectAsync(failed).toBeRejected();
    expect(added()).toEqual([]);

    const retry = loadBracketsViewer();
    expect(retry).not.toBe(failed);
    expect(added().length).toBe(2);
    document.head.querySelector<HTMLScriptElement>('script[src="brackets-viewer.js"]')!.onload!(new Event('load'));
    await retry;
  });
});
