# Stage 1
FROM node:20.11.1-alpine AS build
WORKDIR /usr/build
COPY package.json ./
COPY package-lock.json ./
COPY angular.json ./
COPY tsconfig.json ./
COPY tsconfig.app.json ./
COPY m3-theme.scss ./
COPY tailwind.config.js ./
COPY src src
RUN npm install --force
RUN npm run build

# Stage 2
FROM nginx:1.27.2-alpine-slim
COPY nginx.conf /etc/nginx/nginx.conf
COPY default.conf /etc/nginx/conf.d/default.conf
COPY --from=build /usr/build/dist/grifballwebapp.client/browser /usr/share/nginx/html

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
