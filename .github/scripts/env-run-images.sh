#!/usr/bin/env bash
# env-run-images.sh <environment>
#
# On a re-run of a promotion (promote.yml): the images its earlier attempt already deployed, so the re-run promotes,
# deploys and releases those rather than reading :<from> again, which may have moved since. They are what
# env/<environment>/current's promoted-images.yaml names, when its deploy commit's message says this run made it
# (env-deploy.sh writes "Run: <RUN_URL>"). Prints backend=<image@digest> and frontend=<image@digest>, or nothing
# when this run deployed nothing (or a later promotion has deployed since).
# Env: GH_TOKEN, GITHUB_REPOSITORY, RUN_URL.
set -euo pipefail

environment=${1:?usage: $0 <environment>}
repo=${GITHUB_REPOSITORY:?} run=${RUN_URL:?}

current=$(gh api "repos/$repo/git/ref/tags/env/$environment/current" --jq .object.sha 2> /dev/null) || exit 0
gh api "repos/$repo/git/commits/$current" --jq .message | grep -qxF "Run: $run" || exit 0
gh api "repos/$repo/contents/charts/grifballwebapp/promoted-images.yaml?ref=$current" --jq .content | base64 -d \
  | awk '/^[a-z]+:$/ { side = substr($1, 1, length($1) - 1) }
         $1 == "repository:" { image[side] = $2 }
         $1 == "tag:" { sub(/^[^@]*@/, "", $2); print side "=" image[side] "@" $2 }'
