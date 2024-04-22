const PROXY_CONFIG = [
  {
    context: [
      "/api/",
    ],
    pathRewrite: { '^/api': '' },
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
  }
]

module.exports = PROXY_CONFIG;
