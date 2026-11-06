#!/usr/bin/env bash
# Tests of .github/scripts: bash .github/scripts/tests/run.sh (the Migrations workflow runs them).
#   check-master-migrations.sh: every fixtures/pass-*.txt (git diff --name-status output) passes; every
#     fixtures/fail-*.txt fails, listing exactly the lines of its .expect; then the same on a real
#     repository, from a branch against its master.
#   master-migrations.sh: a repository whose master regenerated a migration in a merged branch, then
#     renamed one: "now" and "ever" lists as the preview reset expects them.
#   changed-areas.sh: the workflows' backend/frontend/images flags for a PR's files (gh stubbed).
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
[ "$(cat "$work/lists/postgres.txt")" = 20250101000005_Initial ] && ok "master-migrations.sh: PostgreSQL's" || bad "master-migrations.sh: postgres.txt"

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
areas() { # areas <event> <files...>: prints "backend=... frontend=..."
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
  [ "$got" = "$expected" ] && ok "changed-areas.sh: $name" || bad "changed-areas.sh: $name gave '$got', not '$expected'"
}
expect_areas "backend only" "backend=true frontend=false images=false" pull_request GrifballWebApp.Server/Program.cs docs/x.md
expect_areas "client only" "backend=false frontend=true images=false" pull_request grifballwebapp.client/src/main.ts
expect_areas "docs only" "backend=false frontend=false images=false" pull_request docs/historical-import.md readme.md charts/grif/values.yaml
expect_areas "the solution and the test workflow" "backend=true frontend=false images=false" pull_request GrifballWebApp.slnx .github/workflows/dotnet-tests.yml
expect_areas "the script itself: everything" "backend=true frontend=true images=true" pull_request .github/scripts/changed-areas.sh
expect_areas "a push: everything" "backend=true frontend=true images=true" push docs/x.md
GH_FAILS=1 expect_areas "unreadable file list: everything" "backend=true frontend=true images=true" pull_request docs/x.md
# What the images are built from: the arm64 check runs; app code alone doesn't change it
expect_areas "a Dockerfile" "backend=true frontend=false images=true" pull_request GrifballWebApp.Server/Dockerfile
expect_areas "the client's packages" "backend=false frontend=true images=true" pull_request grifballwebapp.client/package-lock.json
expect_areas "a project file" "backend=true frontend=false images=true" pull_request GrifballWebApp.Server/GrifballWebApp.Server.csproj
expect_areas "the image workflow" "backend=false frontend=false images=true" pull_request .github/workflows/docker-publish.yml
expect_areas "code, not packages" "backend=true frontend=true images=false" pull_request GrifballWebApp.Server/Program.cs grifballwebapp.client/src/app/app.ts
# A PR as big as this one: past a pipe's buffer (a pipe into grep -q broke here and read as "no change")
big="GrifballWebApp.Server/Program.cs grifballwebapp.client/src/main.ts $(seq -f 'docs/a-long-directory-name-to-pass-the-pipe-buffer/page-%g.md' 1 2500 | tr '\n' ' ')"
# shellcheck disable=SC2086 # one word per file
expect_areas "2500 files, the code first" "backend=true frontend=true images=false" pull_request $big

if [ "$failures" != 0 ]; then echo "$failures failure(s)"; exit 1; fi
echo "All .github/scripts tests passed."
