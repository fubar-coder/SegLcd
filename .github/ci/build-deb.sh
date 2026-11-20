#!/usr/bin/env bash
# Builds the Debian package from the source tarball. Usage: build-deb.sh <SegLcd-VERSION.tar.gz> <outdir>
set -euo pipefail
tarball=$(realpath "$1"); out=$(realpath -m "$2"); mkdir -p "$out"
version=$(basename "$tarball" .tar.gz); version=${version#SegLcd-}
here=$(dirname "$(realpath "$0")")

export DEBIAN_FRONTEND=noninteractive
apt-get -qq update
apt-get -qq install -y --no-install-recommends build-essential debhelper devscripts lintian clang zlib1g-dev \
    curl ca-certificates libicu-dev gettext >/dev/null
"$here/install-dotnet.sh"

work=$(mktemp -d)
cp "$tarball" "$work/seglcd_$version.orig.tar.gz"
cd "$work" && tar -xf "seglcd_$version.orig.tar.gz"
cd "SegLcd-$version" && cp -r packaging/debian debian
# dotnet-sdk-11.0 comes from dotnet-install.sh, not apt -> -d (do not check build dependencies).
dpkg-buildpackage -us -uc -d
lintian "$work"/*.changes || true
cp "$work"/*.deb "$work"/*.dsc "$work"/*.debian.tar.* "$out/"
ls -l "$out"
