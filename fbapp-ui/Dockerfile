# build stage

FROM node:lts-alpine AS build
WORKDIR /app
COPY fbapp-ui/package.json .
COPY fbapp-ui/yarn.lock .
RUN yarn
COPY fbapp-ui .
RUN yarn quasar build -m spa


# production stage

FROM nginx:stable-alpine AS production
RUN mkdir /app
COPY --from=build /app/dist/spa /app
COPY fbapp-ui/nginx.conf /etc/nginx/nginx.conf
