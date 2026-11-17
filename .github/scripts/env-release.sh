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
# A re-run of the promotion releases once: a published release of this run (its notes link RUN_URL) ends it, and an
# unfinished draft of this run is deleted and made again.
# Env: GH_TOKEN (contents: write), GITHUB_REPOSITORY, FROM (the tag promoted from), RUN_URL, ACTOR; COMMIT and
# FRONTEND_COMMIT, the images' revisions when env-deploy.sh already read them; DEPLOY_COMMIT and DEPLOY_TAG, the deploy
# env-deploy.sh made (the chart at the commit, with these images) and its dated tag, when it ran first; CHART_REPO, where
# helm-chart.yml publishes the chart (oci://ghcr.io/playgrifball/charts).
set -euo pipefail

environment=${1:?usage: $0 <environment> <backend@digest> <frontend@digest>}
backend=${2:?} frontend=${3:?}
repo=${GITHUB_REPOSITORY:?}

# A re-run: this run's release published (nothing to do), or left a draft (made again)
mine=$(gh api "repos/$repo/releases" --paginate \
  --jq ".[] | select((.body // \"\") | contains(\"([run]($RUN_URL))\")) | \"\(.id) \(.draft) \(.tag_name)\"")
while read -r id draft tag; do
  [[ -n "$id" ]] || continue
  if [[ "$draft" = false ]]; then echo "This run already released $tag; nothing to do."; exit 0; fi
  echo "Deleting this run's unfinished draft release ($tag) to make it again."
  gh api "repos/$repo/releases/$id" -X DELETE
done <<< "$mine"

# The commit an image was built from: its revision label (given by env-deploy.sh's outputs, or read here)
revision() { "$(dirname "$0")/image-revision.sh" "$1"; }
commit=${COMMIT:-$(revision "$backend")}
frontend_commit=${FRONTEND_COMMIT:-$(revision "$frontend")}
[[ -n "$commit" ]] || { echo "::error::$backend has no org.opencontainers.image.revision label" >&2; exit 1; }
if [[ "$frontend_commit" != "$commit" ]]; then
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

# The chart: what the environment runs is charts/grifballwebapp at the images' commit (env-deploy.sh), compared with the
# newest published chart version that commit includes (helm-chart.yml's grifballwebapp-<version> tags): the same chart,
# or changed since. By the chart directory's tree, so a publish of an unchanged chart counts as the same.
chart_repo=${CHART_REPO:-oci://ghcr.io/playgrifball/charts}
chart_tree() { gh api "repos/$repo/contents/charts?ref=$1" --jq '.[] | select(.name == "grifballwebapp") | .sha'; }
published=""
for t in $(gh api --paginate "repos/$repo/git/matching-refs/tags/grifballwebapp-" --jq '.[].ref | ltrimstr("refs/tags/")' \
  | sort -V -r); do
  status=$(gh api "repos/$repo/compare/$t...$commit" --jq .status)
  if [[ "$status" = ahead || "$status" = identical ]]; then published=${t#grifballwebapp-}; break; fi
done
same_chart=false
if [[ -n "$published" && "$(chart_tree "grifballwebapp-$published")" = "$(chart_tree "$commit")" ]]; then same_chart=true; fi

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
  [[ "$frontend_commit" = "$commit" ]] || echo "(The frontend image was built from \`$frontend_commit\`.)"
  echo
  echo "### Chart"
  echo "\`charts/grifballwebapp\` at [\`${commit:0:7}\`](https://github.com/$repo/commit/$commit): the chart written with these images, deployed with them."
  echo
  if [[ "$same_chart" = true ]]; then
    echo "The same chart as the published \`grifballwebapp-$published\` (\`helm pull $chart_repo/grifballwebapp --version $published\`)."
  elif [[ -n "$published" ]]; then
    echo "Changed since the published \`grifballwebapp-$published\`: [what changed](https://github.com/$repo/compare/grifballwebapp-$published...$commit) (charts/grifballwebapp)."
  else
    echo "No chart version was published before this commit."
  fi
  if [[ -n "${DEPLOY_COMMIT:-}" ]]; then
    echo
    echo "Deployed as [\`${DEPLOY_COMMIT:0:7}\`](https://github.com/$repo/commit/$DEPLOY_COMMIT), tagged \`$DEPLOY_TAG\`: this chart with these images (charts/grifballwebapp/promoted-images.yaml). To render it as deployed, with the environment's own values:"
    echo '```sh'
    echo "git checkout $DEPLOY_TAG && helm dependency build charts/grifballwebapp"
    echo "helm template grif charts/grifballwebapp -f <the environment's values> -f charts/grifballwebapp/promoted-images.yaml"
    echo '```'
  fi
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
  if [[ -n "$previous" ]]; then
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
  --arg deploy_commit "${DEPLOY_COMMIT:-}" --arg deploy_tag "${DEPLOY_TAG:-}" \
  --arg published "$published" --argjson same_chart "$same_chart" \
  '{environment: $environment, tag: $tag, docker_tag: $tag, commit: $commit, promoted_from: $from, run: $run, actor: $actor,
    deploy: (if $deploy_commit == "" then null else {commit: $deploy_commit, tag: $deploy_tag} end),
    chart: {commit: $commit, published: (if $published == "" then null else $published end), same_as_published: $same_chart},
    images: {backend: {image: ($backend | split("@")[0]), digest: ($backend | split("@")[1]), commit: $commit},
             frontend: {image: ($frontend | split("@")[0]), digest: ($frontend | split("@")[1]), commit: $frontend_commit}}}' \
  > "$manifest"

prerelease=true latest=false
if [[ "$environment" = prod ]]; then prerelease=false latest=true; fi
id=$(gh api "repos/$repo/releases" -X POST -f tag_name="$tag" -f target_commitish="$commit" \
  -f name="${environment^} $date$([[ "$n" = 1 ]] || echo " ($n)")" -F body=@"$notes" \
  -F draft=true -F prerelease="$prerelease" -f make_latest="$latest" --jq .id)
gh api -X POST "https://uploads.github.com/repos/$repo/releases/$id/assets?name=release.json" \
  -H "Content-Type: application/json" --input "$manifest" > /dev/null
url=$(gh api "repos/$repo/releases/$id" -X PATCH -F draft=false -f make_latest="$latest" --jq .html_url)
echo "Released $tag: $url"
echo "- release [\`$tag\`]($url)" >> "${GITHUB_STEP_SUMMARY:-/dev/null}"
