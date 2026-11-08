# Reproducible: the same commit builds the same image, byte for byte (docs/reproducible-builds.md).
# The base images are pinned by digest, npm installs exactly package-lock.json, and CI passes
# SOURCE_DATE_EPOCH (a fixed time, IMAGE_EPOCH) and rewrites the layers' timestamps to it.

# Stage 1
# Build on the build machine's own platform: the output is JavaScript, run by Node for each image
# platform (linux/amd64, linux/arm64; docker-publish.yml).
FROM --platform=$BUILDPLATFORM node:26.13.0-alpine@sha256:8996bb5821d62a8fd9bb27c1cb07462f3fdce6e47d640c6a881ead0b064ce216 AS build
WORKDIR /usr/build
COPY package.json ./
COPY package-lock.json ./
RUN npm ci --force

COPY angular.json ./
COPY tsconfig.json ./
COPY tsconfig.app.json ./
COPY m3-theme.scss ./
COPY .postcssrc.json ./
COPY build-manifest.mjs ./
COPY src src
# browser/build-manifest.json: every served file's sha256, which CI attests and verify-site.mjs
# checks a live site against.
RUN npm run build && node build-manifest.mjs

# Stage 2
# The app's server (src/server.ts): the built files, the pages rendered on the server, and /api,
# /hub and /signin-discord passed on to the backend (API_URL). Bundled with everything it needs, so
# no node_modules. It listens on port 80 as nginx did, so it runs as root as nginx's master did; it
# writes nothing (a read-only root filesystem is fine).
FROM node:26.13.0-alpine@sha256:8996bb5821d62a8fd9bb27c1cb07462f3fdce6e47d640c6a881ead0b064ce216
# The commit, from CI (docker-publish.yml). Labels are part of the image, so they come from the
# commit alone: nothing here may depend on when or where it was built.
ARG SOURCE_REVISION=unknown
LABEL org.opencontainers.image.source="https://github.com/PlayGrifball/GrifballWebApp" \
      org.opencontainers.image.revision="${SOURCE_REVISION}" \
      org.opencontainers.image.title="GrifballWebApp client"
ENV NODE_ENV=production \
    PORT=80
WORKDIR /app
COPY --from=build /usr/build/dist/grifballwebapp.client ./

EXPOSE 80
CMD ["node", "server/server.mjs"]
