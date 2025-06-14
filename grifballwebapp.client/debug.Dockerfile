FROM node:22.12.0-alpine AS build

RUN mkdir /usr/share/app
WORKDIR /usr/share/app

COPY package.json package.json
RUN npm i -g @angular/cli@20.0.2
COPY package-lock.json package-lock.json
RUN npm i --force

COPY . .

COPY /src/proxy.conf.docker.js src/proxy.conf.js

ENV PATH /node_modules/.bin:$PATH
EXPOSE 4200
CMD ["ng", "serve", "--host=0.0.0.0", "--watch", "--poll=2000"]
