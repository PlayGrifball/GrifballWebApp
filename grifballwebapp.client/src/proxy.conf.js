const PROXY_CONFIG = [
  {
    context: [
      "/api/",
      "/hub/",
    ],
    pathRewrite: { '^/api': '', '^/hub': '' },
    target: "https://localhost:7210",
    secure: false,
    ws: true
  },
  {
    context: [
      "/signin-*",
    ],
    target: "https://localhost:7210",
    secure: false
  },
  {
    // The hubs' page (TypedSignalR.Client.DevTools): as it is, but its own hub connections go to the hub
    // (src/app/backendPaths.ts)
    context: [
      "/signalr-dev",
    ],
    pathRewrite: { '^/signalr-dev/(\\w*Hub)(?=[/?]|$)': '/$1' },
    target: "https://localhost:7210",
    secure: false,
    ws: true
  }
]

module.exports = PROXY_CONFIG;
