# Stage 1
FROM node:20.9.0-alpine AS build
WORKDIR /usr/test/app
COPY package.json ./
RUN npm install
COPY . .
RUN npm run build

# Stage 2
FROM nginx:1.22.1-alpine
COPY nginx.conf /etc/nginx/nginx.conf
COPY default.conf /etc/nginx/conf.d/default.conf
COPY --from=build /usr/test/app/dist/grifballwebapp.client/browser /usr/share/nginx/html

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
