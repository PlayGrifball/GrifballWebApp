import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { bootstrapApplication } from '@angular/platform-browser';
import { provideRouter, withComponentInputBinding } from '@angular/router';
//import { PreloadAllModules, withDebugTracing, withPreloading } from '@angular/router';
import { AppComponent } from './app/app.component';
import { APP_ROUTES } from './app/app.routes';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { MAT_SNACK_BAR_DEFAULT_OPTIONS } from '@angular/material/snack-bar';
import { importProvidersFrom } from '@angular/core';
import { JwtModule } from '@auth0/angular-jwt';
import { authInterceptor } from './app/auth.interceptor';
import { provideLuxonDatetimeAdapter } from '@ng-matero/extensions-luxon-adapter';

bootstrapApplication(AppComponent, {
  providers: [
    {
      provide: MAT_SNACK_BAR_DEFAULT_OPTIONS, useValue: { duration: 5000 }
    },
    importProvidersFrom(
      JwtModule.forRoot({
        config: {
          tokenGetter: () => localStorage.getItem("access_token"),
          //allowedDomains: ["example.com"],
          //disallowedRoutes: ["http://example.com/examplebadroute/"],
        },
      }),
    ),
    provideLuxonDatetimeAdapter({
      parse: {
        dateInput: 'yyyy-LL-dd',
        monthInput: 'LLLL',
        yearInput: 'yyyy',
        timeInput: 'HH:mm',
        datetimeInput: 'yyyy-LL-dd HH:mm',
      },
      display: {
        dateInput: 'yyyy-LL-dd',
        monthInput: 'LLLL',
        yearInput: 'yyyy',
        timeInput: 'HH:mm',
        datetimeInput: 'yyyy-LL-dd HH:mm',
        monthYearLabel: 'yyyy LLLL',
        dateA11yLabel: 'DDD',
        monthYearA11yLabel: 'LLLL yyyy',
        popupHeaderDateLabel: 'dd LLL, ccc',
      }
    }),
    provideAnimationsAsync(),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideRouter(APP_ROUTES
      , withComponentInputBinding()
      //,withPreloading(PreloadAllModules)
      //,withDebugTracing()
    )
  ],
}).catch(err => console.error(err));
