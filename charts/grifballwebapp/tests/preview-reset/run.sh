#!/bin/sh
# Unit tests of files/preview-reset.sh, the preview database reset's decision: sh tests/preview-reset/run.sh
# (the Helm chart workflow runs them under BusyBox too, as the backend image would).
set -eu
here=$(cd "$(dirname "$0")" && pwd)
. "$here/../../files/preview-reset.sh"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
failures=0

# list <file> <id>...: a migration list, one ID per line ("none" for an empty file).
list() { f=$1; shift; : > "$f"; for id in "$@"; do [ "$id" = none ] || echo "$id" >> "$f"; done; }

# check <name> <expected status> <expected output pattern>, after the fixtures are written.
check() {
  name=$1 want=$2 pattern=$3
  rc=0
  DB_NAME=GrifballWebApp preview_reset_decide "$work/applied" "$work/build" "$work/ever" "$work/current" "$work" > "$work/out" || rc=$?
  if [ "$rc" != "$want" ]; then
    echo "FAIL: $name: status $rc, expected $want"; sed 's/^/  | /' "$work/out"; failures=$((failures + 1)); return
  fi
  if ! grep -q -- "$pattern" "$work/out"; then
    echo "FAIL: $name: output lacks \"$pattern\""; sed 's/^/  | /' "$work/out"; failures=$((failures + 1)); return
  fi
  echo "ok: $name"
}

M1=20250127035001_InitialMigration
M2=20261031053312_AddDiscordOutbox
OLD=20261101000000_AddSeasonName
NEW=20261102000000_AddSeasonName
GONE=20250601040615_DiscordSupport

# The master lists as CI writes them: current, and every ID master has ever had.
list "$work/current" $M1 $M2
list "$work/ever" $M1 $GONE $M2

list "$work/applied" $M1 $M2
list "$work/build" $M1 $M2
check "up to date: normal migrate" 0 "nothing to reset"

list "$work/applied" $M1
check "pending only (the PR adds one): normal migrate" 0 "nothing to reset"

list "$work/applied" none
check "a new database (nothing applied): normal migrate" 0 "nothing to reset"

list "$work/applied" $M1 $M2 $OLD
list "$work/build" $M1 $M2 $NEW
check "the PR's own migration regenerated: reset" 10 "none of them master's: $OLD"

list "$work/applied" $M1 $M2 $OLD "$OLD-b"
check "several of the PR's own, all reset" 10 "$OLD $OLD-b"

list "$work/applied" $M1 $M2 $OLD
list "$work/build" $M1 $NEW
check "master migration missing from the build (behind master): refuse" 1 "lacks master's migration(s): $M2"

list "$work/applied" $M1 $M2 $OLD
list "$work/build" $M1 $NEW
list "$work/current" $M1 $M2
check "behind master: says to merge master" 1 "merge master into the PR"

list "$work/applied" $M1 $M2
list "$work/build" $M1
check "the database has a current master migration the build lacks: refuse" 1 "master migration(s) this build lacks: $M2"

list "$work/applied" $M1 $M2 $GONE
list "$work/build" $M1 $M2
check "the database has a former master migration: refuse" 1 "master migration(s) this build lacks: $GONE"

list "$work/applied" $M1 $M2 $GONE $OLD
check "a former master migration among the PR's own: refuse, naming only master's" 1 "lacks: $GONE\$"

list "$work/applied" $M1 $M2 $OLD
list "$work/build" $M1 $M2 $NEW
list "$work/ever" none
check "no master list in the image: refuse" 1 "not resetting"
list "$work/ever" $M1 $GONE $M2
list "$work/current" none
check "no current master list in the image: refuse" 1 "Reset it by hand"
list "$work/current" $M1 $M2

list "$work/applied" $M1 $M2 $OLD
list "$work/build" $M1 $M2 $NEW
list "$work/ever" none
list "$work/current" none
list "$work/applied" $M1 $M2
check "no master list, but nothing unknown: normal migrate" 0 "nothing to reset"

# After a reset and a restore.
recheck() {
  name=$1 want=$2 pattern=$3
  rc=0
  DB_NAME=GrifballWebApp preview_reset_recheck "$work/applied" "$work/build" "$work" > "$work/out" || rc=$?
  if [ "$rc" != "$want" ] || ! grep -q -- "$pattern" "$work/out"; then
    echo "FAIL: $name: status $rc (expected $want)"; sed 's/^/  | /' "$work/out"; failures=$((failures + 1)); return
  fi
  echo "ok: $name"
}
list "$work/applied" $M1
list "$work/build" $M1 $NEW
recheck "restored backup older than the build: fine" 0 "no migration this build lacks"
list "$work/applied" $M1 $M2
list "$work/build" $M1 $NEW
recheck "restored backup with a master migration the build lacks: refuse" 1 "lacks: $M2"

if [ "$failures" != 0 ]; then echo "$failures failure(s)"; exit 1; fi
echo "All preview reset decision tests passed."
