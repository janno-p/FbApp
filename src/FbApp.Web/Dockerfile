FROM oven/bun:1 AS base
WORKDIR /usr/src/app
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates \
    && update-ca-certificates \
    && rm -rf /var/lib/apt/lists/*

FROM base AS install
RUN mkdir -p /temp/dev
COPY package.json bun.lock /temp/dev
RUN cd /temp/dev && bun install --frozen-lockfile

RUN mkdir -p /temp/prod
COPY package.json bun.lock /temp/prod
RUN cd /temp/prod && bun install --frozen-lockfile --production

FROM base AS prerelease
COPY --from=install /temp/dev/node_modules node_modules
COPY . .

ENV NODE_ENV=production
# RUN bun test
RUN bun run build

# FROM base AS release
# COPY --from=install /temp/prod/node_modules node_modules
# COPY --from=prerelease /usr/src/app/index.ts .
# COPY --from=prerelease /usr/src/app/package.json .
#
# USER bun
# EXPOSE 3000/tcp
# ENTRYPOINT [ "bun", "run", "index.ts" ]


# build stage

# FROM docker.io/library/node:lts-alpine AS build
# WORKDIR /app
# RUN wget -O - 'https://github.com/elm/compiler/releases/download/0.19.1/binary-for-linux-64-bit.gz' | gunzip -c >/usr/local/bin/elm
# RUN chmod +x /usr/local/bin/elm
# COPY .yarn ./.yarn
# COPY package.json yarn.lock .yarnrc.yml ./
# RUN yarn install --immutable
# COPY . .
# RUN yarn build


# production stage

FROM docker.io/library/nginx:stable-alpine AS production
RUN mkdir /app
COPY --from=prerelease /usr/src/app/dist /app
COPY nginx.conf /etc/nginx/nginx.conf
