#!/usr/bin/env bash
# Completes a test report's check run that dorny/test-reporter left "in progress": GitHub sometimes answers the
# reporter's own update with 404 in the seconds after it created the run, the step's continue-on-error carries the job
# on, and nothing else ever completes the run, so the PR waits on a check that will never finish. Run only when the
# report step failed: a run of that name still in progress gets the test step's own outcome (success, or failure for
# anything else: failed, cancelled, skipped); one already completed, or none at all, is left alone. If completing it
# fails too, so does this, and the job: a red check can be rerun, a pending one only sits there.
#
# Env: GH_TOKEN (checks: write), REPO (owner/name), SHA (the commit the report is on), NAME (the report's check name),
# OUTCOME (the test step's steps.<id>.outcome); REPORT_RETRY_SECONDS overrides the wait between tries (the tests: 0).
set -euo pipefail

# GitHub's API, tried again over about a minute: its 404s right after a run is created, and its 5xx, pass
retry() {
  local attempt
  for attempt in 1 2 3 4 5 6; do
    "$@" && return 0
    [[ "$attempt" = 6 ]] || sleep "${REPORT_RETRY_SECONDS:-$((attempt * 4))}"
  done
  return 1
}

# Every run of that name on the commit (filter=all: the default, latest, can return an earlier attempt's completed run
# and hide a rerun's stuck one); the newest still in progress, if any
find_stuck() {
  gh api -X GET "repos/$REPO/commits/$SHA/check-runs" -f check_name="$NAME" -f filter=all -f per_page=100 \
    --jq '[.check_runs[] | select(.status != "completed")] | max_by(.id) | .id // empty'
}
id=$(retry find_stuck) || { echo "::error::$NAME: its check runs couldn't be read: rerun this job"; exit 1; }
if [[ -z "$id" ]]; then
  echo "$NAME: nothing stuck"
  exit 0
fi

conclusion=failure
[[ "$OUTCOME" = success ]] && conclusion=success
echo "$NAME: check run $id is still in progress; completing it as $conclusion (the tests' outcome: $OUTCOME)"
complete() {
  gh api -X PATCH "repos/$REPO/check-runs/$id" -f status=completed -f conclusion="$conclusion" \
    -f "output[title]=Tests $conclusion" \
    -f "output[summary]=The test report couldn't update this check itself (GitHub answered it with an error), so the workflow completed it with the test step's outcome: $OUTCOME. The full results are in the run's test-results artifact." \
    > /dev/null
}
retry complete || { echo "::error::$NAME: check run $id is stuck in progress and couldn't be completed: rerun this job"; exit 1; }
echo "$NAME: completed as $conclusion"
