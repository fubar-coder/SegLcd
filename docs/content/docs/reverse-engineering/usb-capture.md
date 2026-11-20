---
title: USB capture & analysis
weight: 2
---

## Capturing on the Linux host (usbmon)

QEMU talks to the real device via libusb or usbredir – so the traffic passes through the host's USB stack
and is visible there with **usbmon**. Nothing needs to be installed in Windows.

{{% steps %}}

### Tools

```bash
sudo pacman -S wireshark-cli      # dumpcap + tshark; for the GUI: wireshark-qt
```

### Determine bus and address

```bash
lsusb -d 5131:200e
# Bus 001 Device 003: ID 5131:200e MSR   -> usbmon1, address 3
```

### Record

{{< tabs >}}
  {{< tab name="fish" >}}
```fish
sudo tools/capture-usbmon.sh captures/(date +%Y%m%d-%H%M)-lb360s-start.pcapng
```
  {{< /tab >}}
  {{< tab name="bash" >}}
```bash
sudo tools/capture-usbmon.sh captures/$(date +%Y%m%d-%H%M)-lb360s-start.pcapng
```
  {{< /tab >}}
{{< /tabs >}}

The script loads `usbmon` and captures the **entire bus** (usbmon cannot filter by address).
Now run through the scenario in the VM (see below), then press Ctrl+C.

Without the script: `sudo modprobe usbmon && sudo dumpcap -q -i usbmon1 -w - > file.pcapng`.

{{< callout type="info" >}}
`dumpcap -w file` fails under `sudo` with *Permission denied*: after opening the interface, dumpcap drops
its privileges and is then no longer allowed to create the file. It therefore writes to stdout (`-w -`), and the
file is opened by the shell. The script additionally hands the file over to the calling user.
{{< /callout >}}

{{% /steps %}}

{{< callout type="info" >}}
When attaching to the VM, QEMU resets the device; the **address may change**. Therefore first
redirect, then check `lsusb`, then record – or work without an address filter during analysis
and filter for 64-byte interrupt packets.
{{< /callout >}}

### Alternative: USBPcap in Windows

Wireshark for Windows optionally ships **USBPcap**. In the VM, select the interface `USBPcap1…n`
to which the device is attached. The analysis below works the same (the fields have identical names).

## Analysis

The host→device packets are **interrupt OUT transfers on endpoint `0x01`** with a 64-byte payload.

| Wireshark display filter | Purpose |
|---|---|
| `usb.device_address == 3` | only the display |
| `usb.transfer_type == 0x01` | only interrupt transfers |
| `usb.endpoint_address == 0x01 && usb.data_len == 64` | output reports (host → display) |
| `usb.endpoint_address == 0x81 && usb.data_len > 0` | input reports (display → host) |
| `usb.transfer_type == 0x02` | control transfers (enumeration, possibly `SET_REPORT`/`SET_IDLE`) |

Extract and decode the payload:

```bash
tools/extract-packets.sh captures/…-lb360s-start.pcapng 3 > captures/…-lb360s-start.txt
seglcd decode captures/…-lb360s-start.txt | less          # all fields per packet
seglcd decode --diff captures/…-lb360s-start.txt | less   # only changed bytes
```

`extract-packets.sh` outputs `<time>\t<hex>` per packet (field `usb.capdata`, or `usbhid.data` in newer
Wireshark versions). `seglcd decode` understands exactly this format; for 65-byte data with
report ID, use `--strip-report-id`.

The interval between packets can be read directly from the time column:

```bash
tools/extract-packets.sh file.pcapng 3 | awk -F'\t' 'NR>1{printf "%.3f\n", $1-p} {p=$1}' | sort | uniq -c
```

## Scenarios

Record each scenario as a separate file so that changes can be attributed unambiguously.

| # | Scenario | Expectation / question |
|---|---|---|
| 1 | Start the software, let it run for 30 s, exit | Are there initialization packets? Interval? Off packet `0F` at the end? |
| 2 | Switch unit °C ↔ °F | Do bytes 5/15 change to `1`? Values converted? |
| 3 | Change display mode (if available, e.g. CPU/GPU) | New command or just different values? |
| 4 | Generate load (e.g. Prime95 in the VM) | Bytes 3, 6, 7–10 change |
| 5 | Software settings such as brightness, display off/on | **new, unknown commands** – especially interesting |
| 6 | Unplug/replug the device while the software is running | Behavior on reconnect |
| 7 | Windows shutdown with the software running | Is `0F` sent? |

Enter results in [Verification](../../protocol/verification/); document new commands as a separate page
under *Protocol*.

## Checking without Windows

To check your own code, you can also capture the packets from `seglcd` itself
and compare them with `--verbose` – this lets you verify, for example, that really 64 bytes without report ID end up on the
wire:

```bash
sudo dumpcap -i usbmon1 -a duration:5 -w /tmp/self.pcapng & seglcd send --cpu-temp 42 --duration 4 --verbose
tools/extract-packets.sh /tmp/self.pcapng | seglcd decode | head -20
```
