import { Component, PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { LiveService } from './live.service';
import { provideLive } from './provideLive';

@Component({ template: '' })
class BlankPage {}

/** Lets the initializer's import of LiveService resolve, however long it takes, and what it does then run. */
async function settled(): Promise<void> {
  await import('./live.service');
  for (let i = 0; i < 5; i++)
    await new Promise(resolve => setTimeout(resolve));
}

/** Waits for `done`, up to two seconds: the initializer's import is a chunk of its own, slower on a busy machine. */
async function until(done: () => boolean): Promise<void> {
  for (let i = 0; i < 200 && !done(); i++)
    await new Promise(resolve => setTimeout(resolve, 10));
}

describe('provideLive', () => {
  /**
   * An app with provideLive on `platform`, and its LiveService with `viewing` and `connect` spied on. Injecting the
   * service starts the app's injector, and with it the initializer, whose import of the service resolves later: the
   * spies are in place by then.
   */
  function start(platform: 'browser' | 'server'): jasmine.SpyObj<LiveService> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'seasons', component: BlankPage }, { path: 'old-seasons', redirectTo: 'seasons' }]),
        provideLive(),
        { provide: PLATFORM_ID, useValue: platform },
      ],
    });
    const live = TestBed.inject(LiveService);
    spyOn(live, 'viewing');
    spyOn(live, 'connect').and.resolveTo();
    return live as jasmine.SpyObj<LiveService>;
  }

  it('in the browser, says the page the tab shows, then each page it goes to, and connects', async () => {
    const live = start('browser');
    await until(() => live.connect.calls.count() > 0);

    expect(live.viewing.calls.allArgs()).toEqual([['/']]);
    expect(live.connect).toHaveBeenCalledTimes(1);

    await TestBed.inject(Router).navigateByUrl('/old-seasons?page=2');
    // The page it ends on, after redirects (LiveService drops the query)
    expect(live.viewing.calls.mostRecent().args).toEqual(['/seasons?page=2']);
    expect(live.connect).toHaveBeenCalledTimes(1);
  });

  it('does nothing on the server', async () => {
    const live = start('server');
    await settled();

    expect(live.viewing).not.toHaveBeenCalled();
    expect(live.connect).not.toHaveBeenCalled();
  });

  it('does nothing when the app is gone before LiveService has loaded', async () => {
    const live = start('browser');
    TestBed.resetTestingModule();
    await settled();

    expect(live.viewing).not.toHaveBeenCalled();
    expect(live.connect).not.toHaveBeenCalled();
  });
});
