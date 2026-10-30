import { provideHttpClient, withInterceptors, withXhr } from '@angular/common/http';
import { ApplicationConfig } from '@angular/core';
import { provideClientHydration, withEventReplay } from '@angular/platform-browser';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter, withComponentInputBinding } from '@angular/router';
//import { PreloadAllModules, withDebugTracing, withPreloading } from '@angular/router';
import { APP_ROUTES } from './app.routes';
import { authInterceptor } from './auth.interceptor';
import { earlyFetchInterceptor } from './earlyFetch.interceptor';
import { provideSnackBarDefaults } from './shared/snackBarDefaults';

/** The app's providers, in the browser and (with app.config.server.ts on top) on the server. */
export const appConfig: ApplicationConfig = {
  providers: [
    provideSnackBarDefaults(),
    provideAnimationsAsync(),
    provideHttpClient(withXhr(), withInterceptors([earlyFetchInterceptor, authInterceptor])),
    provideRouter(APP_ROUTES
      , withComponentInputBinding()
      //,withPreloading(PreloadAllModules)
      //,withDebugTracing()
    ),
    // Takes over the page the server rendered rather than drawing it again, reuses the API answers
    // the server fetched for it (HttpClient's transfer cache), and replays clicks made before then.
    // The server fetched them with the visitor's own cookie, so they are that user's answers.
    provideClientHydration(withEventReplay()),
  ],
};
