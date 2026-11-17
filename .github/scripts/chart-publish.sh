#!/usr/bin/env bash
# chart-publish.sh <version>
#
# Publishes the chart as <version> and signs it (helm-chart.yml's publish job, logged in to the registry), then
# outputs version and digest to $GITHUB_OUTPUT for the release job (chart-release.sh). A re-run of the run reuses its
# number, so its version may already be published: never overwritten, but signed if this workflow's signature isn't on
# it yet (the first attempt failed between push and sign), so a release never names an unsigned chart. Either way
# nothing is output until cosign verifies the digest against this workflow's identity.
# Env: CHART (the chart's directory), CHART_REPO (oci://<registry>/<path>), IDENTITY (the signing workflow's
# certificate identity, a regular expression).
set -euo pipefail

version=${1:?usage: $0 <version>}
chart=${CHART:?} chart_repo=${CHART_REPO:?} identity=${IDENTITY:?}
name=$(basename "$chart")
issuer=https://token.actions.githubusercontent.com
verify() { cosign verify "$1" --certificate-identity-regexp "$identity" --certificate-oidc-issuer "$issuer" > /dev/null; }
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

if helm show chart "$chart_repo/$name" --version "$version" > /dev/null 2>&1; then
  echo "$name $version is already published; not overwriting it."
  # helm v4 pulls only into a directory that exists
  mkdir -p "$work/pulled"
  digest=$(helm pull "$chart_repo/$name" --version "$version" -d "$work/pulled" 2>&1 | sed -n 's/^Digest: *//p')
  test -n "$digest"
  ref="${chart_repo#oci://}/$name@$digest"
  if verify "$ref"; then
    echo "$ref is signed by this workflow."
  else
    echo "$ref has no signature from this workflow: signing it."
    cosign sign --yes "$ref"
    verify "$ref"
  fi
else
  helm package "$chart" --version "$version" -d dist
  helm push "dist/$name-$version.tgz" "$chart_repo" 2>&1 | tee "$work/push.txt"
  digest=$(sed -n 's/^Digest: *//p' "$work/push.txt")
  test -n "$digest"
  ref="${chart_repo#oci://}/$name@$digest"
  cosign sign --yes "$ref"
  verify "$ref"
fi

{ echo "version=$version"; echo "digest=$digest"; } >> "${GITHUB_OUTPUT:-/dev/null}"
{
  echo "Published and signed $chart_repo/$name:$version ($digest)."
  echo
  echo "Verify: cosign verify ${ref%@*}:$version --certificate-identity-regexp '$identity' --certificate-oidc-issuer $issuer"
} >> "${GITHUB_STEP_SUMMARY:-/dev/null}"
