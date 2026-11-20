#!/usr/bin/env bash
# Builds the RPM from the source tarball. Usage: build-rpm.sh <SegLcd-VERSION.tar.gz> <outdir>
set -euo pipefail
tarball=$(realpath "$1"); out=$(realpath -m "$2"); mkdir -p "$out"
version=$(basename "$tarball" .tar.gz); version=${version#SegLcd-}

dnf -y -q install rpm-build rpmlint systemd-rpm-macros clang zlib-devel tar gzip gettext dotnet-sdk-11.0
mkdir -p ~/rpmbuild/SOURCES
cp "$tarball" ~/rpmbuild/SOURCES/
tar -xOf "$tarball" "SegLcd-$version/packaging/rpm/seglcd.spec" > ~/seglcd.spec
rpmbuild -ba ~/seglcd.spec
rpmlint ~/rpmbuild/RPMS/*/seglcd-*.rpm || true
cp ~/rpmbuild/RPMS/*/seglcd-*.rpm ~/rpmbuild/SRPMS/seglcd-*.src.rpm "$out/"
ls -l "$out"
