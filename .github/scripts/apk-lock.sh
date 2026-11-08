#!/usr/bin/env bash
# apk-lock.sh [lock, default GrifballWebApp.Server/apk.lock]
#
# Re-resolves the backend image's Alpine packages (GrifballWebApp.Server/Dockerfile) and rewrites the lock: the packages
# in its "# packages:" line, and every one they pull in that the base image doesn't already have, as each platform's
# base image (the Dockerfile's "AS base" FROM, by digest) would install them today. One line per package and platform:
#   <arch> <name> <version> <sha256 of the .apk>
# The image installs exactly these, from the repo's apk-archive release (apk-archive.sh uploads them), so the same
# commit always installs the same bytes. Needs Docker, and QEMU for the arm64 base image. apk-lock.yml runs it.
set -euo pipefail

lock=${1:-GrifballWebApp.Server/apk.lock}
dockerfile="$(dirname "$lock")/Dockerfile"
base=$(sed -n -E 's#^FROM (mcr\.microsoft\.com/dotnet/aspnet:[^ ]+) AS base$#\1#p' "$dockerfile")
packages=$(sed -n -E 's/^# packages: //p' "$lock")
[ -n "$base" ] || { echo "::error::No 'FROM ... AS base' in $dockerfile" >&2; exit 1; }
[ -n "$packages" ] || { echo "::error::No '# packages:' line in $lock" >&2; exit 1; }

# name version, from apk's database of what is installed
resolve='set -eu
installed() { awk "/^P:/ { p = substr(\$0, 3) } /^V:/ { print p, substr(\$0, 3) }" /lib/apk/db/installed | sort; }
installed > /tmp/before
apk add -q --no-cache $PACKAGES > /dev/null
installed > /tmp/after
comm -13 /tmp/before /tmp/after > /tmp/added
test -s /tmp/added
mkdir -p /tmp/apk && cd /tmp/apk
while read -r name version; do
  apk fetch -q --no-cache -o . "$name" || exit 1
  test -f "$name-$version.apk" || { echo "$name: fetched another version than $version" >&2; exit 1; }
  printf "%s %s %s %s\n" "$(apk --print-arch)" "$name" "$version" "$(sha256sum "$name-$version.apk" | cut -d" " -f1)"
done < /tmp/added'

body=$(mktemp)
for platform in linux/amd64 linux/arm64; do
  docker run --rm --platform "$platform" --user root --network host -e PACKAGES="$packages" --entrypoint sh "$base" \
    -c "$resolve" >> "$body"
done
for arch in x86_64 aarch64; do
  grep -q "^$arch " "$body" || { echo "::error::No $arch packages resolved: the lock is left as it was" >&2; exit 1; }
done
alpine=$(docker run --rm --platform linux/amd64 --network host --entrypoint cat "$base" /etc/alpine-release)

{
  echo "# The Alpine packages the backend image installs (GrifballWebApp.Server/Dockerfile), from the repo's apk-archive"
  echo "# release. Written by .github/scripts/apk-lock.sh (apk-lock.yml); change the packages line, not the rest."
  echo "# packages: $packages"
  echo "# alpine: $alpine"
  echo "# base: $base"
  LC_ALL=C sort "$body"
} > "$lock"
echo "$lock: $(grep -vc '^#' "$lock") packages for $base (Alpine $alpine)"
