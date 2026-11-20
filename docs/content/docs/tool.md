---
title: Test tool seglcd
weight: 6
---

`seglcd` sends to and analyses the display. Full option reference: `seglcd <command> --help` and `seglcd(1)`.
Requirements: `hidapi` and the [udev rule](../linux/udev/); installed by the packages or `packaging/install-user.sh`.
From the source tree: `dotnet run --project src/SegLcd.Tool -- <command>`.

{{< callout type="warning" >}}
Pause a running [`seglcdd`](../service/#operation) before sending commands, otherwise both send alternately.
{{< /callout >}}

| Command | Purpose |
|---|---|
| `list [--all]` | known displays (or all HID devices): IDs, descriptor, whether the device can be opened |
| `send` | send fixed values: `--cpu-temp`, `--gpu-temp`, `--fan`, `--pump`, `--ram`, … (missing = 0) |
| `live` | send live sensor values; sources as in [Sensors & configuration](../sensors/), `--show` prints them |
| `probe` | set one byte at a time to find out what the display shows |
| `sweep <offset>` | count one byte up (`--from`, `--to`, `--step`, `--step-ms`) |
| `raw <hex…>` | send up to 64 bytes (without report ID), periodically or `--once` |
| `off` | send the off packet |
| `hold` | send for `--send` s, then stop but keep the device open for `--hold` s (timeout test) |
| `listen` | print input reports (the LB360-S sends none) |
| `sensors` | list all sensor sources with spec and current value |
| `decode [file]` | decode hex packets, e.g. from `tools/extract-packets.sh`; `--diff` shows changes only |

Sending commands repeat their packet every `--interval` ms (250; warning above 400) until `--duration` ends or
Ctrl+C. Common options: `--device /dev/hidrawN`, `--dry-run` (print the packet only), `--verbose`.
Exit codes: 0 success, 1 nothing found / invalid input, 2 device missing or not accessible.

`--fahrenheit` is not suitable for the LB360-S: values ≥ 100 appear with two digits (122 → "22").

## Examples

```fish
seglcd list
seglcd send --cpu-temp 42 --gpu-temp 57 --duration 20
seglcd send --cpu-temp 42.5 --fan 1234 --dry-run | seglcd decode
seglcd live --show --fan-sensor it8686_31050607:fan1 --pump-sensor it8686_31050607:fan5
seglcd raw 00 01 02 2a --duration 5
```

## probe

Writes `--value` (88) – or, with `--values 0,1,50,99,128,255`, a sequence of `--value-seconds` (1) each – into one
byte while all others stay 0. `--offsets 3,13,36-63` limits the bytes (default 3–63), `--auto <s>` advances
automatically. Interactive keys: Enter next byte, `b` back, a number changes the value, `o<offset>` jumps, `q` quits.

```fish
seglcd probe --values 0,1,50,99,128,255 --auto 0 --offsets 3-63
```
