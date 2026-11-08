#!/usr/bin/env bash
# rebuild-image.sh <client|server> [master commit, default HEAD]
#
# Rebuilds an image from the checked-out commit from nothing, as docker-publish.yml builds it, and prints its digest:
# a new builder on the BuildKit CI pins (BUILDKIT in docker-publish.yml), no cache, the commit's time as
# SOURCE_DATE_EPOCH. The same commit gives the same digest (docs/reproducible-builds.md); with a published image for the
# commit (sha-<7>), it's compared. The reproducible job in docker-publish.yml runs this too.
#
# The server image carries master's migration list (master-migrations.sh): for a commit on master, master is the commit
# itself (HEAD, the default); for a preview, the master commit its build read. Needs a full clone (not shallow), Docker
# with Buildx, and emulation for each platform this machine can't run (the script says how).
# Env: PLATFORMS (default linux/amd64,linux/arm64). Writes digest= to $GITHUB_OUTPUT when it's set.
set -euo pipefail

image=${1:?usage: $0 <client|server> [master commit]}
master=${2:-HEAD}
platforms=${PLATFORMS:-linux/amd64,linux/arm64}
case "$image" in
  client) context=grifballwebapp.client; dockerfile=grifballwebapp.client/Dockerfile; package=grifballwebappclient ;;
  server) context=.; dockerfile=GrifballWebApp.Server/Dockerfile; package=grifballwebappserver ;;
  *) echo "usage: $0 <client|server> [master commit]" >&2; exit 1 ;;
esac

buildkit=$(sed -n -E 's/^  BUILDKIT: image=(.*)$/\1/p' .github/workflows/docker-publish.yml)
[ -n "$buildkit" ] || { echo "No BUILDKIT in .github/workflows/docker-publish.yml" >&2; exit 1; }
if [ "$image" = server ]; then
  .github/scripts/master-migrations.sh .master-migrations "$master"
fi

builder="rebuild-$$"
docker buildx create --name "$builder" --driver docker-container --driver-opt "image=$buildkit" > /dev/null
trap 'docker buildx rm -f "$builder" > /dev/null 2>&1 || true' EXIT
supported=$(docker buildx inspect --bootstrap "$builder" | sed -n -E 's/^Platforms:[[:space:]]*//p')
for platform in ${platforms//,/ }; do
  if ! grep -qE "(^|[ ,])$platform(\*)?(,|$)" <<< "$supported"; then
    echo "This machine can't build $platform. Install emulation for it, then run this again:" >&2
    echo "  docker run --privileged --rm tonistiigi/binfmt --install ${platform#linux/}" >&2
    exit 1
  fi
done

metadata=$(mktemp)
docker buildx build --builder "$builder" --no-cache --platform "$platforms" --provenance=false \
  --build-arg "SOURCE_DATE_EPOCH=$(git log -1 --format=%ct)" \
  --build-arg "SOURCE_REVISION=$(git rev-parse HEAD)" \
  --output type=image,push=false,rewrite-timestamp=true \
  --metadata-file "$metadata" -f "$dockerfile" "$context"
digest=$(grep -o '"containerimage.digest": *"sha256:[0-9a-f]*"' "$metadata" | grep -o 'sha256:[0-9a-f]*')
[ -n "$digest" ] || { echo "No digest in the build's metadata" >&2; exit 1; }
[ -z "${GITHUB_OUTPUT:-}" ] || echo "digest=$digest" >> "$GITHUB_OUTPUT"
echo "Rebuilt $image ($platforms): $digest"

published="ghcr.io/playgrifball/$package:sha-$(git rev-parse --short=7 HEAD)"
if remote=$(docker buildx imagetools inspect "$published" --format '{{json .Manifest.Digest}}' 2> /dev/null); then
  remote=${remote//\"/}
  echo "Published $published: $remote"
  if [ "$remote" = "$digest" ]; then
    echo "Same image, byte for byte."
  else
    echo "Different. (A preview is amd64 only: PLATFORMS=linux/amd64, with the master commit its build read.)"
    exit 2
  fi
fi
