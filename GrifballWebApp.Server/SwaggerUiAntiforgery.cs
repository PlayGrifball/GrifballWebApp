using GrifballWebApp.Server.Identity;

namespace GrifballWebApp.Server;

/// <summary>
/// Swagger UI's "Try it out" for a signed-in browser. Its session is the cookie, which <c>CookieAntiforgeryFilter</c>
/// only accepts on a change with the antiforgery token in <see cref="BrowserAuth.AntiforgeryHeader"/>: before each
/// request that isn't a GET and has no bearer token, the page asks this API for one (next to the Swagger page, so
/// under /api for the site and at the root for the backend itself) and adds it. A request with a bearer token goes as
/// it is: it is no cookie session.
/// </summary>
public static class SwaggerUiAntiforgery
{
    /// <summary>Swagger UI's requestInterceptor: JavaScript, returning the request or a promise of it.</summary>
    public const string RequestInterceptor = $$"""
        (request) => {
          const method = (request.method || 'GET').toUpperCase();
          if (method === 'GET' || method === 'HEAD' || (request.headers && request.headers.Authorization)) return request;
          const path = window.location.pathname;
          const base = path.slice(0, path.indexOf('/swagger/'));
          return fetch(base + '/identity/antiforgery-token', { credentials: 'same-origin' })
            .then(response => response.ok ? response.json() : null)
            .then(body => {
              if (body && body.token) request.headers['{{BrowserAuth.AntiforgeryHeader}}'] = body.token;
              return request;
            })
            .catch(() => request);
        }
        """;
}
