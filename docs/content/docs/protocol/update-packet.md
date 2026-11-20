---
title: Update packet (01 02)
weight: 1
---

The update packet carries all values that a segment LCD of this family can display.
Which of them a particular model actually shows depends on its segment layout.

## Byte layout

Offsets refer to the 64-byte packet **without** report ID (see [Transport](../#transport)).

| Offset | Hex | Content | Encoding | Range |
|---:|---:|---|---|---|
| 0 | `0x00` | reserved | always `0x00` | – |
| 1 | `0x01` | **Command** | `0x01` | – |
| 2 | `0x02` | **Subcommand** | `0x02` | – |
| 3 | `0x03` | CPU temperature | integer part | 0–255 |
| 4 | `0x04` | CPU temperature | hundredths ×100 | 0–99 |
| 5 | `0x05` | CPU temperature unit | `0` = °C, `1` = °F ³ | 0–1 |
| 6 | `0x06` | CPU usage | percent, integer part (hundredths: offset 48) | 0–100 |
| 7 | `0x07` | CPU power | watts **mod 100** (tens + ones) | 0–99 |
| 8 | `0x08` | CPU power | hundredths ×100 | 0–99 |
| 9 | `0x09` | CPU clock | MHz **÷ 100** (integer) | 0–99 |
| 10 | `0x0A` | CPU clock | MHz **mod 100** | 0–99 |
| 11 | `0x0B` | CPU voltage | integer part (V) | 0–255 |
| 12 | `0x0C` | CPU voltage | hundredths ×100 ¹ | 0–99 |
| 13 | `0x0D` | GPU temperature | integer part | 0–255 |
| 14 | `0x0E` | GPU temperature | hundredths ×100 | 0–99 |
| 15 | `0x0F` | GPU temperature unit | `0` = °C, `1` = °F ³ | 0–1 |
| 16 | `0x10` | GPU usage | percent, integer | 0–100 |
| 17 | `0x11` | GPU power | watts **mod 100** | 0–99 |
| 18 | `0x12` | GPU power | hundredths ×100 | 0–99 |
| 19 | `0x13` | GPU clock | MHz ÷ 100 | 0–99 |
| 20 | `0x14` | GPU clock | MHz mod 100 | 0–99 |
| 21 | `0x15` | Fan speed | RPM ÷ 100 | 0–99 |
| 22 | `0x16` | Fan speed | RPM mod 100 | 0–99 |
| 23 | `0x17` | Pump speed | RPM ÷ 100 | 0–99 |
| 24 | `0x18` | Pump speed | RPM mod 100 | 0–99 |
| 25 | `0x19` | Year | century (`20` for 2026) | 0–99 |
| 26 | `0x1A` | Year | year mod 100 (`26` for 2026) | 0–99 |
| 27 | `0x1B` | Month | 1–12 | 1–12 |
| 28 | `0x1C` | Day | 1–31 | 1–31 |
| 29 | `0x1D` | Hour | 0–23 | 0–23 |
| 30 | `0x1E` | Minute | 0–59 | 0–59 |
| 31 | `0x1F` | Second | 0–59 | 0–59 |
| 32 | `0x20` | Day of week | `0` = Sunday … `6` = Saturday | 0–6 |
| 33 | `0x21` | RAM usage | percent, integer part (hundredths: offset 50) | 0–100 |
| 34 | `0x22` | CPU power | watts **÷ 100** (hundreds) | 0–99 |
| 35 | `0x23` | GPU power | watts ÷ 100 | 0–99 |
| 36 | `0x24` | unused | `0x00` | – |
| 37 | `0x25` | **unknown** ² | original software (LB360-S): always `5` | ? |
| 38–47 | | unused | `0x00` | – |
| 48 | `0x30` | CPU usage ² | hundredths ×100 (always `0` at 100 %) | 0–99 |
| 49 | `0x31` | unused | `0x00` | – |
| 50 | `0x32` | RAM usage ² | hundredths ×100 | 0–99 |
| 51–63 | | unused | `0x00` | – |

¹ The original software (LA series, version 2025-03) sends `floor(V) × 100` mod 256 here, i.e.
the value `100` (`0x64`) for every 1.x V core voltage – presumably a bug. Since no known display
shows the voltage, this has no consequences. The reference implementation sends the real two decimals.

² These bytes are not known from the LA analysis; they come from the [captures of the TT SEGMENT LCD Software](../verification/#capture-of-the-original-software-vm)
for the LB360-S. 48 and 50 are clearly identified: at 100 % CPU usage, byte 48 is `0` in all packets,
and byte 33 + byte 50/100 yields a continuous RAM curve (e.g. 27.98 → 28.03). Byte 37 is always `5`,
meaning unknown; the reference implementation sends `0` there – the display works with that.

³ The LB360-S does not convert and always shows the °C symbol; with `1`, values ≥ 100 are only shown with two digits
(mod 100) instead of being capped at 99. For this model **always send `0`** – details in
[Verification](../verification/#lb360-s-unit-flag).

### Encoding rules

All values are **single unsigned bytes**. Multi-digit quantities are split into **base-100 digits**
(not binary!): 1234 RPM → `12, 34` → `0x0C 0x22`, *not* `0x04 0xD2`.

| Quantity | Formula | Example |
|---|---|---|
| Fixed point (temperature, voltage) | `c = round(v × 100)`; `whole = c div 100`; `frac = c mod 100` | 42.5 °C → `42, 50` |
| Base 100 (clock, speed) | `v = min(floor(v), 9999)`; `hi = v div 100`; `lo = v mod 100` | 4567 MHz → `45, 67` |
| Power (3 bytes) | `c = round(v × 100)`; `W = c div 100`; `[34/35] = W div 100`; `[7/17] = W mod 100`; `[8/18] = c mod 100` | 123.45 W → `[34]=1, [7]=23, [8]=45` |
| Percent (CPU, RAM) | fixed point like temperature: `[6]/[48]` or `[33]/[50]` | 37.25 % → `37, 25` |
| Percent (GPU) | `min(floor(v), 255)`, no known decimals | 37.8 % → `37` |
| Fahrenheit | host converts: `F = C × 9/5 + 32`, unit byte `1` | 50 °C → `122`, flag `1` |

Recommended handling of edge cases (as implemented in the reference library):

| Input | Behavior |
|---|---|
| Value unknown / not available | send `0` |
| `NaN`, ±∞, negative | send `0` |
| Overflow | clamp to the field's maximum (temperature `255.99`, base 100 `9999`, power `9999.99`) |
| Rounding for fixed point | **first** round to 1/100, **then** split – otherwise 42.999 becomes `42, 100` instead of `43, 00` |

{{< callout type="info" >}}
The LB360-S shows temperatures with **two digits**: values ≥ 100 appear as **99** (tested up to 255).
Whether other models meaningfully display values above 99 in the "÷ 100" bytes or decimals above 99
is unknown – only send values within the specified range.
{{< /callout >}}

### Time

Fields 25–32 contain the host's **local** time. The LA display does not show it;
whether the round LB360-S display has a clock is an open question. The original software always fills the fields;
a library should do the same (it costs nothing, and devices with a clock will then work).

## Test vectors

All vectors use the fixed time **Friday, 2026-11-20 13:45:30** (bytes 25–32 = `14 1a 0b 14 0d 2d 1e 05`).
Generated with `seglcd send … --time 2026-11-20T13:45:30 --dry-run`, backed by the unit tests.

### Vector 1 – example from the LA documentation

CPU 42.5 °C, 37 %, 123.45 W, 4567 MHz; fan 1234 RPM; pump 2345 RPM.

```text
00 01 02 2a 32 00 25 17 2d 2d 43 00 00 00 00 00 00 00 00 00 00 0c 22 17 2d 14 1a 0b 14 0d 2d 1e
05 00 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
```

### Vector 2 – all fields populated

CPU 65.37 °C, 12 %, 88.2 W, 4450 MHz, 1.2 V; GPU 47 °C, 3 %, 18.5 W, 1714 MHz;
fan 1566 RPM; pump 2947 RPM; RAM 41 %.

```text
00 01 02 41 25 00 0c 58 14 2c 32 01 14 2f 00 00 03 12 32 11 0e 0f 42 1d 2f 14 1a 0b 14 0d 2d 1e
05 29 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
```

### Vector 3 – Fahrenheit

CPU 50 °C, GPU 100 °C, unit °F → 122 °F / 212 °F, flags at offsets 5 and 15.

```text
00 01 02 7a 00 01 00 00 00 00 00 00 00 d4 00 01 00 00 00 00 00 00 00 00 00 14 1a 0b 14 0d 2d 1e
05 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
```

### Vector 4 – rounding and clamping

CPU 42.999 °C (→ 43.00), CPU 1234.567 W (→ 12 | 34 | 57), fan 20000 RPM (→ 9999).

```text
00 01 02 2b 00 00 00 22 39 00 00 00 00 00 00 00 00 00 00 00 00 63 63 00 00 14 1a 0b 14 0d 2d 1e
05 00 0c 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
```

### Vector 5 – packet from the original software

From the second capture of the original software on 2026-11-20 (t = 0.161 s): CPU usage 6.20 %, "voltage" 1.55 V,
RAM 27.64 %, Friday 2026-11-20 03:14:53, byte 37 = `5`. The reference implementation produces it byte-identically
from these values (byte 37 set by hand) – see unit test `Reproduces_packet_of_original_software_except_byte_37`.

```text
00 01 02 00 00 00 06 00 00 00 00 01 37 00 00 00 00 00 00 00 00 00 00 00 00 14 1a 0b 14 03 0e 35
05 1b 00 00 00 05 00 00 00 00 00 00 00 00 00 00 14 00 40 00 00 00 00 00 00 00 00 00 00 00 00 00
```

### As hidraw write

Vector 1 as passed to `write(fd, buf, 65)` – with report ID `00` in front:

```text
00 | 00 01 02 2a 32 00 25 17 2d 2d 43 00 00 … (65 bytes in total)
```

## Reference pseudocode

```text
function encode_update(f, now) -> byte[64]:
    p = zeros(64)
    p[1] = 0x01; p[2] = 0x02
    fixed2(p, 3, 4,   to_unit(f.cpu_temp, f.unit), max_whole=255)
    p[5]  = f.unit                      # 0 = °C, 1 = °F
    fixed2(p, 6, 48,  f.cpu_usage, max_whole=255)
    power(p, 34, 7, 8, f.cpu_power)
    base100(p, 9, 10, f.cpu_clock)
    fixed2(p, 11, 12, f.cpu_voltage, max_whole=255)
    fixed2(p, 13, 14, to_unit(f.gpu_temp, f.unit), max_whole=255)
    p[15] = f.unit
    p[16] = int_clamp(f.gpu_usage, 255)
    power(p, 35, 17, 18, f.gpu_power)
    base100(p, 19, 20, f.gpu_clock)
    base100(p, 21, 22, f.fan_rpm)
    base100(p, 23, 24, f.pump_rpm)
    p[25] = now.year div 100;  p[26] = now.year mod 100
    p[27] = now.month; p[28] = now.day
    p[29] = now.hour;  p[30] = now.minute; p[31] = now.second
    p[32] = now.weekday                 # Sunday = 0
    fixed2(p, 33, 50, f.ram_usage, max_whole=255)
    return p

sanitize(v)        = (v is finite and v > 0) ? v : 0
int_clamp(v, m)    = min(floor(sanitize(v)), m)
fixed2(p,a,b,v,m): c = min(round_half_away(sanitize(v)*100), m*100+99); p[a]=c div 100; p[b]=c mod 100
base100(p,a,b,v):  n = min(floor(sanitize(v)), 9999);                 p[a]=n div 100; p[b]=n mod 100
power(p,h,l,f,v):  c = min(round_half_away(sanitize(v)*100), 999999); w=c div 100
                   p[h]=w div 100; p[l]=w mod 100; p[f]=c mod 100
```
