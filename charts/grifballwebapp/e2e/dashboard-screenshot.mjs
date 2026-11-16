// A screenshot of the chart's dashboard as Grafana draws it, once dashboard-grafana.mjs has passed (e2e/dashboard.sh
// runs it in Microsoft's Playwright image, which has the browser; CI uploads the picture):
//
//   node dashboard-screenshot.mjs <grafana url> <namespace> <out.png>
//
// Dark theme; the viewport is as tall as the whole dashboard, since Grafana only loads the panels it has on screen (its
// rows are all open: tests/dashboard.test.mjs). Not "networkidle": Grafana keeps a live connection open, so the network
// never goes quiet. It waits for a legend entry of "Requests by status" instead, which only renders once that query has
// returned data, then gives the other panels time to draw.
import { chromium } from 'playwright';

const [url, namespace, out] = process.argv.slice(2);
if (!out) throw new Error('usage: node dashboard-screenshot.mjs <grafana url> <namespace> <out.png>');

const browser = await chromium.launch();
try {
  const page = await browser.newPage({ viewport: { width: 1600, height: 6000 } });
  const query = new URLSearchParams({ orgId: '1', theme: 'dark', from: 'now-15m', to: 'now', 'var-namespace': namespace });
  await page.goto(`${url}/d/grifballwebapp?${query}`, { waitUntil: 'load' });
  // The panels' data was checked already (dashboard-grafana.mjs): a legend that never shows is worth a warning, and the
  // picture is still taken, to show why
  await page.waitForSelector('[data-testid="data-testid VizLegend series 200"]', { timeout: 60000 })
    .catch(error => console.log(`::warning::The dashboard's "Requests by status" legend didn't show: ${error.message}`));
  await page.waitForTimeout(15000);
  await page.screenshot({ path: out, fullPage: true });
} finally {
  await browser.close();
}
