/**
 * Loads the brackets-viewer library (window.bracketsViewer) and its stylesheet, built as their own
 * files (angular.json: bundleName "brackets-viewer", not injected) so only the bracket pays for them.
 * Loads once; resolves at once when the library is already there.
 */
let loading: Promise<void> | null = null;

export function loadBracketsViewer(): Promise<void> {
  if (window.bracketsViewer)
    return Promise.resolve();
  loading ??= new Promise<void>((resolve, reject) => {
    const style = document.createElement('link');
    style.rel = 'stylesheet';
    style.href = 'brackets-viewer.css';
    document.head.appendChild(style);

    const script = document.createElement('script');
    script.src = 'brackets-viewer.js';
    // From then on window.bracketsViewer is there; only an in-flight load is shared.
    script.onload = () => {
      loading = null;
      resolve();
    };
    script.onerror = () => {
      // Let a later bracket try again.
      loading = null;
      style.remove();
      script.remove();
      reject(new Error('Could not load the bracket viewer'));
    };
    document.head.appendChild(script);
  });
  return loading;
}
