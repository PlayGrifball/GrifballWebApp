import { expect, test } from '@playwright/test';
import { settled } from './testPages.mts';

// settled() (testPages.mts) on pages made for each case with page.setContent: no app, no mock API.
test.describe('settled()', () => {
  test('waits for a running animation to finish, and not for an endless one', async ({ page }) => {
    await page.setContent(`<style>
      @keyframes fade { from { opacity: 0; } }
      @keyframes spin { to { transform: rotate(1turn); } }
      .fade { animation: fade 400ms; }
      .spin { animation: spin 1s infinite; }
    </style><div class="spin">spinner</div><div id="box" class="fade">box</div>`);
    await settled(page);
    // A finished animation leaves getAnimations(): returned early, the fade would still be there
    expect(await page.evaluate(() => document.getElementById('box')!.getAnimations().length)).toBe(0);
  });

  test('fails past its limit, naming what still animates and on what, an SVG\'s classes included', async ({ page }) => {
    await page.setContent(`<style>
      @keyframes grow { to { r: 20px; } }
      .ring { animation: grow 60s; }
    </style><svg width="50" height="50"><circle class="ring hud" cx="25" cy="25" r="5" /></svg>`);
    await expect(settled(page, { animationsMs: 300 })).rejects.toThrow('The page did not settle in 300 ms: still animating grow on circle.ring.hud');
  });

  test('gives any other error as it is: a page closed while it waits is not "did not settle"', async ({ page }) => {
    await page.setContent(`<style>
      @keyframes fade { from { opacity: 0; } }
      div { animation: fade 60s; }
    </style><div>box</div>`);
    const waiting = settled(page, { animationsMs: 30_000 }).catch((error: Error) => error);
    await page.waitForTimeout(200); // eslint-disable-line sonarjs/no-fixed-wait-in-tests -- settled() has to be waiting before the page goes
    await page.close();
    const error = await waiting;
    expect(error).toBeInstanceOf(Error);
    expect((error as Error).message).not.toContain('did not settle');
  });

  test('waits for the header to stop moving: the frames after its last change, not before', async ({ page }) => {
    // The header's link changes width on each of its first 30 frames, as the toolbar measuring itself again
    await page.setContent(`<header class="hud-header"><a id="link" style="display: inline-block; width: 10px;">x</a></header>
      <script>
        window.moves = 0;
        const move = () => {
          document.getElementById('link').style.width = (10 + ++window.moves) + 'px';
          if (window.moves < 30) requestAnimationFrame(move);
        };
        requestAnimationFrame(move);
      </script>`);
    await settled(page);
    expect(await page.evaluate(() => (window as unknown as { moves: number }).moves)).toBe(30);
  });

  test('fails when the header keeps moving, saying so', async ({ page }) => {
    await page.setContent(`<header class="hud-header"><a id="link" style="display: inline-block; width: 10px;">x</a></header>
      <script>
        let width = 10;
        const move = () => {
          document.getElementById('link').style.width = (10 + (++width % 20)) + 'px';
          requestAnimationFrame(move);
        };
        requestAnimationFrame(move);
      </script>`);
    await expect(settled(page, { layoutFrames: 30 })).rejects.toThrow('The header was still moving after 30 frames');
  });
});
