FROM node:24.22.0-alpine3.24 AS build
WORKDIR /web
COPY package.json package-lock.json ./
RUN npm ci --no-fund --no-audit
COPY . .
RUN npx ng build

FROM nginx:1.30.6-alpine3.24
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /web/dist/web/browser /usr/share/nginx/html
