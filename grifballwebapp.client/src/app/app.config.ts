import { provideHttpClient, withInterceptors, withXhr } from '@angular/common/http';
import { ApplicationConfig, importProvidersFrom } from '@angular/core';
import { provideClientHydration, withEventReplay, withHttpTransferCacheOptions } from '@angular/platform-browser';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter, withComponentInputBinding } from '@angular/router';
//import { PreloadAllModules, withDebugTracing, withPreloading } from '@angular/router';
import { JwtModule } from '@auth0/angular-jwt';
import { APP_ROUTES } from './app.routes';
import { authInterceptor } from './auth.interceptor';
import { earlyFetchInterceptor } from './earlyFetch.interceptor';
import { provideSnackBarDefaults } from './shared/snackBarDefaults';

/** The app's providers, in the browser and (with app.config.server.ts on top) on the server. */
export const appConfig: ApplicationConfig = {
  providers: [
    provideSnackBarDefaults(),
    importProvidersFrom(
      JwtModule.forRoot({
        config: {
          tokenGetter: () => localStorage.getItem("access_token"),
          //allowedDomains: ["example.com"],
          //disallowedRoutes: ["http://example.com/examplebadroute/"],
        },
      }),
    ),
    provideAnimationsAsync(),
    provideHttpClient(withXhr(), withInterceptors([earlyFetchInterceptor, authInterceptor])),
    provideRouter(APP_ROUTES
      , withComponentInputBinding()
      //,withPreloading(PreloadAllModules)
      //,withDebugTracing()
    ),
    // Takes over the page the server rendered rather than drawing it again, reuses the API answers
    // the server fetched for it (HttpClient's transfer cache), and replays clicks made before then.
    // A signed-in user's requests carry their token, but take the server's answers too: the server
    // only renders public pages, as a visitor, and their GET endpoints answer everyone the same.
    provideClientHydration(withEventReplay(), withHttpTransferCacheOptions({ includeRequestsWithAuthHeaders: true })),
  ],
};
