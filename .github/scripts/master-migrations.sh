#!/usr/bin/env bash
# master-migrations.sh <out dir> [ref, default origin/master]
#
# Writes master's EF Core migration IDs, per provider, for the backend image (GrifballWebApp.Server/
# Dockerfile copies <out dir> = .master-migrations into /app/master-migrations): a preview's migration
# Job uses them to tell the PR's own migrations from master's before it resets a preview database
# (charts/grifballwebapp, files/preview-reset.sh). docker-publish.yml and helm-chart.yml run it before
# building, in a full clone (fetch-depth: 0).
#   sqlserver.txt, postgres.txt:            master's migrations now (its tree);
#   sqlserver-ever.txt, postgres-ever.txt:  every migration master has ever had: those now, and every
#     one a commit on master's first-parent line added (a merged PR's net change, so a migration a PR
#     regenerated before it merged was never master's).
# An ID is its file's name: EF Core names <id>.cs and <id>.Designer.cs after the migration's ID.
set -euo pipefail

out=${1:?usage: $0 <out dir> [ref]}
ref=${2:-origin/master}
if [ "$(git rev-parse --is-shallow-repository)" = true ]; then
  echo "::error::A shallow clone: master's history is incomplete. Check out with fetch-depth: 0." >&2
  exit 1
fi
git rev-parse --verify -q "$ref^{commit}" >/dev/null || { echo "::error::$ref isn't a commit here." >&2; exit 1; }

ids() { sed -n -E 's#^(.*/)?([0-9]{14}_[A-Za-z0-9_]+)\.Designer\.cs$#\2#p' | LC_ALL=C sort -u; }

mkdir -p "$out"
for p in SqlServer:sqlserver Postgres:postgres; do
  dir="GrifballWebApp.Migrations.${p%%:*}/Migrations"
  name=${p##*:}
  git ls-tree -r --name-only "$ref" -- "$dir" | ids > "$out/$name.txt"
  {
    git ls-tree -r --name-only "$ref" -- "$dir"
    git log --first-parent --diff-merges=first-parent --no-renames --diff-filter=A \
      --name-only --format= "$ref" -- "$dir"
  } | ids > "$out/$name-ever.txt"
  [ -s "$out/$name.txt" ] || { echo "::error::No migrations in $dir on $ref." >&2; exit 1; }
  echo "$dir on $ref ($(git rev-parse --short "$ref")): $(wc -l < "$out/$name.txt") migrations, $(wc -l < "$out/$name-ever.txt") ever."
done
