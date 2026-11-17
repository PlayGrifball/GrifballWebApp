#!/usr/bin/env bash
# shellcheck disable=SC2016 # the single-quoted $ are jq's, not the shell's
# Structural tests of .github/workflows: the wiring between jobs and steps that no script test covers, read from the
# YAML (yq, then jq; both on GitHub's runners). bash .github/scripts/tests/workflows.sh [workflows dir]; run.sh runs it
# on the repository's workflows, then on broken copies, each of which must fail.
#   Every workflow: each needs.<job>.(outputs|result) a job reads names a job in its needs, and an output that job
#     declares; each steps.<id>.* names a step of the same job, and from a step one that runs before it.
#   docker-publish.yml: deploy-test runs after release, only when release moved :test (release runs only on master);
#     the jobs that can write the repository are deploy-test and apk-archive (the lock's release), nothing else;
#     release reads; deploy-test's needs.release.outputs are declared and set by a step that writes them.
#   promote.yml: the promote step chooses the images (env-run-images.sh on a re-run, else :from's digests) and retags
#     those; then Deploy, which gets them; then Release, with the deploy's outputs, which env-deploy.sh writes; the job
#     and its callers grant contents and packages write, nothing more.
#   helm-chart.yml: release needs publish and runs only when publish output a chart; the jobs that can write the
#     repository are release and apk-archive; release's needs.publish.outputs are declared and set by the publish step,
#     whose script (chart-publish.sh) writes them.
set -euo pipefail
here=$(cd "$(dirname "$0")" && pwd)
scripts=$(dirname "$here")
dir=${1:-$(dirname "$scripts")/workflows}
if ! command -v yq > /dev/null || ! command -v jq > /dev/null; then echo "workflows.sh needs yq (mikefarah) and jq"; exit 2; fi
failures=0
ok() { echo "ok: $*"; }
bad() { echo "FAIL: $*"; failures=$((failures + 1)); }

# Shared jq definitions. Each check's filter prints its problems, one per line: none is a pass.
lib='
# The ids a reference pattern captures in a value (all its strings)
def refs($re): [.. | strings | scan($re)] | unique;
def needs_of: [.needs // empty] | flatten;
# A job that can write the repository: contents write, write-all, or no permissions at all (the token default)
def writes($wf): (.permissions // $wf.permissions) as $p
  | $p == null or $p == "write-all" or (($p | type) == "object" and $p.contents == "write");
def writers: . as $wf | [.jobs | to_entries[] | select(.value | writes($wf)) | .key] | sort;
def step($id): [.steps[] | select(.id == $id)][0];
def step_index(f): [.steps | to_entries[] | select(.value | f) | .key][0];
# Every needs.<producer>.outputs.X the consumer reads: declared by the producer as ${{ steps.<id>.outputs.X }}, with
# a step <id> whose run: writes X=
def outputs_wired($consumer; $producer):
  .jobs[$producer] as $p
  | (.jobs[$consumer] | refs("needs\\.\($producer)\\.outputs\\.([A-Za-z0-9_-]+)")[]) as [$o]
  | ($p.outputs[$o] // "") as $v
  | ($v | capture("^\\$\\{\\{ *steps\\.(?<id>[A-Za-z0-9_-]+)\\.outputs\\.(?<o>[A-Za-z0-9_-]+) *}}$") // null) as $m
  | if $v == "" then "\($consumer) reads needs.\($producer).outputs.\($o), which \($producer) does not declare"
    elif $m == null or $m.o != $o then "\($producer) output \($o) is \($v), not its step output \($o)"
    elif ($p | step($m.id)) == null then "\($producer) output \($o) reads step \($m.id), which \($producer) does not have"
    elif (($p | step($m.id)).run // "" | test("(^|[^A-Za-z0-9_])\($o)=")) | not
    then "\($producer) step \($m.id) never writes \($o)= to GITHUB_OUTPUT"
    else empty end;
'
check() { # check <name> <workflow file> <jq filter> [jq args...]
  local name=$1 file=$2 filter=$3 out lines
  shift 3
  if ! out=$(yq -o=json "$dir/$file" | jq -r "$@" "$lib $filter" 2>&1); then bad "$name: $out"; return; fi
  if [[ -z "$out" ]]; then ok "$name"; return; fi
  bad "$name:"; mapfile -t lines <<< "$out"; printf '  | %s\n' "${lines[@]}"
}

# Every workflow: needs and steps references point at what exists
generic='
.jobs as $jobs
| $jobs | to_entries[] | .key as $j | .value as $job | ($job | needs_of) as $needs
| ( ($job | refs("needs\\.([A-Za-z0-9_-]+)\\.")[]) as [$n]
    | if $jobs[$n] == null then "job \($j): needs.\($n), but there is no job \($n)"
      elif ($needs | index($n)) == null then "job \($j): needs.\($n), but \($n) is not in its needs"
      else empty end ),
  ( ($job | refs("needs\\.([A-Za-z0-9_-]+)\\.outputs\\.([A-Za-z0-9_-]+)")[]) as [$n, $o]
    | $jobs[$n] as $p
    | if $p == null or $p.uses != null then empty  # a reusable workflow declares its outputs itself
      elif ($p.outputs // {} | has($o)) | not then "job \($j): needs.\($n).outputs.\($o), which \($n) does not declare"
      else empty end ),
  ( [$job.steps[]? | .id // empty] as $ids
    | ( ($job | del(.steps) | refs("steps\\.([A-Za-z0-9_-]+)\\.")[]) as [$s]
        | if ($ids | index($s)) == null then "job \($j): steps.\($s), but it has no step \($s)" else empty end ),
      ( $job.steps // [] | to_entries[] | .key as $i | .value as $step
        | ($step | refs("steps\\.([A-Za-z0-9_-]+)\\.")[]) as [$s]
        | if ($ids | index($s)) == null then "job \($j) step \($step.name // $step.id // $i): steps.\($s), but the job has no step \($s)"
          elif ($job.steps | map(.id) | index($s)) >= $i
          then "job \($j) step \($step.name // $step.id // $i): steps.\($s), which runs after it"
          else empty end ) )
'
for f in "$dir"/*.yml "$dir"/*.yaml; do
  [[ -e "$f" ]] || continue
  check "$(basename "$f"): every needs and steps reference names a job in needs or an earlier step" "$(basename "$f")" "$generic"
done

# docker-publish.yml
check "docker-publish: deploy-test needs release and runs only when release moved :test" docker-publish.yml '
.jobs["deploy-test"] as $d | .jobs.release as $r
| if ($d | needs_of) != ["release"] then "deploy-test needs \($d | needs_of), not [release]" else empty end,
  (["needs.release.outputs.moved == '"'true'"'", "!failure()", "!cancelled()"][] as $c
    | if ($d.if // "" | contains($c)) then empty else "deploy-test if (\($d.if)) lacks \($c)" end),
  (if ($r.if // "" | contains("github.ref == '"'refs/heads/master'"'")) then empty else "release if (\($r.if)) is not master only" end)'
check "docker-publish: only deploy-test and apk-archive can write the repository; release reads it" docker-publish.yml '
(writers | if . != ["apk-archive", "deploy-test"] then "the jobs that can write the repository are \(.)" else empty end),
(.jobs.release.permissions.contents | if . != "read" then "release has contents: \(.)" else empty end)'
check "docker-publish: deploy-test reads release outputs that a release step writes" docker-publish.yml \
  'outputs_wired("deploy-test"; "release")'

# promote.yml
# The outputs env-deploy.sh writes: its printf "commit=%s\nfrontend_commit=%s\n..." to $GITHUB_OUTPUT
deploy_outputs=$(grep -E '^ *printf "[a-z_]+=%s' "$scripts/env-deploy.sh" | sed 's/\\n/ /g' | grep -oE '[a-z_]+=%s' \
  | sed 's/=%s$//' | jq -Rsc 'split("\n") | map(select(. != ""))')
check "promote: promote chooses the images (a re-run's deploy, else :from's), retags them, then Deploy, then Release" promote.yml '
.jobs.promote as $j
| ($j | step_index(.id == "promote")) as $p | ($j | step_index(.id == "deploy")) as $d
| ($j | step_index(.name // "" | startswith("Release"))) as $r
| if $p == null or $d == null or $r == null then "promote, deploy or Release step missing (\([$p, $d, $r]))"
  elif ($p < $d and $d < $r) | not then "steps out of order: promote \($p), deploy \($d), Release \($r)"
  else empty end,
  ($j | step("promote").run // "") as $run
  | ("deployed=$(.github/scripts/env-run-images.sh \"$TO\")",
     "backend_digest=$(sed ", "frontend_digest=$(sed ",
     "backend_digest=$(docker buildx imagetools inspect \"$BACKEND:$FROM\"",
     "frontend_digest=$(docker buildx imagetools inspect \"$FRONTEND:$FROM\"",
     "imagetools create -t \"$BACKEND:$TO\" \"$BACKEND@$backend_digest\"",
     "imagetools create -t \"$FRONTEND:$TO\" \"$FRONTEND@$frontend_digest\"",
     "echo \"backend=$BACKEND@$backend_digest\" >> \"$GITHUB_OUTPUT\"",
     "echo \"frontend=$FRONTEND@$frontend_digest\" >> \"$GITHUB_OUTPUT\"")
  | . as $want | if ($run | contains($want)) then empty else "the promote step lacks: \($want)" end,
  ($j | step("deploy").env) as $e
  | if $e.BACKEND != "${{ steps.promote.outputs.backend }}" or $e.FRONTEND != "${{ steps.promote.outputs.frontend }}"
    then "Deploy does not get the promoted images: \($e)" else empty end'
check "promote: Release gets every deploy output env-deploy.sh writes, from the deploy step" promote.yml '
([.jobs.promote.steps[] | select(.name // "" | startswith("Release"))][0].env // {}) as $e
| ($outs[] | . as $o | ($o | ascii_upcase) as $var
   | if $e[$var] == "${{ steps.deploy.outputs.\($o) }}" then empty else "Release env \($var) is \($e[$var]), not the deploy output \($o)" end),
  (if ($outs | sort) != (["commit", "deploy_commit", "deploy_tag", "frontend_commit"]) then "env-deploy.sh writes \($outs)" else empty end)' \
  --argjson outs "$deploy_outputs"
for f in promote.yml promote-to-staging.yml promote-to-prod.yml; do
  check "$f: the promotion has contents and packages write, nothing more" "$f" '
  .jobs.promote.permissions | if . != {"contents": "write", "packages": "write"} then "permissions \(.)" else empty end'
done

# helm-chart.yml
check "helm-chart: release needs publish and runs only when publish output a chart" helm-chart.yml '
.jobs.release
| if (needs_of | index("publish")) == null then "release needs \(needs_of)" else empty end,
  (if (.if // "" | test("needs\\.publish\\.outputs\\.(version|digest) != '"''"'")) then empty else "release if: \(.if)" end)'
check "helm-chart: only release and apk-archive can write the repository" helm-chart.yml '
writers | if . != ["apk-archive", "release"] then "the jobs that can write the repository are \(.)" else empty end'
# The outputs chart-publish.sh writes: its { echo "version=..."; echo "digest=..."; } >> "$GITHUB_OUTPUT" line
publish_outputs=$(grep -E '^[^#]*>> *"\$\{?GITHUB_OUTPUT' "$scripts/chart-publish.sh" | grep -oE 'echo "[a-z_]+=' \
  | sed 's/^echo "//; s/=$//' | jq -Rsc 'split("\n") | map(select(. != "")) | unique')
check "helm-chart: release reads publish outputs that the publish step declares, from chart-publish.sh" helm-chart.yml '
. as $w | $w.jobs.release | refs("needs\\.publish\\.outputs\\.([A-Za-z0-9_-]+)")[] as [$o] | $w.jobs.publish as $p
| ($p.outputs[$o] // "") as $v
| if $v != "${{ steps.publish.outputs.\($o) }}" then "release reads needs.publish.outputs.\($o); publish declares \($v | tojson)"
  elif ($p | step("publish").run // "" | contains(".github/scripts/chart-publish.sh")) | not then "the publish step does not run chart-publish.sh"
  elif ($outs | index($o)) == null then "chart-publish.sh writes \($outs), not \($o)"
  else empty end' --argjson outs "$publish_outputs"

if [[ "$failures" != 0 ]]; then echo "$failures workflow check(s) failed"; exit 1; fi
echo "All workflow checks passed."
