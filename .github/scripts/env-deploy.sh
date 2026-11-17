#!/usr/bin/env bash
# env-deploy.sh <environment> <backend image@digest> <frontend image@digest>
#
# Deploys an environment: test after every master build (docker-publish.yml), staging and prod on a promotion
# (promote.yml). The homelab reads this chart from git at the tag env/<environment>/current, so moving that tag deploys
# the chart and the images together, in one sync. The tag points at a deploy commit: the commit the images were built
# from (their revision label, as env-release.sh tags its release), plus charts/grifballwebapp/promoted-images.yaml,
# which names both images by digest. The chart is always the one written with the code it runs, and the images can't
# arrive before or after it. The commit is on no branch; its tags keep it: env/<environment>/current, and for staging
# and prod env/<environment>/<date>, which never moves (test has no dated tags: it deploys every master build).
#
# - The same images already deployed (env/<environment>/current's promoted-images.yaml names them: a re-run, or a
#   second promotion of the same images): that deploy is reused, nothing new is made.
# - test only moves forward: master's runs finish in any order, so when this commit isn't ahead of the one
#   env/test/current was built on, nothing changes and nothing is output. After moving it, it checks again that no
#   older run's deploy landed after this one (as the :test retag does), and moves it forward again if one did.
# - staging and prod move to whatever was promoted (promoting older images is a rollback), with a notice when that
#   isn't ahead.
# Env: GH_TOKEN (contents: write), GITHUB_REPOSITORY; RUN_URL (the run, named in the deploy commit's message, which
# env-run-images.sh reads on a re-run); DEPLOY_SETTLE_SECONDS (test: the wait before checking again, 20).
# Outputs commit and frontend_commit (the images' revisions, for env-release.sh), deploy_commit, and deploy_tag (the
# dated tag; empty for test) to $GITHUB_OUTPUT.
set -euo pipefail

environment=${1:?usage: $0 <environment> <backend@digest> <frontend@digest>}
backend=${2:?} frontend=${3:?}
repo=${GITHUB_REPOSITORY:?}
here=$(cd "$(dirname "$0")" && pwd)
settle=${DEPLOY_SETTLE_SECONDS:-20}
tags="refs/tags/env/$environment"

commit=$("$here/image-revision.sh" "$backend")
[[ -n "$commit" ]] || { echo "::error::$backend has no org.opencontainers.image.revision label" >&2; exit 1; }
frontend_commit=$("$here/image-revision.sh" "$frontend")
if [[ "$frontend_commit" != "$commit" ]]; then
  echo "::warning::The frontend image was built from $frontend_commit, the backend from $commit: deploying the chart at the backend's"
fi

# <environment>@sha256:...: the tag says which environment, the digest is what runs (as the Image Updater wrote it)
images=$(printf '%s\n' \
  "# The images deployed to $environment with this chart (env-deploy.sh), by digest." \
  'backend:' '  image:' "    repository: ${backend%@*}" "    tag: $environment@${backend#*@}" \
  'frontend:' '  image:' "    repository: ${frontend%@*}" "    tag: $environment@${frontend#*@}")

# The images' commit a deploy commit was built on
parent_of() { gh api "repos/$repo/git/commits/$1" --jq '.parents[0].sha'; }
# This commit against another: ahead (comes after it), identical, behind or diverged
against() { if [[ "$1" = "$commit" ]]; then echo identical; else gh api "repos/$repo/compare/$1...$commit" --jq .status; fi; }
move_current() { # move_current <exists>: points env/<environment>/current at the deploy - moved, never deleted and remade
  if [[ "$1" = true ]]; then
    gh api "repos/$repo/git/refs/tags/env/$environment/current" -X PATCH -f sha="$deploy" -F force=true --jq .object.sha > /dev/null
  else
    gh api "repos/$repo/git/refs" -X POST -f ref="$tags/current" -f sha="$deploy" --jq .object.sha > /dev/null
  fi
}

# Every env/<environment>/ tag, in one call: current, and the dated ones. (Not env/<environment>: a ref can't be both a
# tag and a folder of tags.)
refs=$(gh api --paginate "repos/$repo/git/matching-refs/tags/env/$environment/" --jq '.[] | "\(.ref) \(.object.sha)"')
current=$(awk -v ref="$tags/current" '$1 == ref { print $2 }' <<< "$refs")

deploy=""
if [[ -n "$current" ]]; then
  deployed=$(gh api "repos/$repo/contents/charts/grifballwebapp/promoted-images.yaml?ref=$current" --jq .content 2> /dev/null \
    | base64 -d) || deployed=""
  if [[ "$deployed" = "$images" ]]; then
    echo "env/$environment/current ($current) already deploys these images: reusing it."
    deploy=$current
  else
    on=$(parent_of "$current")
    status=$(against "$on")
    if [[ "$status" != ahead && "$status" != identical ]]; then
      if [[ "$environment" = test ]]; then
        echo "::notice::env/test/current stays on ${on:0:7}: this run's ${commit:0:7} is $status of it"
        echo "- \`env/test/current\` stays on \`${on:0:7}\` (\`${commit:0:7}\` is $status of it)" >> "${GITHUB_STEP_SUMMARY:-/dev/null}"
        exit 0
      fi
      echo "::notice::env/$environment/current moves back: ${commit:0:7} is $status of ${on:0:7}, which it deployed"
    fi
  fi
fi

if [[ -z "$deploy" ]]; then
  work=$(mktemp -d)
  trap 'rm -rf "$work"' EXIT
  base_tree=$(gh api "repos/$repo/git/commits/$commit" --jq .tree.sha)
  jq -cn --arg base "$base_tree" --arg content "$images"$'\n' \
    '{base_tree: $base, tree: [{path: "charts/grifballwebapp/promoted-images.yaml", mode: "100644", type: "blob", content: $content}]}' \
    > "$work/tree.json"
  tree=$(gh api "repos/$repo/git/trees" -X POST --input "$work/tree.json" --jq .sha)
  message="Deploy $environment: $commit with backend ${backend#*@}, frontend ${frontend#*@}"
  [[ -z "${RUN_URL:-}" ]] || message+=$'\n\n'"Run: $RUN_URL"
  jq -cn --arg tree "$tree" --arg parent "$commit" --arg message "$message" \
    '{message: $message, tree: $tree, parents: [$parent]}' > "$work/commit.json"
  deploy=$(gh api "repos/$repo/git/commits" -X POST --input "$work/commit.json" --jq .sha)
fi

# Staging and prod: a dated tag that never moves (env/<environment>/<UTC date>, .2, .3 for more that day), what the
# environment ran and since when. A reused deploy keeps the one it has.
dated=""
if [[ "$environment" != test ]]; then
  dated=$(awk -v sha="$deploy" -v ref="$tags/current" '$2 == sha && $1 != ref { print $1 }' <<< "$refs" | sort -V | tail -n 1)
  dated=${dated#refs/tags/}
  if [[ -z "$dated" ]]; then
    day="env/$environment/$(date -u +%Y-%m-%d)"
    # The highest of today's: the date alone is 1, .N is N
    taken=$(awk '{ print $1 }' <<< "$refs" | sed -n "s|^refs/tags/$day||p" \
      | awk '$0 == "" { if (n < 1) n = 1 } /^\.[0-9]+$/ { k = substr($0, 2) + 0; if (k > n) n = k } END { print n + 0 }')
    dated=$day
    [[ "$taken" = 0 ]] || dated="$day.$((taken + 1))"
    gh api "repos/$repo/git/refs" -X POST -f ref="refs/tags/$dated" -f sha="$deploy" --jq .object.sha > /dev/null
  fi
fi

if [[ "$deploy" != "$current" ]]; then
  move_current "$([[ -n "$current" ]] && echo true || echo false)"
  if [[ "$environment" = test ]]; then
    # An older run that read env/test/current before this one moved it may move it after: check again, as the retag does
    for _ in 2 3 4 5; do
      sleep "$settle"
      now=$(gh api "repos/$repo/git/ref/tags/env/test/current" --jq .object.sha)
      [[ "$now" != "$deploy" ]] || break
      on=$(parent_of "$now")
      status=$(against "$on")
      if [[ "$status" != ahead ]]; then
        echo "env/test/current moved on to $now (${on:0:7}): this run's ${commit:0:7} is $status of it; leaving it there."
        break
      fi
      echo "An older deploy ($now, ${on:0:7}) landed after this one: moving env/test/current forward again."
      move_current true
    done
  fi
fi

echo "Deployed $environment: env/$environment/current${dated:+ and $dated} -> $deploy (the chart at $commit, with the promoted images)"
printf "commit=%s\nfrontend_commit=%s\ndeploy_commit=%s\ndeploy_tag=%s\n" "$commit" "$frontend_commit" "$deploy" "$dated" \
  >> "${GITHUB_OUTPUT:-/dev/null}"
echo "- deployed \`env/$environment/current\`${dated:+ (\`$dated\`)} at [\`${deploy:0:7}\`](https://github.com/$repo/commit/$deploy)" \
  >> "${GITHUB_STEP_SUMMARY:-/dev/null}"
