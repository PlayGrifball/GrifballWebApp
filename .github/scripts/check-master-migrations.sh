#!/usr/bin/env bash
# check-master-migrations.sh [base ref]
#
# Fails if a change modifies, deletes or renames a migration master already has: a migration file
# (GrifballWebApp.Migrations.SqlServer/Migrations/*.cs, GrifballWebApp.Migrations.Postgres/Migrations/*.cs,
# .Designer.cs included) that exists at the merge base with <base ref>. Databases record a migration's
# ID once they've applied it and never run it again, so an edited or regenerated master migration
# never reaches the databases that already have it (grif-test, staging, prod, every preview). New
# migration files are fine; the GrifballContextModelSnapshot.cs files are expected to change.
#
# With a base ref (the workflow: origin/master), it diffs <base ref>...HEAD (from the merge base, so a
# PR behind master isn't blamed for master's own new migrations). Without one, it reads
# `git diff --name-status -M` output on stdin (the tests' fixtures: .github/scripts/tests).
set -euo pipefail

if [ $# -gt 0 ]; then
  diff=$(git diff --name-status -M "$1...HEAD" --)
else
  diff=$(cat)
fi

migration='^GrifballWebApp\.Migrations\.(SqlServer|Postgres)/Migrations/[^/]+\.cs$'
snapshot='ModelSnapshot\.cs$'
bad=()
while IFS=$'\t' read -r status path new; do
  [ -n "$status" ] || continue
  case "$status" in
    A*|C*) continue ;;  # a new file (a copy leaves its source as it was)
    D*) what=deleted ;;
    R*) what="renamed to $new" ;;
    *) what=modified ;;  # M, T (type change), anything else
  esac
  [[ $path =~ $migration ]] || continue
  [[ $path =~ $snapshot ]] && continue
  bad+=("$path ($what)")
done <<< "$diff"

if [ ${#bad[@]} -eq 0 ]; then
  echo "No migration that master already has was modified, deleted or renamed."
  exit 0
fi

{
  echo "::error title=Master migrations changed::This change modifies, deletes or renames ${#bad[@]} migration file(s) that already exist on master. Master migrations must never be edited or regenerated: add a new migration instead."
  echo
  echo "Master migrations changed:"
  printf '  %s\n' "${bad[@]}"
  echo
  echo "A database records each migration's ID when it applies it and never runs it again, so an edited or"
  echo "regenerated master migration never reaches grif-test, staging, prod or the previews, and a renamed"
  echo "one runs a second time. Restore these files from master (git checkout origin/master -- <file>)"
  echo "and put the change in a new migration (dotnet ef migrations add ...), for both providers."
} >&2
exit 1
