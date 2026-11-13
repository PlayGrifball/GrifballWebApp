import { provideHttpClient, withInterceptors, withXhr } from '@angular/common/http';
import { ApplicationConfig, ErrorHandler, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideClientHydration, withEventReplay } from '@angular/platform-browser';
import { provideRouter, TitleStrategy, withComponentInputBinding } from '@angular/router';
//import { PreloadAllModules, withDebugTracing, withPreloading } from '@angular/router';
import { APP_ROUTES } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { ReportingErrorHandler, clientErrorsInterceptor } from './core/http/clientErrors';
import { earlyFetchInterceptor } from './core/http/earlyFetch.interceptor';
import { PageMetaTitleStrategy } from './shared/pageMeta';
import { provideRowEnterAnimation } from './shared/rowEnterAnimation';
import { provideSnackBarDefaults } from './shared/snackBarDefaults';
import { provideBrowserTelemetry, tracingInterceptor } from './core/telemetry/browserTracing';

/** The app's providers, in the browser and (with app.config.server.ts on top) on the server. */
export const appConfig: ApplicationConfig = {
  providers: [
    provideSnackBarDefaults(),
    provideRowEnterAnimation(),
    // Errors (and window's uncaught errors and rejections) still go to the console, and to the API
    // (clientErrors.ts), as do failed API calls: outermost, so it sees what the app does
    provideBrowserGlobalErrorListeners(),
    { provide: ErrorHandler, useClass: ReportingErrorHandler },
    // tracingInterceptor last, nearest the network: a span (and traceparent) for each call that goes out, the
    // antiforgery token's fetch included, none for an answer earlyFetchInterceptor already had
    provideHttpClient(withXhr(), withInterceptors([clientErrorsInterceptor, earlyFetchInterceptor, authInterceptor, tracingInterceptor])),
    // The browser's telemetry: a span for each navigation, and the Web Vitals (core/telemetry/browserTracing.ts)
    provideBrowserTelemetry(),
    provideRouter(APP_ROUTES
      , withComponentInputBinding()
      //,withPreloading(PreloadAllModules)
      //,withDebugTracing()
    ),
    // Each page's title, description, link-preview tags and canonical URL (shared/pageMeta.ts)
    { provide: TitleStrategy, useClass: PageMetaTitleStrategy },
    // Takes over the page the server rendered rather than drawing it again, reuses the API answers
    // the server fetched for it (HttpClient's transfer cache), and replays clicks made before then.
    // The server fetched them with the visitor's own cookie, so they are that user's answers.
    provideClientHydration(withEventReplay()),
  ],
};
