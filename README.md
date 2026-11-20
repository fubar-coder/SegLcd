# SegLcd – Thermaltake LB360-S ARGB Sync segment LCD on Linux

**English** · [Deutsch](README.de.md)

> SegLcd is an independent project and not affiliated with Thermaltake. "Thermaltake" is a trademark of Thermaltake Technology Co., Ltd.; the name is used only to identify the supported hardware.

Protocol documentation, library and tools to drive the segment LCD on the pump block of the
**Thermaltake LB360-S ARGB Sync** (USB `5131:200e`) on Linux – from reverse engineering the USB HID protocol
to a systemd service that shows the CPU and GPU temperature live on the display.

📖 **Documentation:** https://fubar-coder.github.io/SegLcd/ ([Deutsch](https://fubar-coder.github.io/SegLcd/de/))

- `seglcd` – CLI for testing, sending and analysing (`list`, `send`, `live`, `probe`, `decode`, …)
- `seglcdd` – systemd user service with D-Bus interface and TOML configuration
- Sensors for AMD, Intel and NVIDIA (hwmon, RAPL, NVML, DRM), Native AOT, English/German

| Path | Contents |
|---|---|
| `docs/` | Hugo/Hextra documentation (`cd docs && hugo server`) |
| `udev/70-seglcd.rules` | access without root + USB passthrough under `qemu:///session` |
| `udev/72-rapl-energy-read.rules` | optional: CPU package power (RAPL) for group `rapl` |
| `vm/lb360s-usb-hostdev.xml` | libvirt hostdev for a Windows VM |
| `tools/` | usbmon capture, packet extraction, package test (`test-package.sh`) |
| `src/SegLcd.Core` | library (.NET 11, AOT, HidApi.Net → native hidapi) |
| `src/SegLcd.Linux` | Linux sensors (hwmon, procfs, cpufreq, RAPL, NVML, DRM) |
| `src/SegLcd.Localization` | gettext localization (`po/`: English → German) |
| `src/SegLcd.Tool` | CLI `seglcd` (DotMake.CommandLine, Native AOT) |
| `src/SegLcd.Daemon` | service `seglcdd` (systemd --user, D-Bus, TOML configuration) |
| `man/` | man pages `seglcd(1)`, `seglcdd(1)`, `seglcdd.toml(5)` – English and German (`man/de/`) |
| `packaging/` | packages (AUR, RPM, DEB), system/user units, D-Bus policy, example configuration, `install-user.sh` / `uninstall-user.sh` |
| `tests/SegLcd.Tests` | TUnit tests, including the test vectors from the documentation |

```bash
git submodule update --init
sudo pacman -S hidapi                # native dependency (libhidapi-hidraw.so.0)
sudo install -m 644 udev/70-seglcd.rules /etc/udev/rules.d/
sudo udevadm control --reload
sudo udevadm trigger --action=change --subsystem-match=hidraw --subsystem-match=usb

dotnet test
dotnet publish src/SegLcd.Tool -c Release -r linux-x64
# -> artifacts/publish/SegLcd.Tool/release_linux-x64/seglcd
artifacts/publish/SegLcd.Tool/release_linux-x64/seglcd list
artifacts/publish/SegLcd.Tool/release_linux-x64/seglcd send --cpu-temp 42 --gpu-temp 57 --duration 20
```

Install the service:

- **system-wide as package** (system service from boot, user `seglcd`, system bus): Arch (AUR), Fedora/openSUSE
  (RPM), Debian/Ubuntu (DEB) – see [packaging/README.md](packaging/README.md); the service starts automatically when the display is present;
- **per user without root** (systemd user service, session bus):

```bash
packaging/install-user.sh          # --help, --reset-config, --no-start
journalctl --user -u seglcdd -f
packaging/uninstall-user.sh        # --purge also removes ~/.config/seglcd
```

## License

GPL-3.0-or-later – see [LICENSE](LICENSE).
