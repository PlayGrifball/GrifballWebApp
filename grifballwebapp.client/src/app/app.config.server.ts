import { HttpBackend } from '@angular/common/http';
import { mergeApplicationConfig, ApplicationConfig, provideEnvironmentInitializer } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { apiUrl } from './core/http/apiUrl';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';
import { API_ORIGIN, ServerApiBackend } from './core/server/serverApi.backend';
import { placeholderNullWhenNone } from './teamBuilder/dndServer';

const serverConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(withRoutes(serverRoutes)),
    // The backend, as the proxy in server.ts reaches it
    { provide: API_ORIGIN, useFactory: () => apiUrl(process.env['API_URL']) },
    { provide: HttpBackend, useClass: ServerApiBackend },
    // ngx-drag-drop's dropzones, as they work in the browser (teamBuilder/dndServer.ts)
    provideEnvironmentInitializer(placeholderNullWhenNone),
  ]
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
