import { HttpBackend } from '@angular/common/http';
import { mergeApplicationConfig, ApplicationConfig } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';
import { API_ORIGIN, ServerApiBackend } from './serverApi.backend';

const serverConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(withRoutes(serverRoutes)),
    // The backend, as the proxy in server.ts reaches it
    { provide: API_ORIGIN, useFactory: () => (process.env['API_URL'] ?? 'http://grifballwebapp-server:5295').replace(/\/+$/, '') },
    { provide: HttpBackend, useClass: ServerApiBackend },
  ]
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
