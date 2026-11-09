FROM node:26.13.0-alpine AS build

# The dev server runs as the image's own unprivileged user, node, which owns the app directory.
WORKDIR /usr/share/app
RUN chown node:node /usr/share/app
USER node

# Exactly package-lock.json, without the packages' install scripts: the native ones (esbuild, lmdb,
# @parcel/watcher) come prebuilt, as optional dependencies. The Angular CLI is the lock's own.
COPY --chown=node:node package.json package-lock.json ./
RUN npm ci --force --ignore-scripts

# Only what the dev server reads, never the whole directory.
COPY --chown=node:node angular.json tsconfig.json tsconfig.app.json m3-theme.scss .postcssrc.json ./
COPY --chown=node:node src src
COPY --chown=node:node src/proxy.conf.docker.js src/proxy.conf.js

ENV PATH=/usr/share/app/node_modules/.bin:$PATH
EXPOSE 4200
CMD ["ng", "serve", "--host=0.0.0.0", "--watch", "--poll=2000"]
