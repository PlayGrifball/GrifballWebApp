import { loadScriptsAfterFirstPaint } from './afterFirstPaint';
import { forLog } from '../http/forLog';
import { pageCacheHeaders } from './pageCache';
import { redactUrl } from '../http/redactUrl';
import type { RenderOutcome, ServerRequest, ServerTelemetryCore } from './serverTelemetryCore';

/** What the page handler needs of the server (server.ts gives it Angular's engine and Express's response). */
export interface PageHandlerDeps<Req extends ServerRequest, Res> {
  /** Angular's render of the page; null for a page that renders in the browser (app.routes.server.ts). */
  handle(req: Req): Promise<Response | null>;
  /** Writes the rendered page to the response (writeResponseToNodeResponse). */
  write(response: Response, res: Res): Promise<void> | void;
  /** Sends the page that loads the app and renders in the browser. */
  sendClientPage(res: Res, next: (err?: unknown) => void): void;
  telemetry: Pick<ServerTelemetryCore, 'render' | 'log'>;
}

/**
 * Every request that isn't a file or the backend's: the page, rendered on the server or (for the pages in
 * app.routes.server.ts that render in the browser) the page that loads the app. Should rendering fail, the
 * browser renders the page instead. A page rendered with the visitor's session cookie (which its API calls
 * carried) is theirs alone: private and no-store (pageCache.ts). The page's scripts load once it has painted
 * (afterFirstPaint.ts). Rendering is a span of its own, which the API calls it makes carry as their traceparent
 * (FORWARDED_HEADERS in serverApi.backend.ts).
 */
export function pageHandler<Req extends ServerRequest & { headers: { traceparent?: string | string[]; cookie?: string } }, Res>(
  deps: PageHandlerDeps<Req, Res>,
): (req: Req, res: Res, next: (err?: unknown) => void) => void {
  return (req, res, next) => {
    // The render's span and outcome cover writing the page: a failure there is a failed render too
    deps.telemetry
      .render(req, async (traceparent): Promise<RenderOutcome> => {
        if (traceparent)
          req.headers.traceparent = traceparent;
        let response = await deps.handle(req);
        if (!response) {
          deps.sendClientPage(res, next);
          return 'browser';
        }
        if (response.headers.get('Content-Type')?.startsWith('text/html')) {
          const headers = new Headers(response.headers);
          headers.delete('Content-Length');
          response = new Response(loadScriptsAfterFirstPaint(await response.text()), { status: response.status, statusText: response.statusText, headers });
        }
        for (const [name, value] of Object.entries(pageCacheHeaders(req.headers.cookie)))
          response.headers.set(name, value);
        await deps.write(response, res);
        return 'server';
      }, outcome => outcome)
      .catch(error => {
        deps.telemetry.log('error', 'Rendering failed, sending the page that renders in the browser', { path: forLog(redactUrl(req.originalUrl)), error });
        deps.sendClientPage(res, next);
      });
  };
}

/** The line lighthouserc.cjs, lighthouse-signed-in.mjs and e2e/serve.mjs wait for ("Serving"), whatever LOG_LEVEL says. */
export function announceServing(telemetry: Pick<ServerTelemetryCore, 'announce'>, port: number, backendUrl: string): void {
  telemetry.announce(`Serving on http://localhost:${port}, the API at ${backendUrl}`);
}
