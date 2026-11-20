#!/usr/bin/env bash
# Schneidet den USB-Verkehr des Segment-LCD auf dem Linux-Host mit (usbmon).
# Funktioniert auch, wenn das Gerät per Passthrough/SPICE an eine VM durchgereicht
# ist: QEMU spricht über libusb mit dem Gerät, usbmon sieht den Verkehr trotzdem.
#
# Aufruf:  sudo tools/capture-usbmon.sh [ausgabe.pcapng] [VID:PID]
# Danach:  tools/extract-packets.sh ausgabe.pcapng | seglcd decode --diff
set -euo pipefail

out="${1:-captures/$(date +%Y%m%d-%H%M%S)-lb360s.pcapng}"
id="${2:-5131:200e}"
vid="${id%%:*}"; pid="${id##*:}"

dev=""
for d in /sys/bus/usb/devices/*; do
  [[ -f "$d/idVendor" ]] || continue
  if [[ "$(cat "$d/idVendor")" == "$vid" && "$(cat "$d/idProduct")" == "$pid" ]]; then dev="$d"; break; fi
done
[[ -n "$dev" ]] || { echo "Gerät $id nicht gefunden." >&2; exit 1; }

bus=$(cat "$dev/busnum"); addr=$(cat "$dev/devnum")
echo "Gerät $id: Bus $bus, Adresse $addr ($dev)" >&2
echo "Hinweis: Nach Passthrough-Start/-Ende kann sich die Adresse ändern – dann neu starten." >&2

modprobe usbmon
mkdir -p "$(dirname "$out")"
command -v dumpcap >/dev/null || { echo "dumpcap fehlt (pacman -S wireshark-cli)." >&2; exit 1; }

# dumpcap gibt nach dem Öffnen des Interfaces seine Rechte ab und darf die Zieldatei
# dann nicht mehr anlegen ("Permission denied"). Deshalb öffnet die Shell die Datei
# und dumpcap schreibt nach stdout. Die Datei gehört vorab dem aufrufenden Benutzer.
: > "$out"
if [[ -n "${SUDO_UID:-}" ]]; then chown "${SUDO_UID}:${SUDO_GID}" "$out"; fi

echo "Schreibe nach $out – Strg+C beendet." >&2
# Capture-Filter auf Adresse ist bei usbmon nicht möglich; Bus wird mitgeschnitten,
# gefiltert wird beim Auswerten (usb.device_address == $addr).
exec dumpcap -q -i "usbmon${bus}" -w - > "$out"
