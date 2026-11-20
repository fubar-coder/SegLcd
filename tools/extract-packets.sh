#!/usr/bin/env bash
# Extracts the interrupt OUT packets (host -> display) from a usbmon or USBPcap capture
# as "<time>\t<hex>" – directly usable with `seglcd decode`.
#
# Usage: tools/extract-packets.sh capture.pcapng [device address] [in|out]
set -euo pipefail
file="$1"; addr="${2:-}"; dir="${3:-out}"

ep="0x01"; [[ "$dir" == "in" ]] && ep="0x81"
filter="usb.transfer_type == 0x01 && usb.endpoint_address == ${ep} && usb.data_len > 0"
[[ -n "$addr" ]] && filter="$filter && usb.device_address == ${addr}"

# Depending on the Wireshark version, the payload is in usb.capdata or usbhid.data.
tshark -r "$file" -Y "$filter" -T fields -E separator=/t \
  -e frame.time_relative -e usb.capdata -e usbhid.data \
| awk -F'\t' '{ d = ($2 != "") ? $2 : $3; gsub(":", "", d); if (d != "") print $1 "\t" d }'
