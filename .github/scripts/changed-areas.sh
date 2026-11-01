#!/usr/bin/env bash
# Which parts of the repo a run has to test, for the test workflows to skip what a PR can't affect: backend=true when
# the .NET code (or its build or test setup) changed, frontend=true when the Angular client did. Anything that isn't a
# pull request (a push to master, a manual run) tests everything, and so does a PR whose file list can't be read.
# The end-to-end tests don't ask: they run on every PR.
#
# Env: GITHUB_EVENT_NAME, GITHUB_REPOSITORY, PR (the PR number), GH_TOKEN. Writes backend= and frontend= to $GITHUB_OUTPUT.
set -euo pipefail

out="${GITHUB_OUTPUT:-/dev/stdout}"
everything() {
  echo "backend=true" >> "$out"
  echo "frontend=true" >> "$out"
  echo "Testing everything: $1"
  exit 0
}

[ "${GITHUB_EVENT_NAME:-}" = "pull_request" ] || everything "not a pull request (${GITHUB_EVENT_NAME:-unknown})"
files=$(gh api --paginate "repos/$GITHUB_REPOSITORY/pulls/$PR/files" --jq '.[].filename') || everything "the PR's files could not be listed"
count=$(printf '%s\n' "$files" | grep -c . || true)
# The files API stops at 3000: a PR that big may have changed anything
[ "$count" -lt 3000 ] || everything "$count files is more than the API lists"

# This script and the workflows that use it test both sides: a change to them is checked by a full run
shared='^\.github/scripts/changed-areas\.sh$'
backend="$shared|^GrifballWebApp\.|^DiscordInterface|\.slnx$|^coverlet\.runsettings$|^\.github/workflows/dotnet-tests\.yml$"
frontend="$shared|^grifballwebapp\.client/|^\.github/workflows/frontend-tests\.yml$"

has() { printf '%s\n' "$files" | grep -Eq "$1" && echo true || echo false; }
echo "backend=$(has "$backend")" >> "$out"
echo "frontend=$(has "$frontend")" >> "$out"
echo "$count files changed: backend $(has "$backend"), frontend $(has "$frontend")"
