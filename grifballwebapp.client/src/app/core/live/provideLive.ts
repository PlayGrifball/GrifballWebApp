import { isPlatformBrowser } from '@angular/common';
import { DestroyRef, EnvironmentInjector, EnvironmentProviders, PLATFORM_ID, inject, makeEnvironmentProviders, provideEnvironmentInitializer } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';

/**
 * The live connection, in the browser: started with the app, and told each page the tab shows (who is on the site).
 * LiveService and the SignalR client come in their own chunks once the app has started, so the first page's download
 * carries neither; the page the tab shows by then is told as it connects.
 */
export function provideLive(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideEnvironmentInitializer(() => {
      if (!isPlatformBrowser(inject(PLATFORM_ID)))
        return;
      const injector = inject(EnvironmentInjector);
      const router = inject(Router);
      const destroyRef = inject(DestroyRef);
      let gone = false;
      destroyRef.onDestroy(() => gone = true);
      void import('./live.service').then(({ LiveService }) => {
        if (gone)
          return;
        const live = injector.get(LiveService);
        live.viewing(router.url);
        router.events.pipe(filter(event => event instanceof NavigationEnd), takeUntilDestroyed(destroyRef))
          .subscribe(event => live.viewing(event.urlAfterRedirects));
        void live.connect();
      });
    }),
  ]);
}
