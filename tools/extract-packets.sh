#!/usr/bin/env bash
# Extrahiert die Interrupt-OUT-Pakete (Host -> Display) aus einem usbmon- oder
# USBPcap-Mitschnitt als "<Zeit>\t<Hex>" – direkt verwendbar mit `seglcd decode`.
#
# Aufruf: tools/extract-packets.sh mitschnitt.pcapng [Geräteadresse] [in|out]
set -euo pipefail
file="$1"; addr="${2:-}"; dir="${3:-out}"

ep="0x01"; [[ "$dir" == "in" ]] && ep="0x81"
filter="usb.transfer_type == 0x01 && usb.endpoint_address == ${ep} && usb.data_len > 0"
[[ -n "$addr" ]] && filter="$filter && usb.device_address == ${addr}"

# Je nach Wireshark-Version liegt die Nutzlast in usb.capdata oder usbhid.data.
tshark -r "$file" -Y "$filter" -T fields -E separator=/t \
  -e frame.time_relative -e usb.capdata -e usbhid.data \
| awk -F'\t' '{ d = ($2 != "") ? $2 : $3; gsub(":", "", d); if (d != "") print $1 "\t" d }'
