Zoneless investigation harness (prototype branch only).

walk.js serves one or more production builds, mocks /api/** with page.route,
fakes the SignalR hub with page.routeWebSocket, opens 21 routes and checks that
data from delayed (400 ms) API responses reaches the DOM without user input.
A failing check is then re-tested after a "nudge" (open/close the side menu).

  npm i playwright-core
  node walk.js zone=<dist-a>/browser zoneless=<dist-b>/browser

Needs Chromium at /usr/bin/chromium.
