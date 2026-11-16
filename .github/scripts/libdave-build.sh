#!/bin/sh
# Builds Discord's libdave (DAVE, Discord voice's end-to-end encryption) as a shared library for Alpine (musl), in the
# Alpine container the backend image is based on: Discord's own builds are for glibc. Run by libdave.yml, on each
# architecture's own runner, inside alpine:<the lock's alpine>. Writes /out/libdave-<arch>.so and the licenses.
#   libdave-build.sh <libdave tag> <libdave commit>
set -eu
tag="$1"
commit="$2"
apk add -q build-base cmake ninja git curl zip unzip tar pkgconf linux-headers perl bash >/dev/null
git clone -q --depth 1 --branch "$tag" https://github.com/discord/libdave /src
# The tag still names the commit the lock was written for
test "$(git -C /src rev-parse HEAD)" = "$commit" || { echo "$tag is no longer $commit" >&2; exit 1; }
cd /src && git -c advice.detachedHead=false submodule update -q --init cpp/vcpkg
cd /src/cpp
# vcpkg's own tools from the system (its downloaded ones are glibc's)
export VCPKG_FORCE_SYSTEM_BINARIES=1
./vcpkg/bootstrap-vcpkg.sh -disableMetrics >/dev/null
make shared
arch="$(uname -m)"
mkdir -p /out/licenses
cp build/libdave.so "/out/libdave-$arch.so"
cp ../LICENSE /out/licenses/libdave
