#!/usr/bin/env bash
# image-revision.sh <image@digest>
#
# The commit an image was built from: its org.opencontainers.image.revision label (one platform's: they're one
# build). Prints nothing when the image has no label. Used by env-release.sh and env-deploy.sh.
set -euo pipefail

docker buildx imagetools inspect "${1:?usage: $0 <image@digest>}" --format '{{json .Image}}' \
  | jq -r 'if has("config") then . else to_entries[0].value end | .config.Labels["org.opencontainers.image.revision"] // empty'
