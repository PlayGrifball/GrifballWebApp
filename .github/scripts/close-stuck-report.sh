#!/usr/bin/env bash
# Completes a test report's check run that dorny/test-reporter left "in progress": GitHub sometimes answers the
# reporter's own update with 404 seconds after it created the run, the step's continue-on-error carries the job on, and
# nothing else ever completes the run, so the PR waits on a check that will never finish. Run after the report step
# (always): a report still in progress gets the test step's own outcome (success, or failure for anything else); one
# already completed, or none at all, is left alone. If completing it fails too, so does this, and the job: a red check
# can be rerun, a pending one only sits there.
#
# Env: GH_TOKEN (checks: write), REPO (owner/name), SHA (the commit the report is on), NAME (the report's check name),
# OUTCOME (the test step's steps.<id>.outcome); REPORT_RETRY_SECONDS overrides the wait between tries (the tests: 0).
set -euo pipefail

runs=$(gh api "repos/$REPO/commits/$SHA/check-runs?check_name=$(node -p 'encodeURIComponent(process.argv[1])' "$NAME")&per_page=100")
# The newest run of that name (a rerun makes another), if it isn't completed
id=$(node -e '
  const runs = JSON.parse(process.argv[1]).check_runs ?? [];
  const newest = runs.reduce((a, b) => (b.id > (a?.id ?? -1) ? b : a), undefined);
  if (newest && newest.status !== "completed") console.log(newest.id);
' "$runs")
if [[ -z "$id" ]]; then
  echo "$NAME: nothing stuck"
  exit 0
fi

conclusion=failure
[[ "$OUTCOME" = success ]] && conclusion=success
echo "$NAME: check run $id is still in progress; completing it as $conclusion (the tests' outcome: $OUTCOME)"
# GitHub 404s updates to a run for a few seconds after creating it (the reporter's own update is refused that way):
# tried again over about a minute before giving up
for attempt in 1 2 3 4 5 6; do
  if gh api -X PATCH "repos/$REPO/check-runs/$id" -f status=completed -f conclusion=$conclusion \
    -f "output[title]=Tests $conclusion" \
    -f "output[summary]=The test report couldn't update this check itself (GitHub answered it with an error), so the workflow completed it with the test step's outcome: $OUTCOME. The full results are in the run's test-results artifact." \
    > /dev/null; then
    echo "$NAME: completed as $conclusion (attempt $attempt)"
    exit 0
  fi
  [[ "$attempt" = 6 ]] || sleep "${REPORT_RETRY_SECONDS:-$((attempt * 4))}"
done
echo "::error::$NAME: check run $id is stuck in progress and couldn't be completed: rerun this job"
exit 1
