#!/usr/bin/env bash
# Installs a SegLcd DEB or RPM in a Debian/Fedora container with systemd and tests it: user, unit, service start,
# D-Bus policy, SaveConfig, reload, removal. With a device the service also drives the display.
#
# Usage: tools/test-package.sh <package.deb|package.rpm> [/dev/hidrawN] [--keep]
#
# Runs rootless (no sudo). The container user "seglcd" gets the host UID (--userns=keep-id), so the service reaches
# the display through the host user's uaccess ACL. udev does not run in containers: the script starts the service.
set -uo pipefail
here=$(dirname "$(realpath "$0")")/test-package; dev=""; keep=0; pkg=""
for a in "$@"; do case $a in --keep) keep=1;; /dev/*) dev=$a;; *.deb|*.rpm) pkg=$(realpath "$a");; esac; done
[ -f "$pkg" ] || { echo "Usage: $0 <package.deb|package.rpm> [/dev/hidrawN] [--keep]"; exit 1; }
fmt=${pkg##*.}
name=seglcd-$fmt-test
step() { printf '\n\e[1m== %s\e[0m\n' "$*"; }
x() { podman exec $name "$@"; }

[ "$(id -u)" = 0 ] && { echo "Run without sudo."; exit 1; }
if [ -n "$dev" ] && systemctl is-active -q seglcdd; then
    echo "The host service seglcdd is running – stop it first: sudo systemctl stop seglcdd"; exit 1
fi
echo "building the test image…"
log=$(mktemp)
podman build -q --build-arg SEGLCD_UID="$(id -u)" -t $name -f "$here/Containerfile.$fmt" "$here" >/dev/null 2>"$log" || { cat "$log"; exit 1; }
rm -f "$log"
podman rm -f $name >/dev/null 2>&1
podman run -d --name $name --systemd=always --cap-add SYS_ADMIN --userns=keep-id --user 0 -v "$pkg:/pkg/$(basename "$pkg"):ro,Z" ${dev:+--device "$dev"} $name >/dev/null || exit 1
trap '[ $keep = 1 ] && echo "container kept: podman exec -it $name bash" || podman rm -f $name >/dev/null' EXIT
for _ in $(seq 30); do x systemctl is-system-running 2>/dev/null | grep -qE 'running|degraded' && break; sleep 1; done

step "install ($fmt)"
if [ $fmt = deb ]; then
    x bash -c 'apt-get -qq update && DEBIAN_FRONTEND=noninteractive apt-get -qq install -y /pkg/*.deb' 2>&1 | tail -3
else
    x bash -c 'dnf -y -q install /pkg/*.rpm' 2>&1 | tail -3
fi
step "user and files"
x id seglcd
x ls -ld /etc/seglcd /usr/lib/udev/rules.d/70-seglcd.rules /usr/lib/systemd/system/seglcdd.service
x systemctl is-enabled seglcdd

if [ -n "$dev" ]; then
    step "device in the container"
    x ls -l "$dev"
fi
step "start (normally done by udev SYSTEMD_WANTS)"
x systemctl start seglcdd; sleep 3
x systemctl --no-pager status seglcdd | head -5
x journalctl -u seglcdd --no-pager -o cat | grep -vE "Source (cpu|gpu|fan|pump)"
if [ -n "$dev" ]; then
    step "device diagnostics"
    x ls -l "$dev"; x stat -c '%u:%g %a' "$dev"; x cat /proc/self/cgroup
    x runuser -u seglcd -- seglcd list; x runuser -u seglcd -- seglcd send --cpu-temp 42 --gpu-temp 57 --duration 3 --dry-run >/dev/null
    x systemctl show seglcdd -p DevicePolicy -p DeviceAllow -p MainPID -p ActiveState
fi

N=io.github.fubar_coder.SegLcd1; P=/io/github/fubar_coder/SegLcd1
step "D-Bus"
x busctl --system get-property $N $P $N Connected Model DevicePath ConfigPath
x busctl --system get-property $N $P $N CurrentValues
x usermod -aG seglcd member
echo "-- member (group seglcd) sets Enabled=false, then true:"
x runuser -u member -- busctl --system set-property $N $P $N Enabled b false && echo ok
[ -n "$dev" ] && { echo "   display should be off now (5 s)"; sleep 5; }
x runuser -u member -- busctl --system set-property $N $P $N Enabled b true && echo ok
echo "-- stranger (not in group) reads and must not write:"
x runuser -u stranger -- busctl --system get-property $N $P $N Enabled
x runuser -u stranger -- busctl --system set-property $N $P $N Enabled b false && echo "UNEXPECTED: allowed" || echo "denied (expected)"
echo "-- member: change interval, SaveConfig, restart:"
x runuser -u member -- busctl --system set-property $N $P $N UpdateIntervalMs u 200
x runuser -u member -- busctl --system call $N $P $N SaveConfig && x ls -l /etc/seglcd/ /var/lib/seglcd/
x cat /var/lib/seglcd/config.toml
x systemctl restart seglcdd; sleep 2
x busctl --system get-property $N $P $N UpdateIntervalMs
x journalctl -u seglcdd --no-pager -o cat -n 30 | grep -E "Saved settings|Configuration"
echo "-- back to the /etc value removes the state file:"
x runuser -u member -- busctl --system set-property $N $P $N UpdateIntervalMs u 250
x runuser -u member -- busctl --system call $N $P $N SaveConfig; x ls /var/lib/seglcd/
step "reload"
x systemctl reload seglcdd && x journalctl -u seglcdd --no-pager -n 2 -o cat
[ -n "$dev" ] && { step "display should show CPU/GPU temperature now (15 s)"; sleep 15; }

step "remove ($fmt)"
x systemctl stop seglcdd
if [ $fmt = deb ]; then
    x bash -c 'DEBIAN_FRONTEND=noninteractive apt-get -qq purge -y seglcd' 2>&1 | grep -v 'Reading database' | tail -2
else
    x bash -c 'dnf -y -q remove seglcd' 2>&1 | tail -2
fi
x bash -c 'ls -d /usr/bin/seglcd* /etc/seglcd /var/lib/seglcd 2>&1; id seglcd'
