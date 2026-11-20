---
title: Test tool seglcd
weight: 6
---

`seglcd` (project `src/SegLcd.Tool`, .NET 11, DotMake.CommandLine, Native AOT) is the tool for trying out,
verifying and analyzing the protocol. For continuous operation there is the service
[`seglcdd`](../service/); both use the same sensor library ([Sensors & configuration](../sensors/)).

## Installation

Runtime requirements: native **hidapi** (`sudo pacman -S hidapi`) and the [udev rule](../linux/udev/).

```fish
dotnet publish src/SegLcd.Tool -c Release -r linux-x64
install -Dm755 artifacts/publish/SegLcd.Tool/release_linux-x64/seglcd ~/.local/bin/seglcd
```

`packaging/install-user.sh` also installs the man page `seglcd(1)` (English and German, source `man/`);
to view it without installing: `man -l man/de/man1/seglcd.1`.

Without installing: `dotnet run --project src/SegLcd.Tool -- <command> …` or directly
`artifacts/bin/SegLcd.Tool/debug/seglcd` after `dotnet build`. Help: `seglcd --help`,
`seglcd <command> --help`.

{{< callout type="warning" >}}
If the service `seglcdd` is running, set its `Enabled` property to `false` or stop it before running sending
commands (`send`, `live`, `probe`, `sweep`, `raw`, `off`, `hold`) – otherwise both send alternately
([details](../service/#interaction-with-seglcd)).
{{< /callout >}}

## Overview

| Command | Purpose | sends |
|---|---|---|
| [`list`](#list) | Displays or all HID devices with descriptor and open test | no |
| [`send`](#send) | send fixed values | yes |
| [`live`](#live) | send real sensor values | yes |
| [`probe`](#probe) | test byte by byte which field is shown | yes |
| [`sweep`](#sweep) | count one byte up | yes |
| [`raw`](#raw) | send an arbitrary packet | yes |
| [`off`](#off) | send off packet `0F` | yes |
| [`hold`](#hold) | timeout test: send, stop, keep device open | yes |
| [`listen`](#listen) | read input reports | optional |
| [`sensors`](#sensors) | available sensors with current values | no |
| [`decode`](#decode) | decode hex packets (e.g. from captures) | no |

### Common options of the device commands

| Option | Meaning |
|---|---|
| `--device <path>` | specific device, e.g. `/dev/hidraw5`; default: first known segment LCD |
| `--dry-run` | send nothing, only print the (first) packet as hex (64 bytes) |
| `--verbose` | print every packet sent with a timestamp |

All sending commands run until the end of `--duration` or until Ctrl+C and send periodically (keep-alive).
If `--interval` is greater than 400 ms, the tool warns: the display switches off after approx. 400–500 ms without a packet.

Exit codes: `0` success, `1` nothing found / invalid input, `2` device not found or cannot be opened.

## Commands

### list

```fish
seglcd list          # only known segment LCDs (5131:200e, 5131:2007)
seglcd list --all    # all HID devices
```

Shows path, VID:PID with model, bus/interface, usage page, strings, whether the device can be opened (permissions!)
and the report descriptor. First step when troubleshooting.

### send

Send fixed values; all fields are optional (missing = 0):

| Option | Field | Option | Field |
|---|---|---|---|
| `--cpu-temp` | CPU temperature °C | `--gpu-temp` | GPU temperature °C |
| `--cpu-usage` | CPU usage % | `--gpu-usage` | GPU usage % |
| `--cpu-power` | CPU power W | `--gpu-power` | GPU power W |
| `--cpu-clock` | CPU clock MHz | `--gpu-clock` | GPU clock MHz |
| `--cpu-voltage` | CPU voltage V | `--fan`, `--pump` | speeds RPM |
| `--ram` | RAM usage % | `--time` | time of day (ISO 8601), default: current system time |
| `--fahrenheit` | temperatures in °F – **not for the LB360-S** (values ≥ 100 appear with two digits, e.g. 122 → "22") | `--off-at-end` | send `0F` when finished |
| `--duration` | run time in s | `--interval` | update interval in ms (250) |

```fish
seglcd send --cpu-temp 42 --gpu-temp 57 --duration 20
seglcd send --cpu-temp 42.5 --fan 1234 --dry-run | seglcd decode
```

### live

Real sensor values. Sources, defaults and spec syntax: [Sensors & configuration](../sensors/).

| Option | Default |
|---|---|
| `--cpu-temp-sensor` | `k10temp:Tctl`, `zenpower:Tdie`, `zenpower:Tctl`, `coretemp:Package id 0` |
| `--cpu-power-sensor` | `rapl`, otherwise `zenpower:SVI2_P_Core+zenpower:SVI2_P_SoC` |
| `--cpu-voltage-sensor` | `zenpower:SVI2_Core` |
| `--cpu-clock-mode Avg\|Max\|Min\|First` | `Avg` |
| `--gpu-temp-sensor` | `amdgpu:edge`, `nvml:temperature`, `xe:pkg` |
| `--gpu-usage-sensor` | `drm:busy`, `nvml:usage` |
| `--gpu-power-sensor` | `amdgpu:PPT`, `nvml:power`, `rapl:uncore` |
| `--gpu-clock-sensor` | `amdgpu:sclk`, `nvml:clock`, `drm:freq` |
| `--fan-sensor` / `--pump-sensor` | none |
| `--sample-interval` | 1000 ms (read sensors) |
| `--interval` | 250 ms (send) |
| `--show` | print values continuously |
| `--fahrenheit`, `--duration` | as for `send` |

```fish
seglcd live --show --fan-sensor it8686_31050607:fan1 --pump-sensor it8686_31050607:fan5
```

On startup, `live` prints the selected source for each field; on exit (Ctrl+C) it sends `0F`.
Disconnecting and reconnecting the device (e.g. via VM redirection) is detected.

### probe

Sets **one** byte at a time to a conspicuous value, all others stay 0 – to find out
which fields a display shows.

| Option | Meaning |
|---|---|
| `--value` | value written (88) |
| `--values 0,1,50,99,128,255` | run through a **sequence of values** per byte instead of a single value |
| `--value-seconds` | seconds per value of the sequence (1) |
| `--offsets 3,13,36-63` | only these offsets or ranges (default 3–63) |
| `--auto <s>` | advance automatically instead of waiting for Enter |
| `--no-header` | do not set bytes 1/2 to `01 02` |

Interactive: Enter = next byte, `b` = back, number = change value, `o<offset>` = jump, `q` = quit.
With `--values` the sequence repeats until Enter is pressed; with `--auto` it runs once per
byte. Boundary values such as 0, 1, 99, 128 and 255 reveal flags, two-digit limits and sign effects:

```fish
seglcd probe --values 0,1,50,99,128,255 --auto 0 --offsets 3-63
```

### sweep

Counts one byte up from `--from` to `--to` (100) in steps of `--step` (1) every `--step-ms` (500);
`--set 21=12,22=34` sets further fixed bytes. For bar graphs and scales.

```fish
seglcd sweep 6 --to 100 --step 5 --step-ms 700
```

### raw

Sends up to 64 bytes **without** report ID (the rest is filled with `00`), periodically or once with `--once`.

```fish
seglcd raw 00 01 02 2a --duration 5
seglcd raw --once 00 0f
```

### off

Sends the off packet `00 0F 00 …` once.

### hold

Sends update packets with `--cpu-temp`/`--gpu-temp` (42/57) for `--send` seconds (5), then stops and keeps the
device open for another `--hold` seconds (10). This showed that the display turns off because of the **timeout**,
not because the device is closed.

### listen

Reads input reports (64 bytes) and prints them with a timestamp; `--keep-alive` sends zero values in parallel,
`--duration` limits the run time. The LB360-S does not send input reports.

### sensors

Lists all available sources grouped by type – hwmon, RAPL domains, NVIDIA (NVML), DRM as well as CPU clock and
RAM – each with the sensor spec that can be used directly in options or `config.toml`, and the current
value. Basis for the configuration ([example output](../sensors/#finding-available-sensors)).

### decode

Decodes hex packets field by field – one line per packet, optionally prefixed with a time and a tab
(output format of `tools/extract-packets.sh`). Reads from a file or from stdin.

| Option | Meaning |
|---|---|
| `--diff` | only show bytes that changed compared to the previous packet |
| `--strip-report-id` | remove the leading report ID byte from 65-byte lines |

```fish
tools/extract-packets.sh captures/x.pcapng 3 | seglcd decode --diff
```

Each update packet ends with a summary, e.g. `=> CPU 42.50°C 37.25% … | RAM 27.98%`.
