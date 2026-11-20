#!/usr/bin/env bash
# Builds the Arch package from the source tarball. Usage: build-arch.sh <SegLcd-VERSION.tar.gz> <outdir>
set -euo pipefail
tarball=$(realpath "$1"); out=$(realpath -m "$2"); mkdir -p "$out"
version=$(basename "$tarball" .tar.gz); version=${version#SegLcd-}
here=$(dirname "$(realpath "$0")")

pacman -Syu --noconfirm --needed base-devel clang zlib hidapi git gettext >/dev/null
"$here/install-dotnet.sh"

# makepkg refuses to run as root.
id builder >/dev/null 2>&1 || useradd -m builder
work=/home/builder/pkg; rm -rf "$work"; mkdir -p "$work"
tar -xOf "$tarball" "SegLcd-$version/packaging/arch/seglcd/PKGBUILD" > "$work/PKGBUILD"
cp "$tarball" "$work/seglcd-$version.tar.gz"
chown -R builder: "$work"
# dotnet-sdk comes from dotnet-install.sh, not pacman -> --nodeps (makedepends would require the AUR package).
# su -l: clean environment like a normal package build (GITHUB_* would make TUnit write the root-owned step summary).
su -l builder -c "cd '$work' && PATH=/opt/dotnet:\$PATH makepkg --nodeps --noconfirm"
cp "$work"/seglcd-*.pkg.tar.zst "$out/"
ls -l "$out"
