import { provideHttpClient } from '@angular/common/http';
import { bootstrapApplication } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
//import { PreloadAllModules, withDebugTracing, withPreloading } from '@angular/router';
import { AppComponent } from './app/app.component';
import { APP_ROUTES } from './app/app.routes';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';

bootstrapApplication(AppComponent, {
  providers: [
    provideAnimationsAsync(),
    provideHttpClient(),
    provideRouter(APP_ROUTES
      //,withPreloading(PreloadAllModules)
      //,withDebugTracing()
    )
  ],
}).catch(err => console.error(err));
