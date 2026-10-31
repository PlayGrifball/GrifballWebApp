# Stage 1
# Build on the build machine's own platform: the output is JavaScript, run by Node for each image
# platform (linux/amd64, linux/arm64; docker-publish.yml).
FROM --platform=$BUILDPLATFORM node:26.12.1-alpine AS build
WORKDIR /usr/build
COPY package.json ./
COPY package-lock.json ./
RUN npm install --force

COPY angular.json ./
COPY tsconfig.json ./
COPY tsconfig.app.json ./
COPY m3-theme.scss ./
COPY .postcssrc.json ./
COPY src src
RUN npm run build

# Stage 2
# The app's server (src/server.ts): the built files, the pages rendered on the server, and /api,
# /hub and /signin-discord passed on to the backend (API_URL). Bundled with everything it needs, so
# no node_modules. It listens on port 80 as nginx did, so it runs as root as nginx's master did; it
# writes nothing (a read-only root filesystem is fine).
FROM node:26.12.1-alpine
ENV NODE_ENV=production \
    PORT=80
WORKDIR /app
COPY --from=build /usr/build/dist/grifballwebapp.client ./

EXPOSE 80
CMD ["node", "server/server.mjs"]
