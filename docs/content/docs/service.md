---
title: Service seglcdd
weight: 7
---

`seglcdd` feeds the display with sensor values and offers a D-Bus interface; [`seglcd-gtk`](../gui/) is its settings app. Use **one** of the two variants –
two instances would send alternately.

| | System service (RPM/DEB/AUR) | User service (`install-user.sh`) |
|---|---|---|
| Start | automatically when the display is present (udev); prevent with `systemctl mask seglcdd` | at login: `systemctl --user enable --now seglcdd` |
| Runs as | user `seglcd` (`seglcdd --system`) | the logged-in user |
| Configuration | `/etc/seglcd/config.toml` | `~/.config/seglcd/config.toml` |
| D-Bus | system bus – read: everybody, control: root + group `seglcd` | session bus |
| Log | `journalctl -u seglcdd` | `journalctl --user -u seglcdd` |

Configuration: [Sensors & configuration](../sensors/) and `seglcdd.toml(5)`. Reference: `seglcdd(1)`.

## Installation

**Packages:** see [`packaging/README.md`](https://github.com/fubar-coder/SegLcd/blob/main/packaging/README.md).
To control the service: `sudo usermod -aG seglcd $USER` (log in again).

**Per user**, without root (requires the .NET SDK, `hidapi` and the [udev rule](../linux/udev/)):

```fish
packaging/install-user.sh      # --reset-config replaces config.toml (backup .bak), --no-start, --help
packaging/uninstall-user.sh    # --purge also removes ~/.config/seglcd
```

## Operation

```fish
systemctl --user reload seglcdd      # reload the configuration without the display going dark
journalctl --user -u seglcdd -f      # system service: without --user
```

| Situation | Behavior |
|---|---|
| display missing or passed through to a VM | retries every second, connects automatically |
| service stopped | sends the off packet, releases the device |
| configuration invalid | error in the journal, defaults are used |
| no D-Bus | warning, the display keeps running |

**Before experiments with `seglcd`** (`send`, `probe`, `raw`, …) pause the service; this sends the off packet and
releases the device:

```fish
set N io.github.fubar_coder.SegLcd1; set P /io/github/fubar_coder/SegLcd1
busctl --user set-property $N $P $N Enabled b false     # … and b true afterwards
```

## D-Bus interface

Name and interface `io.github.fubar_coder.SegLcd1`, object `/io/github/fubar_coder/SegLcd1`, defined in
`src/SegLcd.Daemon/DBus/io.github.fubar_coder.SegLcd1.xml`. For the system service use `busctl --system`.

| Property | Type | Access | Meaning |
|---|---|---|---|
| `Enabled` | `b` | rw | feed the display; `false` = off packet, device released |
| `UpdateIntervalMs` | `u` | rw | 50–400 ms |
| `SampleIntervalMs` | `u` | rw | 100–10000 ms |
| `Connected`, `DevicePath`, `Model`, `ConfigPath` | `b`, `s` | r | state and paths |
| `SensorSpecs` | `a{ss}` | r | field → configured spec (empty = automatic) |
| `Sources` | `a{ss}` | r | field → resolved sensor (empty = none) |
| `CurrentValues` | `a{sd}` | r | last values (`cpu_temperature`, `gpu_temperature`, `fan`, …) |
| `LastPacket` | `ay` | r | last 64-byte packet |

| Method | Meaning |
|---|---|
| `SetSensor(field, spec) → source` | set a sensor ([spec syntax](../sensors/#sensor-specs)); not saved until `SaveConfig` |
| `ListSensors() → a(sssd)` | usable sensors: spec, kind (`temperature`, `power`, `fan`, …), description, value (NaN = not readable) |
| `ReloadConfig()` | re-read `config.toml`; on error the old settings stay active |
| `SaveConfig()` | write the current settings; user service: `config.toml` (comments are lost), system service: the values that differ from `/etc` to `/var/lib/seglcd/config.toml` |

`PropertiesChanged` is sent for all properties except `CurrentValues` and `LastPacket`. Invalid values are rejected
with `InvalidArgs`.

```fish
busctl --user get-property $N $P $N Connected CurrentValues
busctl --user set-property $N $P $N UpdateIntervalMs u 200
busctl --user call $N $P $N SetSensor ss pump it8686_31050607:fan5
busctl --user call $N $P $N SaveConfig
```
