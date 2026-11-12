import { HttpErrorResponse, HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { ApplicationRef, EnvironmentProviders, inject, makeEnvironmentProviders, provideEnvironmentInitializer } from '@angular/core';
import { ActivatedRouteSnapshot, NavigationCancel, NavigationEnd, NavigationError, NavigationStart, Router } from '@angular/router';
import { finalize, tap } from 'rxjs';
import { isCancelledByBrowser, isReportedFailure, pageTraceparent } from '../clientErrors';
import { redactUrl } from '../redactUrl';
import { BrowserTelemetry, REQUEST_KIND_ATTRIBUTE, TELEMETRY_URL, parseTraceparent } from './browserTelemetry';

/** Whether a request goes to this app's API (same origin, under /api), the telemetry's own batches aside. */
export function isApiCall(url: string, base: string): boolean {
  let parsed: URL;
  try {
    parsed = new URL(url, base);
  } catch {
    return false;
  }
  return parsed.origin === new URL(base).origin
    && parsed.pathname.startsWith('/api/')
    && parsed.pathname !== TELEMETRY_URL;
}

/**
 * A client span for each call to the API, its traceparent sent along so the app's server and the backend continue the
 * trace: one trace from the click to the database. Under the navigation under way, if there is one. A call that got
 * a 5xx, or no answer the browser didn't cause itself, is an error.
 */
export const tracingInterceptor: HttpInterceptorFn = (req, next) => {
  const telemetry = inject(BrowserTelemetry);
  if (!telemetry.enabled || !isApiCall(req.url, location.href))
    return next(req);

  const span = telemetry.startSpan(req.method, {
    kind: 'client',
    attributes: {
      'http.request.method': req.method,
      'url.path': redactUrl(req.urlWithParams, location.href),
      [REQUEST_KIND_ATTRIBUTE]: 'api',
    },
  });
  return next(req.clone({ setHeaders: { traceparent: span.traceparent } })).pipe(
    tap({
      next: event => {
        if (event instanceof HttpResponse)
          span.setAttribute('http.response.status_code', event.status);
      },
      error: (error: unknown) => {
        if (!(error instanceof HttpErrorResponse)) {
          span.setStatus('error');
          return;
        }
        span.setAttribute('http.response.status_code', error.status);
        if (error.status === 0 && isCancelledByBrowser(false, document, navigator))
          span.setAttribute('grif.cancelled', true);
        else if (isReportedFailure(error))
          span.setStatus('error');
      },
    }),
    finalize(() => span.end()),
  );
};

/** The route a navigation landed on, as the app's routes name it (/season/:seasonID): few values, unlike URLs. */
export function routeTemplate(root: ActivatedRouteSnapshot): string {
  const parts: string[] = [];
  for (let route: ActivatedRouteSnapshot | null = root.firstChild; route; route = route.firstChild) {
    const path = route.routeConfig?.path;
    if (path)
      parts.push(path);
  }
  return '/' + parts.join('/');
}

/**
 * Traces each navigation in the app: a span from NavigationStart until the page has loaded what it asked for
 * (the app is stable again), so the API calls it made are its children. The first is the server's page's (its trace
 * from Server-Timing): the browser's start of the page joins the server's render. Then the Web Vitals, loaded once
 * the app runs.
 */
export function provideBrowserTelemetry(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideEnvironmentInitializer(() => {
      const telemetry = inject(BrowserTelemetry);
      if (!telemetry.enabled)
        return;
      const router = inject(Router);
      const appRef = inject(ApplicationRef);
      let first = true;
      router.events.subscribe(event => {
        if (event instanceof NavigationStart) {
          // A navigation that hadn't settled when the next began: over all the same
          telemetry.active()?.end();
          const parent = first ? parseTraceparent(pageTraceparent()) ?? null : null;
          first = false;
          const span = telemetry.startSpan('navigation', {
            parent,
            attributes: { [REQUEST_KIND_ATTRIBUTE]: 'navigation', 'url.path': redactUrl(event.url, location.href) },
          });
          telemetry.setActive(span);
        } else if (event instanceof NavigationEnd || event instanceof NavigationCancel || event instanceof NavigationError) {
          const span = telemetry.active();
          if (!span)
            return;
          if (event instanceof NavigationEnd) {
            const route = routeTemplate(router.routerState.snapshot.root);
            span.setName(`navigation ${route}`);
            span.setAttribute('http.route', route);
            void appRef.whenStable().then(() => span.end());
          } else {
            span.setAttribute('grif.cancelled', event instanceof NavigationCancel);
            if (event instanceof NavigationError)
              span.setStatus('error');
            span.end();
          }
        }
      });
      void import('web-vitals').then(({ onCLS, onFCP, onINP, onLCP, onTTFB }) => {
        onCLS(metric => telemetry.vital('CLS', metric.value));
        onFCP(metric => telemetry.vital('FCP', metric.value));
        onINP(metric => telemetry.vital('INP', metric.value));
        onLCP(metric => telemetry.vital('LCP', metric.value));
        onTTFB(metric => telemetry.vital('TTFB', metric.value));
      });
    }),
  ]);
}
