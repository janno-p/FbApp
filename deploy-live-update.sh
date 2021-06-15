#!/bin/sh

set -eu

docker build -t localhost:32000/fbapp-live-update:registry -f fbapp-live-update/Dockerfile .
docker push localhost:32000/fbapp-live-update:registry
