# build stage

FROM node:lts-alpine AS build
WORKDIR /app
COPY fbapp-ui/package*.json ./
RUN npm install
COPY fbapp-ui .
RUN npm run build


# production stage

FROM nginx:stable-alpine AS production
RUN mkdir /app
COPY --from=build /app/dist /app
COPY fbapp-ui/nginx.conf /etc/nginx/nginx.conf
