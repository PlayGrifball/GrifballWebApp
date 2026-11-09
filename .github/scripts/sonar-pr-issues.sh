#!/usr/bin/env bash
# sonar-pr-issues.sh: fails a pull request that SonarCloud finds any open issue or security hotspot in, listing each.
#
# The SonarCloud workflow runs it after `dotnet sonarscanner end`, which only uploads the report: SonarCloud then
# analyses it in a background task. This waits for that task (its id is ceTaskId in the scanner's report-task.txt),
# then asks for the PR's open issues and its hotspots still to review, and its new_violations measure (the count
# SonarCloud's own PR summary shows) as a cross-check. The quality gate is SonarCloud's to set; this holds every PR to
# zero of each without it, and puts each issue and hotspot on the PR's diff as an annotation.
#
# Env: PR (the PR number), SONAR_PROJECT_KEY. Optional: SONAR_TOKEN (sent to the task API when set; never to the
# others, which answer a token's request with a 400 and the public project's without one), SONAR_HOST_URL
# (https://sonarcloud.io), SONAR_REPORT_TASK (.sonarqube/out/.sonar/report-task.txt), SONAR_WAIT_SECONDS (600),
# SONAR_POLL_SECONDS (5).
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
  status=$(api "ce/task?id=$task" "${SONAR_TOKEN:-}" | jq -er '.task.status') \
    || fail "Could not read the analysis task $task."
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

found=$(mktemp)
trap 'rm -f "$found"' EXIT

# collect <what> <path and query, without the page> <jq: the total> <jq: the items as [path, line, rule, message]>
# Appends one line per item to $found, a page of 100 at a time (the APIs stop at 10,000): path, line (empty for a
# file-level one), rule and message, split by the unit separator (not a tab: read merges tabs, so an empty line number
# would shift the fields).
collect() {
  local page=1 body total
  while :; do
    body=$(api "$2&ps=100&p=$page") || fail "Could not list the PR's $1."
    total=$(jq -er "$3" <<< "$body") \
      && jq -r "$4"' | map(tostring | gsub("[\r\n\u001f]"; " ")) | join("\u001f")' <<< "$body" >> "$found" \
      || fail "SonarCloud's list of the PR's $1 isn't the JSON expected."
    (( page * 100 < total && page < 100 )) || break
    page=$((page + 1))
  done
}
collect issues "issues/search?componentKeys=$SONAR_PROJECT_KEY&pullRequest=$PR&issueStatuses=OPEN,CONFIRMED" \
  '.total' '.issues[] | [(.component | sub("^[^:]*:"; "")), (.line // ""), .rule, .message]'
collect hotspots "hotspots/search?projectKey=$SONAR_PROJECT_KEY&pullRequest=$PR&status=TO_REVIEW" \
  '.paging.total' '.hotspots[] | [(.component | sub("^[^:]*:"; "")), (.line // ""), .ruleKey, "Security hotspot: \(.message)"]'

# The PR's new_violations: what SonarCloud's summary counts, in case it ever counts what the lists above don't show
violations=$(api "measures/component?component=$SONAR_PROJECT_KEY&pullRequest=$PR&metricKeys=new_violations" \
  | jq -er '.component.measures[] | select(.metric == "new_violations") | (.periods[0].value // .period.value // .value)') \
  || fail "Could not read the PR's new_violations measure."
[[ $violations =~ ^[0-9]+$ ]] || fail "The PR's new_violations measure is '$violations', not a count."

count=$(grep -c . "$found" || true)
if [[ $count = 0 && $violations = 0 ]]; then
  echo "No SonarCloud issues or security hotspots in PR #$PR."
  exit 0
fi

# Workflow commands: a message escapes %, CR and LF; a property also escapes : and ,
esc() { local s=${1//%/%25}; s=${s//$'\r'/%0D}; printf '%s' "${s//$'\n'/%0A}"; }
prop() { local s; s=$(esc "$1"); s=${s//:/%3A}; printf '%s' "${s//,/%2C}"; }

echo "::error title=SonarCloud issues::PR #$PR adds $count SonarCloud issue(s) and hotspot(s) (new_violations: $violations). Fix each one (a false positive takes a targeted #pragma or NOSONAR, with the reason):"
while IFS=$'\x1f' read -r path line rule message; do
  where=$path${line:+:$line}
  echo "::error file=$(prop "$path")${line:+,line=$line},title=$(prop "$rule")::$(esc "$where $rule $message")"
done < "$found"
echo
echo "$host/project/issues?id=$SONAR_PROJECT_KEY&pullRequest=$PR&issueStatuses=OPEN,CONFIRMED"
echo "$host/project/security_hotspots?id=$SONAR_PROJECT_KEY&pullRequest=$PR"
exit 1
