#!/usr/bin/env bash
# Tests of .github/scripts: bash .github/scripts/tests/run.sh (the Migrations workflow runs them).
#   check-master-migrations.sh: every fixtures/pass-*.txt (git diff --name-status output) passes; every
#     fixtures/fail-*.txt fails, listing exactly the lines of its .expect; then the same on a real
#     repository, from a branch against its master.
#   master-migrations.sh: a repository whose master regenerated a migration in a merged branch, then
#     renamed one: "now" and "ever" lists as the preview reset expects them.
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

if [ "$failures" != 0 ]; then echo "$failures failure(s)"; exit 1; fi
echo "All .github/scripts tests passed."
