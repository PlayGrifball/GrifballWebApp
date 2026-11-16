#!/bin/sh
# The real archive's import before and after a change (historical-archive.yml): from the two summary.md files
# GrifballWebApp.HistoricalArchive.Test writes, a Markdown report of each season (and Late League season) whose counts
# changed, each count that changed as "before → after", and the seasons that came or went.
# Usage: historical-archive-compare.sh <base summary.md> <head summary.md>
set -eu
base=$1
head=$2
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

# The rows of a summary's table under a "## <title>" heading: "season<TAB>header=value<TAB>..." per row
rows() {
  awk -v title="$2" '
    /^## / { inside = ($0 == "## " title); header = ""; next }
    !inside || !/^\|/ { next }
    {
      n = split($0, cells, "|")
      for (i = 2; i < n; i++) { gsub(/^ +| +$/, "", cells[i]) }
      if (header == "") { for (i = 2; i < n; i++) names[i] = cells[i]; header = 1; next }
      if (cells[2] ~ /^-+$/) next
      line = cells[2]
      for (i = 4; i < n; i++) line = line "\t" names[i] "=" cells[i]
      print line
    }' "$1" | sort
}

compare() {
  title=$1
  rows "$base" "$title" > "$work/base" 2>/dev/null || true
  rows "$head" "$title" > "$work/head" 2>/dev/null || true
  awk -F'\t' -v title="$title" '
    NR == FNR { before[$1] = $0; next }
    {
      seen[$1] = 1
      if (!($1 in before)) { added[++a] = $1; next }
      if (before[$1] == $0) next
      # By header, so a column a change adds (or drops) is new (or gone), not a shift of the others
      split(before[$1], old, "\t"); delete was
      for (i = 2; i in old; i++) { split(old[i], kv, "="); was[kv[1]] = kv[2] }
      changes = ""
      for (i = 2; i <= NF; i++) {
        split($i, kv, "=")
        if ((kv[1] in was) ? was[kv[1]] == kv[2] : (kv[2] == "" || kv[2] == "0")) continue
        changes = changes (changes == "" ? "" : ", ") kv[1] " " ((kv[1] in was) ? was[kv[1]] : "–") " → " kv[2]
      }
      if (changes == "") next
      changed[++c] = "| " $1 " | " changes " |"
    }
    END {
      for (s in before) if (!(s in seen)) removed[++r] = s
      if (c + a + r == 0) { print "**" title "**: no change."; exit }
      print "**" title "**: " c + 0 " changed, " a + 0 " new, " r + 0 " gone."
      if (c) { print ""; print "| Season | Change |"; print "|---|---|"; for (i = 1; i <= c; i++) print changed[i] }
      if (a) { print ""; printf "New:"; for (i = 1; i <= a; i++) printf " %s%s", added[i], (i < a ? ";" : ""); print "" }
      if (r) { print ""; printf "Gone:"; for (i = 1; i <= r; i++) printf " %s%s", removed[i], (i < r ? ";" : ""); print "" }
    }' "$work/base" "$work/head"
  echo
}

compare "Seasons"
compare "Late League"
