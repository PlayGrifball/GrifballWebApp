#!/usr/bin/env bash
# apk-archive.sh [lock, default GrifballWebApp.Server/apk.lock]
#
# Makes sure the lock's packages (apk-lock.sh) are in its own release, apk-<first 12 of the lock's sha256>, which the
# backend image installs from (GrifballWebApp.Server/Dockerfile works out the same name): one immutable release per
# lock, published by release-archive.sh (which see), so a commit's packages can't disappear and any commit can be
# rebuilt. Assets are named <sha256>.apk: no name GitHub would rename (libstdc++), none standing for two files.
# Alpine's mirrors keep only each package's newest version, so a lock's release is made as soon as a build needs it
# (docker-publish.yml and helm-chart.yml run this before building).
# Env: GH_TOKEN (contents: write), GITHUB_REPOSITORY, TARGET (see release-archive.sh).
set -euo pipefail

lock=${1:-GrifballWebApp.Server/apk.lock}
repo=${GITHUB_REPOSITORY:?}
tag="apk-$(sha256sum "$lock" | cut -c1-12)"
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

# The lock's files, each from an earlier archive if one has it (apk-* releases, and apk-archive, the single release
# before these), else Alpine's mirror: "<sha256> <sha256>.apk <source>..." for release-archive.sh
archived=$(gh api "repos/$repo/releases" --paginate --jq '.[] | select(.tag_name | startswith("apk-")) | .assets[] | "\(.name) \(.browser_download_url)"')
list=$(mktemp)
while read -r arch name version sha; do
  url=$(awk -v a="$sha.apk" '$1 == a { print $2; exit }' <<< "$archived")
  echo "$sha $sha.apk $url https://dl-cdn.alpinelinux.org/alpine/v$alpine/main/$arch/$name-$version.apk https://dl-cdn.alpinelinux.org/alpine/v$alpine/community/$arch/$name-$version.apk"
done < <(grep -v '^#' "$lock") > "$list"

exec "$(dirname "$0")/release-archive.sh" "$tag" "Alpine packages $tag" \
  "The Alpine packages of one GrifballWebApp.Server/apk.lock (sha256 starting ${tag#apk-}), named by sha256, which the backend image installs. Alpine's mirrors keep only each package's newest version; these releases keep every one a build has used, so any commit can be rebuilt byte for byte (docs/reproducible-builds.md). Made by CI (.github/scripts/apk-archive.sh); immutable once published." \
  "$list"
