#!/usr/bin/env bash
# Creates the source tarball SegLcd-<version>.tar.gz (same layout as GitHub tag archives) from the committed state (HEAD) in artifacts/dist/.
#
#   packaging/make-tarball.sh            source only (the build downloads NuGet packages)
#   packaging/make-tarball.sh --vendor   additionally bundles all NuGet packages (incl. the Native AOT
#                                        compiler for linux-x64 and linux-arm64) in vendor/nuget for
#                                        offline builds (e.g. mock, sbuild, OBS); much larger
set -euo pipefail

vendor=false
case "${1:-}" in
    --vendor) vendor=true ;;
    -h|--help) sed -n '2,7p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    "") ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
esac

cd "$(dirname "$0")/.."
version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props)
name="SegLcd-$version"
out="$PWD/artifacts/dist"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
mkdir -p "$out"

if [[ -n "$(git status --porcelain)" ]]; then
    echo "Warning: uncommitted changes are not included (tarball is built from HEAD)." >&2
fi

git archive --format=tar --prefix="$name/" HEAD | tar -x -C "$work"

suffix=""
if [[ "$vendor" == true ]]; then
    suffix="-vendored"
    for rid in linux-x64 linux-arm64; do
        for project in src/SegLcd.Tool src/SegLcd.Daemon; do
            (cd "$work/$name" && DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 \
                dotnet restore "$project" -r "$rid" -p:PublishAot=true --packages vendor/nuget)
        done
    done
    find "$work/$name" -type d \( -name obj -o -name bin \) -prune -exec rm -rf {} +
    rm -rf "$work/$name/artifacts"
fi

tar -C "$work" --sort=name --owner=0 --group=0 --numeric-owner --mtime="@$(git log -1 --format=%ct)" \
    -czf "$out/$name$suffix.tar.gz" "$name"
echo "$out/$name$suffix.tar.gz"
