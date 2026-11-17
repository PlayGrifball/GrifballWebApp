#!/usr/bin/env bash
# env-deploy.sh <environment> <backend image@digest> <frontend image@digest>
#
# Deploys an environment: test after every master build (docker-publish.yml), staging and prod on a promotion
# (promote.yml). The homelab reads this chart from git at the tag env/<environment>/current, so moving that tag deploys
# the chart and the images together, in one sync. The tag points at a deploy commit: the commit the images were built
# from (their revision label, as env-release.sh tags its release), plus charts/grifballwebapp/promoted-images.yaml,
# which names both images by digest. The chart is always the one written with the code it runs, and the images can't
# arrive before or after it. The commit is on no branch; its tags keep it: env/<environment>/<date>, which never
# moves, and env/<environment>/current.
# Env: GH_TOKEN (contents: write), GITHUB_REPOSITORY. Outputs deploy_commit and deploy_tag to $GITHUB_OUTPUT.
set -euo pipefail

environment=${1:?usage: $0 <environment> <backend@digest> <frontend@digest>}
backend=${2:?} frontend=${3:?}
repo=${GITHUB_REPOSITORY:?}
here=$(cd "$(dirname "$0")" && pwd)

commit=$("$here/image-revision.sh" "$backend")
[[ -n "$commit" ]] || { echo "::error::$backend has no org.opencontainers.image.revision label" >&2; exit 1; }
frontend_commit=$("$here/image-revision.sh" "$frontend")
if [[ "$frontend_commit" != "$commit" ]]; then
  echo "::warning::The frontend image was built from $frontend_commit, the backend from $commit: deploying the chart at the backend's"
fi

# <environment>@sha256:...: the tag says which promotion, the digest is what runs (as the Image Updater wrote it)
images=$(printf '%s\n' \
  "# The images deployed to $environment with this chart (env-deploy.sh), by digest." \
  'backend:' '  image:' "    repository: ${backend%@*}" "    tag: $environment@${backend#*@}" \
  'frontend:' '  image:' "    repository: ${frontend%@*}" "    tag: $environment@${frontend#*@}")

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
base_tree=$(gh api "repos/$repo/git/commits/$commit" --jq .tree.sha)
jq -cn --arg base "$base_tree" --arg content "$images"$'\n' \
  '{base_tree: $base, tree: [{path: "charts/grifballwebapp/promoted-images.yaml", mode: "100644", type: "blob", content: $content}]}' \
  > "$work/tree.json"
tree=$(gh api "repos/$repo/git/trees" -X POST --input "$work/tree.json" --jq .sha)
jq -cn --arg tree "$tree" --arg parent "$commit" \
  --arg message "Deploy $environment: $commit with backend ${backend#*@}, frontend ${frontend#*@}" \
  '{message: $message, tree: $tree, parents: [$parent]}' > "$work/commit.json"
deploy=$(gh api "repos/$repo/git/commits" -X POST --input "$work/commit.json" --jq .sha)

# A dated tag that never moves (env/<environment>/<UTC date>, .2, .3 for more that day): what the environment ran, and
# since when. Then env/<environment>/current moves to it - moved, not deleted and made again, so a deploy never leaves
# the environment without its tag. (Not env/<environment>: a ref can't be both a tag and a folder of tags.)
dated="env/$environment/$(date -u +%Y-%m-%d)"
n=1
while gh api "repos/$repo/git/ref/tags/$dated$([[ "$n" = 1 ]] || echo ".$n")" > /dev/null 2>&1; do n=$((n + 1)); done
[[ "$n" = 1 ]] || dated="$dated.$n"
gh api "repos/$repo/git/refs" -X POST -f ref="refs/tags/$dated" -f sha="$deploy" --jq .object.sha > /dev/null
if gh api "repos/$repo/git/ref/tags/env/$environment/current" > /dev/null 2>&1; then
  gh api "repos/$repo/git/refs/tags/env/$environment/current" -X PATCH -f sha="$deploy" -F force=true --jq .object.sha > /dev/null
else
  gh api "repos/$repo/git/refs" -X POST -f ref="refs/tags/env/$environment/current" -f sha="$deploy" --jq .object.sha > /dev/null
fi

echo "Deployed $environment: env/$environment/current and $dated -> $deploy (the chart at $commit, with the promoted images)"
printf "deploy_commit=%s\ndeploy_tag=%s\n" "$deploy" "$dated" >> "${GITHUB_OUTPUT:-/dev/null}"
echo "- deployed \`env/$environment/current\` (\`$dated\`) at [\`${deploy:0:7}\`](https://github.com/$repo/commit/$deploy)" >> "${GITHUB_STEP_SUMMARY:-/dev/null}"
