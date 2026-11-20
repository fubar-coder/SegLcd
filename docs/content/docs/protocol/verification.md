---
title: Verification & open questions
weight: 4
---

## Status

Legend: ✅ verified · ❌ tested, no visible effect · 🟡 assumed (adopted from LA series) · ❓ unknown

| Aspect | LA series `5131:2007` | LB360-S `5131:200e` |
|---|---|---|
| HID descriptor (64/64 bytes, no report IDs) | ✅ | ✅ (byte-identical, read out ourselves) |
| Update packet `01 02`, layout | ✅ | ✅ (2026-11-20, `seglcd send`/`probe`) |
| Off packet `0F` | ✅ | 🟡 effect not distinguishable from the timeout (~450 ms); the LB360-S original software does not send it |
| Update interval 250 ms | ✅ | ✅ works |
| Display turns off when the program exits | ✅ | ✅ **immediately** when `seglcd send` ends (without `--off-at-end`, i.e. without `0F`); it stays on while running (e.g. 30 s) |
| Cause: timeout after last packet **or** closing the device | ❓ | ✅ **Timeout** – with `seglcd hold --send 5 --hold 15` the display turned off when sending ended, although the device was still open |
| Timeout after last packet | ❓ ("short") | ✅ **approx. 400–500 ms** (400 ms interval stable, 500 ms flashes) |
| Displayed fields | CPU temp. (integer), CPU usage + bar, fan RPM + bar | CPU and GPU temperature (integer, °C) – [details](#lb360-s-field-mapping) |
| Full-scale value of the bar displays | ❓ (1234 RPM ≈ half fan bar) | ❓ (CPU usage has no effect) |
| °F display | ❓ | ❌ symbol stays °C; with flag `1`, values ≥ 100 are only shown with two digits (mod 100) – **do not use** |
| Meaning of the input reports | ❓ | ❓ |
| Reconnecting after disconnect (VM redirection) | – | ✅ `seglcd live` reconnects by itself after the device is returned |
| String descriptors 4/5 | – | ❓ |

## LB360-S: field mapping

Result of `seglcd send` and `seglcd probe` on the LB360-S on 2026-11-20:

| Offset | Field | Result |
|---:|---|---|
| 3 | CPU temperature, integer | ✅ is displayed |
| 4 | CPU temperature, hundredths | ❌ no effect – the display has no decimal place |
| 6 | CPU usage | ❌ no visible effect |
| 13 | GPU temperature, integer | ✅ is displayed |
| 14 | GPU temperature, hundredths | ❌ no effect |
| 5 / 15 | CPU/GPU temperature unit | ⚠️ symbol stays °C, but the **rendering changes**: flag `0` caps at 99, flag `1` shows only the **last two digits** (122 → "22") and "99" for value 0 – see [unit flag](#lb360-s-unit-flag) |
| remaining (7–12, 16–35) | power, clock, voltage, speeds, time, RAM | ❓ not yet confirmed individually |
| 36–49 | extended range, incl. 37 (unknown), 48 (CPU usage hundredths) | ❌ no visible effect (`seglcd probe --offsets 36-63`, tested up to 49) |
| 50–63 | incl. 50 (RAM usage hundredths) | ❌ no visible effect (`seglcd probe --offsets 50-63`) |

### LB360-S: unit flag

Test with `seglcd raw` (2026-11-20), with only the temperature byte and flag set in each case:

| Value (byte 3 or 13) | Flag (byte 5 or 15) | Display |
|---:|:---:|---:|
| 0 | 0 | 0 |
| ≥ 100 (100, 105, 123, 199, 255) | 0 | 99 |
| 0 | 1 | 99 |
| 31 | 1 | 31 |
| 32 | 1 | 32 |
| 50 | 1 | 50 |
| 122 | 1 | 22 (same for CPU and GPU) |

The display does **not** convert from °F to °C. With flag `1` the cap at 99 is dropped and only the
last two digits are shown – presumably firmware for three-digit °F values, without a hundreds digit on this
LCD. Why 0 with flag `1` appears as 99 is unclear. The symbol does not change to °F.

Conclusions for implementations:

- For the LB360-S it is sufficient to fill **offset 3 (CPU) and 13 (GPU)**. Still send the remaining fields
  as specified: it costs nothing and keeps the packets compatible with the original software.
- **Always send in °C with flag `0`.** With flag `1` (°F), values ≥ 100 appear with only two digits: 50 °C =
  122 °F would be shown as "22", and the symbol stays °C. A library should ignore the Fahrenheit option for this
  model or warn about it (`seglcd` warns).
- Temperatures are truncated, not rounded (byte 3 = `floor`). To round instead, round the value
  yourself before encoding (e.g. 42.7 → 43).
- Values **≥ 100** are shown by the display as **99** (tested with 99, 100, 105, 123, 199, 255). A library does not
  need to clamp, but should be aware that e.g. 105 °C appears as "99".

## Capture of the original software (VM)

Recorded on 2026-11-20 with `tools/capture-usbmon.sh`: TT SEGMENT LCD Software in the Windows VM
(`qemu:///session`, SPICE USB redirection, 8 vCPUs). The raw data is not in the repository; the full analysis is here.
In the VM most sensor values are 0. The software shows the tiles *CPU Temp*, *CPU Usage*, *CPU Fan Speed*,
*CPU Frequency*, *CPU Power* and *GPU Fan Speed / Temp*; only the source for *CPU Usage* is configurable
(*Total CPU Usage*, *Core 0…7 Usage*, *Max CPU/Thread Usage*, *Core 0…7 Utility*, *Total CPU Utility*).
During the capture, the source was switched once from *Total CPU Usage* to another source.

| Finding | Detail |
|---|---|
| Commands | exclusively `00 01 02 …` (567 packets in ~117 s) – **no initialization**, no other commands |
| Off packet `0F` | **is not sent** – software exited normally: last update at 141.16 s, ~1 s later it closes the device (pending IN requests are canceled), no traffic afterwards. The software relies on the timeout. |
| Update interval | **200–220 ms** (median 0.20 s) |
| Value refresh | approx. every **2 s**; in between, the last packet is repeated unchanged |
| First packets | for approx. 1 s only time, RAM, byte 37 and byte 50 populated, all readings 0 |
| Byte 6 (CPU usage) | 0–58 %, plausible for the VM ✅ |
| Bytes 11/12 (CPU voltage) | constant `01 37` → "1.55 V" (the LA software sent `floor(V) × 100` here) |
| Bytes 25–32 | local time ✅, day of week 5 = Friday ✅ |
| Byte 33 (RAM) | 28–30 % ✅ |
| **Byte 37** | **new**, constant `5` – meaning unknown (not the vCPU count: 8) |
| **Byte 48** | **new**, 0–99, jumps a lot, changes every 2 s → later identified as **CPU usage, hundredths** |
| **Byte 50** | **new**, 11–99, changes more slowly, changes every 2 s → later identified as **RAM usage, hundredths** |
| Control transfers | only `GET_DESCRIPTOR` (device, configuration) on redirection and two `SET_IDLE` (HID class, `bmRequestType 0x21`) from Windows |
| Input reports | **none** – Windows keeps IN requests pending, the device never responds |
| String descriptors | are not queried by Windows |

### Second capture: all usage sources

Second capture – 866 packets in ~176 s; all sources for
*CPU Usage* (Total, Core 0–7 Usage, Max CPU/Thread Usage, Core 0–7 Utility) one after another at ~10 s intervals.

| Finding | Detail |
|---|---|
| Commands | again exclusively `01 02` – **the selection does not produce a separate command** |
| Byte 6 | 4–30 %, no recognizable steps in the 10 s windows – with a uniformly lightly loaded VM, total and per-core values are indistinguishable |
| Byte 37 | again constant `5` |
| Byte 48 | 1–98, jumps on every refresh (looks random), correlation with byte 6: −0.06 |
| Byte 50 | 3–98, drifts slowly (random walk), correlation with byte 6: −0.01 |
| Bytes 11/12 | again constant `01 37` |

Conclusion: with a uniformly lightly loaded VM the source cannot be identified – the third capture settles this.

### Third capture: one core under full load

Third capture – `while ($true) {}` ran in the VM with affinity to core 5;
in the software, *Core 5 Usage* → *Total CPU Usage* → *Core 7 Usage* one after another.

| Section | Byte 6 | Byte 48 |
|---|---|---|
| Core 5 Usage | **100** (constant, ~12 s) | **0** in all 58 packets |
| Total CPU Usage | 23–29 (≈ 100/8 + base load) | 40–86 |
| Core 7 Usage | 8–18 | 3–85 |

Conclusions:

- The **usage source selected in the software ends up in byte 6** (no separate command, no separate field).
- **Byte 48 = hundredths of the CPU usage** (exactly 0 at 100.00 %, otherwise uniformly distributed 0–99).
- **Byte 50 = hundredths of the RAM usage:** byte 33 + byte 50/100 runs smoothly in all three captures
  (max. jump of 0.2 percentage points between two refreshes, e.g. 27.94 → 27.97 → 27.98 → 28.03).

Open points from the captures:

- **Byte 37:** stayed constant `5` when switching the usage source → **not** the core selection.
- ✅ **Bytes 48/50:** CPU and RAM usage, hundredths (third capture).
- Test **byte 37** on the display: `seglcd probe --offsets 37 --value 88`.
- ✅ **Usage source:** ends up in byte 6 (third capture).

## Test plan for the LB360-S

Prerequisite: [udev rule](../../linux/udev/) installed, `seglcd list` shows "Open OK".

1. ✅ **Basic function** – does the display show anything at all?
   ```bash
   seglcd send --cpu-temp 42 --gpu-temp 57 --duration 20
   ```
2. ✅ (partially) **Field mapping** – set each byte individually to a conspicuous value:
   ```bash
   seglcd probe --value 88            # interactive, Enter = next byte
   seglcd probe --value 88 --auto 3   # automatic, 3 s per byte
   ```
   For each byte, note whether and where the display changes.
3. ✅ **Decimals / unit** – no decimals, no °F (see above).
   ```bash
   seglcd send --cpu-temp 42.75 --gpu-temp 57.25 --duration 10
   seglcd send --cpu-temp 42 --gpu-temp 57 --fahrenheit --duration 10
   ```
4. **Bars/scales** – determine full-scale values:
   ```bash
   seglcd sweep 6 --from 0 --to 100 --step 5 --step-ms 700     # CPU usage
   seglcd sweep 21 --from 0 --to 50 --step-ms 700              # fan (×100 RPM)
   ```
5. ✅ **Timeout or closing?** Result: **timeout** (display off as soon as no more packets arrive). When the program exits, two things happen at the same time: no more packets arrive,
   **and** the process closes `/dev/hidrawN` (then `usbhid` also stops polling the interrupt IN endpoint).
   `seglcd hold` separates the two: send first, then stop, but leave the device open.
   ```bash
   seglcd hold --send 5 --hold 15
   ```
   - Display turns off **at the message "Last packet sent"** (or shortly after) → genuine timeout; note the time.
   - Display only turns off **at "Closing the device"** → closing (end of IN polling) triggers it;
     then a keep-alive in the sense of "repeating packets" may not be needed at all – to be checked with
     `seglcd hold --send 1 --hold 60`.
   - Display stays on **even after closing** → only the off packet turns it off.

   ✅ Timeout narrowed down: **400 ms** interval stable, **500 ms** flashes. Measurement:
   ```bash
   for i in 300 400 500 750 1000; do echo "Interval $i ms"; seglcd send --cpu-temp 42 --gpu-temp 57 --interval $i --duration 6; done
   ```

6. **Comparison with the original** – USB capture of the Windows software ([instructions](../../reverse-engineering/usb-capture/)),
   analyze with `seglcd decode --diff` and compare with this document.

Please add results to this table. Captures belong in the local folder `captures/`; it is excluded via `.gitignore` because usbmon records the entire bus (including other devices).
