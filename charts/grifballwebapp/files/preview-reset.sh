# Previews only (backend.config.Preview.Enabled; templates/_migrations.tpl, the preview-reset and
# preview-recheck steps). POSIX sh and BusyBox awk (the backend image is Alpine). Unit tests:
# tests/preview-reset/run.sh. No doubled dollar or dollar-parenthesis-variable-name here: Kubernetes
# expands both in a container's args.
#
# A preview keeps its database across pushes. When the PR regenerates or renames a migration of its
# own, the database still has the old ID applied and the new one fails (it makes what is already
# there). preview_reset_decide says what to do with the database's migrations ($1), the image's ($2),
# every ID master has ever had ($3) and master's current ones ($4), when the image was built; $5 is a
# scratch folder:
#   0  nothing to reset: the database has no migration the image lacks (the usual case);
#   10 reset: it has some, and none of them is or was master's - only this PR's own;
#   1  refuse: one of them is master's, the image lacks some of master's (the PR is behind master: a
#      reset would restore a backup with master's newer migrations and fail the same way), or the
#      image has no master list to tell by.
# It says why on stdout either way.

# Lines of file $1 that aren't in file $2, blank ones aside. FILENAME, not NR == FNR: that misreads an
# empty $2.
preview_minus() {
  awk 'FILENAME == ARGV[1] { if ($0 != "") seen[$0]; next } $0 != "" && !($0 in seen)' "$2" "$1"
}

# Lines of file $1 that are in file $2.
preview_both() {
  awk 'FILENAME == ARGV[1] { if ($0 != "") seen[$0]; next } $0 != "" && ($0 in seen)' "$2" "$1"
}

preview_words() { tr '\n' ' ' < "$1" | sed 's/ *$//'; }

preview_reset_decide() {
  applied=$1 build=$2 ever=$3 current=$4 scratch=$5
  db=${DB_NAME:-the database}
  preview_minus "$applied" "$build" > "$scratch/preview-unknown.txt"
  if [ ! -s "$scratch/preview-unknown.txt" ]; then
    echo "Preview database $db has no migration this build lacks: nothing to reset."
    return 0
  fi
  unknown=$(preview_words "$scratch/preview-unknown.txt")
  if [ ! -s "$ever" ] || [ ! -s "$current" ]; then
    echo "ERROR: preview database $db has migrations this build lacks: $unknown"
    echo "This image has no list of master's migrations ($ever, $current: CI writes them when it"
    echo "builds the image), so whether they are only this PR's can't be told: not resetting."
    echo "Reset it by hand: remove the PR's preview label, wait for its namespace to go, add the label back."
    return 1
  fi
  preview_both "$scratch/preview-unknown.txt" "$ever" > "$scratch/preview-master.txt"
  if [ -s "$scratch/preview-master.txt" ]; then
    echo "ERROR: preview database $db has master migration(s) this build lacks: $(preview_words "$scratch/preview-master.txt")"
    echo "They came from master (grif-test's backup, or an earlier build of this PR that had merged master),"
    echo "so this PR is behind master: merge master into the PR. Not resetting: a master migration is never"
    echo "this PR's own to throw away."
    return 1
  fi
  preview_minus "$current" "$build" > "$scratch/preview-behind.txt"
  if [ -s "$scratch/preview-behind.txt" ]; then
    echo "ERROR: preview database $db has migrations this build lacks, only this PR's own: $unknown"
    echo "But this build also lacks master's migration(s): $(preview_words "$scratch/preview-behind.txt")"
    echo "The PR is behind master, and a reset restores a backup that has them: merge master into the PR"
    echo "(its next build resets the database). Not resetting."
    return 1
  fi
  echo "Preview database $db has migrations this build lacks, none of them master's: $unknown"
  echo "They are this PR's own, regenerated or renamed since they were applied: resetting the database."
  return 10
}

# After a reset, the database restored (or converted) again must have no migration the image lacks:
# 0 if so; 1 if it has (master has moved on since the image was built, and grif-test with it).
preview_reset_recheck() {
  applied=$1 build=$2 scratch=$3
  db=${DB_NAME:-the database}
  preview_minus "$applied" "$build" > "$scratch/preview-unknown.txt"
  if [ -s "$scratch/preview-unknown.txt" ]; then
    echo "ERROR: preview database $db, reset and restored again, has migrations this build lacks: $(preview_words "$scratch/preview-unknown.txt")"
    echo "Master has moved on since this image was built, and the backup with it: merge master into the PR."
    return 1
  fi
  echo "Preview database $db, reset, has no migration this build lacks."
  return 0
}
