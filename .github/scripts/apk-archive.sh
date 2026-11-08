#!/usr/bin/env bash
# apk-archive.sh [--check] [lock, default GrifballWebApp.Server/apk.lock]
#
# Makes sure the lock's packages (apk-lock.sh) are in its own release, apk-<first 12 of the lock's sha256>, which the
# backend image installs from (GrifballWebApp.Server/Dockerfile works out the same name). One release per lock, each
# published once with all its packages and never changed: with the repository's release immutability on, its tag and
# assets can't be moved, replaced or deleted, and GitHub attests them. A commit's packages then can't disappear, so any
# commit can be rebuilt. Assets are named <sha256>.apk: no name GitHub would rename (libstdc++), none standing for two
# files. Alpine's mirrors keep only each package's newest version, so a lock's release is made as soon as a build
# needs it (docker-publish.yml runs this before building); packages Alpine has since replaced come from earlier
# archives (apk-* releases, and apk-archive, the single release before these).
# --check only reads: it writes tag=<the lock's release> and published=true|false to $GITHUB_OUTPUT. The apk-check job
# runs it on every build, so only a build whose lock has no release yet waits in that tag's concurrency group to make
# it. GitHub cancels a job waiting in a group when another one queues, so one group every build joined cancelled builds.
# Env: GH_TOKEN (contents: write; read for --check), GITHUB_REPOSITORY, TARGET (the commit a new release's tag points
# at: a master commit, which GitHub signed, so the tag shows as Verified).
set -euo pipefail

check=false
if [ "${1:-}" = --check ]; then check=true; shift; fi
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

# The lock's release, drafts included (the tags endpoint doesn't return drafts): id draft
release=$(gh api "repos/$repo/releases" --paginate --jq ".[] | select(.tag_name == \"$tag\") | \"\(.id) \(.draft)\"" | head -n 1)
if [ -n "$release" ] && [ "${release#* }" = false ]; then
  # Published, so sealed: check it has every package, by GitHub's digest of each
  have=$(gh api "repos/$repo/releases/${release% *}" --jq '.assets[] | "\(.name) \(.digest)"')
  while read -r arch name version sha; do
    [ "$(awk -v a="$sha.apk" '$1 == a { print $2 }' <<< "$have")" = "sha256:$sha" ] \
      || { echo "::error::$tag is published without $name $version ($arch, $sha.apk)" >&2; exit 1; }
  done < <(grep -v '^#' "$lock")
  echo "$tag: published, all $(grep -vc '^#' "$lock") packages there"
  if $check; then printf 'tag=%s\npublished=true\n' "$tag" >> "${GITHUB_OUTPUT:?}"; fi
  exit 0
fi
if $check; then
  echo "$tag: not published yet${release:+ (a draft is there)}"
  printf 'tag=%s\npublished=false\n' "$tag" >> "${GITHUB_OUTPUT:?}"
  exit 0
fi

if [ -z "$release" ]; then
  id=$(gh api "repos/$repo/releases" -X POST -f tag_name="$tag" -f target_commitish="${TARGET:?}" \
    -f name="Alpine packages $tag" -F draft=true -F prerelease=false -f make_latest=false \
    -f body="The Alpine packages of one GrifballWebApp.Server/apk.lock (sha256 starting ${tag#apk-}), named by sha256, which the backend image installs. Alpine's mirrors keep only each package's newest version; these releases keep every one a build has used, so any commit can be rebuilt byte for byte (docs/reproducible-builds.md). Made by CI (.github/scripts/apk-archive.sh); immutable once published." \
    --jq .id)
else
  id=${release% *}
fi
have=$(gh api "repos/$repo/releases/$id" --jq '.assets[] | "\(.name) \(.digest)"')

# Where an archived package can come from besides Alpine's mirror: every earlier archive's assets, by name
archived=$(gh api "repos/$repo/releases" --paginate --jq '.[] | select(.tag_name | startswith("apk-")) | .assets[] | "\(.name) \(.browser_download_url)"')

work=$(mktemp -d)
added=0
while read -r arch name version sha; do
  asset="$sha.apk"
  digest=$(awk -v a="$asset" '$1 == a { print $2 }' <<< "$have")
  if [ -n "$digest" ]; then
    [ "$digest" = "sha256:$sha" ] || { echo "::error::$asset in the $tag draft has digest $digest" >&2; exit 1; }
    continue
  fi
  url=$(awk -v a="$asset" '$1 == a { print $2; exit }' <<< "$archived")
  if [ -z "$url" ] || ! curl -fsSL -o "$work/$asset" "$url"; then
    for section in main community; do
      curl -fsSL -o "$work/$asset" "https://dl-cdn.alpinelinux.org/alpine/v$alpine/$section/$arch/$name-$version.apk" && break
    done
  fi
  [ -s "$work/$asset" ] || { echo "::error::$name $version ($arch) is in no archive and on neither of Alpine's v$alpine mirrors: run apk-lock.yml" >&2; exit 1; }
  echo "$sha  $work/$asset" | sha256sum -c - > /dev/null \
    || { echo "::error::$name $version ($arch) doesn't match the lock's sha256" >&2; exit 1; }
  # By the release's id: a draft has no tag yet to find it by
  gh api -X POST "https://uploads.github.com/repos/$repo/releases/$id/assets?name=$asset" \
    -H "Content-Type: application/octet-stream" --input "$work/$asset" > /dev/null
  added=$((added + 1))
done < <(grep -v '^#' "$lock")

# Every package there: publish, which seals it
gh api "repos/$repo/releases/$id" -X PATCH -F draft=false -f make_latest=false > /dev/null
echo "$tag: published with $(grep -vc '^#' "$lock") packages ($added uploaded now)"
