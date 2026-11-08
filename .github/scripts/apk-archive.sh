#!/usr/bin/env bash
# apk-archive.sh [lock, default GrifballWebApp.Server/apk.lock]
#
# Makes sure every package in the lock (apk-lock.sh) is in the repo's apk-archive release, which the backend image
# installs from. Assets are named by content, <sha256>.apk: a name can't stand for two different files (Alpine can
# rebuild a version with other bytes on another branch), and has no characters GitHub would rename (libstdc++). One the
# release has is checked against GitHub's own digest of it; one it lacks is downloaded from Alpine's mirror, checked
# against the lock and uploaded. Assets are only ever added, so any commit's lock can still be built. Alpine's mirrors
# keep only each package's newest version, so a package is archived as soon as a lock names it (docker-publish.yml runs
# this before building).
# Env: GH_TOKEN (contents: write), GITHUB_REPOSITORY.
set -euo pipefail

lock=${1:-GrifballWebApp.Server/apk.lock}
tag=apk-archive
repo=${GITHUB_REPOSITORY:?}
alpine=$(sed -n -E 's/^# alpine: ([0-9]+\.[0-9]+).*/\1/p' "$lock")
[ -n "$alpine" ] || { echo "::error::No '# alpine:' line in $lock" >&2; exit 1; }
# The lock is resolved against one base image: another (a Renovate digest update) may already have newer versions of
# the packages it names, which installing the lock's would downgrade. Re-resolve it for the new one.
locked=$(sed -n -E 's/^# base: //p' "$lock")
base=$(sed -n -E 's#^FROM (mcr\.microsoft\.com/dotnet/aspnet:[^ ]+) AS base$#\1#p' "$(dirname "$lock")/Dockerfile")
if [ "$locked" != "$base" ]; then
  echo "::error::$lock was resolved for $locked, but the Dockerfile's base is $base. Run the 'Alpine package lock' workflow on this branch, then close and reopen the PR." >&2
  exit 1
fi

if ! gh release view "$tag" -R "$repo" > /dev/null 2>&1; then
  gh release create "$tag" -R "$repo" --title "Alpine package archive" --latest=false \
    --notes "The Alpine packages the backend image installs, by exact version (GrifballWebApp.Server/apk.lock), named by sha256. Alpine's mirrors keep only the newest version of each; this keeps every one a build has used, so any commit can be rebuilt byte for byte. Added to by CI; never delete assets."
fi
# name digest, for the assets the release has
have=$(gh api "repos/$repo/releases/tags/$tag" --paginate --jq '.assets[] | "\(.name) \(.digest)"')

work=$(mktemp -d)
added=0
while read -r arch name version sha; do
  asset="$sha.apk"
  digest=$(awk -v a="$asset" '$1 == a { print $2 }' <<< "$have")
  if [ -n "$digest" ]; then
    [ "$digest" = "sha256:$sha" ] || { echo "::error::$asset in the release has digest $digest" >&2; exit 1; }
    continue
  fi
  for section in main community; do
    curl -fsSL -o "$work/$asset" "https://dl-cdn.alpinelinux.org/alpine/v$alpine/$section/$arch/$name-$version.apk" && break
  done
  [ -s "$work/$asset" ] || { echo "::error::$name $version ($arch) is on neither of Alpine's v$alpine mirrors: run apk-lock.yml" >&2; exit 1; }
  echo "$sha  $work/$asset" | sha256sum -c - > /dev/null \
    || { echo "::error::$name $version ($arch) from Alpine's mirror doesn't match the lock's sha256" >&2; exit 1; }
  gh release upload "$tag" -R "$repo" "$work/$asset"
  added=$((added + 1))
done < <(grep -v '^#' "$lock")
echo "$tag: $added packages added, $(grep -vc '^#' "$lock") in the lock"
