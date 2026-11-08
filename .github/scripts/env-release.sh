#!/usr/bin/env bash
# env-release.sh <environment> <backend image@digest> <frontend image@digest>
#
# The GitHub release of a promotion (promote.yml): what an environment now runs, published once and, with the
# repository's release immutability on, never changed. Tagged <environment>-<UTC date> (.2, .3 for more that day) at
# the commit the images were built from (their org.opencontainers.image.revision label). Its notes: both images by
# digest, how to verify them, and what changed since the environment's previous release. Its asset, release.json,
# says the same for machines. Prod's is the repository's latest release; staging's are pre-releases. Both images also
# get the release's tag as a docker tag (grifballwebappserver:prod-2026-11-08), to find them by; a docker tag can be
# moved, so the release's digests are what count.
# Made as a draft, then published once complete: an immutable release can't take assets after.
# Env: GH_TOKEN (contents: write), GITHUB_REPOSITORY, FROM (the tag promoted from), RUN_URL, ACTOR.
set -euo pipefail

environment=${1:?usage: $0 <environment> <backend@digest> <frontend@digest>}
backend=${2:?} frontend=${3:?}
repo=${GITHUB_REPOSITORY:?}

# The commit an image was built from: its revision label (one platform's: they're one build)
revision() {
  docker buildx imagetools inspect "$1" --format '{{json .Image}}' \
    | jq -r 'if has("config") then . else to_entries[0].value end | .config.Labels["org.opencontainers.image.revision"] // empty'
}
commit=$(revision "$backend")
frontend_commit=$(revision "$frontend")
[ -n "$commit" ] || { echo "::error::$backend has no org.opencontainers.image.revision label" >&2; exit 1; }
if [ "$frontend_commit" != "$commit" ]; then
  echo "::warning::The frontend image was built from $frontend_commit, the backend from $commit: the release is tagged at the backend's"
fi

# <environment>-<date>, or the next .N free that day
date=$(date -u +%Y-%m-%d)
tag="$environment-$date"
n=1
while gh api "repos/$repo/git/ref/tags/$tag" > /dev/null 2>&1 \
  || gh api "repos/$repo/releases" --paginate --jq '.[].tag_name' | grep -qxF "$tag"; do
  n=$((n + 1))
  tag="$environment-$date.$n"
done

# The images by the release's tag too: ghcr.io/playgrifball/grifballwebappserver:<tag>
docker buildx imagetools create -t "${backend%@*}:$tag" "$backend"
docker buildx imagetools create -t "${frontend%@*}:$tag" "$frontend"

# The environment's previous release (published), for what changed since
previous=$(gh api "repos/$repo/releases" --paginate \
  --jq ".[] | select(.draft == false and (.tag_name | startswith(\"$environment-\"))) | \"\(.created_at) \(.tag_name)\"" \
  | sort -r | head -n 1 | cut -d' ' -f2)

digest() { echo "${1#*@}"; }
image() { echo "${1%@*}"; }
notes=$(mktemp)
{
  echo "**$environment** runs commit [\`${commit:0:7}\`](https://github.com/$repo/commit/$commit), promoted from \`:$FROM\` by @$ACTOR ([run]($RUN_URL))."
  echo
  echo "| Image | Digest |"
  echo "| --- | --- |"
  echo "| \`$(image "$backend")\` | \`$(digest "$backend")\` |"
  echo "| \`$(image "$frontend")\` | \`$(digest "$frontend")\` |"
  echo
  echo "Also tagged \`:$tag\`, e.g. \`docker pull $(image "$backend"):$tag\`. A docker tag can be moved; the digests above are what this release recorded."
  [ "$frontend_commit" = "$commit" ] || echo "(The frontend image was built from \`$frontend_commit\`.)"
  echo
  echo "### Verify"
  echo '```sh'
  echo "gh release verify $tag -R $repo"
  echo "gh attestation verify oci://$backend -R $repo --source-digest $commit"
  echo "gh attestation verify oci://$frontend -R $repo --source-digest $frontend_commit"
  echo "# Rebuild them from the commit, byte for byte (docs/reproducible-builds.md):"
  echo "git checkout $commit && .github/scripts/rebuild-image.sh server && .github/scripts/rebuild-image.sh client"
  echo '```'
  echo
  if [ -n "$previous" ]; then
    echo "### Since $previous"
    gh api "repos/$repo/releases/generate-notes" -f tag_name="$tag" -f target_commitish="$commit" \
      -f previous_tag_name="$previous" --jq .body
  else
    echo "The first $environment release."
  fi
} > "$notes"

manifest=$(mktemp -d)/release.json
jq -n --arg environment "$environment" --arg tag "$tag" --arg commit "$commit" --arg frontend_commit "$frontend_commit" \
  --arg backend "$backend" --arg frontend "$frontend" --arg from "$FROM" --arg run "$RUN_URL" --arg actor "$ACTOR" \
  '{environment: $environment, tag: $tag, docker_tag: $tag, commit: $commit, promoted_from: $from, run: $run, actor: $actor,
    images: {backend: {image: ($backend | split("@")[0]), digest: ($backend | split("@")[1]), commit: $commit},
             frontend: {image: ($frontend | split("@")[0]), digest: ($frontend | split("@")[1]), commit: $frontend_commit}}}' \
  > "$manifest"

prerelease=true latest=false
if [ "$environment" = prod ]; then prerelease=false latest=true; fi
id=$(gh api "repos/$repo/releases" -X POST -f tag_name="$tag" -f target_commitish="$commit" \
  -f name="${environment^} $date$([ "$n" = 1 ] || echo " ($n)")" -F body=@"$notes" \
  -F draft=true -F prerelease="$prerelease" -f make_latest="$latest" --jq .id)
gh api -X POST "https://uploads.github.com/repos/$repo/releases/$id/assets?name=release.json" \
  -H "Content-Type: application/json" --input "$manifest" > /dev/null
url=$(gh api "repos/$repo/releases/$id" -X PATCH -F draft=false -f make_latest="$latest" --jq .html_url)
echo "Released $tag: $url"
echo "- release [\`$tag\`]($url)" >> "${GITHUB_STEP_SUMMARY:-/dev/null}"
