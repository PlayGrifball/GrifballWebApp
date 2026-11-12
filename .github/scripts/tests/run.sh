#!/usr/bin/env bash
# Tests of .github/scripts: bash .github/scripts/tests/run.sh (the Migrations workflow runs them).
#   check-master-migrations.sh: every fixtures/pass-*.txt (git diff --name-status output) passes; every
#     fixtures/fail-*.txt fails, listing exactly the lines of its .expect; then the same on a real
#     repository, from a branch against its master.
#   master-migrations.sh: a repository whose master regenerated a migration in a merged branch, then
#     renamed one: "now" and "ever" lists as the preview reset expects them.
#   check-one-migration.sh: one new migration per project passes; a second in one project fails, naming both.
#   apk-archive.sh --check: the lock's tag and whether its release is published (gh stubbed).
#   changed-areas.sh: the workflows' backend/frontend/images flags for a PR's files (gh stubbed).
#   sonar-pr-issues.sh: waits for SonarCloud's analysis, then fails a PR with issues or hotspots, one annotation each
#     (curl stubbed per endpoint); and analyzers/*.globalconfig name the analyzer version Directory.Build.props uses.
#   close-stuck-report.sh: a test report's check run left in progress is completed with the tests' outcome (gh stubbed).
#   layer-coverage.mjs: each layer's numbers from its own test project's Cobertura report, and their change from master.
set -euo pipefail
here=$(cd "$(dirname "$0")" && pwd)
scripts=$(dirname "$here")
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
failures=0
ok() { echo "ok: $*"; }
bad() { echo "FAIL: $*"; failures=$((failures + 1)); }

guard() { "$scripts/check-master-migrations.sh" "$@"; }

for f in "$here"/fixtures/pass-*.txt; do
  name=$(basename "$f" .txt)
  if guard < "$f" > "$work/out" 2>&1; then ok "$name"; else bad "$name failed:"; sed 's/^/  | /' "$work/out"; fi
done
for f in "$here"/fixtures/fail-*.txt; do
  name=$(basename "$f" .txt)
  if guard < "$f" > "$work/out" 2>&1; then bad "$name passed"; continue; fi
  grep -q '^::error title=Master migrations changed::' "$work/out" || { bad "$name: no ::error annotation"; continue; }
  grep -q 'add a new migration instead' "$work/out" || { bad "$name: doesn't say to add a new migration"; continue; }
  sed -n '/^Master migrations changed:$/,/^$/p' "$work/out" | sed -e '1d' -e '/^$/d' -e 's/^  //' > "$work/listed"
  if diff -u "${f%.txt}.expect" "$work/listed" > "$work/diff"; then ok "$name"; else bad "$name listed:"; cat "$work/diff"; fi
done

# A repository: master with migrations, then branches.
repo=$work/repo
git init -q -b master "$repo"
g() { git -C "$repo" -c user.name=t -c user.email=t@example.invalid "$@"; }
S=GrifballWebApp.Migrations.SqlServer/Migrations
P=GrifballWebApp.Migrations.Postgres/Migrations
add() { mkdir -p "$repo/$(dirname "$1")"; echo "${2:-x}" > "$repo/$1"; g add "$1"; }
for m in 20250101000000_Initial 20250201000000_Second; do
  add "$S/$m.cs"; add "$S/$m.Designer.cs"
done
add "$P/20250101000005_Initial.cs"; add "$P/20250101000005_Initial.Designer.cs"
add "$S/GrifballContextModelSnapshot.cs"; add "$P/GrifballContextModelSnapshot.cs"
g commit -q -m initial
# The workflow runs in a clone, against origin/master.
git clone -q "$repo" "$work/clone"
c() { git -C "$work/clone" -c user.name=t -c user.email=t@example.invalid "$@"; }
in_clone() { (cd "$work/clone" && "$@"); }

c checkout -q -b feature
echo y > "$work/clone/$S/20250301000000_Third.cs"; echo y > "$work/clone/$S/20250301000000_Third.Designer.cs"
echo y >> "$work/clone/$S/GrifballContextModelSnapshot.cs"
c add -A; c commit -q -m third
if in_clone guard origin/master > "$work/out" 2>&1; then ok "repo: a new migration and the snapshot"; else bad "repo: a new migration failed"; cat "$work/out"; fi

# check-one-migration.sh: one new migration per project passes (one in another project too); a second fails, naming both.
one() { "$scripts/check-one-migration.sh" "$@"; }
echo y > "$work/clone/$P/20250301000005_Third.cs"; echo y > "$work/clone/$P/20250301000005_Third.Designer.cs"
c add -A; c commit -q -m third-postgres
if in_clone one origin/master > "$work/out" 2>&1; then ok "one: one new migration per project"; else bad "one: one per project failed"; cat "$work/out"; fi
echo y > "$work/clone/$S/20250302000000_Fourth.cs"; echo y > "$work/clone/$S/20250302000000_Fourth.Designer.cs"
c add -A; c commit -q -m fourth
if in_clone one origin/master > "$work/out" 2>&1; then bad "one: two new migrations passed"
elif grep -q '^::error title=More than one new migration::' "$work/out" \
  && grep -qxF "  GrifballWebApp.Migrations.SqlServer: 20250301000000_Third.Designer.cs 20250302000000_Fourth.Designer.cs" "$work/out" \
  && ! grep -q 'Postgres:' "$work/out"; then ok "one: two new migrations fail, naming the project's two"
else bad "one: two new migrations, wrong output"; cat "$work/out"; fi
c reset -q --hard HEAD~2

echo edited >> "$work/clone/$S/20250201000000_Second.cs"
c add -A; c commit -q -m edit
if in_clone guard origin/master > "$work/out" 2>&1; then bad "repo: an edited master migration passed"
elif grep -qxF "  $S/20250201000000_Second.cs (modified)" "$work/out"; then ok "repo: an edited master migration fails"
else bad "repo: edited, but not listed"; cat "$work/out"; fi

# Behind master: master's newer migration isn't the PR's change.
c reset -q --hard HEAD~1
add "$S/20250401000000_MasterOnly.cs"; add "$S/20250401000000_MasterOnly.Designer.cs"; g commit -q -m master-only
c fetch -q origin
if in_clone guard origin/master > "$work/out" 2>&1; then ok "repo: behind master, master's new migration not blamed"; else bad "repo: behind master failed"; cat "$work/out"; fi

# master-migrations.sh: a branch that regenerated its migration before merging (never master's),
# then master renaming one later (a rename the guard would now refuse): ever keeps the old name.
g checkout -q -b pr
add "$S/20250501000000_Draft.cs"; add "$S/20250501000000_Draft.Designer.cs"; g commit -q -m draft
g rm -q "$S/20250501000000_Draft.cs" "$S/20250501000000_Draft.Designer.cs"
add "$S/20250502000000_Final.cs"; add "$S/20250502000000_Final.Designer.cs"; g commit -q -m regenerate
g checkout -q master
g merge -q --no-ff pr -m "Merge pr"
g mv "$S/20250201000000_Second.Designer.cs" "$S/20250201000009_SecondRenamed.Designer.cs"
g mv "$S/20250201000000_Second.cs" "$S/20250201000009_SecondRenamed.cs"
g commit -q -m rename
(cd "$repo" && "$scripts/master-migrations.sh" "$work/lists" master) > "$work/out" 2>&1 || { bad "master-migrations.sh failed"; cat "$work/out"; }
printf '%s\n' 20250101000000_Initial 20250201000009_SecondRenamed 20250401000000_MasterOnly 20250502000000_Final > "$work/want-now"
printf '%s\n' 20250101000000_Initial 20250201000000_Second 20250201000009_SecondRenamed 20250401000000_MasterOnly 20250502000000_Final > "$work/want-ever"
diff -u "$work/want-now" "$work/lists/sqlserver.txt" && ok "master-migrations.sh: master's migrations now" || bad "master-migrations.sh: sqlserver.txt"
diff -u "$work/want-ever" "$work/lists/sqlserver-ever.txt" && ok "master-migrations.sh: every one master has had, not the branch's draft" || bad "master-migrations.sh: sqlserver-ever.txt"
[[ "$(cat "$work/lists/postgres.txt")" = 20250101000005_Initial ]] && ok "master-migrations.sh: PostgreSQL's" || bad "master-migrations.sh: postgres.txt"

git clone -q --depth 1 "file://$repo" "$work/shallow"
if (cd "$work/shallow" && "$scripts/master-migrations.sh" "$work/lists2" origin/master) > "$work/out" 2>&1; then bad "master-migrations.sh ran in a shallow clone"
else ok "master-migrations.sh refuses a shallow clone"; fi

# changed-areas.sh: which side a PR's files make the test workflows run, with gh stubbed to list them
mkdir -p "$work/bin"
cat > "$work/bin/gh" <<'GH'
#!/usr/bin/env bash
[ -n "${GH_FAILS:-}" ] && exit 1
cat "$GH_LIST"
GH
chmod +x "$work/bin/gh"
areas() { # areas <event> <files...>: prints "backend=... frontend=... images=... migrations=..."
  local event=$1; shift
  : > "$work/areas"
  printf '%s\n' "$@" > "$work/files"
  GH_LIST="$work/files" GITHUB_EVENT_NAME=$event GITHUB_REPOSITORY=o/r PR=1 GITHUB_OUTPUT="$work/areas" PATH="$work/bin:$PATH" \
    "$scripts/changed-areas.sh" > /dev/null
  tr '\n' ' ' < "$work/areas" | sed 's/ $//'
}
expect_areas() { # expect_areas <name> <expected> <event> <files...>
  local name=$1 expected=$2; shift 2
  local got; got=$(areas "$@")
  [[ "$got" = "$expected" ]] && ok "changed-areas.sh: $name" || bad "changed-areas.sh: $name gave '$got', not '$expected'"
}
expect_areas "backend only" "backend=true frontend=false images=false migrations=false" pull_request GrifballWebApp.Server/Program.cs docs/x.md
expect_areas "client only" "backend=false frontend=true images=false migrations=false" pull_request grifballwebapp.client/src/main.ts
expect_areas "docs only" "backend=false frontend=false images=false migrations=false" pull_request docs/historical-import.md readme.md charts/grif/values.yaml
expect_areas "the solution and the test workflow" "backend=true frontend=false images=false migrations=false" pull_request GrifballWebApp.slnx .github/workflows/dotnet-tests.yml
expect_areas "the script itself: everything" "backend=true frontend=true images=true migrations=true" pull_request .github/scripts/changed-areas.sh
expect_areas "a push: everything" "backend=true frontend=true images=true migrations=true" push docs/x.md
GH_FAILS=1 expect_areas "unreadable file list: everything" "backend=true frontend=true images=true migrations=true" pull_request docs/x.md
# What the images are built from: the arm64 check runs; app code alone doesn't change it
expect_areas "a Dockerfile" "backend=true frontend=false images=true migrations=false" pull_request GrifballWebApp.Server/Dockerfile
expect_areas "the client's packages" "backend=false frontend=true images=true migrations=false" pull_request grifballwebapp.client/package-lock.json
expect_areas "a project file" "backend=true frontend=false images=true migrations=false" pull_request GrifballWebApp.Server/GrifballWebApp.Server.csproj
expect_areas "the image workflow" "backend=false frontend=false images=true migrations=false" pull_request .github/workflows/docker-publish.yml
expect_areas "the build's analyzers" "backend=true frontend=false images=true migrations=false" pull_request Directory.Build.props
expect_areas "the analyzers' rules" "backend=true frontend=false images=false migrations=false" pull_request analyzers/sonar-main.globalconfig
# The migrations: the image's build bundles them, so the PR builds its images (images stays false: no arm64 check)
expect_areas "a migration" "backend=true frontend=false images=false migrations=true" pull_request GrifballWebApp.Migrations.Postgres/Migrations/20261112000000_X.cs
expect_areas "the design-time context" "backend=true frontend=false images=false migrations=true" pull_request GrifballWebApp.Infrastructure/DesignTimeContext.cs
expect_areas "code, not packages" "backend=true frontend=true images=false migrations=false" pull_request GrifballWebApp.Server/Program.cs grifballwebapp.client/src/app/app.ts
# A PR as big as this one: past a pipe's buffer (a pipe into grep -q broke here and read as "no change")
big="GrifballWebApp.Server/Program.cs grifballwebapp.client/src/main.ts $(seq -f 'docs/a-long-directory-name-to-pass-the-pipe-buffer/page-%g.md' 1 2500 | tr '\n' ' ')"
# shellcheck disable=SC2086 # one word per file
expect_areas "2500 files, the code first" "backend=true frontend=true images=false migrations=false" pull_request $big

# apk-archive.sh --check: the lock's tag, and whether its release is published with every package (gh stubbed: the
# release list, then the release's assets). It only reads: the stub fails on anything else.
mkdir -p "$work/apk/GrifballWebApp.Server" "$work/apkbin"
cat > "$work/apk/GrifballWebApp.Server/Dockerfile" <<'DF'
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:abc AS base
DF
cat > "$work/apk/GrifballWebApp.Server/apk.lock" <<'LOCK'
# alpine: 3.24.4
# base: mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:abc
x86_64 tzdata 2026a-r0 1111
x86_64 icu-libs 76.1-r0 2222
LOCK
cat > "$work/apkbin/gh" <<'GH'
#!/usr/bin/env bash
case "$*" in
  *"releases --paginate"*) cat "$APK_RELEASES" ;;
  *"releases/7 --jq"*) cat "$APK_ASSETS" ;;
  *) echo "unexpected gh $*" >&2; exit 1 ;;
esac
GH
chmod +x "$work/apkbin/gh"
apk_check() { # apk_check <releases> <assets>: runs --check, leaving its outputs in $work/apkout
  printf '%s' "$1" > "$work/apkrel"; printf '%s' "$2" > "$work/apkassets"; : > "$work/apkout"
  (cd "$work/apk" && APK_RELEASES="$work/apkrel" APK_ASSETS="$work/apkassets" GITHUB_REPOSITORY=o/r GITHUB_OUTPUT="$work/apkout" \
    PATH="$work/apkbin:$PATH" "$scripts/apk-archive.sh" --check) > "$work/out" 2>&1
}
apk_tag="apk-$(sha256sum "$work/apk/GrifballWebApp.Server/apk.lock" | cut -c1-12)"
all=$(printf '1111.apk sha256:1111\n2222.apk sha256:2222')
if apk_check "7 false" "$all" && [[ "$(cat "$work/apkout")" = "$(printf 'tag=%s\npublished=true' "$apk_tag")" ]]; then
  ok "apk-archive --check: published with every package"
else bad "apk-archive --check: published"; cat "$work/out" "$work/apkout"; fi
if apk_check "" "" && [[ "$(cat "$work/apkout")" = "$(printf 'tag=%s\npublished=false' "$apk_tag")" ]]; then
  ok "apk-archive --check: no release yet"
else bad "apk-archive --check: no release"; cat "$work/out" "$work/apkout"; fi
if apk_check "7 true" "1111.apk sha256:1111" && grep -qx "published=false" "$work/apkout"; then
  ok "apk-archive --check: only a draft"
else bad "apk-archive --check: a draft"; cat "$work/out" "$work/apkout"; fi
if apk_check "7 false" "1111.apk sha256:1111"; then bad "apk-archive --check: published without a package passed"
elif grep -q "is published without icu-libs" "$work/out"; then ok "apk-archive --check: published without a package fails"
else bad "apk-archive --check: incomplete, wrong error"; cat "$work/out"; fi

# sonar-pr-issues.sh: SonarCloud's API stubbed, each endpoint on its own. curl answers the analysis task with the next of
# $SONAR_STATUSES' lines (the last one from then on); a page of the PR's issues with $SONAR_FILES/issues-<page>.json, of
# its hotspots with hotspots-<page>.json, and its measures with measures.json. An endpoint named in $SONAR_DOWN (task,
# issues, hotspots, measures) fails as curl --fail does. It logs each call.
mkdir -p "$work/sonarbin" "$work/sonar/.sonarqube/out/.sonar" "$work/sonarfiles"
cat > "$work/sonarbin/curl" <<'CURL'
#!/usr/bin/env bash
url=${!#}
echo "$*" >> "$SONAR_CALLS"
down() { [[ " ${SONAR_DOWN:-} " == *" $1 "* ]] && { echo "curl: (22) The requested URL returned error: 500" >&2; exit 22; }; return 0; }
case "$url" in
  */api/ce/task?id=task-1)
    down task
    n=$(( $(cat "$SONAR_COUNT" 2>/dev/null || echo 0) + 1 )); echo "$n" > "$SONAR_COUNT"
    status=$(sed -n "${n}p" "$SONAR_STATUSES"); [[ -n $status ]] || status=$(tail -n 1 "$SONAR_STATUSES")
    printf '{"task":{"id":"task-1","status":"%s"}}' "$status" ;;
  */api/issues/search?componentKeys=Proj\&pullRequest=7\&issueStatuses=OPEN,CONFIRMED\&ps=100\&p=*)
    down issues; cat "$SONAR_FILES/issues-${url##*&p=}.json" ;;
  */api/hotspots/search?projectKey=Proj\&pullRequest=7\&status=TO_REVIEW\&ps=100\&p=*)
    down hotspots; cat "$SONAR_FILES/hotspots-${url##*&p=}.json" ;;
  */api/measures/component?component=Proj\&pullRequest=7\&metricKeys=new_violations)
    down measures; cat "$SONAR_FILES/measures.json" ;;
  *) echo "unexpected curl $url" >&2; exit 22 ;;
esac
CURL
chmod +x "$work/sonarbin/curl"
printf 'projectKey=Proj\nceTaskId=task-1\n' > "$work/sonar/.sonarqube/out/.sonar/report-task.txt"
none='{"total":0,"issues":[]}'
no_hotspots='{"paging":{"pageIndex":1,"pageSize":100,"total":0},"hotspots":[]}'
measure() { printf '{"component":{"key":"Proj","measures":[{"metric":"new_violations","periods":[{"index":1,"value":"%s"}]}]}}' "$1"; }
two='{"total":2,"issues":[
  {"component":"Proj:GrifballWebApp.Server/Program.cs","line":12,"rule":"csharpsquid:S1244","message":"Do not check floating point equality: 100% sure, really."},
  {"component":"Proj:grifballwebapp.client/package.json","rule":"javascript:S1","message":"A file-level issue."}]}'
sonar_check() { # sonar_check <statuses, one per line> <issues page 1> [issues page 2] [hotspots page 1] [new_violations]
  printf '%s\n' "$1" > "$work/statuses"
  printf '%s' "$2" > "$work/sonarfiles/issues-1.json"; printf '%s' "${3:-}" > "$work/sonarfiles/issues-2.json"
  printf '%s' "${4:-$no_hotspots}" > "$work/sonarfiles/hotspots-1.json"; measure "${5:-0}" > "$work/sonarfiles/measures.json"
  rm -f "$work/count"; : > "$work/calls"
  (cd "$work/sonar" && PR=7 SONAR_PROJECT_KEY=Proj SONAR_POLL_SECONDS=0 SONAR_WAIT_SECONDS="${WAIT:-600}" \
    SONAR_STATUSES="$work/statuses" SONAR_COUNT="$work/count" SONAR_FILES="$work/sonarfiles" SONAR_CALLS="$work/calls" \
    PATH="$work/sonarbin:$PATH" "$scripts/sonar-pr-issues.sh") > "$work/out" 2>&1
}
fails_with() { # fails_with <name> <the exact ::error line>: the last sonar_check failed, saying exactly that
  local status=$1; shift
  if [[ $status = 0 ]]; then bad "sonar-pr-issues: $1 passed"
  elif grep -qxF "$2" "$work/out"; then ok "sonar-pr-issues: $1 fails"
  else bad "sonar-pr-issues: $1, wrong output"; cat "$work/out"; fi
}
if sonar_check "$(printf 'PENDING\nIN_PROGRESS\nSUCCESS')" "$none" \
  && grep -qx 'No SonarCloud issues or security hotspots in PR #7.' "$work/out" \
  && [[ $(grep -c 'api/ce/task' "$work/calls") = 3 ]] && ! grep -q -- '--user' "$work/calls"; then
  ok "sonar-pr-issues: waits for the analysis, then passes a PR with no issues or hotspots"
else bad "sonar-pr-issues: no issues"; cat "$work/out"; fi
if sonar_check SUCCESS "$two" "" "" 2; then bad "sonar-pr-issues: a PR with issues passed"
elif grep -qxF '::error title=SonarCloud issues::PR #7 adds 2 SonarCloud issue(s) and hotspot(s) (new_violations: 2). Fix each one (a false positive takes a targeted #pragma or NOSONAR, with the reason):' "$work/out" \
  && grep -qxF '::error file=GrifballWebApp.Server/Program.cs,line=12,title=csharpsquid%3AS1244::GrifballWebApp.Server/Program.cs:12 csharpsquid:S1244 Do not check floating point equality: 100%25 sure, really.' "$work/out" \
  && grep -qxF '::error file=grifballwebapp.client/package.json,title=javascript%3AS1::grifballwebapp.client/package.json javascript:S1 A file-level issue.' "$work/out"; then
  ok "sonar-pr-issues: fails a PR with issues, an annotation for each (file:line, rule, message)"
else bad "sonar-pr-issues: issues, wrong output"; cat "$work/out"; fi
first_page='{"total":101,"issues":[{"component":"Proj:a.cs","line":1,"rule":"csharpsquid:S1","message":"First page."}]}'
last_page='{"total":101,"issues":[{"component":"Proj:b.cs","line":2,"rule":"csharpsquid:S2","message":"Second page."}]}'
if sonar_check SUCCESS "$first_page" "$last_page" "" 101; then bad "sonar-pr-issues: two pages passed"
elif grep -q 'adds 2 SonarCloud issue' "$work/out" && grep -q '^::error file=b.cs,line=2,' "$work/out"; then
  ok "sonar-pr-issues: reads every page"
else bad "sonar-pr-issues: two pages, wrong output"; cat "$work/out"; fi
hotspot='{"paging":{"pageIndex":1,"pageSize":100,"total":1},"hotspots":[{"component":"Proj:GrifballWebApp.Server/Dockerfile","line":31,"ruleKey":"docker:S6506","vulnerabilityProbability":"MEDIUM","message":"Not disabling redirects might allow for redirections to insecure websites."}]}'
if sonar_check SUCCESS "$none" "" "$hotspot"; then bad "sonar-pr-issues: a PR with a hotspot passed"
elif grep -qxF '::error file=GrifballWebApp.Server/Dockerfile,line=31,title=docker%3AS6506::GrifballWebApp.Server/Dockerfile:31 docker:S6506 Security hotspot: Not disabling redirects might allow for redirections to insecure websites.' "$work/out"; then
  ok "sonar-pr-issues: fails a PR with a security hotspot to review, annotated"
else bad "sonar-pr-issues: hotspot, wrong output"; cat "$work/out"; fi
st=0; sonar_check SUCCESS "$none" "" "" 3 || st=$?; fails_with "$st" "new_violations above 0 with nothing listed" \
  '::error title=SonarCloud issues::PR #7 adds 0 SonarCloud issue(s) and hotspot(s) (new_violations: 3). Fix each one (a false positive takes a targeted #pragma or NOSONAR, with the reason):'
st=0; sonar_check FAILED "$none" || st=$?; fails_with "$st" "a failed analysis" \
  "::error title=SonarCloud check::SonarCloud's analysis task task-1 ended FAILED."
st=0; WAIT=0 sonar_check PENDING "$none" || st=$?; fails_with "$st" "an analysis that doesn't finish" \
  "::error title=SonarCloud check::SonarCloud's analysis task task-1 was still PENDING after 0s."
st=0; SONAR_DOWN=task sonar_check SUCCESS "$none" || st=$?; fails_with "$st" "an unreachable task API" \
  '::error title=SonarCloud check::Could not read the analysis task task-1.'
st=0; SONAR_DOWN=issues sonar_check SUCCESS "$none" || st=$?; fails_with "$st" "an unreachable issues API" \
  "::error title=SonarCloud check::Could not list the PR's issues."
st=0; SONAR_DOWN=hotspots sonar_check SUCCESS "$none" || st=$?; fails_with "$st" "an unreachable hotspots API" \
  "::error title=SonarCloud check::Could not list the PR's hotspots."
st=0; SONAR_DOWN=measures sonar_check SUCCESS "$none" || st=$?; fails_with "$st" "an unreachable measures API" \
  "::error title=SonarCloud check::Could not read the PR's new_violations measure."
st=0; sonar_check SUCCESS 'not json' || st=$?; fails_with "$st" "an issue list that isn't JSON" \
  "::error title=SonarCloud check::SonarCloud's list of the PR's issues isn't the JSON expected."
st=0; sonar_check SUCCESS "$none" "" '{"hotspots":[]}' || st=$?; fails_with "$st" "a hotspot list without its total" \
  "::error title=SonarCloud check::SonarCloud's list of the PR's hotspots isn't the JSON expected."
if SONAR_TOKEN=tok sonar_check SUCCESS "$none" && grep -q -- '--user tok: .*api/ce/task' "$work/calls" \
  && [[ $(grep -c -- '--user' "$work/calls") = 1 ]]; then
  ok "sonar-pr-issues: sends the token to the task API alone"
else bad "sonar-pr-issues: token"; cat "$work/out" "$work/calls"; fi
rm "$work/sonar/.sonarqube/out/.sonar/report-task.txt"
st=0; sonar_check SUCCESS "$none" || st=$?; fails_with "$st" "no scanner report" \
  "::error title=SonarCloud check::No .sonarqube/out/.sonar/report-task.txt: the scanner's end step didn't upload an analysis."

# The analyzer rules (analyzers/*.globalconfig) were made for the SonarAnalyzer.CSharp version the build uses: a new
# version (Renovate's SonarAnalyzer PR) needs them regenerated (.github/scripts/sonar-rules.sh)
root=$(cd "$scripts/../.." && pwd)
analyzer=$(sed -n 's/.*Include="SonarAnalyzer.CSharp" Version="\([^"]*\)".*/\1/p' "$root/Directory.Build.props")
for f in "$root"/analyzers/sonar-main.globalconfig "$root"/analyzers/sonar-tests.globalconfig; do
  made_for=$(sed -n '1s/.*(SonarAnalyzer\.CSharp \([^)]*\)).*/\1/p' "$f")
  if [[ -n $analyzer && $made_for = "$analyzer" ]]; then ok "${f##*/} is for SonarAnalyzer.CSharp $analyzer"
  else bad "${f##*/} is for SonarAnalyzer.CSharp '$made_for', the build uses '$analyzer': run bash .github/scripts/sonar-rules.sh"; fi
  # Each rule on is an error (the build fails on it), none a warning that only scrolls past
  if grep -q '= warning$' "$f"; then bad "${f##*/} has rules as warnings: run bash .github/scripts/sonar-rules.sh"
  else ok "${f##*/} has its rules as errors"; fi
done

# layer-coverage.mjs: a layer counts its own package only, each line once (listed by its class and its method), and
# a test project without its layer's package (the architecture tests) is left out
cov=$work/coverage
mkdir -p "$cov"
cat > "$cov/GrifballWebApp.Domain.Test.cobertura.xml" <<'XML'
<coverage><packages>
<package name="GrifballWebApp.Domain"><classes><class name="A" filename="A.cs"><methods><method name="M"><lines>
<line number="1" hits="1" branch="True" condition-coverage="50% (1/2)" /></lines></method></methods><lines>
<line number="1" hits="1" branch="True" condition-coverage="50% (1/2)" /><line number="2" hits="0" branch="False" />
<line number="3" hits="0" branch="False" /><line number="4" hits="2" branch="False" /></lines></class></classes></package>
</packages></coverage>
XML
cat > "$cov/GrifballWebApp.Application.Test.cobertura.xml" <<'XML'
<coverage><packages>
<package name="GrifballWebApp.Domain"><classes><class name="A" filename="A.cs"><lines>
<line number="2" hits="5" branch="False" /><line number="3" hits="5" branch="False" /></lines></class></classes></package>
<package name="GrifballWebApp.Application"><classes><class name="B" filename="B.cs"><lines>
<line number="1" hits="1" branch="False" /><line number="2" hits="0" branch="False" /></lines></class></classes></package>
</packages></coverage>
XML
cat > "$cov/GrifballWebApp.Architecture.Test.cobertura.xml" <<'XML'
<coverage><packages><package name="GrifballWebApp.Domain"><classes><class name="A" filename="A.cs"><lines>
<line number="2" hits="1" branch="False" /></lines></class></classes></package></packages></coverage>
XML
node "$scripts/layer-coverage.mjs" collect "$cov" "$cov/layers.json"
node "$scripts/layer-coverage.mjs" render "$cov/layers.json" "" abc1234 > "$cov/table.md"
if grep -qF '| Domain | 50.0% · 2/4 | 50.0% | ❌ 1 lines short |' "$cov/table.md" \
  && grep -qF '| Application | 50.0% · 1/2 | – | ❌ 1 lines short |' "$cov/table.md" \
  && ! grep -q Architecture "$cov/table.md"; then ok "layer-coverage: each layer from its own tests"
else bad "layer-coverage table:"; sed 's/^/  | /' "$cov/table.md"; fi
printf '{"GrifballWebApp.Domain":{"lines":4,"linesCovered":1,"branches":2,"branchesCovered":1}}\n' > "$cov/base.json"
if node "$scripts/layer-coverage.mjs" render "$cov/layers.json" "$cov/base.json" | grep -qF '| Domain | 50.0% (+25.0) · 2/4 | 50.0% (±0) |'
then ok "layer-coverage: change from master"; else bad "layer-coverage: no change from master shown"; fi
# A layer master had at the target that drops below it fails; one still below it doesn't
printf '{"GrifballWebApp.Domain":{"lines":4,"linesCovered":3,"branches":2,"branchesCovered":1}}\n' > "$cov/base.json"
if node "$scripts/layer-coverage.mjs" render "$cov/layers.json" "$cov/base.json" > "$cov/table.md"; then bad "layer-coverage: a drop below the target passed"
elif grep -qF '**Domain dropped below 70%**' "$cov/table.md"; then ok "layer-coverage: a drop below the target fails"
else bad "layer-coverage: the drop isn't named:"; sed 's/^/  | /' "$cov/table.md"; fi

# close-stuck-report.sh: a test report's check run left "in progress" (GitHub answered the reporter's update with 404)
# is completed with the test step's own outcome; one already completed is left alone; if completing it fails, so does
# the script (the job goes red instead of a check staying pending). gh stubbed: the list call must ask for every run of
# the report's name (filter=all) and answers what its --jq would (REPORT_STUCK: the stuck run's id, or nothing); the
# PATCH is recorded, and refused REPORT_PATCH_FAILS times first ("all": every time).
mkdir -p "$work/reportbin"
cat > "$work/reportbin/gh" <<'GH'
#!/usr/bin/env bash
case "$*" in
  *"-X GET repos/o/r/commits/abc/check-runs "*)
    [[ "$*" == *"check_name=.NET Test Results (Unit)"* && "$*" == *"filter=all"* && "$*" == *"--jq"* ]] \
      || { echo "bad list call: $*" >&2; exit 9; }
    [[ -n "${REPORT_LIST_FAILS:-}" && ! -e "$REPORT_PATCHED.listed" ]] && { touch "$REPORT_PATCHED.listed"; exit 1; }
    echo "${REPORT_STUCK:-}" ;;
  *"-X PATCH"*) echo "$*" >> "$REPORT_PATCHED"
    [[ "${REPORT_PATCH_FAILS:-0}" = all ]] && exit 1
    [[ "$(wc -l < "$REPORT_PATCHED")" -gt "${REPORT_PATCH_FAILS:-0}" ]] ;;
  *) echo "unexpected gh $*" >&2; exit 9 ;;
esac
GH
chmod +x "$work/reportbin/gh"
report() { # report <stuck id or ""> <outcome> [patch fails] [list fails]: the script's exit code; PATCHes in $work/patched
  : > "$work/patched"; rm -f "$work/patched.listed"
  REPORT_STUCK="$1" REPORT_PATCHED="$work/patched" REPORT_PATCH_FAILS="${3:-}" REPORT_LIST_FAILS="${4:-}" \
    PATH="$work/reportbin:$PATH" REPORT_RETRY_SECONDS=0 REPO=o/r SHA=abc NAME='.NET Test Results (Unit)' OUTCOME="$2" \
    "$scripts/close-stuck-report.sh" > "$work/out" 2>&1
}
if report 9 success && grep -q 'check-runs/9 ' "$work/patched" && grep -q 'conclusion=success' "$work/patched"
then ok "close-stuck-report: a stuck report gets the tests' success"; else bad "close-stuck-report: success not set:"; cat "$work/out" "$work/patched"; fi
if report 9 failure && grep -q 'conclusion=failure' "$work/patched"
then ok "close-stuck-report: and their failure"; else bad "close-stuck-report: failure not set:"; cat "$work/out" "$work/patched"; fi
# Concurrency cancels a PR's older run: its tests didn't pass
if report 9 cancelled && grep -q 'conclusion=failure' "$work/patched"
then ok "close-stuck-report: a cancelled test step is a failure"; else bad "close-stuck-report: cancelled not a failure:"; cat "$work/out" "$work/patched"; fi
if report "" success && [[ ! -s "$work/patched" ]] && grep -q 'nothing stuck' "$work/out"
then ok "close-stuck-report: nothing in progress, nothing done"; else bad "close-stuck-report: nothing stuck:"; cat "$work/out"; fi
# GitHub 404s an update in the seconds after the run is created (#401: created at :24.0, updates refused at :24.9 and
# :25.7): retried, and so is a failed list call
if report 9 success 2 && [[ "$(wc -l < "$work/patched")" = 3 ]]
then ok "close-stuck-report: a 404 right after creation is retried"; else bad "close-stuck-report: no retry:"; cat "$work/out" "$work/patched"; fi
if report 9 success "" yes && grep -q 'conclusion=success' "$work/patched"
then ok "close-stuck-report: a failed list call is retried"; else bad "close-stuck-report: list not retried:"; cat "$work/out"; fi
if report 9 success all; then bad "close-stuck-report: a failed PATCH passed"
else ok "close-stuck-report: a report it can't complete fails the job"; fi

if [[ "$failures" != 0 ]]; then echo "$failures failure(s)"; exit 1; fi
echo "All .github/scripts tests passed."
