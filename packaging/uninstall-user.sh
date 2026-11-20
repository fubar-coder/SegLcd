#!/usr/bin/env bash
# Removes what packaging/install-user.sh installed for the current user. No root needed.
set -euo pipefail

usage() {
    cat <<'EOT'
Usage: packaging/uninstall-user.sh [options]

Stops and disables the seglcdd user service and removes the files installed by
packaging/install-user.sh:
  ~/.local/bin/seglcd, ~/.local/bin/seglcdd, ~/.local/bin/seglcd-gtk
  ~/.local/share/applications/io.github.fubar_coder.SegLcd.desktop (and the icon)
  ~/.config/systemd/user/seglcdd.service
  ~/.local/share/dbus-1/services/io.github.fubar_coder.SegLcd1.service
  ~/.local/share/man/{man1,man5,de/man1,de/man5}/seglcd*.{1,5}
  ~/.local/share/doc/seglcd/

The configuration ~/.config/seglcd/ is kept unless --purge is given.
The udev rules in /etc/udev/rules.d/ are not touched (they were installed with sudo).

Options:
  --purge      Also remove ~/.config/seglcd/ (configuration and backups).
  -h, --help   Show this help.
EOT
}

purge=false
while [[ $# -gt 0 ]]; do
    case "$1" in
        --purge) purge=true ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
    esac
    shift
done

bin="$HOME/.local/bin"
units="${XDG_CONFIG_HOME:-$HOME/.config}/systemd/user"
dbus="${XDG_DATA_HOME:-$HOME/.local/share}/dbus-1/services"
conf="${XDG_CONFIG_HOME:-$HOME/.config}/seglcd"
doc="${XDG_DATA_HOME:-$HOME/.local/share}/doc/seglcd"
man="${XDG_DATA_HOME:-$HOME/.local/share}/man"
share="${XDG_DATA_HOME:-$HOME/.local/share}"

if systemctl --user cat seglcdd.service >/dev/null 2>&1; then
    systemctl --user disable --now seglcdd.service || true
fi

rm -f "$bin/seglcd" "$bin/seglcdd" "$bin/seglcd-gtk" \
      "$share/applications/io.github.fubar_coder.SegLcd.desktop" \
      "$share/icons/hicolor/scalable/apps/io.github.fubar_coder.SegLcd.svg" \
      "$units/seglcdd.service" \
      "$dbus/io.github.fubar_coder.SegLcd1.service"
for page in man1/seglcd.1 man1/seglcdd.1 man1/seglcd-gtk.1 man5/seglcdd.toml.5 \
            de/man1/seglcd.1 de/man1/seglcdd.1 de/man1/seglcd-gtk.1 de/man5/seglcdd.toml.5; do
    rm -f "$man/$page"
done
rm -rf "$doc"
command -v mandb >/dev/null && mandb -q "$man" 2>/dev/null || true
systemctl --user daemon-reload

if [[ "$purge" == true ]]; then
    rm -rf "$conf"
    echo "Configuration removed: $conf"
else
    echo "Configuration kept: $conf (use --purge to remove it)"
fi
echo "Note: the udev rules in /etc/udev/rules.d/ (70-seglcd.rules, optionally 72-rapl-energy-read.rules) remain;"
echo "      remove them with sudo if no longer needed."
