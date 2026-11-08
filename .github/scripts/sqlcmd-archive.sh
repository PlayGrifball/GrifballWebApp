#!/usr/bin/env bash
# sqlcmd-archive.sh [Dockerfile, default GrifballWebApp.Server/Dockerfile]
#
# Makes sure the sqlcmd the backend image installs (Microsoft's go-sqlcmd release, at the Dockerfile's SQLCMD_VERSION
# and sha256s) is in this repo's own release, sqlcmd-<version>, as <sha256>.tar.bz2 for each platform, published by
# release-archive.sh (which see). The Dockerfile downloads from there first: the build doesn't depend on Microsoft's
# download staying up or staying there (github.com once answered 504 for one of them for hours), and the files can't
# change. From Microsoft's release through GitHub's API, else its download link.
# Env: GH_TOKEN (contents: write), GITHUB_REPOSITORY, TARGET (see release-archive.sh).
set -euo pipefail

dockerfile=${1:-GrifballWebApp.Server/Dockerfile}
version=$(sed -n -E 's/^ARG SQLCMD_VERSION=(v[0-9.]+)$/\1/p' "$dockerfile")
[ -n "$version" ] || { echo "::error::No ARG SQLCMD_VERSION in $dockerfile" >&2; exit 1; }
list=$(mktemp)
for arch in amd64 arm64; do
  sha=$(sed -n -E "s/^ +$arch\) sha=([0-9a-f]{64}) ;; \\\\$/\\1/p" "$dockerfile")
  [ -n "$sha" ] || { echo "::error::No $arch sha256 for sqlcmd in $dockerfile" >&2; exit 1; }
  file="sqlcmd-linux-$arch.tar.bz2"
  echo "$sha $sha.tar.bz2 gh:microsoft/go-sqlcmd@$version/$file https://github.com/microsoft/go-sqlcmd/releases/download/$version/$file"
done > "$list"

exec "$(dirname "$0")/release-archive.sh" "sqlcmd-$version" "sqlcmd $version" \
  "Microsoft's go-sqlcmd $version (https://github.com/microsoft/go-sqlcmd/releases/tag/$version) for linux/amd64 and linux/arm64, named by sha256, which the backend image installs (GrifballWebApp.Server/Dockerfile): the build downloads it from here, so it doesn't depend on that download staying up or staying there (docs/reproducible-builds.md). Made by CI (.github/scripts/sqlcmd-archive.sh); immutable once published." \
  "$list"
