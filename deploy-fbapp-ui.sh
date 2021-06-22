#!/bin/sh

set -eu

docker build -t localhost:32000/fbapp-ui:registry -f fbapp-ui/Dockerfile .
docker push localhost:32000/fbapp-ui:registry
