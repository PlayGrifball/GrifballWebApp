# Stage 1
# Build on the build machine's own platform: the output is static files, served by nginx for each
# image platform (linux/amd64, linux/arm64; docker-publish.yml).
FROM --platform=$BUILDPLATFORM node:24.22.1-alpine AS build
WORKDIR /usr/build
COPY package.json ./
COPY package-lock.json ./
RUN npm install --force

COPY angular.json ./
COPY tsconfig.json ./
COPY tsconfig.app.json ./
COPY m3-theme.scss ./
COPY tailwind.config.js ./
COPY src src
RUN npm run build

# Stage 2
FROM nginx:1.31.8-alpine-slim
COPY nginx.conf /etc/nginx/nginx.conf
COPY default.conf /etc/nginx/conf.d/default.conf
COPY --from=build /usr/build/dist/grifballwebapp.client/browser /usr/share/nginx/html

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
