#!/usr/bin/env bash
# Captures the USB traffic of the segment LCD on the Linux host (usbmon).
# Also works while the device is passed through to a VM (passthrough/SPICE):
# QEMU talks to the device via libusb, so usbmon still sees the traffic.
#
# Usage:  sudo tools/capture-usbmon.sh [output.pcapng] [VID:PID]
# Then:   tools/extract-packets.sh output.pcapng | seglcd decode --diff
set -euo pipefail

out="${1:-captures/$(date +%Y%m%d-%H%M%S)-lb360s.pcapng}"
id="${2:-5131:200e}"
vid="${id%%:*}"; pid="${id##*:}"

dev=""
for d in /sys/bus/usb/devices/*; do
  [[ -f "$d/idVendor" ]] || continue
  if [[ "$(cat "$d/idVendor")" == "$vid" && "$(cat "$d/idProduct")" == "$pid" ]]; then dev="$d"; break; fi
done
[[ -n "$dev" ]] || { echo "Device $id not found." >&2; exit 1; }

bus=$(cat "$dev/busnum"); addr=$(cat "$dev/devnum")
echo "Device $id: bus $bus, address $addr ($dev)" >&2
echo "Note: the address may change when passthrough starts/ends – restart the capture then." >&2

modprobe usbmon
mkdir -p "$(dirname "$out")"
command -v dumpcap >/dev/null || { echo "dumpcap is missing (pacman -S wireshark-cli)." >&2; exit 1; }

# dumpcap drops its privileges after opening the interface and can then no longer create
# the output file ("Permission denied"). So the shell opens the file and dumpcap writes to
# stdout. The file is handed to the calling user beforehand.
: > "$out"
if [[ -n "${SUDO_UID:-}" ]]; then chown "${SUDO_UID}:${SUDO_GID}" "$out"; fi

echo "Writing to $out – Ctrl+C stops." >&2
# usbmon cannot filter by address; the whole bus is captured and filtered when
# analysing (usb.device_address == $addr).
exec dumpcap -q -i "usbmon${bus}" -w - > "$out"
