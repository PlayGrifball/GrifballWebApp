#!/bin/sh
# The real archive's checks that a change broke (historical-archive-run.yml): from the base's and the head's test results
# (GrifballWebApp.HistoricalArchive.Test, as trx), every check that passed on the base and fails (or didn't run) on the
# head, one per line. The import's known gaps (#511) fail some checks on both sides: those, a check the change adds that
# fails, and one it removes or renames are not regressions. Exits 1 when there is one, 0 otherwise.
# Usage: historical-archive-regressions.sh <base.trx> <head.trx>
set -eu
base=$1
head=$2
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

# "<outcome><TAB><test name>" for each result in a trx (a test case with arguments is its own name, its quotes unescaped)
outcomes() {
  grep -o '<UnitTestResult [^>]*>' "$1" \
    | sed -nE 's/.*testName="([^"]*)".*outcome="([^"]*)".*/\2\t\1/p' \
    | sed -e 's/&quot;/"/g; s/&apos;/'"'"'/g; s/&lt;/</g; s/&gt;/>/g; s/&amp;/\&/g'
}

outcomes "$base" > "$work/base"
outcomes "$head" > "$work/head"
awk -F'\t' '
  NR == FNR { if ($1 == "Passed") passed[$2] = 1; next }
  ($2 in passed) && $1 != "Passed" { print $2 }
' "$work/base" "$work/head" | sort -u > "$work/regressions"
cat "$work/regressions"
[ ! -s "$work/regressions" ]
