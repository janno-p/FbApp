#!/bin/sh

set -eu

docker build -t localhost:32000/fbapp-init-events:registry -f fbapp-init-events/Dockerfile .
docker push localhost:32000/fbapp-init-events:registry
