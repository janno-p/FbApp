#!/bin/sh

set -eu

docker build -t localhost:32000/fbapp-api:registry -f fbapp-api/Dockerfile  .
docker push localhost:32000/fbapp-api:registry
