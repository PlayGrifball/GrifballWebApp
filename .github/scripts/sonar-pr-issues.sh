#!/usr/bin/env bash
# sonar-pr-issues.sh: fails a pull request that SonarCloud finds any open issue in, listing each one.
#
# The SonarCloud workflow runs it after `dotnet sonarscanner end`, which only uploads the report: SonarCloud then
# analyses it in a background task. This waits for that task (its id is ceTaskId in the scanner's report-task.txt),
# then asks for the PR's open issues. The quality gate is SonarCloud's to set; this holds every PR to zero new issues
# without it, and puts each one on the PR's diff as an annotation.
#
# Env: PR (the PR number), SONAR_PROJECT_KEY. Optional: SONAR_TOKEN (sent to the task API when set; never to the issues
# API, which answers a token's request with a 400 and the public project's without one), SONAR_HOST_URL (https://sonarcloud.io), SONAR_REPORT_TASK
# (.sonarqube/out/.sonar/report-task.txt), SONAR_WAIT_SECONDS (600), SONAR_POLL_SECONDS (5).
set -euo pipefail

host="${SONAR_HOST_URL:-https://sonarcloud.io}"
report="${SONAR_REPORT_TASK:-.sonarqube/out/.sonar/report-task.txt}"
wait_seconds="${SONAR_WAIT_SECONDS:-600}"
poll_seconds="${SONAR_POLL_SECONDS:-5}"
: "${PR:?PR (the pull request number) is not set}"
: "${SONAR_PROJECT_KEY:?SONAR_PROJECT_KEY is not set}"

fail() {
  echo "::error title=SonarCloud check::$1"
  exit 1
}

api() { # api <path and query> [token]: the response body; fails on an HTTP error
  local auth=()
  [[ -n "${2:-}" ]] && auth=(--user "$2:")
  curl --silent --show-error --fail --retry 3 "${auth[@]}" "$host/api/$1"
}

[[ -f "$report" ]] || fail "No $report: the scanner's end step didn't upload an analysis."
task=$(sed -n 's/^ceTaskId=//p' "$report" | tr -d '\r')
[[ -n "$task" ]] || fail "$report has no ceTaskId."

# The background task: PENDING, then IN_PROGRESS, then SUCCESS (or FAILED or CANCELED)
waited=0
while :; do
  status=$(api "ce/task?id=$task" "${SONAR_TOKEN:-}" | jq -r '.task.status') || fail "Could not read the analysis task $task."
  case "$status" in
    SUCCESS) break ;;
    PENDING|IN_PROGRESS) ;;
    *) fail "SonarCloud's analysis task $task ended $status." ;;
  esac
  (( waited < wait_seconds )) || fail "SonarCloud's analysis task $task was still $status after ${wait_seconds}s."
  sleep "$poll_seconds"
  waited=$((waited + poll_seconds))
done
echo "SonarCloud analysed the PR (task $task)."

# The PR's open issues, a page of 100 at a time (the API stops at 10,000)
issues=$(mktemp)
trap 'rm -f "$issues"' EXIT
page=1
while :; do
  body=$(api "issues/search?componentKeys=$SONAR_PROJECT_KEY&pullRequest=$PR&issueStatuses=OPEN,CONFIRMED&ps=100&p=$page") \
    || fail "Could not list the PR's issues."
  # One line per issue: path, line (empty for a file-level issue), rule and message, split by the unit separator (not
  # a tab: read merges tabs, so an empty line number would shift the fields)
  jq -r '.issues[] | [(.component | sub("^[^:]*:"; "")), (.line // "" | tostring), .rule, .message]
    | map(gsub("[\r\n\u001f]"; " ")) | join("\u001f")' <<< "$body" >> "$issues"
  total=$(jq -r '.total' <<< "$body")
  (( page * 100 < total && page < 100 )) || break
  page=$((page + 1))
done

count=$(grep -c . "$issues" || true)
if [[ "$count" = 0 ]]; then
  echo "No SonarCloud issues in PR #$PR."
  exit 0
fi

# Workflow commands: a message escapes %, CR and LF; a property also escapes : and ,
esc() { local s=${1//%/%25}; s=${s//$'\r'/%0D}; printf '%s' "${s//$'\n'/%0A}"; }
prop() { local s; s=$(esc "$1"); s=${s//:/%3A}; printf '%s' "${s//,/%2C}"; }

echo "::error title=SonarCloud issues::PR #$PR adds $count SonarCloud issue(s). Fix each one (a false positive takes a targeted #pragma or NOSONAR, with the reason):"
while IFS=$'\x1f' read -r path line rule message; do
  where=$path${line:+:$line}
  echo "::error file=$(prop "$path")${line:+,line=$line},title=$(prop "$rule")::$(esc "$where $rule $message")"
done < "$issues"
echo
echo "$host/project/issues?id=$SONAR_PROJECT_KEY&pullRequest=$PR&issueStatuses=OPEN,CONFIRMED"
exit 1
