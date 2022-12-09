
# build stage

FROM docker.io/library/node:lts-alpine AS build
WORKDIR /app
RUN wget -O - 'https://github.com/elm/compiler/releases/download/0.19.1/binary-for-linux-64-bit.gz' | gunzip -c >/usr/local/bin/elm
RUN chmod +x /usr/local/bin/elm
COPY .yarn ./.yarn
COPY package.json yarn.lock .yarnrc.yml ./
RUN yarn install --immutable
COPY . .
RUN yarn build


# production stage

FROM docker.io/library/nginx:stable-alpine AS production
RUN mkdir /app
COPY --from=build /app/dist /app
COPY nginx.conf /etc/nginx/nginx.conf
