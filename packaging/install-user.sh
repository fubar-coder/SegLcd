#!/usr/bin/env bash
# Builds seglcd + seglcdd (Native AOT) and installs seglcdd as a systemd user service. No root needed.
set -euo pipefail

usage() {
    cat <<'EOF'
Usage: packaging/install-user.sh [options]

Builds seglcd and seglcdd (Native AOT) and installs them for the current user:
  ~/.local/bin/seglcd, ~/.local/bin/seglcdd
  ~/.config/systemd/user/seglcdd.service
  ~/.local/share/dbus-1/services/io.github.fubar_coder.SegLcd1.service
  ~/.local/share/man/{man1,man5,de/man1,de/man5}/   (seglcd(1), seglcdd(1), seglcdd.toml(5))
  ~/.config/seglcd/config.toml   (only if it does not exist yet)

An existing configuration is never overwritten unless --reset-config is given.

Options:
  --reset-config   Replace ~/.config/seglcd/config.toml with the default configuration
                   (the old file is kept as config.toml.bak).
  --no-start       Install only; do not enable/start the service.
  -h, --help       Show this help.

Requirements: .NET SDK, hidapi (e.g. pacman -S hidapi), udev rule udev/70-seglcd.rules.
EOF
}

reset_config=false
start=true
while [[ $# -gt 0 ]]; do
    case "$1" in
        --reset-config) reset_config=true ;;
        --no-start) start=false ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
    esac
    shift
done

cd "$(dirname "$0")/.."

dotnet publish src/SegLcd.Daemon -c Release -r linux-x64
dotnet publish src/SegLcd.Tool -c Release -r linux-x64

bin="$HOME/.local/bin"
units="${XDG_CONFIG_HOME:-$HOME/.config}/systemd/user"
dbus="${XDG_DATA_HOME:-$HOME/.local/share}/dbus-1/services"
conf="${XDG_CONFIG_HOME:-$HOME/.config}/seglcd"
doc="${XDG_DATA_HOME:-$HOME/.local/share}/doc/seglcd"
man="${XDG_DATA_HOME:-$HOME/.local/share}/man"

install -Dm755 artifacts/publish/SegLcd.Daemon/release_linux-x64/seglcdd "$bin/seglcdd"
install -Dm755 artifacts/publish/SegLcd.Tool/release_linux-x64/seglcd "$bin/seglcd"
install -Dm644 packaging/systemd/user/seglcdd.service "$units/seglcdd.service"
install -Dm644 packaging/dbus/session/io.github.fubar_coder.SegLcd1.service "$dbus/io.github.fubar_coder.SegLcd1.service"
install -Dm644 README.md "$doc/README.md"
for page in man/man1/*.1 man/man5/*.5 man/de/man1/*.1 man/de/man5/*.5; do
    install -Dm644 "$page" "$man/${page#man/}"
done
command -v mandb >/dev/null && mandb -q "$man" 2>/dev/null || true

if [[ -e "$conf/config.toml" && "$reset_config" == true ]]; then
    cp -p "$conf/config.toml" "$conf/config.toml.bak"
    install -Dm644 packaging/config.example.toml "$conf/config.toml"
    echo "Configuration reset: $conf/config.toml (backup: config.toml.bak)"
elif [[ ! -e "$conf/config.toml" ]]; then
    install -Dm644 packaging/config.example.toml "$conf/config.toml"
    echo "Default configuration created: $conf/config.toml"
else
    echo "Existing configuration kept: $conf/config.toml (use --reset-config to replace it)"
fi

systemctl --user daemon-reload
if [[ "$start" == true ]]; then
    systemctl --user enable seglcdd.service
    systemctl --user restart seglcdd.service
    systemctl --user --no-pager status seglcdd.service | head -12
else
    echo "Service not started (--no-start). Start with: systemctl --user enable --now seglcdd"
fi
