const PROXY_CONFIG = [
  {
    context: [
      "/api/",
      "/hub/",
    ],
    pathRewrite: { '^/api': '', '^/hub': '' },
    target: "https://grifballwebapp.server:7210",
    secure: false,
    ws: true
  },
  {
    context: [
      "/signin-*",
    ],
    target: "https://grifballwebapp.server:7210",
    secure: false
  }
]

module.exports = PROXY_CONFIG;
