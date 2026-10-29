import { provideHttpClient, withInterceptors, withXhr } from '@angular/common/http';
import { bootstrapApplication } from '@angular/platform-browser';
import { provideRouter, withComponentInputBinding } from '@angular/router';
//import { PreloadAllModules, withDebugTracing, withPreloading } from '@angular/router';
import { AppComponent } from './app/app.component';
import { APP_ROUTES } from './app/app.routes';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { importProvidersFrom } from '@angular/core';
import { JwtModule } from '@auth0/angular-jwt';
import { authInterceptor } from './app/auth.interceptor';
import { provideSnackBarDefaults } from './app/shared/snackBarDefaults';
import { earlyFetchInterceptor } from './app/earlyFetch.interceptor';

bootstrapApplication(AppComponent, {
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
    )
  ],
}).catch(err => console.error(err));
