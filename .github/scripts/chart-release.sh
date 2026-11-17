#!/usr/bin/env bash
# chart-release.sh <version> <digest>
#
# The GitHub release of a published chart version (helm-chart.yml's publish job): tag grifballwebapp-<version> at the
# commit it was packaged from, so tools that read a chart's release notes from its source repository (Renovate, on a
# chart update) find them: Renovate matches a release tagged <chart name>-<version>, the chart-releaser convention.
# Its notes: the chart's OCI reference and digest, how to verify its signature, and the chart's changes since the
# previous chart release, one line per master commit that changed it (a merged PR by its title and number). The first
# chart release lists the chart's last 20 changes. Never the repository's latest release (that's prod's,
# env-release.sh) and not a pre-release. A version already released (a re-run of the publish job) is left alone: with
# the repository's release immutability on, a published release can't change anyway.
# Env: GH_TOKEN (contents: write), GITHUB_REPOSITORY, CHART (the chart's directory), CHART_REPO (oci://<registry>/<path>),
# IDENTITY (the signing workflow's certificate identity, a regular expression), TARGET (the commit; default HEAD).
set -euo pipefail

version=${1:?usage: $0 <version> <digest>}
digest=${2:?usage: $0 <version> <digest>}
repo=${GITHUB_REPOSITORY:?}
chart=${CHART:?} chart_repo=${CHART_REPO:?} identity=${IDENTITY:?}
commit=$(git rev-parse "${TARGET:-HEAD}^{commit}")
name=$(basename "$chart")
tag="$name-$version"
url="https://github.com/$repo"

if gh api "repos/$repo/releases/tags/$tag" > /dev/null 2>&1; then
  echo "$tag is already released; leaving it."
  exit 0
fi

# The previous chart release: the newest grifballwebapp-* tag in the commit's history (not this one, on a re-run whose
# release failed after its tag was made).
previous=$(git describe --tags --abbrev=0 --match "$name-*" --exclude "$tag" "$commit" 2> /dev/null || true)

# One line per master commit (first parent: a merged PR is its merge commit) that changed the chart:
# a merge commit by its PR's title (the message's body) and number; a squash merge's (#N) linked; then the commit.
changes() {
  git log --first-parent --format=%H "$@" -- "$chart" \
    | while read -r sha; do
        subject=$(git log -1 --format=%s "$sha")
        line=$subject
        if [[ "$(git log -1 --format=%P "$sha")" = *" "* && "$subject" =~ ^Merge\ pull\ request\ \#([0-9]+) ]]; then
          pr=${BASH_REMATCH[1]}
          title=$(git log -1 --format=%b "$sha" | sed -n '/[^[:space:]]/{p;q;}')
          line="${title:-$subject} ([#$pr]($url/pull/$pr))"
        elif [[ "$subject" =~ ^(.*)\ \(#([0-9]+)\)$ ]]; then
          line="${BASH_REMATCH[1]} ([#${BASH_REMATCH[2]}]($url/pull/${BASH_REMATCH[2]}))"
        fi
        echo "- $line ([\`${sha:0:7}\`]($url/commit/$sha))"
      done
}

ref="${chart_repo#oci://}/$name"
notes=$(mktemp)
{
  echo "\`$chart_repo/$name:$version\`, digest \`$digest\`."
  echo
  echo '```sh'
  echo "helm pull $chart_repo/$name --version $version"
  echo "cosign verify $ref@$digest --certificate-identity-regexp '$identity' --certificate-oidc-issuer https://token.actions.githubusercontent.com"
  echo '```'
  echo
  if [[ -n "$previous" ]]; then
    echo "### Since $previous"
    echo
    echo "[Compare]($url/compare/$previous...$tag)"
    echo
    listed=$(changes "$previous..$commit")
    echo "${listed:-No chart changes (republished).}"
  else
    echo "The chart's first tagged release. Its last 20 changes:"
    echo
    changes -n 20 "$commit"
  fi
} > "$notes"

# The tag is made with the release, at the commit.
gh api "repos/$repo/releases" -X POST -f tag_name="$tag" -f target_commitish="$commit" -f name="$name $version" \
  -F body=@"$notes" -F draft=false -F prerelease=false -f make_latest=false --jq .html_url
echo "- chart release [\`$tag\`]($url/releases/tag/$tag)" >> "${GITHUB_STEP_SUMMARY:-/dev/null}"
