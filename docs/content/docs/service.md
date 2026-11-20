---
title: Service seglcdd
weight: 7
---

`seglcdd` (project `src/SegLcd.Daemon`) continuously supplies the display with sensor values. It runs as a
**systemd user service**, needs no root (the [udev rule](../linux/udev/) grants access via `uaccess`)
and provides a D-Bus interface on the **session bus** for querying and control.

| | |
|---|---|
| Binary | `~/.local/bin/seglcdd` (Native AOT, ~6 MB, requires `libhidapi-hidraw.so.0` at runtime) |
| Unit | `~/.config/systemd/user/seglcdd.service` (`Type=notify`) |
| Configuration | `~/.config/seglcd/config.toml` – see [Sensors & configuration](../sensors/#configtoml) |
| D-Bus | session bus, name `io.github.fubar_coder.SegLcd1`, object `/io/github/fubar_coder/SegLcd1` |
| Log | journald: `journalctl --user -u seglcdd` |
| Man pages | `seglcdd(1)`, `seglcdd.toml(5)` (English and German, source `man/`) |
| Libraries | Microsoft.Extensions.Hosting(.Systemd), Tmds.DBus.Protocol + Generator, Tomlyn – all AOT-compatible, publish without warnings |

## Installation

Requirements: .NET SDK, `hidapi` (`sudo pacman -S hidapi`), udev rule `70-seglcd.rules`.

{{< tabs >}}
  {{< tab name="Script" >}}
```fish
packaging/install-user.sh
```

The script builds `seglcdd` and `seglcd` (Native AOT), installs binaries, unit and D-Bus activation file and
starts (or restarts) the service. An **existing `config.toml` remains unchanged**; the default configuration is
only created if none exists yet.

| Option | Effect |
|---|---|
| `--reset-config` | replace an existing `config.toml` with the default configuration (old file → `config.toml.bak`) |
| `--no-start` | only install, do not enable/start the service |
| `-h`, `--help` | show help |
  {{< /tab >}}
  {{< tab name="Manual" >}}
```fish
dotnet publish src/SegLcd.Daemon -c Release -r linux-x64
install -Dm755 artifacts/publish/SegLcd.Daemon/release_linux-x64/seglcdd ~/.local/bin/seglcdd
install -Dm644 packaging/systemd/seglcdd.service ~/.config/systemd/user/seglcdd.service
install -Dm644 packaging/dbus/io.github.fubar_coder.SegLcd1.service ~/.local/share/dbus-1/services/io.github.fubar_coder.SegLcd1.service
test -e ~/.config/seglcd/config.toml; or install -Dm644 packaging/config.example.toml ~/.config/seglcd/config.toml
systemctl --user daemon-reload
systemctl --user enable --now seglcdd
```
  {{< /tab >}}
{{< /tabs >}}

Then adjust the configuration (at least `fan`/`pump`, see [Sensors](../sensors/#assigning-fan-and-pump))
and run `systemctl --user reload seglcdd`.

### Unit file

```ini {filename="packaging/systemd/seglcdd.service"}
[Service]
Type=notify
ExecStart=%h/.local/bin/seglcdd
ExecReload=/usr/bin/busctl --user call io.github.fubar_coder.SegLcd1 /io/github/fubar_coder/SegLcd1 io.github.fubar_coder.SegLcd1 ReloadConfig
Restart=on-failure
RestartSec=3
NoNewPrivileges=yes
PrivateTmp=yes

[Install]
WantedBy=default.target
```

- `Type=notify`: `seglcdd` reports via `sd_notify` when it is ready (Microsoft.Extensions.Hosting.Systemd).
- `ExecReload` calls the D-Bus method `ReloadConfig` – so `systemctl --user reload seglcdd` reloads the
  configuration without the display briefly turning off.
- `WantedBy=default.target`: starts at login. Without a login (e.g. at the login screen) no user service runs;
  if you want that, set `loginctl enable-linger` – but the display then only works if the
  udev rule additionally uses a group instead of `uaccess` (`uaccess` only applies to the active session).

### D-Bus activation

`packaging/dbus/io.github.fubar_coder.SegLcd1.service` links the bus name to the unit
(`SystemdService=seglcdd.service`): if a program accesses the name while the service is not running,
systemd starts it automatically.

## Operation

```fish
systemctl --user status seglcdd
journalctl --user -u seglcdd -f
systemctl --user reload seglcdd      # reload configuration
systemctl --user restart seglcdd
```

On startup, the service logs the configuration file, the selected source for each field, and the
connection status:

```text
Configuration loaded: /home/…/.config/seglcd/config.toml
Source cpu_temperature: hwmon4/temp1 (k10temp:Tctl)
Source fan: hwmon5/fan1 (it8686_31050607:fan1)
D-Bus ready: io.github.fubar_coder.SegLcd1 /io/github/fubar_coder/SegLcd1
Display connected: /dev/hidraw5 (Thermaltake LB360-S ARGB Sync)
```

| Situation | Behavior |
|---|---|
| Display not connected / passed through to a VM | waits, searches again every second, connects automatically |
| Display is disconnected | "Display disconnected", reconnect as above (verified via VM redirection) |
| Service is stopped | off packet `0F`, device is released |
| Configuration invalid | error in the journal, start with default values |
| No session bus | warning, display runs anyway (without D-Bus) |
| Name already taken (second instance) | error in the journal, the second instance serves the display anyway – do not start twice |

### Interaction with `seglcd`

The service and `seglcd` must not send at the same time, otherwise their packets alternate. Before experimenting with
`seglcd send`/`probe`/`raw`, **pause** the service:

```fish
busctl --user set-property io.github.fubar_coder.SegLcd1 /io/github/fubar_coder/SegLcd1 io.github.fubar_coder.SegLcd1 Enabled b false
# … seglcd experiments …
busctl --user set-property io.github.fubar_coder.SegLcd1 /io/github/fubar_coder/SegLcd1 io.github.fubar_coder.SegLcd1 Enabled b true
```

`Enabled = false` sends the off packet and releases the device; the service keeps running. Alternatively
`systemctl --user stop seglcdd`. `seglcd list`, `sensors` and `decode` are safe.

## D-Bus interface

Definition: `src/SegLcd.Daemon/DBus/io.github.fubar_coder.SegLcd1.xml`
(Tmds.DBus.Generator generates the server code from it).

| | |
|---|---|
| Bus | Session |
| Name | `io.github.fubar_coder.SegLcd1` |
| Object | `/io/github/fubar_coder/SegLcd1` |
| Interface | `io.github.fubar_coder.SegLcd1` (+ `org.freedesktop.DBus.Properties`, `…Introspectable`) |

### Properties

| Name | Type | Access | Meaning |
|---|---|---|---|
| `Enabled` | `b` | read/write | supply the display; `false` = off packet, device released |
| `UpdateIntervalMs` | `u` | read/write | update interval, 50–400 ms; takes effect immediately, without flicker |
| `SampleIntervalMs` | `u` | read/write | sample interval, 100–10000 ms |
| `Connected` | `b` | read | display open |
| `DevicePath` | `s` | read | e.g. `/dev/hidraw5`, empty when disconnected |
| `Model` | `s` | read | e.g. `Thermaltake LB360-S ARGB Sync` |
| `ConfigPath` | `s` | read | path of the configuration file |
| `Sources` | `a{ss}` | read | field → resolved source (empty = none) |
| `CurrentValues` | `a{sd}` | read | most recently read values, only those present (`cpu_temperature`, `cpu_usage`, `cpu_power`, `cpu_clock`, `cpu_voltage`, `gpu_temperature`, `gpu_usage`, `gpu_power`, `gpu_clock`, `fan`, `pump`, `ram_usage`) |
| `LastPacket` | `ay` | read | most recently sent 64-byte packet |

`PropertiesChanged` is emitted for `Enabled`, `UpdateIntervalMs`, `SampleIntervalMs`, `Connected`,
`DevicePath`, `Model` and `Sources`. **Not** for `CurrentValues` and `LastPacket` (they change constantly – query
them as needed). Invalid values on write are rejected with `org.freedesktop.DBus.Error.InvalidArgs`.

### Methods

| Method | Signature | Meaning |
|---|---|---|
| `SetSensor(field, spec) → source` | `ss` → `s` | set the source of a field ([spec syntax](../sensors/#sensor-specs-spec-syntax), empty = automatic); returns the resolved source. Unknown field or sensor not found → `InvalidArgs`. **Not persistent** until `SaveConfig`. |
| `ReloadConfig()` | – | re-read and apply `config.toml`; on error the old configuration remains active |
| `SaveConfig()` | – | write the current runtime settings to `config.toml` (comments in the file are lost) |

### Examples

```fish
set N io.github.fubar_coder.SegLcd1
set P /io/github/fubar_coder/SegLcd1

busctl --user introspect $N $P $N
busctl --user get-property $N $P $N Connected DevicePath Model
busctl --user get-property $N $P $N CurrentValues
busctl --user set-property $N $P $N UpdateIntervalMs u 200
busctl --user call $N $P $N SetSensor ss pump it8686_31050607:fan5
busctl --user call $N $P $N SaveConfig
busctl --user monitor $N            # watch signals
```

With `gdbus`:

```fish
gdbus call --session --dest io.github.fubar_coder.SegLcd1 --object-path /io/github/fubar_coder/SegLcd1 \
  --method org.freedesktop.DBus.Properties.GetAll io.github.fubar_coder.SegLcd1
```

{{< callout type="info" >}}
The bus name follows the D-Bus "reverse domain" convention: `io.github.fubar_coder` corresponds to the
GitHub namespace `github.com/fubar-coder` (hyphens are not allowed in D-Bus names and become `_`).
{{< /callout >}}

## Structure

| File | Responsibility |
|---|---|
| `Program.cs` | Generic Host, `AddSystemd()`, register services, `--config` |
| `DaemonConfig.cs` | configuration model, load/save TOML (Tomlyn source generator, snake_case) |
| `DisplayController.cs` | read sensors, manage `SegmentLcdSession`, runtime changes, logging |
| `DBus/DBusService.cs` | connection to the session bus, request name |
| `DBus/SegmentLcdObject.cs` | implementation of the properties/methods, `PropertiesChanged` |
| `DBus/io.github.fubar_coder.SegLcd1.xml` | interface definition (source for the generator) |
