#!/usr/bin/env bash
# check-one-migration.sh [base ref]
#
# Fails if a change adds more than one migration to a migrations project
# (GrifballWebApp.Migrations.SqlServer, .Postgres or .Sqlite): counted by new *.Designer.cs files. `ef migrations
# remove` rebuilds the snapshot from the newest remaining migration's Designer, and only the newest one's is checked
# against the snapshot (DatabaseProviderTests.NewestMigration_DesignerMatchesTheSnapshot). With two, the older one's
# Designer can miss what master gained since: removing the newer one then drops master's changes from the snapshot.
#
# With a base ref (the workflow: origin/master), it diffs <base ref>...HEAD (from the merge base, so master's own new
# migrations aren't counted). Without one, it reads `git diff --name-status -M` output on stdin.
set -euo pipefail

if [ $# -gt 0 ]; then
  diff=$(git diff --name-status -M "$1...HEAD" --)
else
  diff=$(cat)
fi

designer='^GrifballWebApp\.Migrations\.(SqlServer|Postgres|Sqlite)/Migrations/[^/]+\.Designer\.cs$'
declare -A added=()
while IFS=$'\t' read -r status path new; do
  [ -n "$status" ] || continue
  case "$status" in
    A*|C*) ;;
    R*) path=$new ;;  # a new migration git paired with a deleted one; master's own renames fail check-master-migrations.sh
    *) continue ;;
  esac
  [[ $path =~ $designer ]] || continue
  project=${path%%/*}
  added[$project]+="${path##*/} "
done <<< "$diff"

bad=()
for project in "${!added[@]}"; do
  read -ra files <<< "${added[$project]}"
  [ ${#files[@]} -gt 1 ] && bad+=("$project: ${files[*]}")
done

if [ ${#bad[@]} -eq 0 ]; then
  echo "At most one new migration per migrations project."
  exit 0
fi

{
  echo "::error title=More than one new migration::This change adds more than one migration to a migrations project. Fold them into one migration, generated on top of master."
  echo
  echo "Projects with more than one new migration:"
  printf '  %s\n' "${bad[@]}"
  echo
  echo "Delete the new migrations (both files each) and restore the snapshot from master"
  echo "(git checkout origin/master -- <project>/Migrations/GrifballContextModelSnapshot.cs), then generate one"
  echo "migration (dotnet ef migrations add ...) and put any hand-written SQL back in it, for every provider."
} >&2
exit 1
