#!/usr/bin/env bash
# apk-archive.sh [lock, default GrifballWebApp.Server/apk.lock]
#
# Makes sure every package in the lock (apk-lock.sh) is in the repo's apk-archive release, which the backend image
# installs from: one it lacks is downloaded from Alpine's mirror, checked against the lock's sha256 and uploaded as
# <arch>-<name>-<version>.apk. Assets are only ever added, so any commit's lock can still be built. Alpine's mirrors
# keep only each package's newest version, so a package is archived as soon as a lock names it (docker-publish.yml
# runs this before building).
# Env: GH_TOKEN (contents: write), GITHUB_REPOSITORY.
set -euo pipefail

lock=${1:-GrifballWebApp.Server/apk.lock}
tag=apk-archive
repo=${GITHUB_REPOSITORY:?}
alpine=$(sed -n -E 's/^# alpine: ([0-9]+\.[0-9]+).*/\1/p' "$lock")
[ -n "$alpine" ] || { echo "::error::No '# alpine:' line in $lock" >&2; exit 1; }

if ! gh release view "$tag" -R "$repo" > /dev/null 2>&1; then
  gh release create "$tag" -R "$repo" --title "Alpine package archive" --latest=false \
    --notes "The Alpine packages the backend image installs, by exact version (GrifballWebApp.Server/apk.lock). Alpine's mirrors keep only the newest version of each; this keeps every one a build has used, so any commit can be rebuilt byte for byte. Added to by CI; never delete assets."
fi
have=$(gh release view "$tag" -R "$repo" --json assets --jq '.assets[].name')

work=$(mktemp -d)
added=0
while read -r arch name version sha; do
  file="$arch-$name-$version.apk"
  grep -qxF "$file" <<< "$have" && continue
  for section in main community; do
    curl -fsSL -o "$work/$file" "https://dl-cdn.alpinelinux.org/alpine/v$alpine/$section/$arch/$name-$version.apk" && break
  done
  [ -s "$work/$file" ] || { echo "::error::$name $version ($arch) is on neither of Alpine's v$alpine mirrors: run apk-lock.yml" >&2; exit 1; }
  echo "$sha  $work/$file" | sha256sum -c - > /dev/null \
    || { echo "::error::$file doesn't match the lock's sha256" >&2; exit 1; }
  gh release upload "$tag" -R "$repo" "$work/$file"
  added=$((added + 1))
done < <(grep -v '^#' "$lock")
echo "$tag: $added packages added, $(grep -vc '^#' "$lock") in the lock"
